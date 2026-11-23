using Microsoft.Extensions.Logging;

namespace ProcX.Application.Settings;

/// <summary>
/// Holds the current settings and persists every change.
/// </summary>
/// <param name="store">Where settings are persisted.</param>
/// <param name="logger">The logger.</param>
public sealed partial class AppSettingsService(ISettingsStore store, ILogger<AppSettingsService> logger)
{
    /// <summary>
    /// Gets the current settings.
    /// </summary>
    public AppSettings Current { get; private set; } = store.Load();

    /// <summary>
    /// Raised after the settings changed.
    /// </summary>
    public event EventHandler<AppSettings>? Changed;

    /// <summary>
    /// Applies a change, saves it and notifies subscribers. Does nothing if the result equals the current settings.
    /// </summary>
    /// <remarks>A failure to save is logged and does not undo the change for this run.</remarks>
    /// <param name="change">Produces the new settings from the current ones.</param>
    public void Update(Func<AppSettings, AppSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var next = change(Current).Sanitize();
        if (next == Current)
        {
            return;
        }

        Current = next;
        try
        {
            store.Save(next);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(ex);
        }

        Changed?.Invoke(this, next);
    }

    /// <summary>
    /// Logs a settings file that could not be written.
    /// </summary>
    /// <param name="exception">The failure.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save settings")]
    private partial void LogSaveFailed(Exception exception);
}
