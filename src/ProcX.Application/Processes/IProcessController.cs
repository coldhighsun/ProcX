using ProcX.Domain.Processes;

namespace ProcX.Application.Processes;

/// <summary>
/// Performs actions that change the state of a process.
/// </summary>
/// <remarks>
/// Implementations must verify the process identity (PID and creation time) before acting, so that a
/// recycled PID is never changed by mistake, and must not throw for expected failures.
/// </remarks>
public interface IProcessController
{
    /// <summary>
    /// Terminates a single process.
    /// </summary>
    /// <param name="key">The process identity.</param>
    /// <returns>The result.</returns>
    TerminationResult Terminate(ProcessKey key);

    /// <summary>
    /// Sets the priority class of a process.
    /// </summary>
    /// <param name="key">The process identity.</param>
    /// <param name="priority">The new priority class.</param>
    /// <returns>The result.</returns>
    ProcessOperationResult<bool> SetPriority(ProcessKey key, ProcessPriority priority);

    /// <summary>
    /// Reads the processor affinity of a process.
    /// </summary>
    /// <param name="key">The process identity.</param>
    /// <returns>The process and system affinity masks.</returns>
    ProcessOperationResult<AffinityInfo> GetAffinity(ProcessKey key);

    /// <summary>
    /// Sets the processor affinity of a process.
    /// </summary>
    /// <param name="key">The process identity.</param>
    /// <param name="mask">The processors the process may use; must be non-empty and a subset of the system mask.</param>
    /// <returns>The result.</returns>
    ProcessOperationResult<bool> SetAffinity(ProcessKey key, AffinityMask mask);
}
