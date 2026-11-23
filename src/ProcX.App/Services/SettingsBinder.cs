using System.ComponentModel;
using ProcX.App.ViewModels;
using ProcX.Application.Processes;
using ProcX.Application.Settings;

namespace ProcX.App.Services;

/// <summary>
/// Connects persisted settings to the running application in both directions: applies them at startup and
/// when they change, and saves the view options (details panel, tree view) the user toggles in the main window.
/// </summary>
public sealed class SettingsBinder
{
    /// <summary>
    /// The settings holder.
    /// </summary>
    private readonly AppSettingsService _settings;

    /// <summary>
    /// The main window view model.
    /// </summary>
    private readonly MainWindowViewModel _main;

    /// <summary>
    /// The monitor whose interval is configurable.
    /// </summary>
    private readonly ProcessMonitor _monitor;

    /// <summary>
    /// Applies the color theme.
    /// </summary>
    private readonly IThemeService _theme;

    /// <summary>
    /// Set while settings are being pushed to the view models so they are not saved back.
    /// </summary>
    private bool _isApplying;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsBinder"/> class.
    /// </summary>
    /// <param name="settings">The settings holder.</param>
    /// <param name="main">The main window view model.</param>
    /// <param name="monitor">The monitor whose interval is configurable.</param>
    /// <param name="theme">Applies the color theme.</param>
    public SettingsBinder(AppSettingsService settings, MainWindowViewModel main, ProcessMonitor monitor, IThemeService theme)
    {
        _settings = settings;
        _main = main;
        _monitor = monitor;
        _theme = theme;
    }

    /// <summary>
    /// Applies the current settings and starts keeping both sides in sync.
    /// </summary>
    public void Start()
    {
        Apply(_settings.Current);
        _settings.Changed += (_, settings) => Apply(settings);
        _main.PropertyChanged += OnMainPropertyChanged;
        _main.Processes.PropertyChanged += OnProcessesPropertyChanged;
    }

    /// <summary>
    /// Pushes settings to the application.
    /// </summary>
    /// <param name="settings">The settings.</param>
    private void Apply(AppSettings settings)
    {
        _isApplying = true;
        try
        {
            _theme.Apply(settings.Theme);
            _monitor.Interval = settings.RefreshInterval;
            _main.IsDetailsVisible = settings.ShowDetails;
            _main.Processes.IsTreeView = settings.TreeView;
        }
        finally
        {
            _isApplying = false;
        }
    }

    /// <summary>
    /// Saves the details panel visibility.
    /// </summary>
    /// <param name="sender">The main window view model.</param>
    /// <param name="e">The event arguments.</param>
    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isApplying && e.PropertyName == nameof(MainWindowViewModel.IsDetailsVisible))
        {
            _settings.Update(s => s with { ShowDetails = _main.IsDetailsVisible });
        }
    }

    /// <summary>
    /// Saves the tree view option.
    /// </summary>
    /// <param name="sender">The process list.</param>
    /// <param name="e">The event arguments.</param>
    private void OnProcessesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isApplying && e.PropertyName == nameof(ProcessListViewModel.IsTreeView))
        {
            _settings.Update(s => s with { TreeView = _main.Processes.IsTreeView });
        }
    }
}
