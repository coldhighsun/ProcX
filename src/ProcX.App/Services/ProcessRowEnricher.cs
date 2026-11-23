using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ProcX.App.ViewModels;
using ProcX.Application.Processes;

namespace ProcX.App.Services;

/// <summary>
/// Enriches rows on a dedicated STA background thread (required by the shell icon APIs) and applies
/// the results on the UI thread.
/// </summary>
public sealed partial class ProcessRowEnricher : IProcessRowEnricher, IDisposable
{
    /// <summary>
    /// How long <see cref="Dispose"/> waits for the worker thread to finish.
    /// </summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Rows waiting to be enriched.
    /// </summary>
    private readonly BlockingCollection<ProcessRowViewModel> _queue = [];

    /// <summary>
    /// The cached static information source.
    /// </summary>
    private readonly ProcessInfoCache _infoCache;

    /// <summary>
    /// The icon source.
    /// </summary>
    private readonly ProcessIconProvider _iconProvider;

    /// <summary>
    /// Marshals results to the UI thread.
    /// </summary>
    private readonly IUiDispatcher _dispatcher;

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger<ProcessRowEnricher> _logger;

    /// <summary>
    /// The worker thread.
    /// </summary>
    private readonly Thread _worker;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessRowEnricher"/> class and starts the worker thread.
    /// </summary>
    /// <param name="infoCache">The cached static information source.</param>
    /// <param name="iconProvider">The icon source.</param>
    /// <param name="dispatcher">Marshals results to the UI thread.</param>
    /// <param name="logger">The logger.</param>
    public ProcessRowEnricher(
        ProcessInfoCache infoCache,
        ProcessIconProvider iconProvider,
        IUiDispatcher dispatcher,
        ILogger<ProcessRowEnricher> logger)
    {
        _infoCache = infoCache;
        _iconProvider = iconProvider;
        _dispatcher = dispatcher;
        _logger = logger;

        _worker = new Thread(Run) { IsBackground = true, Name = "ProcX row enricher" };
        _worker.SetApartmentState(ApartmentState.STA);
        _worker.Start();
    }

    /// <inheritdoc />
    public void Enqueue(ProcessRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!_queue.IsAddingCompleted)
        {
            _queue.Add(row);
        }
    }

    /// <summary>
    /// Stops the worker thread.
    /// </summary>
    public void Dispose()
    {
        _queue.CompleteAdding();
        _worker.Join(ShutdownTimeout);
        _queue.Dispose();
    }

    /// <summary>
    /// The worker loop.
    /// </summary>
    private void Run()
    {
        foreach (var row in _queue.GetConsumingEnumerable())
        {
            try
            {
                var info = _infoCache.Get(row.Key);
                var icon = _iconProvider.GetIcon(info.ImagePath);
                _dispatcher.Post(() => row.ApplyStaticInfo(info, icon));
            }
#pragma warning disable CA1031 // One bad process must not stop enrichment of the others; the error is logged.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogEnrichFailed(ex, row.Pid);
            }
        }
    }

    /// <summary>
    /// Logs a failed enrichment.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <param name="pid">The process ID.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read details of process {Pid}")]
    private partial void LogEnrichFailed(Exception exception, int pid);
}
