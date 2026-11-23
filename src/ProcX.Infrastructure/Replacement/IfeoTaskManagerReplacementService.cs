using Microsoft.Win32;
using ProcX.Application.Replacement;

namespace ProcX.Infrastructure.Replacement;

/// <summary>
/// Replaces Task Manager with an Image File Execution Options "Debugger" value for <c>taskmgr.exe</c>.
/// </summary>
/// <remarks>
/// When <c>taskmgr.exe</c> is started, Windows starts the debugger instead and passes the original command
/// line to it as arguments, which ProcX ignores. The location is injectable so tests can use a scratch key.
/// </remarks>
/// <param name="root">The registry root, <see cref="Registry.LocalMachine"/> in production.</param>
/// <param name="optionsPath">The Image File Execution Options key path below <paramref name="root"/>.</param>
public sealed class IfeoTaskManagerReplacementService(RegistryKey root, string optionsPath) : ITaskManagerReplacementService
{
    /// <summary>
    /// The default Image File Execution Options key path.
    /// </summary>
    public const string DefaultOptionsPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";

    /// <summary>
    /// The image name Windows launches for Task Manager.
    /// </summary>
    private const string ImageName = "taskmgr.exe";

    /// <summary>
    /// The registry value holding the debugger command.
    /// </summary>
    private const string DebuggerValue = "Debugger";

    /// <summary>
    /// Initializes a new instance of the <see cref="IfeoTaskManagerReplacementService"/> class for the real
    /// system configuration (HKLM).
    /// </summary>
    public IfeoTaskManagerReplacementService()
        : this(Registry.LocalMachine, DefaultOptionsPath)
    {
    }

    /// <inheritdoc />
    public ReplacementStatus GetStatus(string currentExecutable)
    {
        ArgumentException.ThrowIfNullOrEmpty(currentExecutable);

        using var key = root.OpenSubKey($@"{optionsPath}\{ImageName}");
        var target = key?.GetValue(DebuggerValue) is string command ? ExtractExecutable(command) : null;
        if (string.IsNullOrWhiteSpace(target))
        {
            return new ReplacementStatus(ReplacementState.Disabled, null);
        }

        if (string.Equals(Path.GetFullPath(target), Path.GetFullPath(currentExecutable), StringComparison.OrdinalIgnoreCase))
        {
            return new ReplacementStatus(ReplacementState.Enabled, target);
        }

        return new ReplacementStatus(File.Exists(target) ? ReplacementState.EnabledForOther : ReplacementState.Broken, target);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="executable"/> is not an existing file.</exception>
    public void Enable(string executable)
    {
        ArgumentException.ThrowIfNullOrEmpty(executable);
        if (!File.Exists(executable))
        {
            throw new ArgumentException($"The executable '{executable}' does not exist.", nameof(executable));
        }

        using var key = root.CreateSubKey($@"{optionsPath}\{ImageName}", writable: true);
        key.SetValue(DebuggerValue, $"\"{Path.GetFullPath(executable)}\"", RegistryValueKind.String);
    }

    /// <inheritdoc />
    public void Disable()
    {
        using var key = root.OpenSubKey($@"{optionsPath}\{ImageName}", writable: true);
        if (key is null)
        {
            return;
        }

        key.DeleteValue(DebuggerValue, throwOnMissingValue: false);

        // Remove the key itself only when nothing else (e.g. GlobalFlag) is configured on it.
        if (key.ValueCount == 0 && key.SubKeyCount == 0)
        {
            key.Dispose();
            using var options = root.OpenSubKey(optionsPath, writable: true);
            options?.DeleteSubKey(ImageName, throwOnMissingSubKey: false);
        }
    }

    /// <summary>
    /// Extracts the executable path from a debugger command line such as <c>"C:\Tools\a.exe" /flag</c>.
    /// </summary>
    /// <param name="command">The command line.</param>
    /// <returns>The executable path, or <see langword="null"/> if the command is blank.</returns>
    internal static string? ExtractExecutable(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var text = command.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : text[1..];
        }

        // Unquoted: the executable ends at the first ".exe" (paths may contain spaces), else at the first space.
        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe >= 0)
        {
            return text[..(exe + 4)];
        }

        var space = text.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? text : text[..space];
    }
}
