using System.ComponentModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProcX.App.Services;
using ProcX.Application.Processes;
using ProcX.Application.Security;
using ProcX.Domain.Processes;

namespace ProcX.App.ViewModels;

/// <summary>
/// The main window: the process list plus the actions that operate on the selected process.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    /// <summary>
    /// The maximum number of failed processes listed in an error message.
    /// </summary>
    private const int MaxListedFailures = 5;

    /// <summary>
    /// Terminates processes and trees.
    /// </summary>
    private readonly ProcessTerminator _terminator;

    /// <summary>
    /// Refreshes the list right after an action.
    /// </summary>
    private readonly ProcessMonitor _monitor;

    /// <summary>
    /// Queries and requests elevation.
    /// </summary>
    private readonly IElevationService _elevation;

    /// <summary>
    /// Dialogs and shell commands.
    /// </summary>
    private readonly IShellInteraction _shell;

    /// <summary>
    /// The selected row whose property changes are being observed.
    /// </summary>
    private ProcessRowViewModel? _observedRow;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="processes">The process list.</param>
    /// <param name="terminator">Terminates processes and trees.</param>
    /// <param name="monitor">Refreshes the list right after an action.</param>
    /// <param name="elevation">Queries and requests elevation.</param>
    /// <param name="shell">Dialogs and shell commands.</param>
    public MainWindowViewModel(
        ProcessListViewModel processes,
        ProcessTerminator terminator,
        ProcessMonitor monitor,
        IElevationService elevation,
        IShellInteraction shell)
    {
        Processes = processes;
        _terminator = terminator;
        _monitor = monitor;
        _elevation = elevation;
        _shell = shell;

        Processes.PropertyChanged += OnProcessesPropertyChanged;
    }

    /// <summary>
    /// Gets the process list.
    /// </summary>
    public ProcessListViewModel Processes { get; }

    /// <summary>
    /// Gets whether ProcX runs elevated.
    /// </summary>
    public bool IsElevated => _elevation.IsElevated;

    /// <summary>
    /// Gets whether ProcX can be restarted with administrator rights.
    /// </summary>
    public bool CanElevate => !IsElevated;

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string Title => IsElevated ? "ProcX (Administrator)" : "ProcX";

    /// <summary>
    /// Gets the currently selected row.
    /// </summary>
    private ProcessRowViewModel? Selected => Processes.SelectedRow;

    /// <summary>
    /// Ends the selected process, asking for confirmation first if it is a critical system process.
    /// </summary>
    /// <returns>A task that completes when the action has finished.</returns>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EndTaskAsync()
    {
        if (Selected is not { } row)
        {
            return;
        }

        if (CriticalProcesses.IsCritical(row.Pid, row.Name)
            && !await _shell.ConfirmAsync(
                "End critical process?",
                $"{row.Name} (PID {row.Pid}) is a critical system process. Ending it will make Windows unusable or shut it down.\n\nEnd it anyway?",
                "End process").ConfigureAwait(true))
        {
            return;
        }

        var result = await Task.Run(() => _terminator.Terminate(row.Key)).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        await ReportFailuresAsync([result], new Dictionary<ProcessKey, string> { [row.Key] = row.Name }).ConfigureAwait(true);
    }

    /// <summary>
    /// Ends the selected process and all of its descendants after confirmation.
    /// </summary>
    /// <returns>A task that completes when the action has finished.</returns>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EndProcessTreeAsync()
    {
        if (Selected is not { } row)
        {
            return;
        }

        var tree = await Task.Run(() => _terminator.GetTree(row.Key)).ConfigureAwait(true);
        if (tree.Count == 0)
        {
            await RefreshAsync().ConfigureAwait(true);
            return;
        }

        var message = new StringBuilder()
            .Append(CultureInfo.CurrentCulture, $"End {row.Name} (PID {row.Pid}) and its descendants? ")
            .Append(CultureInfo.CurrentCulture, $"{tree.Count} process(es) will be ended. Unsaved data will be lost.");

        var critical = tree.Where(CriticalProcesses.IsCritical).Select(static p => p.Name).Distinct().ToList();
        if (critical.Count > 0)
        {
            message.Append(CultureInfo.CurrentCulture, $"\n\nThe tree contains critical system processes ({string.Join(", ", critical)}). Ending them will make Windows unusable or shut it down.");
        }

        if (!await _shell.ConfirmAsync("End process tree?", message.ToString(), "End process tree").ConfigureAwait(true))
        {
            return;
        }

        var results = await Task.Run(() => _terminator.TerminateTree(row.Key)).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        await ReportFailuresAsync(results, tree.ToDictionary(static p => p.Key, static p => p.Name)).ConfigureAwait(true);
    }

    /// <summary>
    /// Opens Explorer at the selected process executable.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedPath))]
    private void OpenFileLocation()
    {
        if (Selected?.ImagePath is { } path)
        {
            _shell.OpenFileLocation(path);
        }
    }

    /// <summary>
    /// Copies the PID of the selected process.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyPid()
    {
        if (Selected is { } row)
        {
            _shell.CopyToClipboard(row.Pid.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Copies the executable path of the selected process.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedPath))]
    private void CopyPath()
    {
        if (Selected?.ImagePath is { } path)
        {
            _shell.CopyToClipboard(path);
        }
    }

    /// <summary>
    /// Restarts ProcX with administrator rights.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanElevate))]
    private void RestartAsAdministrator()
    {
        if (_elevation.TryStartElevated())
        {
            _shell.Shutdown();
        }
    }

    /// <summary>
    /// Gets whether a row is selected.
    /// </summary>
    /// <returns><see langword="true"/> if a row is selected.</returns>
    private bool HasSelection() => Selected is not null;

    /// <summary>
    /// Gets whether the selected row has a known executable path.
    /// </summary>
    /// <returns><see langword="true"/> if a path is available.</returns>
    private bool HasSelectedPath() => Selected?.ImagePath is not null;

    /// <summary>
    /// Samples immediately so the list reflects the action without waiting for the next tick.
    /// </summary>
    /// <returns>A task that completes when the sample was taken.</returns>
    private Task RefreshAsync() => Task.Run(() => { _monitor.RefreshNow(); });

    /// <summary>
    /// Tells the user about processes that could not be ended, offering elevation when it may help.
    /// </summary>
    /// <param name="results">The termination results.</param>
    /// <param name="names">Process names indexed by key, for the message.</param>
    /// <returns>A task that completes when the user has dismissed the message.</returns>
    private async Task ReportFailuresAsync(IReadOnlyList<TerminationResult> results, IReadOnlyDictionary<ProcessKey, string> names)
    {
        var failures = results.Where(static r => !r.IsGone).ToList();
        if (failures.Count == 0)
        {
            return;
        }

        var lines = failures
            .Take(MaxListedFailures)
            .Select(f => $"• {names.GetValueOrDefault(f.Key, "?")} (PID {f.Key.Pid}): {Describe(f)}");
        var message = string.Join('\n', lines);
        if (failures.Count > MaxListedFailures)
        {
            message += $"\n… and {failures.Count - MaxListedFailures} more.";
        }

        var deniedOnly = failures.All(static f => f.Status == TerminationStatus.AccessDenied);
        if (deniedOnly && !IsElevated)
        {
            if (await _shell.ConfirmAsync(
                "Access denied",
                $"{message}\n\nRestart ProcX as administrator and try again?",
                "Restart as administrator").ConfigureAwait(true))
            {
                RestartAsAdministrator();
            }

            return;
        }

        await _shell.ShowMessageAsync("Could not end process", message).ConfigureAwait(true);
    }

    /// <summary>
    /// Describes a failed termination.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>A short explanation.</returns>
    private static string Describe(TerminationResult result) => result.Status switch
    {
        TerminationStatus.AccessDenied => "access denied",
        _ => new Win32Exception(result.ErrorCode).Message,
    };

    /// <summary>
    /// Re-evaluates command availability when the selection or its details change.
    /// </summary>
    /// <param name="sender">The process list.</param>
    /// <param name="e">The event arguments.</param>
    private void OnProcessesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ProcessListViewModel.SelectedRow))
        {
            return;
        }

        _observedRow?.PropertyChanged -= OnSelectedRowPropertyChanged;
        _observedRow = Selected;
        _observedRow?.PropertyChanged += OnSelectedRowPropertyChanged;

        EndTaskCommand.NotifyCanExecuteChanged();
        EndProcessTreeCommand.NotifyCanExecuteChanged();
        CopyPidCommand.NotifyCanExecuteChanged();
        NotifyPathCommandsChanged();
    }

    /// <summary>
    /// Re-evaluates path-dependent commands when the selected row receives its path in the background.
    /// </summary>
    /// <param name="sender">The selected row.</param>
    /// <param name="e">The event arguments.</param>
    private void OnSelectedRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProcessRowViewModel.ImagePath))
        {
            NotifyPathCommandsChanged();
        }
    }

    /// <summary>
    /// Re-evaluates the commands that need the executable path.
    /// </summary>
    private void NotifyPathCommandsChanged()
    {
        OpenFileLocationCommand.NotifyCanExecuteChanged();
        CopyPathCommand.NotifyCanExecuteChanged();
    }
}
