using Microsoft.Extensions.Logging;

namespace ProcX.Application.Processes;

/// <summary>
/// Periodically samples all processes and publishes the changes.
/// </summary>
/// <remarks>
/// <see cref="Updated"/> is raised on a thread-pool thread; UI subscribers must marshal to their dispatcher.
/// </remarks>
public sealed partial class ProcessMonitor
{
    /// <summary>
    /// The default sampling interval.
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The shortest allowed sampling interval.
    /// </summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// The longest allowed sampling interval.
    /// </summary>
    public static readonly TimeSpan MaximumInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The source of process snapshots.
    /// </summary>
    private readonly IProcessSnapshotProvider _provider;

    /// <summary>
    /// The clock used for timers and elapsed-time measurement.
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger<ProcessMonitor> _logger;

    /// <summary>
    /// Computes deltas between consecutive snapshots.
    /// </summary>
    private readonly ProcessDeltaCalculator _calculator;

    /// <summary>
    /// Serializes sampling between the timer loop and <see cref="RefreshNow"/>.
    /// </summary>
    private readonly Lock _sampleLock = new();

    /// <summary>
    /// The timer driving the sampling loop while <see cref="RunAsync"/> is active.
    /// </summary>
    private PeriodicTimer? _timer;

    /// <summary>
    /// The current sampling interval.
    /// </summary>
    private TimeSpan _interval = DefaultInterval;

    /// <summary>
    /// The <see cref="TimeProvider.GetTimestamp"/> value of the previous sample, or <see langword="null"/> if none.
    /// </summary>
    private long? _lastTimestamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessMonitor"/> class.
    /// </summary>
    /// <param name="provider">The source of process snapshots.</param>
    /// <param name="systemInformation">Provides the logical processor count.</param>
    /// <param name="timeProvider">The clock used for timers and elapsed-time measurement.</param>
    /// <param name="logger">The logger.</param>
    public ProcessMonitor(
        IProcessSnapshotProvider provider,
        ISystemInformation systemInformation,
        TimeProvider timeProvider,
        ILogger<ProcessMonitor> logger)
    {
        ArgumentNullException.ThrowIfNull(systemInformation);

        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _calculator = new ProcessDeltaCalculator(systemInformation.LogicalProcessorCount);
    }

    /// <summary>
    /// Raised after every successful sample.
    /// </summary>
    public event EventHandler<ProcessDelta>? Updated;

    /// <summary>
    /// Gets or sets the sampling interval; takes effect from the next tick.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside <see cref="MinimumInterval"/>..<see cref="MaximumInterval"/>.</exception>
    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, MinimumInterval);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaximumInterval);

            _interval = value;
            _timer?.Period = value;
        }
    }

    /// <summary>
    /// Gets or sets whether timer ticks are skipped. <see cref="RefreshNow"/> still samples while paused.
    /// </summary>
    public bool IsPaused { get; set; }

    /// <summary>
    /// Samples immediately and then every <see cref="Interval"/> until cancelled.
    /// </summary>
    /// <param name="cancellationToken">Stops the loop.</param>
    /// <returns>A task that completes when the loop stops.</returns>
    /// <exception cref="InvalidOperationException">The loop is already running.</exception>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval, _timeProvider);
        if (Interlocked.CompareExchange(ref _timer, timer, null) is not null)
        {
            throw new InvalidOperationException("The monitor is already running.");
        }

        try
        {
            TrySample();

            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!IsPaused)
                {
                    TrySample();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        finally
        {
            Interlocked.Exchange(ref _timer, null);
        }
    }

    /// <summary>
    /// Samples immediately, outside the regular schedule.
    /// </summary>
    /// <returns>The delta that was published to <see cref="Updated"/>.</returns>
    public ProcessDelta RefreshNow()
    {
        ProcessDelta delta;
        lock (_sampleLock)
        {
            delta = Sample();
        }

        Updated?.Invoke(this, delta);
        return delta;
    }

    /// <summary>
    /// Samples and publishes, logging (rather than propagating) failures of the snapshot provider so
    /// that a transient native error does not stop the monitoring loop.
    /// </summary>
    private void TrySample()
    {
        ProcessDelta delta;
        lock (_sampleLock)
        {
            try
            {
                delta = Sample();
            }
#pragma warning disable CA1031 // The loop must survive any provider failure; the error is logged.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogSampleFailed(ex);
                return;
            }
        }

        Updated?.Invoke(this, delta);
    }

    /// <summary>
    /// Captures a snapshot and computes its delta. Must be called under <see cref="_sampleLock"/>.
    /// </summary>
    /// <returns>The delta.</returns>
    private ProcessDelta Sample()
    {
        var snapshot = _provider.Capture();
        var now = _timeProvider.GetTimestamp();
        var elapsed = _lastTimestamp is { } last ? _timeProvider.GetElapsedTime(last, now) : TimeSpan.Zero;
        _lastTimestamp = now;

        return _calculator.Compute(snapshot, elapsed);
    }

    /// <summary>
    /// Logs a failed sampling attempt.
    /// </summary>
    /// <param name="exception">The failure.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to capture process snapshot")]
    private partial void LogSampleFailed(Exception exception);
}
