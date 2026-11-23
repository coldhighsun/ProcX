using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using ProcX.Application.Processes;
using ProcX.Domain.Processes;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;

namespace ProcX.Infrastructure.Processes;

/// <summary>
/// Reads static process information with Win32 APIs using the least privileged access rights.
/// </summary>
public sealed class Win32ProcessInfoProvider : IProcessInfoProvider
{
    /// <summary>
    /// Maximum path length supported by long-path aware applications.
    /// </summary>
    private const int MaxPathLength = 32_767;

    /// <summary>
    /// Tolerance when comparing creation times reported by different APIs.
    /// </summary>
    private static readonly TimeSpan CreateTimeTolerance = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// Account names indexed by SID string; translating a SID may involve a network round-trip.
    /// </summary>
    private readonly ConcurrentDictionary<string, string?> _accountNames = new();

    /// <summary>
    /// File descriptions indexed by image path, shared by all processes of the same executable.
    /// </summary>
    private readonly ConcurrentDictionary<string, string?> _descriptions = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public ProcessStaticInfo GetStaticInfo(ProcessKey key)
    {
        using var process = PInvoke.OpenProcess_SafeHandle(
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            false,
            (uint)key.Pid);

        if (process.IsInvalid)
        {
            return (WIN32_ERROR)Marshal.GetLastPInvokeError() == WIN32_ERROR.ERROR_ACCESS_DENIED
                ? ProcessStaticInfo.AccessDenied
                : ProcessStaticInfo.Unavailable;
        }

        if (!IsSameInstance(process, key))
        {
            return ProcessStaticInfo.Unavailable;
        }

        var imagePath = GetImagePath(process);
        var description = imagePath is null ? null : _descriptions.GetOrAdd(imagePath, GetFileDescription);

        return new ProcessStaticInfo(imagePath, GetUserName(process), description, IsAccessDenied: false);
    }

    /// <summary>
    /// Verifies that the opened handle refers to the process instance identified by <paramref name="key"/>,
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

    /// <summary>
    /// Gets the Win32 path of the process executable.
    /// </summary>
    /// <param name="process">The process handle.</param>
    /// <returns>The path, or <see langword="null"/> if it cannot be queried (e.g. for the System process).</returns>
    private static string? GetImagePath(SafeFileHandle process)
    {
        Span<char> buffer = stackalloc char[260];
        var size = (uint)buffer.Length;
        if (PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref size))
        {
            return new string(buffer[..(int)size]);
        }

        if ((WIN32_ERROR)Marshal.GetLastPInvokeError() != WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
        {
            return null;
        }

        var large = new char[MaxPathLength];
        size = (uint)large.Length;
        return PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, large, ref size)
            ? new string(large, 0, (int)size)
            : null;
    }

    /// <summary>
    /// Gets the file description from the version resource of an executable.
    /// </summary>
    /// <param name="imagePath">The executable path.</param>
    /// <returns>The description, or <see langword="null"/> if absent or unreadable.</returns>
    private static string? GetFileDescription(string imagePath)
    {
        try
        {
            var description = FileVersionInfo.GetVersionInfo(imagePath).FileDescription;
            return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the account name (without domain) the process runs as.
    /// </summary>
    /// <param name="process">The process handle.</param>
    /// <returns>The account name, or <see langword="null"/> if the token cannot be queried.</returns>
    private string? GetUserName(SafeFileHandle process)
    {
        if (!PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
        {
            return null;
        }

        using (token)
        {
            var sid = GetTokenUserSid(token);
            return sid is null ? null : _accountNames.GetOrAdd(sid.Value, _ => TranslateSid(sid));
        }
    }

    /// <summary>
    /// Reads the user SID from a token.
    /// </summary>
    /// <param name="token">The token handle opened with <c>TOKEN_QUERY</c>.</param>
    /// <returns>The SID, or <see langword="null"/> on failure.</returns>
    private static unsafe SecurityIdentifier? GetTokenUserSid(SafeFileHandle token)
    {
        PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenUser, null, 0, out var length);
        if (length == 0)
        {
            return null;
        }

        var buffer = stackalloc byte[(int)length];
        if (!PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenUser, buffer, length, out _))
        {
            return null;
        }

        var tokenUser = (TOKEN_USER*)buffer;
        return new SecurityIdentifier((nint)tokenUser->User.Sid.Value);
    }

    /// <summary>
    /// Translates a SID to an account name, falling back to the SID string when it cannot be mapped.
    /// </summary>
    /// <param name="sid">The SID.</param>
    /// <returns>The account name without the domain part.</returns>
    private static string TranslateSid(SecurityIdentifier sid)
    {
        try
        {
            var account = sid.Translate(typeof(NTAccount)).Value;
            var separator = account.IndexOf('\\', StringComparison.Ordinal);
            return separator >= 0 ? account[(separator + 1)..] : account;
        }
        catch (IdentityNotMappedException)
        {
            return sid.Value;
        }
    }
}
