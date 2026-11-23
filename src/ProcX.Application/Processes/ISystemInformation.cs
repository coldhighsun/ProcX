namespace ProcX.Application.Processes;

/// <summary>
/// Provides static information about the machine.
/// </summary>
public interface ISystemInformation
{
    /// <summary>
    /// Gets the number of active logical processors across all processor groups.
    /// </summary>
    int LogicalProcessorCount { get; }
}
