namespace ProcX.Domain.Processes;

/// <summary>
/// Computes CPU usage percentages from cumulative CPU time counters.
/// </summary>
public static class CpuUsageCalculator
{
    /// <summary>
    /// Calculates the CPU usage of a process between two samples, normalized across all logical processors.
    /// </summary>
    /// <param name="previousCpuTime">Total CPU time at the previous sample.</param>
    /// <param name="currentCpuTime">Total CPU time at the current sample.</param>
    /// <param name="elapsed">Wall-clock time between the two samples.</param>
    /// <param name="processorCount">Number of logical processors.</param>
    /// <returns>Usage in percent, clamped to the range 0-100.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="processorCount"/> is less than 1.</exception>
    public static double Calculate(TimeSpan previousCpuTime, TimeSpan currentCpuTime, TimeSpan elapsed, int processorCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(processorCount, 1);

        if (elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        var cpuDelta = currentCpuTime - previousCpuTime;
        if (cpuDelta <= TimeSpan.Zero)
        {
            return 0;
        }

        var percent = cpuDelta / (elapsed * processorCount) * 100;
        return Math.Clamp(percent, 0, 100);
    }
}
