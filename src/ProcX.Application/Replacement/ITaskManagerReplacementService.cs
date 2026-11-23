namespace ProcX.Application.Replacement;

/// <summary>
/// How the system Task Manager entry point (<c>taskmgr.exe</c>) is configured.
/// </summary>
public enum ReplacementState
{
    /// <summary>
    /// Not replaced; Windows starts its own Task Manager.
    /// </summary>
    Disabled,

    /// <summary>
    /// Replaced by the current executable.
    /// </summary>
    Enabled,

    /// <summary>
    /// Replaced by another executable that exists (e.g. another build of ProcX or another tool).
    /// </summary>
    EnabledForOther,

    /// <summary>
    /// Replaced by an executable that no longer exists, so Task Manager cannot be started at all.
    /// </summary>
    Broken,
}

/// <summary>
/// The configured replacement.
/// </summary>
/// <param name="State">The state.</param>
/// <param name="TargetPath">The configured debugger executable, or <see langword="null"/> if not replaced.</param>
public sealed record ReplacementStatus(ReplacementState State, string? TargetPath);

/// <summary>
/// Redirects Task Manager launches (e.g. Ctrl+Shift+Esc) to this application.
/// </summary>
/// <remarks>
/// Implemented with an Image File Execution Options "Debugger" value for <c>taskmgr.exe</c>.
/// Writing it requires administrator rights; reading does not.
/// </remarks>
public interface ITaskManagerReplacementService
{
    /// <summary>
    /// Reads the current configuration.
    /// </summary>
    /// <param name="currentExecutable">The path of the running executable, to tell "ours" from "other".</param>
    /// <returns>The status.</returns>
    ReplacementStatus GetStatus(string currentExecutable);

    /// <summary>
    /// Redirects Task Manager to the given executable.
    /// </summary>
    /// <param name="executable">The full path of the executable.</param>
    /// <exception cref="UnauthorizedAccessException">The caller lacks administrator rights.</exception>
    void Enable(string executable);

    /// <summary>
    /// Restores the system Task Manager.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The caller lacks administrator rights.</exception>
    void Disable();
}
