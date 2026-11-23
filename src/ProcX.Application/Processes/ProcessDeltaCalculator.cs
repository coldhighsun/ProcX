using ProcX.Domain.Processes;

namespace ProcX.Application.Processes;

/// <summary>
/// Compares each snapshot with the previous one to classify processes and compute CPU usage.
/// </summary>
/// <remarks>
/// Processes are matched by <see cref="ProcessKey"/>, so a recycled PID is reported as one removal
/// plus one addition rather than as an update. This type is not thread-safe.
/// </remarks>
/// <param name="processorCount">The number of logical processors used to normalize CPU usage.</param>
internal sealed class ProcessDeltaCalculator(int processorCount)
{
    /// <summary>
    /// PID of the System Idle Process, whose CPU time represents unused capacity.
    /// </summary>
    private const int IdlePid = 0;

    /// <summary>
    /// Samples of the previous snapshot indexed by process key.
    /// </summary>
    private Dictionary<ProcessKey, ProcessSample> _previous = [];

    /// <summary>
    /// Gets the number of logical processors used to normalize CPU usage.
    /// </summary>
    public int ProcessorCount { get; } = processorCount >= 1
        ? processorCount
        : throw new ArgumentOutOfRangeException(nameof(processorCount), processorCount, "At least one processor is required.");

    /// <summary>
    /// Computes the delta between the previous snapshot and <paramref name="snapshot"/>, then remembers it.
    /// </summary>
    /// <param name="snapshot">The latest snapshot.</param>
    /// <param name="elapsed">Wall-clock time since the previous snapshot; ignored for the first one.</param>
    /// <returns>The delta. On the first call every process is reported as added with 0% CPU.</returns>
    public ProcessDelta Compute(ProcessSnapshot snapshot, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var current = new Dictionary<ProcessKey, ProcessSample>(snapshot.Processes.Count);
        var added = new List<ProcessMetrics>();
        var updated = new List<ProcessMetrics>(_previous.Count);
        double? idlePercent = null;

        foreach (var sample in snapshot.Processes)
        {
            current[sample.Key] = sample;

            if (_previous.TryGetValue(sample.Key, out var previousSample))
            {
                var cpu = CpuUsageCalculator.Calculate(previousSample.TotalCpuTime, sample.TotalCpuTime, elapsed, ProcessorCount);
                updated.Add(new ProcessMetrics(sample, cpu));

                if (sample.Pid == IdlePid)
                {
                    idlePercent = cpu;
                }
            }
            else
            {
                added.Add(new ProcessMetrics(sample, 0));
            }
        }

        var removed = _previous.Keys.Where(key => !current.ContainsKey(key)).ToList();
        _previous = current;

        var totalCpu = idlePercent is { } idle ? Math.Clamp(100 - idle, 0, 100) : 0;
        return new ProcessDelta(snapshot, added, updated, removed, totalCpu);
    }

    /// <summary>
    /// Forgets the previous snapshot so the next call reports every process as added.
    /// </summary>
    public void Reset() => _previous = [];
}
