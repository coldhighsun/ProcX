using ProcX.Domain.Processes;

namespace ProcX.Application.Processes;

/// <summary>
/// Retrieves per-process information that requires opening the process.
/// </summary>
public interface IProcessInfoProvider
{
    /// <summary>
    /// Retrieves the static information of a process.
    /// </summary>
    /// <remarks>
    /// Implementations must verify that the process identified by <paramref name="key"/> still exists
    /// (not just its PID) and never throw for exited or inaccessible processes.
    /// </remarks>
    /// <param name="key">The process identity.</param>
    /// <returns>The information; <see cref="ProcessStaticInfo.Unavailable"/> or <see cref="ProcessStaticInfo.AccessDenied"/> on failure.</returns>
    ProcessStaticInfo GetStaticInfo(ProcessKey key);
}
