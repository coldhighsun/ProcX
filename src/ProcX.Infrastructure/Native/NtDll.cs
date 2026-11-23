using System.Runtime.InteropServices;

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
