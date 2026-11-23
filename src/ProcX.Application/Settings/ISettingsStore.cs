namespace ProcX.Application.Settings;

/// <summary>
/// Persists <see cref="AppSettings"/>.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Loads the settings.
    /// </summary>
    /// <returns>The stored settings, or the defaults if none exist or they cannot be read.</returns>
    AppSettings Load();

    /// <summary>
    /// Saves the settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    void Save(AppSettings settings);
}
