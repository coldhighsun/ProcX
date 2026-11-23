using System.ComponentModel;
using ProcX.Application.Processes;
using ProcX.Domain.Processes;
using ProcX.Infrastructure.Native;

namespace ProcX.Infrastructure.Processes;

/// <summary>
/// Enumerates processes with <c>NtQuerySystemInformation(SystemProcessInformation)</c>.
/// </summary>
/// <remarks>
/// A single system call returns the counters of every process without opening any process handle,
/// which is much cheaper than <see cref="System.Diagnostics.Process"/> and works for protected processes.
/// The native buffer is reused across calls; the instance is thread-safe.
/// </remarks>
/// <param name="timeProvider">The clock used to timestamp snapshots.</param>
public sealed class NtProcessSnapshotProvider(TimeProvider timeProvider) : IProcessSnapshotProvider, IDisposable
{
    /// <summary>
    /// The initial native buffer size in bytes.
    /// </summary>
    internal const uint InitialBufferSize = 512 * 1024;

    /// <summary>
    /// Display name the kernel omits for PID 0.
    /// </summary>
    private const string IdleProcessName = "System Idle Process";

    /// <summary>
    /// Serializes access to the native buffer.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The reusable native buffer.
    /// </summary>
    private readonly NtQueryBuffer _buffer = new(InitialBufferSize);

    /// <summary>
    /// Whether the instance has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NtProcessSnapshotProvider"/> class using the system clock.
    /// </summary>
    public NtProcessSnapshotProvider()
        : this(TimeProvider.System)
    {
    }

    /// <inheritdoc />
    /// <exception cref="Win32Exception">The native query failed.</exception>
    public unsafe ProcessSnapshot Capture()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            Fill(_buffer);
            var capturedAt = timeProvider.GetUtcNow();
            var records = EnumerateRecordAddresses(_buffer.Pointer);
            var result = new List<ProcessSample>(records.Count);
            foreach (var record in records)
            {
                result.Add(ToSample(in *(SystemProcessInformation*)record));
            }

            return new ProcessSnapshot(capturedAt, result);
        }
    }

    /// <summary>
    /// Releases the native buffer.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _buffer.Dispose();
        }
    }

    /// <summary>
    /// Fills a buffer with <c>SystemProcessInformation</c>.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <exception cref="Win32Exception">The native query failed.</exception>
    internal static unsafe void Fill(NtQueryBuffer buffer)
    {
        var status = buffer.Fill(static (data, length, out required) =>
            NtDll.NtQuerySystemInformation(NtDll.SystemProcessInformationClass, data, length, out required));

        if (!NtDll.NtSuccess(status))
        {
            throw new Win32Exception((int)NtDll.RtlNtStatusToDosError(status));
        }
    }

    /// <summary>
    /// Enumerates the chain of process records in a filled buffer.
    /// </summary>
    /// <param name="buffer">The start of the buffer.</param>
    /// <returns>Addresses of each <see cref="SystemProcessInformation"/> record, valid while the buffer is unchanged.</returns>
    internal static unsafe List<nint> EnumerateRecordAddresses(byte* buffer)
    {
        var result = new List<nint>(capacity: 512);
        var offset = 0L;
        while (true)
        {
            var info = (SystemProcessInformation*)(buffer + offset);
            result.Add((nint)info);

            if (info->NextEntryOffset == 0)
            {
                return result;
            }

            offset += info->NextEntryOffset;
        }
    }

    /// <summary>
    /// Converts a FILETIME tick count into a UTC timestamp.
    /// </summary>
    /// <param name="fileTime">100 ns ticks since 1601-01-01 UTC.</param>
    /// <returns>The timestamp.</returns>
    internal static DateTimeOffset FromFileTime(long fileTime) => new(DateTime.FromFileTimeUtc(fileTime));

    /// <summary>
    /// Converts one native record into a process sample.
    /// </summary>
    /// <param name="info">The native record.</param>
    /// <returns>The sample.</returns>
    private static ProcessSample ToSample(in SystemProcessInformation info)
    {
        var pid = (int)info.UniqueProcessId;
        var name = info.ImageName.ToManagedString();

        return new ProcessSample
        {
            Pid = pid,
            ParentPid = (int)info.InheritedFromUniqueProcessId,
            Name = pid == 0 && name.Length == 0 ? IdleProcessName : name,
            CreateTime = FromFileTime(info.CreateTime),
            KernelTime = TimeSpan.FromTicks(info.KernelTime),
            UserTime = TimeSpan.FromTicks(info.UserTime),
            WorkingSetBytes = (long)info.WorkingSetSize,
            PrivateWorkingSetBytes = info.WorkingSetPrivateSize,
            PrivateBytes = (long)info.PagefileUsage,
            ThreadCount = (int)info.NumberOfThreads,
            HandleCount = (int)info.HandleCount,
            SessionId = (int)info.SessionId,
            BasePriority = info.BasePriority,
        };
    }
}
