using ProcX.Domain.Processes;

namespace ProcX.Application.Tests.Processes;

/// <summary>
/// Builders for process samples and snapshots used in tests.
/// </summary>
internal static class Samples
{
    /// <summary>
    /// An arbitrary fixed reference time.
    /// </summary>
    public static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Creates a process sample.
    /// </summary>
    /// <param name="pid">The process ID.</param>
    /// <param name="cpuSeconds">Total CPU time in seconds, reported as user time.</param>
    /// <param name="createdAfterSeconds">Creation time offset from <see cref="BaseTime"/>, in seconds.</param>
    /// <returns>The sample.</returns>
    public static ProcessSample Process(int pid, double cpuSeconds = 0, int createdAfterSeconds = 0) => new()
    {
        Pid = pid,
        ParentPid = 0,
        Name = $"p{pid}.exe",
        CreateTime = BaseTime.AddSeconds(createdAfterSeconds),
        UserTime = TimeSpan.FromSeconds(cpuSeconds),
    };

    /// <summary>
    /// Creates a snapshot containing the given processes.
    /// </summary>
    /// <param name="processes">The processes.</param>
    /// <returns>The snapshot.</returns>
    public static ProcessSnapshot Snapshot(params ProcessSample[] processes) => new(BaseTime, processes);
}
