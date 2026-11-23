using ProcX.Domain.Processes;

namespace ProcX.Application.Processes;

/// <summary>
/// The processors a process may run on, and the processors the system offers.
/// </summary>
/// <param name="Process">The processors the process is allowed to use.</param>
/// <param name="System">All processors available to the process.</param>
public sealed record AffinityInfo(AffinityMask Process, AffinityMask System);
