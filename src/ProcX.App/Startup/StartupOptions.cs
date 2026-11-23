namespace ProcX.App.Startup;

/// <summary>
/// What the process was started to do.
/// </summary>
public enum StartupMode
{
    /// <summary>
    /// Show the main window.
    /// </summary>
    Normal,

    /// <summary>
    /// Headless: configure Windows to open ProcX instead of Task Manager, then exit.
    /// </summary>
    RegisterReplacement,

    /// <summary>
    /// Headless: restore the system Task Manager, then exit.
    /// </summary>
    UnregisterReplacement,
}

/// <summary>
/// Parsed command-line arguments.
/// </summary>
/// <param name="Mode">The requested mode.</param>
public sealed record StartupOptions(StartupMode Mode)
{
    /// <summary>
    /// The argument selecting <see cref="StartupMode.RegisterReplacement"/>.
    /// </summary>
    public const string RegisterArgument = "--register-replacement";

    /// <summary>
    /// The argument selecting <see cref="StartupMode.UnregisterReplacement"/>.
    /// </summary>
    public const string UnregisterArgument = "--unregister-replacement";

    /// <summary>
    /// Parses command-line arguments.
    /// </summary>
    /// <remarks>
    /// Unknown arguments are ignored on purpose: when Windows starts ProcX as the Task Manager replacement,
    /// it passes the original <c>taskmgr.exe</c> path and its arguments, which must not affect startup.
    /// </remarks>
    /// <param name="args">The arguments, without the executable path.</param>
    /// <returns>The options.</returns>
    public static StartupOptions Parse(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        foreach (var arg in args)
        {
            if (string.Equals(arg, RegisterArgument, StringComparison.OrdinalIgnoreCase))
            {
                return new StartupOptions(StartupMode.RegisterReplacement);
            }

            if (string.Equals(arg, UnregisterArgument, StringComparison.OrdinalIgnoreCase))
            {
                return new StartupOptions(StartupMode.UnregisterReplacement);
            }
        }

        return new StartupOptions(StartupMode.Normal);
    }
}
