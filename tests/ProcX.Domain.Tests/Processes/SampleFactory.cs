using ProcX.Domain.Processes;

namespace ProcX.Domain.Tests.Processes;

/// <summary>
/// Creates <see cref="ProcessSample"/> instances for tests.
/// </summary>
internal static class SampleFactory
{
    /// <summary>
    /// An arbitrary fixed reference time used as the base for creation times.
    /// </summary>
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Creates a sample whose creation time is <paramref name="createdAfterSeconds"/> seconds after a fixed base time.
    /// </summary>
    /// <param name="pid">The process ID.</param>
    /// <param name="parentPid">The parent process ID.</param>
    /// <param name="createdAfterSeconds">Offset of the creation time from the base time, in seconds.</param>
    /// <returns>The sample.</returns>
    public static ProcessSample Create(int pid, int parentPid, int createdAfterSeconds) => new()
    {
        Pid = pid,
        ParentPid = parentPid,
        Name = $"p{pid}.exe",
        CreateTime = BaseTime.AddSeconds(createdAfterSeconds),
    };
}
