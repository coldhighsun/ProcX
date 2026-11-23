namespace ProcX.Domain.Processes;

/// <summary>
/// Raw counters of a single process captured at one sampling instant.
/// </summary>
public sealed record ProcessSample
{
    /// <summary>
    /// Gets the process identifier.
    /// </summary>
    public required int Pid { get; init; }

    /// <summary>
    /// Gets the identifier of the process that created this process.
    /// </summary>
    /// <remarks>
    /// The parent may have exited and its PID may have been reused; see <see cref="ProcessTreeBuilder"/>.
    /// </remarks>
    public required int ParentPid { get; init; }

    /// <summary>
    /// Gets the image name, e.g. <c>explorer.exe</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the time the process was created.
    /// </summary>
    public required DateTimeOffset CreateTime { get; init; }

    /// <summary>
    /// Gets the total CPU time spent in kernel mode.
    /// </summary>
    public TimeSpan KernelTime { get; init; }

    /// <summary>
    /// Gets the total CPU time spent in user mode.
    /// </summary>
    public TimeSpan UserTime { get; init; }

    /// <summary>
    /// Gets the working set size in bytes.
    /// </summary>
    public long WorkingSetBytes { get; init; }

    /// <summary>
    /// Gets the private working set size in bytes (the "Memory" column of the Windows Task Manager).
    /// </summary>
    public long PrivateWorkingSetBytes { get; init; }

    /// <summary>
    /// Gets the private (commit) bytes.
    /// </summary>
    public long PrivateBytes { get; init; }

    /// <summary>
    /// Gets the number of threads.
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Gets the number of open handles.
    /// </summary>
    public int HandleCount { get; init; }

    /// <summary>
    /// Gets the terminal services session identifier.
    /// </summary>
    public int SessionId { get; init; }

    /// <summary>
    /// Gets the base scheduling priority (0-31).
    /// </summary>
    public int BasePriority { get; init; }

    /// <summary>
    /// Gets the stable identity of this process instance.
    /// </summary>
    public ProcessKey Key => new(Pid, CreateTime);

    /// <summary>
    /// Gets the total CPU time (kernel + user).
    /// </summary>
    public TimeSpan TotalCpuTime => KernelTime + UserTime;
}
