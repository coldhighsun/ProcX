namespace ProcX.Application.Security;

/// <summary>
/// Queries and requests administrative privileges.
/// </summary>
public interface IElevationService
{
    /// <summary>
    /// Gets whether the current process runs with an elevated (administrator) token.
    /// </summary>
    bool IsElevated { get; }

    /// <summary>
    /// Starts a new elevated instance of the current executable, showing the UAC prompt.
    /// </summary>
    /// <param name="arguments">Command-line arguments for the new instance.</param>
    /// <returns><see langword="true"/> if the instance was started; <see langword="false"/> if the user declined the prompt.</returns>
    bool TryStartElevated(string arguments = "");

    /// <summary>
    /// Runs the current executable elevated with the given arguments and waits for it to exit.
    /// </summary>
    /// <param name="arguments">Command-line arguments.</param>
    /// <returns>The exit code, or <see langword="null"/> if the user declined the UAC prompt.</returns>
    int? RunElevatedAndWait(string arguments);
}
