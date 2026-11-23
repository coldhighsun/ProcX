namespace ProcX.Application.Details;

/// <summary>
/// The instruction set a process runs as.
/// </summary>
public enum ProcessArchitecture
{
    /// <summary>
    /// Could not be determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// 32-bit x86.
    /// </summary>
    X86,

    /// <summary>
    /// 64-bit x64.
    /// </summary>
    X64,

    /// <summary>
    /// 32-bit ARM.
    /// </summary>
    Arm,

    /// <summary>
    /// 64-bit ARM.
    /// </summary>
    Arm64,
}

/// <summary>
/// The mandatory integrity level of a process token.
/// </summary>
public enum IntegrityLevel
{
    /// <summary>
    /// Could not be determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// Untrusted.
    /// </summary>
    Untrusted,

    /// <summary>
    /// Low (e.g. sandboxed browser renderers).
    /// </summary>
    Low,

    /// <summary>
    /// Medium (standard user processes).
    /// </summary>
    Medium,

    /// <summary>
    /// Medium plus.
    /// </summary>
    MediumPlus,

    /// <summary>
    /// High (elevated administrator processes).
    /// </summary>
    High,

    /// <summary>
    /// System (services and kernel-mode callers).
    /// </summary>
    System,

    /// <summary>
    /// Protected process.
    /// </summary>
    Protected,
}

/// <summary>
/// General information about a process beyond what the process list shows.
/// </summary>
/// <param name="CommandLine">The command line, or <see langword="null"/> if it cannot be read.</param>
/// <param name="Architecture">The instruction set the process runs as.</param>
/// <param name="IsElevated">Whether the token is elevated, or <see langword="null"/> if the token cannot be read.</param>
/// <param name="IntegrityLevel">The token integrity level.</param>
/// <param name="CompanyName">The company name from the version resource.</param>
/// <param name="FileVersion">The file version from the version resource.</param>
public sealed record ProcessGeneralInfo(
    string? CommandLine,
    ProcessArchitecture Architecture,
    bool? IsElevated,
    IntegrityLevel IntegrityLevel,
    string? CompanyName,
    string? FileVersion);
