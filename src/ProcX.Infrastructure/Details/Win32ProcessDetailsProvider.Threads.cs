using System.Globalization;
using ProcX.Application.Details;
using ProcX.Domain.Processes;
using ProcX.Infrastructure.Native;
using ProcX.Infrastructure.Processes;
using Windows.Win32;
using Windows.Win32.System.Threading;

namespace ProcX.Infrastructure.Details;

/// <summary>
/// Thread enumeration.
/// </summary>
public sealed partial class Win32ProcessDetailsProvider
{
    /// <summary>
    /// Names of <c>KWAIT_REASON</c> values, indexed by value.
    /// </summary>
    private static readonly string[] WaitReasonNames =
    [
        "Executive", "FreePage", "PageIn", "PoolAllocation", "DelayExecution", "Suspended", "UserRequest",
        "WrExecutive", "WrFreePage", "WrPageIn", "WrPoolAllocation", "WrDelayExecution", "WrSuspended",
        "WrUserRequest", "WrSpare0", "WrQueue", "WrLpcReceive", "WrLpcReply", "WrVirtualMemory", "WrPageOut",
        "WrRendezvous", "WrKeyedEvent", "WrTerminated", "WrProcessInSwap", "WrCpuRateControl", "WrCalloutStack",
        "WrKernel", "WrResource", "WrPushLock", "WrMutex", "WrQuantumEnd", "WrDispatchInt", "WrPreempted",
        "WrYieldExecution", "WrFastMutex", "WrGuardedMutex", "WrRundown", "WrAlertByThreadId", "WrDeferredPreempt",
        "WrPhysicalFault", "WrIoRing", "WrMdlCache", "WrRcu",
    ];

    /// <inheritdoc />
    public unsafe DetailsResult<IReadOnlyList<ThreadDetails>> GetThreads(ProcessKey key)
    {
        List<ThreadRecord> records;
        using (var buffer = new NtQueryBuffer(NtProcessSnapshotProvider.InitialBufferSize))
        {
            NtProcessSnapshotProvider.Fill(buffer);
            var process = FindProcessRecord(buffer.Pointer, key);
            if (process is null)
            {
                return DetailsResult.Fail<IReadOnlyList<ThreadDetails>>(DetailsStatus.NotFound);
            }

            records = ReadThreadRecords(process);
        }

        // Module list is only used to label start addresses; missing access just leaves them unresolved.
        var modules = EnumerateModules(key, includeVersions: false).Value ?? [];

        var result = records
            .Select(r =>
            {
                var start = ReadWin32StartAddress(r.ThreadId) ?? r.KernelStartAddress;
                return new ThreadDetails(
                    r.ThreadId,
                    r.CreateTime,
                    r.KernelTime,
                    r.UserTime,
                    r.Priority,
                    r.BasePriority,
                    r.ContextSwitches,
                    r.State,
                    r.State == ThreadSchedulingState.Waiting ? DescribeWaitReason(r.WaitReason) : null,
                    start,
                    ResolveSymbol(modules, start));
            })
            .OrderBy(static t => t.ThreadId)
            .ToList();

        return DetailsResult.Ok<IReadOnlyList<ThreadDetails>>(result);
    }

    /// <summary>
    /// Formats an address as <c>module+0xoffset</c> using the module that contains it.
    /// </summary>
    /// <param name="modules">The loaded modules.</param>
    /// <param name="address">The address.</param>
    /// <returns>The symbol, or <see langword="null"/> if no module contains the address.</returns>
    internal static string? ResolveSymbol(IReadOnlyList<ModuleDetails> modules, ulong address)
    {
        foreach (var module in modules)
        {
            if (module.Contains(address))
            {
                return string.Create(CultureInfo.InvariantCulture, $"{module.Name}+0x{address - module.BaseAddress:x}");
            }
        }

        return null;
    }

    /// <summary>
    /// Names a <c>KWAIT_REASON</c> value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The name, or the number for values newer than this table.</returns>
    internal static string DescribeWaitReason(uint value) =>
        value < WaitReasonNames.Length ? WaitReasonNames[value] : value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Finds the record of a process instance in a <c>SystemProcessInformation</c> buffer.
    /// </summary>
    /// <param name="buffer">The filled buffer.</param>
    /// <param name="key">The process identity.</param>
    /// <returns>The record, or <see langword="null"/> if the process is not present.</returns>
    private static unsafe SystemProcessInformation* FindProcessRecord(byte* buffer, ProcessKey key)
    {
        foreach (var address in NtProcessSnapshotProvider.EnumerateRecordAddresses(buffer))
        {
            var record = (SystemProcessInformation*)address;
            if ((int)record->UniqueProcessId == key.Pid
                && NtProcessSnapshotProvider.FromFileTime(record->CreateTime) == key.CreateTime)
            {
                return record;
            }
        }

        return null;
    }

    /// <summary>
    /// Copies the thread records that follow a process record.
    /// </summary>
    /// <param name="process">The process record.</param>
    /// <returns>The threads.</returns>
    private static unsafe List<ThreadRecord> ReadThreadRecords(SystemProcessInformation* process)
    {
        var threads = (SystemThreadInformation*)((byte*)process + sizeof(SystemProcessInformation));
        var result = new List<ThreadRecord>((int)process->NumberOfThreads);
        for (var i = 0; i < process->NumberOfThreads; i++)
        {
            var t = threads[i];
            result.Add(new ThreadRecord(
                (int)t.UniqueThread,
                NtProcessSnapshotProvider.FromFileTime(t.CreateTime),
                TimeSpan.FromTicks(t.KernelTime),
                TimeSpan.FromTicks(t.UserTime),
                t.Priority,
                t.BasePriority,
                t.ContextSwitches,
                (ThreadSchedulingState)t.ThreadState,
                t.WaitReason,
                (ulong)t.StartAddress));
        }

        return result;
    }

    /// <summary>
    /// Reads the Win32 start address of a thread, which identifies its entry point better than the kernel one.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <returns>The address, or <see langword="null"/> if the thread cannot be queried.</returns>
    private static unsafe ulong? ReadWin32StartAddress(int threadId)
    {
        using var thread = PInvoke.OpenThread_SafeHandle(THREAD_ACCESS_RIGHTS.THREAD_QUERY_INFORMATION, false, (uint)threadId);
        if (thread.IsInvalid)
        {
            return null;
        }

        nint address;
        var status = NtDll.NtQueryInformationThread(thread, NtDll.ThreadQuerySetWin32StartAddress, &address, (uint)sizeof(nint), out _);
        return NtDll.NtSuccess(status) && address != 0 ? (ulong)address : null;
    }

    /// <summary>
    /// A thread record copied out of the native buffer.
    /// </summary>
    /// <param name="ThreadId">The thread ID.</param>
    /// <param name="CreateTime">The creation time.</param>
    /// <param name="KernelTime">Kernel-mode CPU time.</param>
    /// <param name="UserTime">User-mode CPU time.</param>
    /// <param name="Priority">The dynamic priority.</param>
    /// <param name="BasePriority">The base priority.</param>
    /// <param name="ContextSwitches">The number of context switches.</param>
    /// <param name="State">The scheduler state.</param>
    /// <param name="WaitReason">The raw wait reason.</param>
    /// <param name="KernelStartAddress">The start address reported by the kernel.</param>
    private sealed record ThreadRecord(
        int ThreadId,
        DateTimeOffset CreateTime,
        TimeSpan KernelTime,
        TimeSpan UserTime,
        int Priority,
        int BasePriority,
        uint ContextSwitches,
        ThreadSchedulingState State,
        uint WaitReason,
        ulong KernelStartAddress);
}
