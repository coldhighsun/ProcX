using ProcX.Domain.Processes;

namespace ProcX.Application.Processes;

/// <summary>
/// A process sample together with the rates derived from comparing it to the previous sample.
/// </summary>
/// <param name="Sample">The latest raw sample.</param>
/// <param name="CpuPercent">CPU usage since the previous sample, normalized across all logical processors (0-100).</param>
public sealed record ProcessMetrics(ProcessSample Sample, double CpuPercent)
{
    /// <summary>
    /// Gets the stable identity of the process.
    /// </summary>
    public ProcessKey Key => Sample.Key;
}
