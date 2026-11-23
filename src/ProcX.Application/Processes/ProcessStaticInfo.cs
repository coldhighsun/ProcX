namespace ProcX.Application.Processes;

/// <summary>
/// Information about a process that does not change during its lifetime.
/// </summary>
/// <param name="ImagePath">The full path of the executable, or <see langword="null"/> if unavailable.</param>
/// <param name="UserName">The account the process runs as, or <see langword="null"/> if unavailable.</param>
/// <param name="Description">The file description from the version resource, or <see langword="null"/> if unavailable.</param>
/// <param name="IsAccessDenied">Whether the process could not be opened due to insufficient privileges.</param>
public sealed record ProcessStaticInfo(string? ImagePath, string? UserName, string? Description, bool IsAccessDenied)
{
    /// <summary>
    /// Information for a process that could not be opened because access was denied.
    /// </summary>
    public static ProcessStaticInfo AccessDenied { get; } = new(null, null, null, IsAccessDenied: true);

    /// <summary>
    /// Information for a process that exited or has no queryable details.
    /// </summary>
    public static ProcessStaticInfo Unavailable { get; } = new(null, null, null, IsAccessDenied: false);
}
