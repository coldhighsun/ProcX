using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using ProcX.Application.Details;
using ProcX.Domain.Processes;
using ProcX.Infrastructure.Native;
using ProcX.Infrastructure.Processes;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.SystemInformation;
using Windows.Win32.System.Threading;

namespace ProcX.Infrastructure.Details;

/// <summary>
/// Reads process details with Win32 and NT APIs. This part contains shared helpers and general information.
/// </summary>
public sealed partial class Win32ProcessDetailsProvider : IProcessDetailsProvider
{
    /// <summary>
    /// Version resource data indexed by file path, shared by every process that loads the file.
    /// </summary>
    private readonly ConcurrentDictionary<string, FileVersionData?> _versions = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public DetailsResult<ProcessGeneralInfo> GetGeneral(ProcessKey key, string? imagePath)
    {
        using var process = ProcessHandles.OpenVerified(key, PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, out var error);
        if (process is null)
        {
            return DetailsResult.Fail<ProcessGeneralInfo>(ToStatus(error));
        }

        imagePath ??= Win32ProcessInfoProvider.GetImagePath(process);
        var version = imagePath is null ? null : GetVersion(imagePath);
        var (isElevated, integrity) = ReadToken(process);

        return DetailsResult.Ok(new ProcessGeneralInfo(
            ReadCommandLine(process),
            ReadArchitecture(process),
            isElevated,
            integrity,
            version?.CompanyName,
            version?.FileVersion));
    }

    /// <summary>
    /// Maps an integrity level RID (the last sub-authority of a mandatory label SID) to a level.
    /// </summary>
    /// <param name="rid">The RID, e.g. <c>0x2000</c> for medium.</param>
    /// <returns>The level.</returns>
    internal static IntegrityLevel MapIntegrityLevel(uint rid) => rid switch
    {
        < 0x1000 => IntegrityLevel.Untrusted,
        < 0x2000 => IntegrityLevel.Low,
        < 0x2100 => IntegrityLevel.Medium,
        < 0x3000 => IntegrityLevel.MediumPlus,
        < 0x4000 => IntegrityLevel.High,
        < 0x5000 => IntegrityLevel.System,
        _ => IntegrityLevel.Protected,
    };

    /// <summary>
    /// Maps an <c>IMAGE_FILE_MACHINE_*</c> value to an architecture.
    /// </summary>
    /// <param name="machine">The machine value.</param>
    /// <returns>The architecture.</returns>
    internal static ProcessArchitecture MapMachine(ushort machine) => machine switch
    {
        0x014C => ProcessArchitecture.X86,
        0x8664 => ProcessArchitecture.X64,
        0xAA64 => ProcessArchitecture.Arm64,
        0x01C0 or 0x01C2 or 0x01C4 => ProcessArchitecture.Arm,
        _ => ProcessArchitecture.Unknown,
    };

    /// <summary>
    /// Maps the failure of <see cref="ProcessHandles.OpenVerified"/> to a details status.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The status.</returns>
    private static DetailsStatus ToStatus(WIN32_ERROR error) =>
        error == WIN32_ERROR.ERROR_ACCESS_DENIED ? DetailsStatus.AccessDenied : DetailsStatus.NotFound;

    /// <summary>
    /// Reads the command line.
    /// </summary>
    /// <param name="process">A handle with <c>PROCESS_QUERY_LIMITED_INFORMATION</c>.</param>
    /// <returns>The command line, or <see langword="null"/> if unavailable.</returns>
    private static unsafe string? ReadCommandLine(SafeFileHandle process)
    {
        using var buffer = new NtQueryBuffer(1024);
        var status = buffer.Fill((data, length, out required) =>
            NtDll.NtQueryInformationProcess(process, NtDll.ProcessCommandLineInformation, data, length, out required));

        return NtDll.NtSuccess(status) ? ((UnicodeString*)buffer.Pointer)->ToManagedString() : null;
    }

    /// <summary>
    /// Determines the instruction set the process runs as.
    /// </summary>
    /// <param name="process">A handle with <c>PROCESS_QUERY_LIMITED_INFORMATION</c>.</param>
    /// <returns>The architecture.</returns>
    private static unsafe ProcessArchitecture ReadArchitecture(SafeFileHandle process)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10586)
            || !PInvoke.IsWow64Process2(process, out var emulated, out var native))
        {
            return ProcessArchitecture.Unknown;
        }

        // IMAGE_FILE_MACHINE_UNKNOWN means the process is not running under WOW64.
        return MapMachine((ushort)(emulated == IMAGE_FILE_MACHINE.IMAGE_FILE_MACHINE_UNKNOWN ? native : emulated));
    }

    /// <summary>
    /// Reads elevation and integrity level from the process token.
    /// </summary>
    /// <param name="process">A handle with <c>PROCESS_QUERY_LIMITED_INFORMATION</c>.</param>
    /// <returns>The elevation (or <see langword="null"/>) and the integrity level.</returns>
    private static unsafe (bool? IsElevated, IntegrityLevel Integrity) ReadToken(SafeFileHandle process)
    {
        if (!PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, out var token))
        {
            return (null, IntegrityLevel.Unknown);
        }

        using (token)
        {
            bool? isElevated = null;
            TOKEN_ELEVATION elevation;
            if (PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenElevation, &elevation, (uint)sizeof(TOKEN_ELEVATION), out _))
            {
                isElevated = elevation.TokenIsElevated != 0;
            }

            return (isElevated, ReadIntegrityLevel(token));
        }
    }

    /// <summary>
    /// Reads the integrity level of a token.
    /// </summary>
    /// <param name="token">A token handle with <c>TOKEN_QUERY</c>.</param>
    /// <returns>The level, or <see cref="IntegrityLevel.Unknown"/>.</returns>
    private static unsafe IntegrityLevel ReadIntegrityLevel(SafeFileHandle token)
    {
        PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, null, 0, out var length);
        if (length == 0)
        {
            return IntegrityLevel.Unknown;
        }

        var buffer = stackalloc byte[(int)length];
        if (!PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, buffer, length, out _))
        {
            return IntegrityLevel.Unknown;
        }

        var label = (TOKEN_MANDATORY_LABEL*)buffer;
        var sid = new SecurityIdentifier((nint)label->Label.Sid.Value).Value;
        var rid = sid[(sid.LastIndexOf('-') + 1)..];
        return uint.TryParse(rid, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? MapIntegrityLevel(value)
            : IntegrityLevel.Unknown;
    }

    /// <summary>
    /// Gets cached version resource data for a file.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>The data, or <see langword="null"/> if the file cannot be read.</returns>
    private FileVersionData? GetVersion(string path) => _versions.GetOrAdd(path, static p =>
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(p);
            return new FileVersionData(Normalize(info.FileDescription), Normalize(info.CompanyName), Normalize(info.FileVersion));
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    });

    /// <summary>
    /// Trims a version resource string and turns blanks into <see langword="null"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The normalized value.</returns>
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Version resource fields shown in the UI.
    /// </summary>
    /// <param name="Description">The file description.</param>
    /// <param name="CompanyName">The company name.</param>
    /// <param name="FileVersion">The file version.</param>
    private sealed record FileVersionData(string? Description, string? CompanyName, string? FileVersion);
}
