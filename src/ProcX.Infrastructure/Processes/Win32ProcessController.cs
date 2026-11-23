using System.Runtime.InteropServices;
using ProcX.Application.Processes;
using ProcX.Domain.Processes;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace ProcX.Infrastructure.Processes;

/// <summary>
/// Changes process state through Win32 APIs.
/// </summary>
public sealed class Win32ProcessController : IProcessController
{
    /// <summary>
    /// The exit code assigned to terminated processes (the same value Task Manager uses).
    /// </summary>
    private const uint TerminatedExitCode = 1;

    /// <summary>
    /// The exit code reported for a process that is still running (<c>STILL_ACTIVE</c>).
    /// </summary>
    private const uint StillActive = 259;

    /// <inheritdoc />
    public TerminationResult Terminate(ProcessKey key)
    {
        using var process = ProcessHandles.OpenVerified(key, PROCESS_ACCESS_RIGHTS.PROCESS_TERMINATE, out var error);
        if (process is null)
        {
            return new TerminationResult(key, ToStatus(error), (int)error);
        }

        if (PInvoke.TerminateProcess(process, TerminatedExitCode))
        {
            return new TerminationResult(key, TerminationStatus.Succeeded);
        }

        var terminateError = (WIN32_ERROR)Marshal.GetLastPInvokeError();

        // An exited process stays openable while another handle to it is held; terminating it then fails
        // with ERROR_ACCESS_DENIED even though there is nothing left to terminate.
        if (HasExited(process))
        {
            return new TerminationResult(key, TerminationStatus.NotFound, (int)WIN32_ERROR.ERROR_NOT_FOUND);
        }

        return new TerminationResult(key, ToStatus(terminateError), (int)terminateError);
    }

    /// <summary>
    /// Determines whether the process has exited.
    /// </summary>
    /// <param name="process">A handle with at least <c>PROCESS_QUERY_LIMITED_INFORMATION</c> access.</param>
    /// <returns><see langword="true"/> if the process has an exit code.</returns>
    private static bool HasExited(SafeHandle process) =>
        PInvoke.GetExitCodeProcess(process, out var exitCode) && exitCode != StillActive;

    /// <summary>
    /// Maps a Win32 error to a termination status.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The status.</returns>
    private static TerminationStatus ToStatus(WIN32_ERROR error) => error switch
    {
        WIN32_ERROR.ERROR_ACCESS_DENIED => TerminationStatus.AccessDenied,
        WIN32_ERROR.ERROR_NOT_FOUND => TerminationStatus.NotFound,
        _ => TerminationStatus.Failed,
    };
}
