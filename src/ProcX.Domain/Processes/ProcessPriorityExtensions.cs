namespace ProcX.Domain.Processes;

/// <summary>
/// Conversions between <see cref="ProcessPriority"/> and scheduler base priorities.
/// </summary>
public static class ProcessPriorityExtensions
{
    /// <summary>
    /// Infers the priority class from a process base priority.
    /// </summary>
    /// <param name="basePriority">The base priority (0-31).</param>
    /// <returns>The priority class with the highest base priority not exceeding the value.</returns>
    public static ProcessPriority FromBasePriority(int basePriority) => basePriority switch
    {
        >= 24 => ProcessPriority.Realtime,
        >= 13 => ProcessPriority.High,
        >= 10 => ProcessPriority.AboveNormal,
        >= 8 => ProcessPriority.Normal,
        >= 6 => ProcessPriority.BelowNormal,
        _ => ProcessPriority.Idle,
    };

    /// <summary>
    /// Gets the base priority associated with the priority class.
    /// </summary>
    /// <param name="priority">The priority class.</param>
    /// <returns>The base priority.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="priority"/> is not a defined value.</exception>
    public static int ToBasePriority(this ProcessPriority priority) => priority switch
    {
        ProcessPriority.Idle => 4,
        ProcessPriority.BelowNormal => 6,
        ProcessPriority.Normal => 8,
        ProcessPriority.AboveNormal => 10,
        ProcessPriority.High => 13,
        ProcessPriority.Realtime => 24,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, null),
    };
}
