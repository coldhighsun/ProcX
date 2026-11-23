using System.Runtime.InteropServices;

namespace ProcX.Infrastructure.Native;

/// <summary>
/// The <c>SYSTEM_THREAD_INFORMATION</c> record that follows each <see cref="SystemProcessInformation"/> record.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SystemThreadInformation
{
    /// <summary>
    /// Kernel-mode CPU time in 100 ns ticks.
    /// </summary>
    public long KernelTime;

    /// <summary>
    /// User-mode CPU time in 100 ns ticks.
    /// </summary>
    public long UserTime;

    /// <summary>
    /// Creation time as a FILETIME.
    /// </summary>
    public long CreateTime;

    /// <summary>
    /// Time spent waiting, in ticks of the system clock.
    /// </summary>
    public uint WaitTime;

    /// <summary>
    /// The start address reported by the kernel (usually <c>RtlUserThreadStart</c> for user threads).
    /// </summary>
    public nint StartAddress;

    /// <summary>
    /// The owning process ID.
    /// </summary>
    public nint UniqueProcess;

    /// <summary>
    /// The thread ID.
    /// </summary>
    public nint UniqueThread;

    /// <summary>
    /// The current dynamic priority.
    /// </summary>
    public int Priority;

    /// <summary>
    /// The base priority.
    /// </summary>
    public int BasePriority;

    /// <summary>
    /// The number of context switches.
    /// </summary>
    public uint ContextSwitches;

    /// <summary>
    /// The <c>KTHREAD_STATE</c>.
    /// </summary>
    public uint ThreadState;

    /// <summary>
    /// The <c>KWAIT_REASON</c>.
    /// </summary>
    public uint WaitReason;
}
