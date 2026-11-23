using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ProcX.Infrastructure.Native;

/// <summary>
/// A growable native buffer for NT query APIs that report the required size on failure.
/// </summary>
/// <remarks>Not thread-safe.</remarks>
/// <param name="initialSize">The initial size in bytes.</param>
internal sealed unsafe class NtQueryBuffer(uint initialSize) : IDisposable
{
    /// <summary>
    /// The maximum number of attempts to size the buffer before giving up.
    /// </summary>
    private const int MaxAttempts = 8;

    /// <summary>
    /// The largest buffer this class will allocate (256 MB), guarding against runaway sizes.
    /// </summary>
    private const uint MaxSize = 256 * 1024 * 1024;

    /// <summary>
    /// A query that fills <c>buffer</c> of <c>length</c> bytes and reports the required size.
    /// </summary>
    /// <param name="buffer">The output buffer.</param>
    /// <param name="length">The buffer size in bytes.</param>
    /// <param name="returnLength">Receives the bytes written or required.</param>
    /// <returns>An NTSTATUS code.</returns>
    public delegate int Query(void* buffer, uint length, out uint returnLength);

    /// <summary>
    /// The native memory, allocated lazily.
    /// </summary>
    private void* _pointer;

    /// <summary>
    /// Gets the start of the buffer; valid after a successful <see cref="Fill"/>.
    /// </summary>
    public byte* Pointer => (byte*)_pointer;

    /// <summary>
    /// Gets the current capacity in bytes.
    /// </summary>
    public uint Size { get; private set; }

    /// <summary>
    /// Runs the query, growing the buffer until the data fits.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>The final NTSTATUS, which is a success code or an error other than "buffer too small".</returns>
    /// <exception cref="Win32Exception">The buffer could not be sized.</exception>
    public int Fill(Query query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_pointer is null)
        {
            Resize(initialSize);
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var status = query(_pointer, Size, out var required);
            if (!NtDll.IsBufferTooSmall(status))
            {
                return status;
            }

            // Data may grow between calls (new processes, handles), so leave some slack.
            var next = Math.Max(required + (required / 8) + 4096, Size * 2);
            if (next > MaxSize)
            {
                break;
            }

            Resize(next);
        }

        throw new Win32Exception(
            (int)NtDll.RtlNtStatusToDosError(NtDll.StatusInfoLengthMismatch),
            "Native query buffer kept growing; giving up.");
    }

    /// <summary>
    /// Releases the native memory.
    /// </summary>
    public void Dispose()
    {
        if (_pointer is not null)
        {
            NativeMemory.Free(_pointer);
            _pointer = null;
            Size = 0;
        }
    }

    /// <summary>
    /// Replaces the buffer with one of the given size.
    /// </summary>
    /// <param name="size">The new size in bytes.</param>
    private void Resize(uint size)
    {
        Dispose();
        _pointer = NativeMemory.Alloc(size);
        Size = size;
    }
}
