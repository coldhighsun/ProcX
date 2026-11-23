using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ProcX.Application.Settings;

namespace ProcX.Infrastructure.Settings;

/// <summary>
/// Stores settings as a JSON file, by default under <c>%LocalAppData%\ProcX\settings.json</c>.
/// </summary>
/// <param name="path">The settings file path.</param>
/// <param name="logger">The logger.</param>
public sealed partial class JsonSettingsStore(string path, ILogger<JsonSettingsStore> logger) : ISettingsStore
{
    /// <summary>
    /// JSON options; enums are written as names so the file stays readable and editable.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Gets the default settings file path.
    /// </summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProcX",
        "settings.json");

    /// <inheritdoc />
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path);
            return (JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings()).Sanitize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            LogReadFailed(ex, path);
            return new AppSettings();
        }
    }

    /// <summary>
    /// Logs a settings file that could not be read.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <param name="path">The file path.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read settings from {Path}; using defaults")]
    private partial void LogReadFailed(Exception exception, string path);

    /// <inheritdoc />
    /// <exception cref="IOException">The file could not be written.</exception>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to a temp file and move it over the target so a crash never leaves a half-written file.
        var temp = $"{path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings.Sanitize(), Options));
        File.Move(temp, path, overwrite: true);
    }
}
