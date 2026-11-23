using ProcX.Domain.Processes;

namespace ProcX.Application.Processes;

/// <summary>
/// Performs actions that change the state of a process.
/// </summary>
public interface IProcessController
{
    /// <summary>
    /// Terminates a single process.
    /// </summary>
    /// <remarks>
    /// Implementations must verify the process identity (PID and creation time) before acting, so that
    /// a recycled PID is never terminated by mistake, and must not throw for expected failures.
    /// </remarks>
    /// <param name="key">The process identity.</param>
    /// <returns>The result.</returns>
    TerminationResult Terminate(ProcessKey key);
}
