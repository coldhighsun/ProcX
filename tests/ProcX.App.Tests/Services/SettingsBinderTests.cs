using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using ProcX.App.Services;
using ProcX.App.ViewModels;
using ProcX.Application.Details;
using ProcX.Application.Processes;
using ProcX.Application.Security;
using ProcX.Application.Settings;
using ProcX.Domain.Processes;

namespace ProcX.App.Tests.Services;

public class SettingsBinderTests
{
    private readonly MemoryStore _store = new();
    private readonly FakeTheme _theme = new();
    private readonly ProcessMonitor _monitor = new(
        new EmptySnapshots(),
        new FixedSystemInformation(),
        TimeProvider.System,
        NullLogger<ProcessMonitor>.Instance);

    private (AppSettingsService Settings, MainWindowViewModel Main) Start(AppSettings initial)
    {
        _store.Saved = initial;
        var settings = new AppSettingsService(_store, NullLogger<AppSettingsService>.Instance);
        var list = new ProcessListViewModel(new NullEnricher());
        var main = new MainWindowViewModel(
            list,
            new ProcessTerminator(new EmptySnapshots(), new NullController()),
            _monitor,
            new NullElevation(),
            new NullShell(),
            new ProcessDetailsViewModel(new FakeDetailsProvider(), list) { IsActive = false },
            new NullController());
        new SettingsBinder(settings, main, _monitor, _theme).Start();
        return (settings, main);
    }

    [Fact]
    public void Start_StoredSettings_AreAppliedWithoutBeingWrittenBack()
    {
        // Arrange
        var initial = new AppSettings
        {
            Theme = AppTheme.Light,
            RefreshInterval = TimeSpan.FromSeconds(5),
            ShowDetails = false,
            TreeView = true,
        };
        _store.SaveCount = 0;

        // Act
        var (_, main) = Start(initial);

        // Assert
        _theme.Applied.Should().Equal(AppTheme.Light);
        _monitor.Interval.Should().Be(TimeSpan.FromSeconds(5));
        main.IsDetailsVisible.Should().BeFalse();
        main.Processes.IsTreeView.Should().BeTrue();
        _store.SaveCount.Should().Be(0);
    }

    [Fact]
    public void Start_StoredHiddenColumns_AreAppliedWithoutBeingWrittenBack()
    {
        // Arrange
        _store.SaveCount = 0;

        // Act
        var (_, main) = Start(new AppSettings { HiddenColumns = ProcessColumns.Pid | ProcessColumns.Path });

        // Assert
        main.Processes.HiddenColumns.Should().Be(ProcessColumns.Pid | ProcessColumns.Path);
        _store.SaveCount.Should().Be(0);
    }

    [Fact]
    public void HiddenColumns_ChangedInMainWindow_ArePersisted()
    {
        // Arrange
        var (_, main) = Start(new AppSettings());

        // Act
        main.Processes.HiddenColumns = ProcessColumns.Memory;

        // Assert
        _store.Saved.HiddenColumns.Should().Be(ProcessColumns.Memory);
    }

    [Fact]
    public void ViewOptions_ToggledInMainWindow_ArePersisted()
    {
        // Arrange
        var (_, main) = Start(new AppSettings());

        // Act
        main.IsDetailsVisible = false;
        main.Processes.IsTreeView = true;

        // Assert
        _store.Saved.ShowDetails.Should().BeFalse();
        _store.Saved.TreeView.Should().BeTrue();
    }

    [Fact]
    public void SettingsChanged_ThemeAndInterval_AreAppliedLive()
    {
        // Arrange
        var (settings, _) = Start(new AppSettings());

        // Act
        settings.Update(s => s with { Theme = AppTheme.Dark, RefreshInterval = TimeSpan.FromSeconds(2) });

        // Assert
        _theme.Applied.Should().Equal(AppTheme.System, AppTheme.Dark);
        _monitor.Interval.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Update_SameValues_DoesNotSaveOrNotify()
    {
        // Arrange
        var (settings, _) = Start(new AppSettings());
        var notified = 0;
        settings.Changed += (_, _) => notified++;
        _store.SaveCount = 0;

        // Act
        settings.Update(static s => s with { });

        // Assert
        notified.Should().Be(0);
        _store.SaveCount.Should().Be(0);
    }

    [Fact]
    public void Update_SaveThrowsIoException_KeepsChangeInMemory()
    {
        // Arrange
        var (settings, _) = Start(new AppSettings());
        _store.FailSave = true;

        // Act
        settings.Update(static s => s with { Theme = AppTheme.Dark });

        // Assert
        settings.Current.Theme.Should().Be(AppTheme.Dark);
    }

    private sealed class MemoryStore : ISettingsStore
    {
        public AppSettings Saved { get; set; } = new();

        public int SaveCount { get; set; }

        public bool FailSave { get; set; }

        public AppSettings Load() => Saved;

        public void Save(AppSettings settings)
        {
            if (FailSave)
            {
                throw new IOException("disk full");
            }

            Saved = settings;
            SaveCount++;
        }
    }

    private sealed class FakeTheme : IThemeService
    {
        public List<AppTheme> Applied { get; } = [];

        public void Apply(AppTheme theme) => Applied.Add(theme);
    }

    private sealed class EmptySnapshots : IProcessSnapshotProvider
    {
        public ProcessSnapshot Capture() => new(DateTimeOffset.UnixEpoch, []);
    }

    private sealed class FixedSystemInformation : ISystemInformation
    {
        public int LogicalProcessorCount => 4;
    }

    private sealed class NullEnricher : IProcessRowEnricher
    {
        public void Enqueue(ProcessRowViewModel row)
        {
        }
    }

    private sealed class NullController : IProcessController
    {
        public TerminationResult Terminate(ProcessKey key) => new(key, TerminationStatus.Succeeded);

        public ProcessOperationResult<bool> SetPriority(ProcessKey key, ProcessPriority priority) => ProcessOperationResult.Ok(true);

        public ProcessOperationResult<AffinityInfo> GetAffinity(ProcessKey key) =>
            ProcessOperationResult.Ok(new AffinityInfo(AffinityMask.All(1), AffinityMask.All(1)));

        public ProcessOperationResult<bool> SetAffinity(ProcessKey key, AffinityMask mask) => ProcessOperationResult.Ok(true);
    }

    private sealed class NullElevation : IElevationService
    {
        public bool IsElevated => false;

        public bool TryStartElevated(string arguments = "") => false;

        public int? RunElevatedAndWait(string arguments) => null;
    }

    private sealed class NullShell : IShellInteraction
    {
        public Task<bool> ConfirmAsync(string title, string message, string confirmText) => Task.FromResult(false);

        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;

        public Task<AffinityMask?> PickAffinityAsync(string processName, AffinityMask current, AffinityMask available) =>
            Task.FromResult<AffinityMask?>(null);

        public void ShowSettings()
        {
        }

        public void OpenFileLocation(string path)
        {
        }

        public void CopyToClipboard(string text)
        {
        }

        public void Shutdown()
        {
        }
    }
}
