using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
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
            return new TerminationResult(key, ToTerminationStatus(error), (int)error);
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

        return new TerminationResult(key, ToTerminationStatus(terminateError), (int)terminateError);
    }

    /// <inheritdoc />
    public ProcessOperationResult<bool> SetPriority(ProcessKey key, ProcessPriority priority)
    {
        var flag = priority switch
        {
            ProcessPriority.Idle => PROCESS_CREATION_FLAGS.IDLE_PRIORITY_CLASS,
            ProcessPriority.BelowNormal => PROCESS_CREATION_FLAGS.BELOW_NORMAL_PRIORITY_CLASS,
            ProcessPriority.Normal => PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS,
            ProcessPriority.AboveNormal => PROCESS_CREATION_FLAGS.ABOVE_NORMAL_PRIORITY_CLASS,
            ProcessPriority.High => PROCESS_CREATION_FLAGS.HIGH_PRIORITY_CLASS,
            ProcessPriority.Realtime => PROCESS_CREATION_FLAGS.REALTIME_PRIORITY_CLASS,
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, null),
        };

        using var process = ProcessHandles.OpenVerified(key, PROCESS_ACCESS_RIGHTS.PROCESS_SET_INFORMATION, out var error);
        if (process is null)
        {
            return ProcessOperationResult.Fail<bool>(ToStatus(error), (int)error);
        }

        return PInvoke.SetPriorityClass(process, flag)
            ? ProcessOperationResult.Ok(true)
            : Failure<bool>(process);
    }

    /// <inheritdoc />
    public ProcessOperationResult<AffinityInfo> GetAffinity(ProcessKey key)
    {
        if (PInvoke.GetActiveProcessorGroupCount() > 1)
        {
            return ProcessOperationResult.Fail<AffinityInfo>(ProcessOperationStatus.Unsupported);
        }

        using var process = ProcessHandles.OpenVerified(key, PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, out var error);
        if (process is null)
        {
            return ProcessOperationResult.Fail<AffinityInfo>(ToStatus(error), (int)error);
        }

        if (!PInvoke.GetProcessAffinityMask(process, out var processMask, out var systemMask))
        {
            return Failure<AffinityInfo>(process);
        }

        // A process that exited can still be queried while any handle to it is open.
        return HasExited(process)
            ? ProcessOperationResult.Fail<AffinityInfo>(ProcessOperationStatus.NotFound, (int)WIN32_ERROR.ERROR_NOT_FOUND)
            : ProcessOperationResult.Ok(new AffinityInfo(new AffinityMask(processMask), new AffinityMask(systemMask)));
    }

    /// <inheritdoc />
    public ProcessOperationResult<bool> SetAffinity(ProcessKey key, AffinityMask mask)
    {
        if (mask.IsEmpty)
        {
            return ProcessOperationResult.Fail<bool>(ProcessOperationStatus.Failed, (int)WIN32_ERROR.ERROR_INVALID_PARAMETER);
        }

        if (PInvoke.GetActiveProcessorGroupCount() > 1)
        {
            return ProcessOperationResult.Fail<bool>(ProcessOperationStatus.Unsupported);
        }

        using var process = ProcessHandles.OpenVerified(key, PROCESS_ACCESS_RIGHTS.PROCESS_SET_INFORMATION, out var error);
        if (process is null)
        {
            return ProcessOperationResult.Fail<bool>(ToStatus(error), (int)error);
        }

        return PInvoke.SetProcessAffinityMask(process, (nuint)mask.Value)
            ? ProcessOperationResult.Ok(true)
            : Failure<bool>(process);
    }

    /// <summary>
    /// Builds the failure result of the last Win32 call, treating an exited process as not found.
    /// </summary>
    /// <typeparam name="T">The result data type.</typeparam>
    /// <param name="process">The process handle used by the failed call.</param>
    /// <returns>The failure result.</returns>
    private static ProcessOperationResult<T> Failure<T>(SafeFileHandle process)
    {
        var error = (WIN32_ERROR)Marshal.GetLastPInvokeError();
        return HasExited(process)
            ? ProcessOperationResult.Fail<T>(ProcessOperationStatus.NotFound, (int)WIN32_ERROR.ERROR_NOT_FOUND)
            : ProcessOperationResult.Fail<T>(ToStatus(error), (int)error);
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
    private static TerminationStatus ToTerminationStatus(WIN32_ERROR error) => error switch
    {
        WIN32_ERROR.ERROR_ACCESS_DENIED => TerminationStatus.AccessDenied,
        WIN32_ERROR.ERROR_NOT_FOUND => TerminationStatus.NotFound,
        _ => TerminationStatus.Failed,
    };

    /// <summary>
    /// Maps a Win32 error to an operation status.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The status.</returns>
    private static ProcessOperationStatus ToStatus(WIN32_ERROR error) => error switch
    {
        WIN32_ERROR.ERROR_ACCESS_DENIED => ProcessOperationStatus.AccessDenied,
        WIN32_ERROR.ERROR_NOT_FOUND => ProcessOperationStatus.NotFound,
        _ => ProcessOperationStatus.Failed,
    };
}
