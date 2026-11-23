using ProcX.Domain.Processes;

namespace ProcX.Application.Processes;

/// <summary>
/// The changes between two consecutive process snapshots.
/// </summary>
/// <param name="Snapshot">The latest snapshot.</param>
/// <param name="Added">Processes that were not present in the previous snapshot.</param>
/// <param name="Updated">Processes that were present in both snapshots, with refreshed metrics.</param>
/// <param name="Removed">Processes that have exited since the previous snapshot.</param>
/// <param name="TotalCpuPercent">Overall CPU usage of the system (100 minus idle), 0-100.</param>
public sealed record ProcessDelta(
    ProcessSnapshot Snapshot,
    IReadOnlyList<ProcessMetrics> Added,
    IReadOnlyList<ProcessMetrics> Updated,
    IReadOnlyList<ProcessKey> Removed,
    double TotalCpuPercent);
