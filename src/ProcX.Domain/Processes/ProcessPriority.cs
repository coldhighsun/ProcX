namespace ProcX.Domain.Processes;

/// <summary>
/// Process priority classes, mirroring the Win32 priority classes.
/// </summary>
public enum ProcessPriority
{
    /// <summary>
    /// Idle priority (base priority 4).
    /// </summary>
    Idle,

    /// <summary>
    /// Below normal priority (base priority 6).
    /// </summary>
    BelowNormal,

    /// <summary>
    /// Normal priority (base priority 8).
    /// </summary>
    Normal,

    /// <summary>
    /// Above normal priority (base priority 10).
    /// </summary>
    AboveNormal,

    /// <summary>
    /// High priority (base priority 13).
    /// </summary>
    High,

    /// <summary>
    /// Realtime priority (base priority 24).
    /// </summary>
    Realtime,
}
