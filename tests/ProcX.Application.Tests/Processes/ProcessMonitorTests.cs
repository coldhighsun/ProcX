using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using ProcX.Application.Processes;
using ProcX.Domain.Processes;

namespace ProcX.Application.Tests.Processes;

public sealed class ProcessMonitorTests : IAsyncDisposable
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider _time = new();
    private readonly FakeSnapshotProvider _provider = new();
    private readonly ProcessMonitor _monitor;
    private readonly Channel<ProcessDelta> _updates = Channel.CreateUnbounded<ProcessDelta>();
    private readonly CancellationTokenSource _cts = new();
    private Task? _run;

    public ProcessMonitorTests()
    {
        _monitor = new ProcessMonitor(_provider, new FixedSystemInformation(2), _time, NullLogger<ProcessMonitor>.Instance);
        _monitor.Updated += (_, delta) => _updates.Writer.TryWrite(delta);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        if (_run is not null)
        {
            await _run;
        }

        _cts.Dispose();
    }

    [Fact]
    public async Task RunAsync_Started_SamplesImmediately()
    {
        // Arrange
        _provider.Enqueue(Samples.Snapshot(Samples.Process(1)));

        // Act
        Start();
        var delta = await NextUpdateAsync();

        // Assert
        delta.Added.Should().ContainSingle().Which.Sample.Pid.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_IntervalElapsed_SamplesAgainAndComputesCpu()
    {
        // Arrange
        _provider.Enqueue(Samples.Snapshot(Samples.Process(1, cpuSeconds: 0)));
        _provider.Enqueue(Samples.Snapshot(Samples.Process(1, cpuSeconds: 1)));
        Start();
        await NextUpdateAsync();

        // Act
        _time.Advance(ProcessMonitor.DefaultInterval);
        var delta = await NextUpdateAsync();

        // Assert: 1 CPU-second over 1 s on 2 processors.
        delta.Updated.Should().ContainSingle().Which.CpuPercent.Should().BeApproximately(50, 1e-9);
    }

    [Fact]
    public async Task RunAsync_Paused_SkipsTicks()
    {
        // Arrange
        _provider.Enqueue(Samples.Snapshot(Samples.Process(1)));
        Start();
        await NextUpdateAsync();

        // Act
        _monitor.IsPaused = true;
        _time.Advance(ProcessMonitor.DefaultInterval * 3);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        // Assert
        _provider.CaptureCount.Should().Be(1);
        _updates.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ProviderThrows_KeepsRunning()
    {
        // Arrange
        _provider.EnqueueFailure(new InvalidOperationException("boom"));
        _provider.Enqueue(Samples.Snapshot(Samples.Process(1)));
        Start();

        // Act
        _time.Advance(ProcessMonitor.DefaultInterval);
        var delta = await NextUpdateAsync();

        // Assert
        delta.Added.Should().ContainSingle();
        _provider.CaptureCount.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_AlreadyRunning_Throws()
    {
        // Arrange
        _provider.Enqueue(Samples.Snapshot(Samples.Process(1)));
        Start();
        await NextUpdateAsync();

        // Act
        var act = () => _monitor.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RunAsync_Cancelled_CompletesWithoutException()
    {
        // Arrange
        _provider.Enqueue(Samples.Snapshot(Samples.Process(1)));
        Start();
        await NextUpdateAsync();

        // Act
        await _cts.CancelAsync();
        var act = async () => await _run!;

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefreshNow_Called_PublishesAndReturnsDelta()
    {
        // Arrange
        _provider.Enqueue(Samples.Snapshot(Samples.Process(3)));

        // Act
        var returned = _monitor.RefreshNow();
        var published = await NextUpdateAsync();

        // Assert
        published.Should().BeSameAs(returned);
        returned.Added.Should().ContainSingle().Which.Sample.Pid.Should().Be(3);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(120_000)]
    public void Interval_OutOfRange_Throws(int milliseconds)
    {
        // Act
        var act = () => _monitor.Interval = TimeSpan.FromMilliseconds(milliseconds);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Interval_ChangedWhileRunning_AppliesNewPeriod()
    {
        // Arrange
        _provider.Enqueue(Samples.Snapshot(Samples.Process(1)));
        _provider.Enqueue(Samples.Snapshot(Samples.Process(1)));
        Start();
        await NextUpdateAsync();

        // Act
        _monitor.Interval = TimeSpan.FromSeconds(5);
        _time.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        var sampledEarly = _updates.Reader.TryRead(out _);
        _time.Advance(TimeSpan.FromSeconds(4));
        var delta = await NextUpdateAsync();

        // Assert
        sampledEarly.Should().BeFalse();
        delta.Updated.Should().ContainSingle();
    }

    private void Start() => _run = _monitor.RunAsync(_cts.Token);

    private async Task<ProcessDelta> NextUpdateAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(WaitTimeout);
        return await _updates.Reader.ReadAsync(timeout.Token);
    }

    /// <summary>
    /// Returns queued snapshots (or failures) in order; repeats the last snapshot once the queue is drained.
    /// </summary>
    private sealed class FakeSnapshotProvider : IProcessSnapshotProvider
    {
        private readonly Queue<Func<ProcessSnapshot>> _results = new();
        private ProcessSnapshot _last = Samples.Snapshot();
        private int _captureCount;

        public int CaptureCount => Volatile.Read(ref _captureCount);

        public void Enqueue(ProcessSnapshot snapshot) => _results.Enqueue(() => snapshot);

        public void EnqueueFailure(Exception exception) => _results.Enqueue(() => throw exception);

        public ProcessSnapshot Capture()
        {
            Interlocked.Increment(ref _captureCount);
            if (_results.TryDequeue(out var next))
            {
                _last = next();
            }

            return _last;
        }
    }

    private sealed class FixedSystemInformation(int processorCount) : ISystemInformation
    {
        public int LogicalProcessorCount => processorCount;
    }
}
