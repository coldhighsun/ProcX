namespace ProcX.Domain.Processes;

/// <summary>
/// Identifies system processes whose termination crashes or destabilizes Windows.
/// </summary>
public static class CriticalProcesses
{
    /// <summary>
    /// PIDs of kernel pseudo-processes that can never be terminated.
    /// </summary>
    private static readonly HashSet<int> KernelPids = [0, 4];

    /// <summary>
    /// Image names of user-mode processes Windows marks as critical.
    /// </summary>
    private static readonly HashSet<string> CriticalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "csrss.exe",
        "lsass.exe",
        "services.exe",
        "smss.exe",
        "wininit.exe",
        "winlogon.exe",
        "Registry",
        "Memory Compression",
        "Secure System",
    };

    /// <summary>
    /// Determines whether terminating the process would likely crash or log off the system.
    /// </summary>
    /// <remarks>
    /// The check is name-based and intended for warnings only; it is not a security boundary.
    /// </remarks>
    /// <param name="process">The process.</param>
    /// <returns><see langword="true"/> if the process is critical.</returns>
    public static bool IsCritical(ProcessSample process)
    {
        ArgumentNullException.ThrowIfNull(process);

        return IsCritical(process.Pid, process.Name);
    }

    /// <summary>
    /// Determines whether terminating the process would likely crash or log off the system.
    /// </summary>
    /// <param name="pid">The process ID.</param>
    /// <param name="name">The image name.</param>
    /// <returns><see langword="true"/> if the process is critical.</returns>
    public static bool IsCritical(int pid, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return KernelPids.Contains(pid) || CriticalNames.Contains(name);
    }
}
