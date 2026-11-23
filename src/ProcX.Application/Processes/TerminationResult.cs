using ProcX.Domain.Processes;

namespace ProcX.Application.Processes;

/// <summary>
/// The outcome of an attempt to terminate a process.
/// </summary>
public enum TerminationStatus
{
    /// <summary>
    /// The process was terminated.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The process had already exited (or its PID now belongs to another process).
    /// </summary>
    NotFound,

    /// <summary>
    /// The caller lacks the rights to terminate the process; elevation may help.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// Termination failed for another reason; see <see cref="TerminationResult.ErrorCode"/>.
    /// </summary>
    Failed,
}

/// <summary>
/// The result of terminating one process.
/// </summary>
/// <param name="Key">The process identity.</param>
/// <param name="Status">The outcome.</param>
/// <param name="ErrorCode">The Win32 error code when <paramref name="Status"/> is not <see cref="TerminationStatus.Succeeded"/>; otherwise 0.</param>
public sealed record TerminationResult(ProcessKey Key, TerminationStatus Status, int ErrorCode = 0)
{
    /// <summary>
    /// Gets whether the process is gone, either because it was terminated or had already exited.
    /// </summary>
    public bool IsGone => Status is TerminationStatus.Succeeded or TerminationStatus.NotFound;
}
