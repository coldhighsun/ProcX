using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProcX.App.Services;
using ProcX.App.Startup;
using ProcX.Application.Replacement;
using ProcX.Application.Security;
using ProcX.Application.Settings;

namespace ProcX.App.ViewModels;

/// <summary>
/// One selectable refresh interval.
/// </summary>
/// <param name="Value">The interval.</param>
public sealed record IntervalOption(TimeSpan Value)
{
    /// <summary>
    /// Gets the label, e.g. <c>2 seconds</c>.
    /// </summary>
    public string Label => string.Create(CultureInfo.CurrentCulture, $"{Value.TotalSeconds:0.#} seconds");
}

/// <summary>
/// The settings window: theme, refresh interval and replacing the system Task Manager.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    /// <summary>
    /// The refresh intervals offered.
    /// </summary>
    private static readonly TimeSpan[] IntervalValues =
    [
        TimeSpan.FromSeconds(0.5),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
    ];

    /// <summary>
    /// The settings holder.
    /// </summary>
    private readonly AppSettingsService _settings;

    /// <summary>
    /// Reads and writes the Task Manager replacement.
    /// </summary>
    private readonly ITaskManagerReplacementService _replacement;

    /// <summary>
    /// Queries and requests elevation.
    /// </summary>
    private readonly IElevationService _elevation;

    /// <summary>
    /// Dialogs.
    /// </summary>
    private readonly IShellInteraction _shell;

    /// <summary>
    /// Set while the toggle is updated from the registry rather than by the user.
    /// </summary>
    private bool _isSyncing;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="settings">The settings holder.</param>
    /// <param name="replacement">Reads and writes the Task Manager replacement.</param>
    /// <param name="elevation">Queries and requests elevation.</param>
    /// <param name="shell">Dialogs.</param>
    public SettingsViewModel(
        AppSettingsService settings,
        ITaskManagerReplacementService replacement,
        IElevationService elevation,
        IShellInteraction shell)
    {
        _settings = settings;
        _replacement = replacement;
        _elevation = elevation;
        _shell = shell;

        Intervals = [.. IntervalValues.Select(static v => new IntervalOption(v))];
        Theme = settings.Current.Theme;
        SelectedInterval = Nearest(settings.Current.RefreshInterval);
        RefreshReplacement();
    }

    /// <summary>
    /// Gets or sets the path registered as the Task Manager replacement; defaults to the running executable.
    /// </summary>
    public string ExecutablePath { get; init; } = Environment.ProcessPath ?? string.Empty;

    /// <summary>
    /// Gets the refresh intervals offered.
    /// </summary>
    public IReadOnlyList<IntervalOption> Intervals { get; }

    /// <summary>
    /// Gets the themes offered.
    /// </summary>
    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.System, AppTheme.Light, AppTheme.Dark];

    /// <summary>
    /// Gets or sets the color theme.
    /// </summary>
    [ObservableProperty]
    public partial AppTheme Theme { get; set; }

    /// <summary>
    /// Gets or sets the refresh interval.
    /// </summary>
    [ObservableProperty]
    public partial IntervalOption SelectedInterval { get; set; }

    /// <summary>
    /// Gets or sets whether Windows opens ProcX instead of Task Manager.
    /// </summary>
    [ObservableProperty]
    public partial bool ReplaceTaskManager { get; set; }

    /// <summary>
    /// Gets the current state of the replacement.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReplacementText), nameof(CanUseThisInstall))]
    public partial ReplacementStatus ReplacementStatus { get; private set; } = new(ReplacementState.Disabled, null);

    /// <summary>
    /// Gets or sets whether a replacement change is in progress (waiting for the UAC prompt).
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>
    /// Gets a sentence describing the replacement state.
    /// </summary>
    public string ReplacementText => ReplacementStatus.State switch
    {
        ReplacementState.Disabled => "Windows opens its own Task Manager.",
        ReplacementState.Enabled => "Ctrl+Shift+Esc and taskmgr.exe open ProcX.",
        ReplacementState.EnabledForOther => $"Task Manager is replaced by another program: {ReplacementStatus.TargetPath}",
        ReplacementState.Broken => $"Task Manager is replaced by a file that no longer exists ({ReplacementStatus.TargetPath}), so it cannot start at all. Choose \"Use this ProcX\" or turn the option off.",
        _ => string.Empty,
    };

    /// <summary>
    /// Gets whether the replacement points to something other than the running executable.
    /// </summary>
    public bool CanUseThisInstall => ReplacementStatus.State is ReplacementState.EnabledForOther or ReplacementState.Broken;

    /// <summary>
    /// Checks at startup that the replacement is not broken, and offers to repair it.
    /// </summary>
    /// <remarks>Only a broken replacement is reported: it disables Task Manager completely.</remarks>
    /// <returns>A task that completes when the check and any repair have finished.</returns>
    public async Task CheckReplacementAtStartupAsync()
    {
        RefreshReplacement();
        if (ReplacementStatus.State != ReplacementState.Broken)
        {
            return;
        }

        if (await _shell.ConfirmAsync(
            "Task Manager replacement is broken",
            $"Windows is set to open {ReplacementStatus.TargetPath} instead of Task Manager, but that file no longer exists. Ctrl+Shift+Esc therefore does nothing.\n\nPoint it at this copy of ProcX?",
            "Repair").ConfigureAwait(true))
        {
            await ApplyReplacementAsync(enable: true).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Registers the running executable as the replacement, e.g. to repair a stale or foreign entry.
    /// </summary>
    /// <returns>A task that completes when the change has finished.</returns>
    [RelayCommand(CanExecute = nameof(CanUseThisInstall))]
    private Task UseThisInstallAsync() => ApplyReplacementAsync(enable: true);

    /// <summary>
    /// Saves the theme.
    /// </summary>
    /// <param name="value">The theme.</param>
    partial void OnThemeChanged(AppTheme value) => _settings.Update(s => s with { Theme = value });

    /// <summary>
    /// Saves the refresh interval.
    /// </summary>
    /// <param name="value">The interval.</param>
    partial void OnSelectedIntervalChanged(IntervalOption value) => _settings.Update(s => s with { RefreshInterval = value.Value });

    /// <summary>
    /// Enables or disables the replacement when the user flips the switch.
    /// </summary>
    /// <param name="value">The requested state.</param>
    partial void OnReplaceTaskManagerChanged(bool value)
    {
        if (!_isSyncing)
        {
            _ = ApplyReplacementAsync(value);
        }
    }

    /// <summary>
    /// Changes the replacement, elevating if necessary, then reflects the actual state.
    /// </summary>
    /// <param name="enable">Whether to enable or disable it.</param>
    /// <returns>A task that completes when the change has finished.</returns>
    private async Task ApplyReplacementAsync(bool enable)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (enable && !await ConfirmEnableAsync().ConfigureAwait(true))
            {
                RefreshReplacement();
                return;
            }

            var outcome = await Task.Run(() => Change(enable)).ConfigureAwait(true);
            RefreshReplacement();

            var succeeded = enable
                ? ReplacementStatus.State == ReplacementState.Enabled
                : ReplacementStatus.State == ReplacementState.Disabled;
            if (!succeeded && outcome != ChangeOutcome.Declined)
            {
                await _shell.ShowMessageAsync(
                    "Could not change Task Manager replacement",
                    outcome == ChangeOutcome.AccessDenied
                        ? "Administrator rights are required to change this setting."
                        : "The change did not take effect. Windows may have blocked writing the registry.").ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Explains the consequences of replacing Task Manager and asks for confirmation.
    /// </summary>
    /// <returns><see langword="true"/> if the user confirmed.</returns>
    private Task<bool> ConfirmEnableAsync() => _shell.ConfirmAsync(
        "Replace Windows Task Manager?",
        "Ctrl+Shift+Esc, the taskbar menu and taskmgr.exe will open ProcX instead of the Windows Task Manager, which cannot be started while this is on.\n\n" +
        "This needs administrator rights. If you move or delete ProcX, Task Manager stops opening until you turn this off or repair it (ProcX offers to at startup).",
        "Replace");

    /// <summary>
    /// Writes the registry directly when already elevated, otherwise through an elevated instance.
    /// </summary>
    /// <param name="enable">Whether to enable or disable the replacement.</param>
    /// <returns>What happened.</returns>
    private ChangeOutcome Change(bool enable)
    {
        if (_elevation.IsElevated)
        {
            var mode = enable ? StartupMode.RegisterReplacement : StartupMode.UnregisterReplacement;
            return FromExitCode(ReplacementCommand.Execute(mode, _replacement, ExecutablePath));
        }

        var exitCode = _elevation.RunElevatedAndWait(enable ? StartupOptions.RegisterArgument : StartupOptions.UnregisterArgument);
        return exitCode is { } code ? FromExitCode(code) : ChangeOutcome.Declined;
    }

    /// <summary>
    /// Maps a <see cref="ReplacementCommand"/> exit code to an outcome.
    /// </summary>
    /// <param name="exitCode">The exit code.</param>
    /// <returns>The outcome.</returns>
    private static ChangeOutcome FromExitCode(int exitCode) => exitCode switch
    {
        ReplacementCommand.Success => ChangeOutcome.Succeeded,
        ReplacementCommand.AccessDenied => ChangeOutcome.AccessDenied,
        _ => ChangeOutcome.Failed,
    };

    /// <summary>
    /// Re-reads the registry and updates the switch without triggering a change.
    /// </summary>
    private void RefreshReplacement()
    {
        ReplacementStatus = _replacement.GetStatus(ExecutablePath);
        _isSyncing = true;
        try
        {
            ReplaceTaskManager = ReplacementStatus.State != ReplacementState.Disabled;
        }
        finally
        {
            _isSyncing = false;
        }

        UseThisInstallCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Finds the offered interval closest to a stored value.
    /// </summary>
    /// <param name="value">The stored interval.</param>
    /// <returns>The nearest option.</returns>
    private IntervalOption Nearest(TimeSpan value) =>
        Intervals.MinBy(option => (option.Value - value).Duration())!;

    /// <summary>
    /// The result of an attempt to change the replacement.
    /// </summary>
    private enum ChangeOutcome
    {
        /// <summary>
        /// The change was made.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The user declined the UAC prompt.
        /// </summary>
        Declined,

        /// <summary>
        /// The caller lacked the rights.
        /// </summary>
        AccessDenied,

        /// <summary>
        /// The change failed.
        /// </summary>
        Failed,
    }
}
