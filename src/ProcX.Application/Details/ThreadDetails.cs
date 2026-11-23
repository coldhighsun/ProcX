namespace ProcX.Application.Details;

/// <summary>
/// Scheduler state of a thread (<c>KTHREAD_STATE</c>).
/// </summary>
public enum ThreadSchedulingState
{
    /// <summary>
    /// Being initialized.
    /// </summary>
    Initialized = 0,

    /// <summary>
    /// Ready to run.
    /// </summary>
    Ready = 1,

    /// <summary>
    /// Currently running.
    /// </summary>
    Running = 2,

    /// <summary>
    /// Selected to run next on a processor.
    /// </summary>
    Standby = 3,

    /// <summary>
    /// Finished running.
    /// </summary>
    Terminated = 4,

    /// <summary>
    /// Waiting for an object or event; see <see cref="ThreadDetails.WaitReason"/>.
    /// </summary>
    Waiting = 5,

    /// <summary>
    /// Waiting for resources (e.g. its kernel stack to be paged in).
    /// </summary>
    Transition = 6,

    /// <summary>
    /// Ready to run on a deferred processor.
    /// </summary>
    DeferredReady = 7,

    /// <summary>
    /// Obsolete gate wait state.
    /// </summary>
    GateWait = 8,

    /// <summary>
    /// Waiting for its process to be swapped in.
    /// </summary>
    WaitingForProcessInSwap = 9,
}

/// <summary>
/// A thread of a process.
/// </summary>
/// <param name="ThreadId">The thread identifier.</param>
/// <param name="CreateTime">The time the thread was created.</param>
/// <param name="KernelTime">CPU time spent in kernel mode.</param>
/// <param name="UserTime">CPU time spent in user mode.</param>
/// <param name="Priority">The current dynamic priority.</param>
/// <param name="BasePriority">The base priority.</param>
/// <param name="ContextSwitches">The number of context switches.</param>
/// <param name="State">The scheduler state.</param>
/// <param name="WaitReason">Why the thread waits, when <paramref name="State"/> is <see cref="ThreadSchedulingState.Waiting"/>.</param>
/// <param name="StartAddress">The Win32 start address, or the kernel-reported one if the former is unavailable.</param>
/// <param name="StartSymbol">The start address as <c>module+0xoffset</c>, or <see langword="null"/> if it cannot be resolved.</param>
public sealed record ThreadDetails(
    int ThreadId,
    DateTimeOffset CreateTime,
    TimeSpan KernelTime,
    TimeSpan UserTime,
    int Priority,
    int BasePriority,
    uint ContextSwitches,
    ThreadSchedulingState State,
    string? WaitReason,
    ulong StartAddress,
    string? StartSymbol)
{
    /// <summary>
    /// Gets the total CPU time.
    /// </summary>
    public TimeSpan TotalCpuTime => KernelTime + UserTime;
}
