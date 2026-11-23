using System.IO;
using ProcX.Application.Replacement;

namespace ProcX.App.Startup;

/// <summary>
/// Runs the headless, elevated register/unregister commands and maps the outcome to an exit code.
/// </summary>
public static class ReplacementCommand
{
    /// <summary>
    /// The operation succeeded.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// The process lacks the rights to change the configuration.
    /// </summary>
    public const int AccessDenied = 1;

    /// <summary>
    /// The operation failed for another reason.
    /// </summary>
    public const int Failed = 2;

    /// <summary>
    /// Executes a register or unregister command.
    /// </summary>
    /// <param name="mode">The mode; must not be <see cref="StartupMode.Normal"/>.</param>
    /// <param name="service">The replacement service.</param>
    /// <param name="executable">The path to register.</param>
    /// <returns>The process exit code.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is <see cref="StartupMode.Normal"/>.</exception>
    public static int Execute(StartupMode mode, ITaskManagerReplacementService service, string executable)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (mode is not (StartupMode.RegisterReplacement or StartupMode.UnregisterReplacement or StartupMode.UnregisterReplacementIfOurs))
        {
            // Validated before the try block: the catch below maps ArgumentException from the service to Failed.
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a replacement command.");
        }

        try
        {
            switch (mode)
            {
                case StartupMode.RegisterReplacement:
                    service.Enable(executable);
                    break;

                case StartupMode.UnregisterReplacementIfOurs:
                    // Only Enabled is ours; a broken or foreign entry must be left alone.
                    if (service.GetStatus(executable).State == ReplacementState.Enabled)
                    {
                        service.Disable();
                    }

                    break;

                default:
                    service.Disable();
                    break;
            }

            return Success;
        }
        catch (UnauthorizedAccessException)
        {
            return AccessDenied;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or System.Security.SecurityException)
        {
            return Failed;
        }
    }
}
