using ProcX.Domain.Processes;

namespace ProcX.Application.Processes;

/// <summary>
/// Captures the counters of all running processes in a single, consistent pass.
/// </summary>
public interface IProcessSnapshotProvider
{
    /// <summary>
    /// Captures a snapshot of all processes.
    /// </summary>
    /// <returns>The snapshot.</returns>
    ProcessSnapshot Capture();
}
