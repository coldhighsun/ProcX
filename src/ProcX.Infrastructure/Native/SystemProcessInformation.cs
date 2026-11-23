using System.Runtime.InteropServices;

namespace ProcX.Infrastructure.Native;

/// <summary>
/// A counted UTF-16 string as used by native NT APIs (<c>UNICODE_STRING</c>).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct UnicodeString
{
    /// <summary>
    /// Length of the string in bytes, excluding any terminator.
    /// </summary>
    public ushort Length;

    /// <summary>
    /// Capacity of <see cref="Buffer"/> in bytes.
    /// </summary>
    public ushort MaximumLength;

    /// <summary>
    /// Pointer to the UTF-16 characters.
    /// </summary>
    public nint Buffer;

    /// <summary>
    /// Copies the native string into a managed string.
    /// </summary>
    /// <returns>The managed string; empty if the buffer is null.</returns>
    public readonly unsafe string ToManagedString() =>
        Buffer == 0 || Length == 0 ? string.Empty : new string((char*)Buffer, 0, Length / sizeof(char));
}

/// <summary>
/// The full (partially undocumented) <c>SYSTEM_PROCESS_INFORMATION</c> record returned by
/// <see cref="NtDll.NtQuerySystemInformation"/> for <see cref="NtDll.SystemProcessInformationClass"/>.
/// </summary>
/// <remarks>
/// Records are variable-length: each is followed by <see cref="NumberOfThreads"/> thread records,
/// and <see cref="NextEntryOffset"/> gives the byte offset of the next record (0 for the last).
/// Sequential layout reproduces the native alignment on both 64-bit and 32-bit platforms.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct SystemProcessInformation
{
    /// <summary>
    /// Byte offset from this record to the next one; 0 for the last record.
    /// </summary>
    public uint NextEntryOffset;

    /// <summary>
    /// Number of thread records following this record.
    /// </summary>
    public uint NumberOfThreads;

    /// <summary>
    /// Private working set size in bytes.
    /// </summary>
    public long WorkingSetPrivateSize;

    /// <summary>
    /// Number of hard page faults.
    /// </summary>
    public uint HardFaultCount;

    /// <summary>
    /// Peak number of threads.
    /// </summary>
    public uint NumberOfThreadsHighWatermark;

    /// <summary>
    /// Total CPU cycles consumed.
    /// </summary>
    public ulong CycleTime;

    /// <summary>
    /// Creation time as a FILETIME (100 ns ticks since 1601-01-01 UTC).
    /// </summary>
    public long CreateTime;

    /// <summary>
    /// User-mode CPU time in 100 ns ticks.
    /// </summary>
    public long UserTime;

    /// <summary>
    /// Kernel-mode CPU time in 100 ns ticks.
    /// </summary>
    public long KernelTime;

    /// <summary>
    /// Image file name without path.
    /// </summary>
    public UnicodeString ImageName;

    /// <summary>
    /// Base scheduling priority.
    /// </summary>
    public int BasePriority;

    /// <summary>
    /// Process ID.
    /// </summary>
    public nint UniqueProcessId;

    /// <summary>
    /// Parent process ID.
    /// </summary>
    public nint InheritedFromUniqueProcessId;

    /// <summary>
    /// Number of open handles.
    /// </summary>
    public uint HandleCount;

    /// <summary>
    /// Terminal services session ID.
    /// </summary>
    public uint SessionId;

    /// <summary>
    /// Process key (reserved).
    /// </summary>
    public nuint UniqueProcessKey;

    /// <summary>
    /// Peak virtual size in bytes.
    /// </summary>
    public nuint PeakVirtualSize;

    /// <summary>
    /// Virtual size in bytes.
    /// </summary>
    public nuint VirtualSize;

    /// <summary>
    /// Number of page faults.
    /// </summary>
    public uint PageFaultCount;

    /// <summary>
    /// Peak working set size in bytes.
    /// </summary>
    public nuint PeakWorkingSetSize;

    /// <summary>
    /// Working set size in bytes.
    /// </summary>
    public nuint WorkingSetSize;

    /// <summary>
    /// Peak paged pool quota usage in bytes.
    /// </summary>
    public nuint QuotaPeakPagedPoolUsage;

    /// <summary>
    /// Paged pool quota usage in bytes.
    /// </summary>
    public nuint QuotaPagedPoolUsage;

    /// <summary>
    /// Peak non-paged pool quota usage in bytes.
    /// </summary>
    public nuint QuotaPeakNonPagedPoolUsage;

    /// <summary>
    /// Non-paged pool quota usage in bytes.
    /// </summary>
    public nuint QuotaNonPagedPoolUsage;

    /// <summary>
    /// Commit charge in bytes.
    /// </summary>
    public nuint PagefileUsage;

    /// <summary>
    /// Peak commit charge in bytes.
    /// </summary>
    public nuint PeakPagefileUsage;

    /// <summary>
    /// Private bytes.
    /// </summary>
    public nuint PrivatePageCount;

    /// <summary>
    /// Number of read operations.
    /// </summary>
    public long ReadOperationCount;

    /// <summary>
    /// Number of write operations.
    /// </summary>
    public long WriteOperationCount;

    /// <summary>
    /// Number of other I/O operations.
    /// </summary>
    public long OtherOperationCount;

    /// <summary>
    /// Bytes read.
    /// </summary>
    public long ReadTransferCount;

    /// <summary>
    /// Bytes written.
    /// </summary>
    public long WriteTransferCount;

    /// <summary>
    /// Bytes transferred by other I/O operations.
    /// </summary>
    public long OtherTransferCount;
}
