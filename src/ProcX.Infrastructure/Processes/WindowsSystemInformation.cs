using ProcX.Application.Processes;
using Windows.Win32;

namespace ProcX.Infrastructure.Processes;

/// <summary>
/// Reads machine information from Win32 APIs.
/// </summary>
public sealed class WindowsSystemInformation : ISystemInformation
{
    /// <summary>
    /// Gets the number of active logical processors across all processor groups.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Environment.ProcessorCount"/>, this is not limited by the affinity or job
    /// object of the current process, which matters because CPU usage is normalized system-wide.
    /// </remarks>
    public int LogicalProcessorCount { get; } = (int)PInvoke.GetActiveProcessorCount((ushort)PInvoke.ALL_PROCESSOR_GROUPS);
}
