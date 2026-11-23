using ProcX.Application.Processes;
using ProcX.Domain.Processes;

namespace ProcX.App.Tests;

/// <summary>
/// Builders for metrics and deltas used in view model tests.
/// </summary>
internal static class Rows
{
    /// <summary>
    /// An arbitrary fixed reference time.
    /// </summary>
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Creates process metrics.
    /// </summary>
    /// <param name="pid">The process ID.</param>
    /// <param name="name">The image name.</param>
    /// <param name="cpu">The CPU usage in percent.</param>
    /// <param name="memory">The private working set in bytes.</param>
    /// <param name="parentPid">The parent process ID.</param>
    /// <param name="createdAfterSeconds">Creation time offset from the base time, in seconds.</param>
    /// <returns>The metrics.</returns>
    public static ProcessMetrics Metrics(
        int pid,
        string? name = null,
        double cpu = 0,
        long memory = 0,
        int parentPid = 0,
        int createdAfterSeconds = 0) => new(
        new ProcessSample
        {
            Pid = pid,
            ParentPid = parentPid,
            Name = name ?? $"p{pid}.exe",
            CreateTime = BaseTime.AddSeconds(createdAfterSeconds),
            PrivateWorkingSetBytes = memory,
        },
        cpu);

    /// <summary>
    /// Creates a delta.
    /// </summary>
    /// <param name="added">Added processes.</param>
    /// <param name="updated">Updated processes.</param>
    /// <param name="removed">Removed processes.</param>
    /// <param name="totalCpu">Overall CPU usage.</param>
    /// <returns>The delta.</returns>
    public static ProcessDelta Delta(
        ProcessMetrics[]? added = null,
        ProcessMetrics[]? updated = null,
        ProcessKey[]? removed = null,
        double totalCpu = 0) =>
        new(new ProcessSnapshot(BaseTime, []), added ?? [], updated ?? [], removed ?? [], totalCpu);
}
