using Microsoft.Extensions.Logging.Abstractions;
using ProcX.App.Services;
using ProcX.App.Startup;
using ProcX.App.ViewModels;
using ProcX.Application.Replacement;
using ProcX.Application.Security;
using ProcX.Application.Settings;
using ProcX.Domain.Processes;

namespace ProcX.App.Tests.ViewModels;

public class SettingsViewModelTests
{
    private const string Exe = @"C:\Apps\ProcX\ProcX.exe";

    private readonly MemoryStore _store = new();
    private readonly FakeReplacement _replacement = new();
    private readonly FakeElevation _elevation = new();
    private readonly FakeShell _shell = new();

    private SettingsViewModel Create()
    {
        var settings = new AppSettingsService(_store, NullLogger<AppSettingsService>.Instance);
        return new SettingsViewModel(settings, _replacement, _elevation, _shell) { ExecutablePath = Exe };
    }

    [Fact]
    public void Constructor_StoredSettings_ReflectsThemeAndNearestInterval()
    {
        // Arrange
        _store.Saved = new AppSettings { Theme = AppTheme.Dark, RefreshInterval = TimeSpan.FromSeconds(4.4) };

        // Act
        var viewModel = Create();

        // Assert
        viewModel.Theme.Should().Be(AppTheme.Dark);
        viewModel.SelectedInterval.Value.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ThemeAndInterval_Changed_ArePersisted()
    {
        // Arrange
        var viewModel = Create();

        // Act
        viewModel.Theme = AppTheme.Light;
        viewModel.SelectedInterval = viewModel.Intervals.Single(static i => i.Value == TimeSpan.FromSeconds(2));

        // Assert
        _store.Saved.Should().Be(new AppSettings { Theme = AppTheme.Light, RefreshInterval = TimeSpan.FromSeconds(2) });
    }

    [Fact]
    public async Task ReplaceTaskManager_EnabledWhileNotElevated_AsksConfirmationThenRunsElevatedHelper()
    {
        // Arrange
        var viewModel = Create();
        _shell.ConfirmResult = true;
        _elevation.OnRun = argument =>
        {
            _replacement.Status = new ReplacementStatus(ReplacementState.Enabled, Exe);
            return ReplacementCommand.Success;
        };

        // Act
        viewModel.ReplaceTaskManager = true;
        await WaitUntilIdleAsync(viewModel);

        // Assert
        _shell.Confirmations.Should().ContainSingle().Which.Should().Contain("administrator");
        _elevation.Runs.Should().Equal(StartupOptions.RegisterArgument);
        viewModel.ReplaceTaskManager.Should().BeTrue();
        viewModel.ReplacementStatus.State.Should().Be(ReplacementState.Enabled);
        _shell.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceTaskManager_UserCancelsConfirmation_RevertsSwitchWithoutElevating()
    {
        // Arrange
        var viewModel = Create();
        _shell.ConfirmResult = false;

        // Act
        viewModel.ReplaceTaskManager = true;
        await WaitUntilIdleAsync(viewModel);

        // Assert
        viewModel.ReplaceTaskManager.Should().BeFalse();
        _elevation.Runs.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceTaskManager_UserDeclinesUac_RevertsSwitchSilently()
    {
        // Arrange
        var viewModel = Create();
        _shell.ConfirmResult = true;
        _elevation.OnRun = _ => null;

        // Act
        viewModel.ReplaceTaskManager = true;
        await WaitUntilIdleAsync(viewModel);

        // Assert
        viewModel.ReplaceTaskManager.Should().BeFalse();
        _shell.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceTaskManager_HelperFailsWithAccessDenied_ShowsMessageAndRevertsSwitch()
    {
        // Arrange
        var viewModel = Create();
        _shell.ConfirmResult = true;
        _elevation.OnRun = _ => ReplacementCommand.AccessDenied;

        // Act
        viewModel.ReplaceTaskManager = true;
        await WaitUntilIdleAsync(viewModel);

        // Assert
        viewModel.ReplaceTaskManager.Should().BeFalse();
        _shell.Messages.Should().ContainSingle().Which.Should().Contain("Administrator");
    }

    [Fact]
    public async Task ReplaceTaskManager_AlreadyElevated_WritesDirectlyWithoutHelper()
    {
        // Arrange
        _elevation.IsElevated = true;
        var viewModel = Create();
        _shell.ConfirmResult = true;

        // Act
        viewModel.ReplaceTaskManager = true;
        await WaitUntilIdleAsync(viewModel);

        // Assert
        _replacement.Enabled.Should().Equal(Exe);
        _elevation.Runs.Should().BeEmpty();
        viewModel.ReplacementStatus.State.Should().Be(ReplacementState.Enabled);
    }

    [Fact]
    public async Task ReplaceTaskManager_DisabledWhileEnabled_RunsUnregisterHelperWithoutConfirmation()
    {
        // Arrange
        _replacement.Status = new ReplacementStatus(ReplacementState.Enabled, Exe);
        var viewModel = Create();
        viewModel.ReplaceTaskManager.Should().BeTrue();
        _elevation.OnRun = _ =>
        {
            _replacement.Status = new ReplacementStatus(ReplacementState.Disabled, null);
            return ReplacementCommand.Success;
        };

        // Act
        viewModel.ReplaceTaskManager = false;
        await WaitUntilIdleAsync(viewModel);

        // Assert
        _elevation.Runs.Should().Equal(StartupOptions.UnregisterArgument);
        _shell.Confirmations.Should().BeEmpty();
        viewModel.ReplaceTaskManager.Should().BeFalse();
    }

    [Fact]
    public void Constructor_ReplacementPointsElsewhere_ShowsOnAndOffersUseThisInstall()
    {
        // Arrange
        _replacement.Status = new ReplacementStatus(ReplacementState.Broken, @"C:\gone\ProcX.exe");

        // Act
        var viewModel = Create();

        // Assert
        viewModel.ReplaceTaskManager.Should().BeTrue();
        viewModel.CanUseThisInstall.Should().BeTrue();
        viewModel.UseThisInstallCommand.CanExecute(null).Should().BeTrue();
        viewModel.ReplacementText.Should().Contain("no longer exists");
    }

    [Fact]
    public async Task CheckReplacementAtStartup_Broken_OffersRepairAndRepairs()
    {
        // Arrange
        _elevation.IsElevated = true;
        _replacement.Status = new ReplacementStatus(ReplacementState.Broken, @"C:\gone\ProcX.exe");
        var viewModel = Create();
        _shell.ConfirmResult = true;

        // Act
        await viewModel.CheckReplacementAtStartupAsync();

        // Assert
        _shell.Confirmations.Should().HaveCount(2);
        _replacement.Enabled.Should().Equal(Exe);
        viewModel.ReplacementStatus.State.Should().Be(ReplacementState.Enabled);
    }

    [Theory]
    [InlineData(ReplacementState.Disabled)]
    [InlineData(ReplacementState.Enabled)]
    [InlineData(ReplacementState.EnabledForOther)]
    public async Task CheckReplacementAtStartup_NotBroken_DoesNotPrompt(ReplacementState state)
    {
        // Arrange
        _replacement.Status = new ReplacementStatus(state, state == ReplacementState.Disabled ? null : Exe);
        var viewModel = Create();

        // Act
        await viewModel.CheckReplacementAtStartupAsync();

        // Assert
        _shell.Confirmations.Should().BeEmpty();
    }

    private static async Task WaitUntilIdleAsync(SettingsViewModel viewModel)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
            if (!viewModel.IsBusy)
            {
                // Give the continuation after IsBusy=false a chance to finish; dialogs complete synchronously here.
                await Task.Delay(TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken);
                if (!viewModel.IsBusy)
                {
                    return;
                }
            }
        }

        throw new TimeoutException("The settings view model stayed busy.");
    }

    private sealed class MemoryStore : ISettingsStore
    {
        public AppSettings Saved { get; set; } = new();

        public AppSettings Load() => Saved;

        public void Save(AppSettings settings) => Saved = settings;
    }

    private sealed class FakeReplacement : ITaskManagerReplacementService
    {
        public ReplacementStatus Status { get; set; } = new(ReplacementState.Disabled, null);

        public List<string> Enabled { get; } = [];

        public ReplacementStatus GetStatus(string currentExecutable) => Status;

        public void Enable(string executable)
        {
            Enabled.Add(executable);
            Status = new ReplacementStatus(ReplacementState.Enabled, executable);
        }

        public void Disable() => Status = new ReplacementStatus(ReplacementState.Disabled, null);
    }

    private sealed class FakeElevation : IElevationService
    {
        public bool IsElevated { get; set; }

        public Func<string, int?> OnRun { get; set; } = _ => ReplacementCommand.Success;

        public List<string> Runs { get; } = [];

        public bool TryStartElevated(string arguments = "") => true;

        public int? RunElevatedAndWait(string arguments)
        {
            Runs.Add(arguments);
            return OnRun(arguments);
        }
    }

    private sealed class FakeShell : IShellInteraction
    {
        public bool ConfirmResult { get; set; }

        public List<string> Confirmations { get; } = [];

        public List<string> Messages { get; } = [];

        public Task<bool> ConfirmAsync(string title, string message, string confirmText)
        {
            Confirmations.Add($"{title}\n{message}");
            return Task.FromResult(ConfirmResult);
        }

        public Task ShowMessageAsync(string title, string message)
        {
            Messages.Add($"{title}\n{message}");
            return Task.CompletedTask;
        }

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
