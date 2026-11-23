using Microsoft.Extensions.Logging.Abstractions;
using ProcX.Application.Processes;
using ProcX.Application.Settings;
using ProcX.Infrastructure.Settings;

namespace ProcX.Infrastructure.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"procx-settings-{Guid.NewGuid():N}");
    private readonly string _path;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreTests()
    {
        _path = Path.Combine(_directory, "nested", "settings.json");
        _store = new JsonSettingsStore(_path, NullLogger<JsonSettingsStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_FileMissing_ReturnsDefaults()
    {
        // Act
        var settings = _store.Load();

        // Assert
        settings.Should().Be(new AppSettings());
        settings.RefreshInterval.Should().Be(ProcessMonitor.DefaultInterval);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAndCreatesDirectory()
    {
        // Arrange
        var settings = new AppSettings
        {
            Theme = AppTheme.Dark,
            RefreshInterval = TimeSpan.FromSeconds(5),
            ShowDetails = false,
            TreeView = true,
        };

        // Act
        _store.Save(settings);
        var loaded = _store.Load();

        // Assert
        loaded.Should().Be(settings);
        File.ReadAllText(_path).Should().Contain("\"Dark\"");
        Directory.GetFiles(Path.GetDirectoryName(_path)!, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        // Arrange
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, "{ not json");

        // Act
        var settings = _store.Load();

        // Assert
        settings.Should().Be(new AppSettings());
    }

    [Fact]
    public void Load_OutOfRangeValues_AreClamped()
    {
        // Arrange
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, """{ "Theme": "Dark", "RefreshInterval": "00:00:00.001" }""");

        // Act
        var settings = _store.Load();

        // Assert
        settings.Theme.Should().Be(AppTheme.Dark);
        settings.RefreshInterval.Should().Be(ProcessMonitor.MinimumInterval);
    }

    [Fact]
    public void Save_Overwrite_ReplacesPreviousContent()
    {
        // Arrange
        _store.Save(new AppSettings { Theme = AppTheme.Light });

        // Act
        _store.Save(new AppSettings { Theme = AppTheme.Dark });

        // Assert
        _store.Load().Theme.Should().Be(AppTheme.Dark);
    }

    [Fact]
    public void Sanitize_UndefinedTheme_FallsBackToSystem()
    {
        // Arrange
        var settings = new AppSettings { Theme = (AppTheme)99, RefreshInterval = TimeSpan.FromHours(1) };

        // Act
        var result = settings.Sanitize();

        // Assert
        result.Theme.Should().Be(AppTheme.System);
        result.RefreshInterval.Should().Be(ProcessMonitor.MaximumInterval);
    }
}
