using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ProcX.Infrastructure.Native;

/// <summary>
/// P/Invoke declarations for ntdll.dll APIs that are not covered by CsWin32 metadata.
/// </summary>
internal static partial class NtDll
{
    /// <summary>
    /// NTSTATUS returned when the supplied buffer is too small.
    /// </summary>
    public const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

    /// <summary>
    /// <c>SYSTEM_INFORMATION_CLASS</c> value returning <see cref="SystemProcessInformation"/> records.
    /// </summary>
    public const int SystemProcessInformationClass = 5;

    /// <summary>
    /// Retrieves the specified system information.
    /// </summary>
    /// <param name="systemInformationClass">The kind of information to retrieve.</param>
    /// <param name="systemInformation">The output buffer.</param>
    /// <param name="systemInformationLength">The size of the output buffer in bytes.</param>
    /// <param name="returnLength">Receives the number of bytes written or required.</param>
    /// <returns>An NTSTATUS code.</returns>
    [LibraryImport("ntdll.dll")]
    public static unsafe partial int NtQuerySystemInformation(
        int systemInformationClass,
        void* systemInformation,
        uint systemInformationLength,
        out uint returnLength);

    /// <summary>
    /// NTSTATUS (warning) returned when the output was truncated.
    /// </summary>
    public const int StatusBufferOverflow = unchecked((int)0x80000005);

    /// <summary>
    /// NTSTATUS returned when the buffer is too small to hold any data.
    /// </summary>
    public const int StatusBufferTooSmall = unchecked((int)0xC0000023);

    /// <summary>
    /// NTSTATUS returned when access to an object is denied.
    /// </summary>
    public const int StatusAccessDenied = unchecked((int)0xC0000022);

    /// <summary>
    /// <c>PROCESSINFOCLASS</c> value returning the command line as a <see cref="UnicodeString"/> (Windows 8.1+).
    /// </summary>
    public const int ProcessCommandLineInformation = 60;

    /// <summary>
    /// <c>PROCESSINFOCLASS</c> value returning a snapshot of the handle table (Windows 8+).
    /// </summary>
    public const int ProcessHandleInformation = 51;

    /// <summary>
    /// <c>THREADINFOCLASS</c> value returning the Win32 start address of a thread.
    /// </summary>
    public const int ThreadQuerySetWin32StartAddress = 9;

    /// <summary>
    /// <c>OBJECT_INFORMATION_CLASS</c> value returning the object name.
    /// </summary>
    public const int ObjectNameInformation = 1;

    /// <summary>
    /// <c>OBJECT_INFORMATION_CLASS</c> value returning all object types.
    /// </summary>
    public const int ObjectTypesInformation = 3;

    /// <summary>
    /// Retrieves information about a process.
    /// </summary>
    /// <param name="processHandle">The process handle.</param>
    /// <param name="processInformationClass">The kind of information.</param>
    /// <param name="processInformation">The output buffer.</param>
    /// <param name="processInformationLength">The size of the output buffer in bytes.</param>
    /// <param name="returnLength">Receives the number of bytes written or required.</param>
    /// <returns>An NTSTATUS code.</returns>
    [LibraryImport("ntdll.dll")]
    public static unsafe partial int NtQueryInformationProcess(
        SafeFileHandle processHandle,
        int processInformationClass,
        void* processInformation,
        uint processInformationLength,
        out uint returnLength);

    /// <summary>
    /// Retrieves information about a thread.
    /// </summary>
    /// <param name="threadHandle">The thread handle.</param>
    /// <param name="threadInformationClass">The kind of information.</param>
    /// <param name="threadInformation">The output buffer.</param>
    /// <param name="threadInformationLength">The size of the output buffer in bytes.</param>
    /// <param name="returnLength">Receives the number of bytes written or required.</param>
    /// <returns>An NTSTATUS code.</returns>
    [LibraryImport("ntdll.dll")]
    public static unsafe partial int NtQueryInformationThread(
        SafeFileHandle threadHandle,
        int threadInformationClass,
        void* threadInformation,
        uint threadInformationLength,
        out uint returnLength);

    /// <summary>
    /// Retrieves information about a kernel object.
    /// </summary>
    /// <param name="handle">The object handle, or 0 for <see cref="ObjectTypesInformation"/>.</param>
    /// <param name="objectInformationClass">The kind of information.</param>
    /// <param name="objectInformation">The output buffer.</param>
    /// <param name="objectInformationLength">The size of the output buffer in bytes.</param>
    /// <param name="returnLength">Receives the number of bytes written or required.</param>
    /// <returns>An NTSTATUS code.</returns>
    [LibraryImport("ntdll.dll")]
    public static unsafe partial int NtQueryObject(
        nint handle,
        int objectInformationClass,
        void* objectInformation,
        uint objectInformationLength,
        out uint returnLength);

    /// <summary>
    /// Determines whether a status asks for a larger buffer.
    /// </summary>
    /// <param name="status">The NTSTATUS code.</param>
    /// <returns><see langword="true"/> if the call should be retried with a larger buffer.</returns>
    public static bool IsBufferTooSmall(int status) =>
        status is StatusInfoLengthMismatch or StatusBufferOverflow or StatusBufferTooSmall;

    /// <summary>
    /// Converts an NTSTATUS code to a Win32 error code.
    /// </summary>
    /// <param name="status">The NTSTATUS code.</param>
    /// <returns>The Win32 error code.</returns>
    [LibraryImport("ntdll.dll")]
    public static partial uint RtlNtStatusToDosError(int status);

    /// <summary>
    /// Determines whether an NTSTATUS code indicates success (including informational and warning codes).
    /// </summary>
    /// <param name="status">The NTSTATUS code.</param>
    /// <returns><see langword="true"/> if the status is not an error.</returns>
    public static bool NtSuccess(int status) => status >= 0;
}
