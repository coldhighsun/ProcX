using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using ProcX.Domain.Processes;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace ProcX.Infrastructure.Processes;

/// <summary>
/// Opens process handles that are guaranteed to refer to a specific process instance.
/// </summary>
internal static class ProcessHandles
{
    /// <summary>
    /// Tolerance when comparing creation times reported by different APIs.
    /// </summary>
    private static readonly TimeSpan CreateTimeTolerance = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// Opens a process and verifies that it is the instance identified by <paramref name="key"/>.
    /// </summary>
    /// <remarks>
    /// <c>PROCESS_QUERY_LIMITED_INFORMATION</c> is always added because it is required to read the creation time.
    /// </remarks>
    /// <param name="key">The process identity.</param>
    /// <param name="access">The access rights needed by the caller.</param>
    /// <param name="error">
    /// On failure, <see cref="WIN32_ERROR.ERROR_ACCESS_DENIED"/> if access was refused, otherwise
    /// <see cref="WIN32_ERROR.ERROR_NOT_FOUND"/> (the process exited or its PID was reused).
    /// </param>
    /// <returns>The handle, or <see langword="null"/> on failure.</returns>
    public static SafeFileHandle? OpenVerified(ProcessKey key, PROCESS_ACCESS_RIGHTS access, out WIN32_ERROR error)
    {
        var handle = PInvoke.OpenProcess_SafeHandle(
            access | PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            false,
            (uint)key.Pid);

        if (handle.IsInvalid)
        {
            error = (WIN32_ERROR)Marshal.GetLastPInvokeError() == WIN32_ERROR.ERROR_ACCESS_DENIED
                ? WIN32_ERROR.ERROR_ACCESS_DENIED
                : WIN32_ERROR.ERROR_NOT_FOUND;
            handle.Dispose();
            return null;
        }

        if (!IsSameInstance(handle, key))
        {
            error = WIN32_ERROR.ERROR_NOT_FOUND;
            handle.Dispose();
            return null;
        }

        error = WIN32_ERROR.NO_ERROR;
        return handle;
    }

    /// <summary>
    /// Verifies that the handle refers to the process instance identified by <paramref name="key"/>,
    /// guarding against the PID having been reused since the snapshot.
    /// </summary>
    /// <param name="process">The process handle.</param>
    /// <param name="key">The expected identity.</param>
    /// <returns><see langword="true"/> if the creation times match.</returns>
    private static bool IsSameInstance(SafeFileHandle process, ProcessKey key)
    {
        if (!PInvoke.GetProcessTimes(process, out var creation, out _, out _, out _))
        {
            return false;
        }

        var ticks = ((long)creation.dwHighDateTime << 32) | (uint)creation.dwLowDateTime;
        var createTime = new DateTimeOffset(DateTime.FromFileTimeUtc(ticks));
        return (createTime - key.CreateTime).Duration() <= CreateTimeTolerance;
    }
}
