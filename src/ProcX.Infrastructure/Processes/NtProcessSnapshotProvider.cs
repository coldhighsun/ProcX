using System.ComponentModel;
using System.Runtime.InteropServices;
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
    private const int InitialBufferSize = 512 * 1024;

    /// <summary>
    /// Extra bytes added on top of the size reported by the kernel, since processes may start between calls.
    /// </summary>
    private const int BufferSlack = 64 * 1024;

    /// <summary>
    /// The maximum number of attempts to size the buffer before giving up.
    /// </summary>
    private const int MaxAttempts = 8;

    /// <summary>
    /// Display name the kernel omits for PID 0.
    /// </summary>
    private const string IdleProcessName = "System Idle Process";

    /// <summary>
    /// Serializes access to the native buffer.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The reusable native buffer, allocated lazily.
    /// </summary>
    private unsafe void* _buffer;

    /// <summary>
    /// Size of <see cref="_buffer"/> in bytes.
    /// </summary>
    private uint _bufferSize;

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

    /// <summary>
    /// Finalizes the instance, releasing the native buffer.
    /// </summary>
    ~NtProcessSnapshotProvider() => FreeBuffer();

    /// <inheritdoc />
    /// <exception cref="Win32Exception">The native query failed.</exception>
    public unsafe ProcessSnapshot Capture()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            Query();
            var capturedAt = timeProvider.GetUtcNow();
            return new ProcessSnapshot(capturedAt, Parse((byte*)_buffer));
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
            FreeBuffer();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Fills the native buffer, growing it until the data fits.
    /// </summary>
    /// <exception cref="Win32Exception">The native query failed or the buffer could not be sized.</exception>
    private unsafe void Query()
    {
        if (_buffer is null)
        {
            Allocate(InitialBufferSize);
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var status = NtDll.NtQuerySystemInformation(
                NtDll.SystemProcessInformationClass,
                _buffer,
                _bufferSize,
                out var required);

            if (NtDll.NtSuccess(status))
            {
                return;
            }

            if (status != NtDll.StatusInfoLengthMismatch)
            {
                throw new Win32Exception((int)NtDll.RtlNtStatusToDosError(status));
            }

            Allocate(Math.Max(required + BufferSlack, _bufferSize * 2));
        }

        throw new Win32Exception(
            (int)NtDll.RtlNtStatusToDosError(NtDll.StatusInfoLengthMismatch),
            "Process information buffer kept growing; giving up.");
    }

    /// <summary>
    /// Replaces the native buffer with a new one of the given size.
    /// </summary>
    /// <param name="size">The new size in bytes.</param>
    private unsafe void Allocate(uint size)
    {
        FreeBuffer();
        _buffer = NativeMemory.Alloc(size);
        _bufferSize = size;
    }

    /// <summary>
    /// Frees the native buffer if allocated.
    /// </summary>
    private unsafe void FreeBuffer()
    {
        if (_buffer is not null)
        {
            NativeMemory.Free(_buffer);
            _buffer = null;
            _bufferSize = 0;
        }
    }

    /// <summary>
    /// Converts the chain of native records into process samples.
    /// </summary>
    /// <param name="buffer">The filled native buffer.</param>
    /// <returns>The samples in kernel enumeration order.</returns>
    private static unsafe List<ProcessSample> Parse(byte* buffer)
    {
        var result = new List<ProcessSample>(capacity: 512);
        var offset = 0L;

        while (true)
        {
            var info = (SystemProcessInformation*)(buffer + offset);
            result.Add(ToSample(in *info));

            if (info->NextEntryOffset == 0)
            {
                break;
            }

            offset += info->NextEntryOffset;
        }

        return result;
    }

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
            CreateTime = new DateTimeOffset(DateTime.FromFileTimeUtc(info.CreateTime)),
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
