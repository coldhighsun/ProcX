namespace ProcX.Application.Settings;

/// <summary>
/// The optional columns of the process list. The Name column is always shown and therefore not listed.
/// </summary>
[Flags]
public enum ProcessColumns
{
    /// <summary>
    /// No column.
    /// </summary>
    None = 0,

    /// <summary>
    /// The process identifier.
    /// </summary>
    Pid = 1 << 0,

    /// <summary>
    /// The product or file description.
    /// </summary>
    Description = 1 << 1,

    /// <summary>
    /// The CPU usage.
    /// </summary>
    Cpu = 1 << 2,

    /// <summary>
    /// The working set size.
    /// </summary>
    Memory = 1 << 3,

    /// <summary>
    /// The committed memory.
    /// </summary>
    Commit = 1 << 4,

    /// <summary>
    /// The thread count.
    /// </summary>
    Threads = 1 << 5,

    /// <summary>
    /// The handle count.
    /// </summary>
    Handles = 1 << 6,

    /// <summary>
    /// The priority class.
    /// </summary>
    Priority = 1 << 7,

    /// <summary>
    /// The owning user.
    /// </summary>
    User = 1 << 8,

    /// <summary>
    /// The session identifier.
    /// </summary>
    Session = 1 << 9,

    /// <summary>
    /// The image path.
    /// </summary>
    Path = 1 << 10,

    /// <summary>
    /// Every column.
    /// </summary>
    All = (1 << 11) - 1,
}
