using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProcX.Application.Details;

namespace ProcX.App.ViewModels;

/// <summary>
/// The details panel of the selected process. Only the visible tab is loaded, off the UI thread.
/// </summary>
public sealed partial class ProcessDetailsViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// Source of the details.
    /// </summary>
    private readonly IProcessDetailsProvider _provider;

    /// <summary>
    /// The process list, whose selection drives the panel and whose rows name parent processes.
    /// </summary>
    private readonly ProcessListViewModel _processes;

    /// <summary>
    /// Cancels the load in progress when a newer one starts.
    /// </summary>
    private CancellationTokenSource? _loadCancellation;

    /// <summary>
    /// The handles currently loaded, behind <see cref="HandlesView"/>.
    /// </summary>
    private IReadOnlyList<HandleDetails> _handles = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessDetailsViewModel"/> class.
    /// </summary>
    /// <param name="provider">Source of the details.</param>
    /// <param name="processes">The process list.</param>
    public ProcessDetailsViewModel(IProcessDetailsProvider provider, ProcessListViewModel processes)
    {
        _provider = provider;
        _processes = processes;
        _processes.PropertyChanged += OnProcessesPropertyChanged;
        HandlesView = CreateHandlesView([]);
    }

    /// <summary>
    /// Gets or sets how long to wait after the selection changes before loading, so that moving through the
    /// list with the keyboard does not query every process passed.
    /// </summary>
    public TimeSpan SelectionDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Gets or sets whether the panel is shown; nothing is loaded while hidden.
    /// </summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the visible tab.
    /// </summary>
    [ObservableProperty]
    public partial DetailsTab SelectedTab { get; set; }

    /// <summary>
    /// Gets or sets <see cref="SelectedTab"/> as an index, for binding to a tab control.
    /// </summary>
    public int SelectedTabIndex
    {
        get => (int)SelectedTab;
        set => SelectedTab = (DetailsTab)value;
    }

    /// <summary>
    /// Gets the process shown, or <see langword="null"/> if none is selected.
    /// </summary>
    [ObservableProperty]
    public partial ProcessRowViewModel? Process { get; private set; }

    /// <summary>
    /// Gets the general information.
    /// </summary>
    [ObservableProperty]
    public partial ProcessGeneralInfo? General { get; private set; }

    /// <summary>
    /// Gets the parent process as <c>name (PID)</c>, noting when the parent has exited.
    /// </summary>
    [ObservableProperty]
    public partial string? Parent { get; private set; }

    /// <summary>
    /// Gets the threads.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<ThreadDetails> Threads { get; private set; } = [];

    /// <summary>
    /// Gets the modules.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<ModuleDetails> Modules { get; private set; } = [];

    /// <summary>
    /// Gets the filterable view of the handles.
    /// </summary>
    [ObservableProperty]
    public partial ListCollectionView HandlesView { get; private set; }

    /// <summary>
    /// Gets or sets the text that filters handles by type or name.
    /// </summary>
    [ObservableProperty]
    public partial string HandleFilter { get; set; } = string.Empty;

    /// <summary>
    /// Gets whether a load is in progress.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>
    /// Gets a message explaining why the current tab is empty, or <see langword="null"/>.
    /// </summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>
    /// Cancels any load in progress and stops observing the process list.
    /// </summary>
    public void Dispose()
    {
        _processes.PropertyChanged -= OnProcessesPropertyChanged;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
    }

    /// <summary>
    /// Reloads the current tab.
    /// </summary>
    /// <returns>A task that completes when loading has finished or was superseded.</returns>
    [RelayCommand]
    public Task RefreshAsync() => LoadAsync(TimeSpan.Zero);

    /// <summary>
    /// Formats the parent of a process for display.
    /// </summary>
    /// <param name="row">The process.</param>
    /// <param name="rows">All current rows.</param>
    /// <returns>The description, or <see langword="null"/> for processes without a parent.</returns>
    internal static string? DescribeParent(ProcessRowViewModel row, IEnumerable<ProcessRowViewModel> rows)
    {
        var parentPid = row.Sample.ParentPid;
        if (parentPid == 0 || parentPid == row.Pid)
        {
            return null;
        }

        // A process with that PID created after the child is a recycled PID, not the real parent.
        var parent = rows.FirstOrDefault(r => r.Pid == parentPid && r.Key.CreateTime <= row.Key.CreateTime);
        return parent is null
            ? string.Create(CultureInfo.CurrentCulture, $"Non-existent process ({parentPid})")
            : string.Create(CultureInfo.CurrentCulture, $"{parent.Name} ({parentPid})");
    }

    /// <summary>
    /// Loads the selected tab for the selected process after <paramref name="delay"/>, superseding older loads.
    /// </summary>
    /// <param name="delay">How long to wait first.</param>
    /// <returns>A task that completes when loading has finished or was superseded.</returns>
    private async Task LoadAsync(TimeSpan delay)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        var cts = new CancellationTokenSource();
        _loadCancellation = cts;
        var token = cts.Token;

        var row = _processes.SelectedRow;
        if (!IsActive || row is null)
        {
            Clear(row);
            return;
        }

        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, token).ConfigureAwait(true);
            }

            if (!ReferenceEquals(Process, row))
            {
                Clear(row);
            }

            IsLoading = true;
            Message = null;
            var tab = SelectedTab;
            var (status, data) = await Task.Run(() => QueryTab(row, tab, token), token).ConfigureAwait(true);
            token.ThrowIfCancellationRequested();

            ApplyTab(tab, data);
            Message = status switch
            {
                DetailsStatus.AccessDenied => "Access denied. Run ProcX as administrator to see this information.",
                DetailsStatus.NotFound => "The process has exited.",
                _ => null,
            };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Superseded by a newer load.
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cts))
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Queries one tab. Runs on a background thread and must not touch bound state.
    /// </summary>
    /// <param name="row">The process.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="token">Signals that the load was superseded.</param>
    /// <returns>The status and the data (or <see langword="null"/>).</returns>
    private (DetailsStatus Status, object? Data) QueryTab(ProcessRowViewModel row, DetailsTab tab, CancellationToken token) => tab switch
    {
        DetailsTab.General => Unwrap(_provider.GetGeneral(row.Key, row.ImagePath)),
        DetailsTab.Threads => Unwrap(_provider.GetThreads(row.Key)),
        DetailsTab.Modules => Unwrap(_provider.GetModules(row.Key)),
        DetailsTab.Handles => Unwrap(_provider.GetHandles(row.Key, token)),
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, null),
    };

    /// <summary>
    /// Splits a result into status and untyped data.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The status and data.</returns>
    private static (DetailsStatus Status, object? Data) Unwrap<T>(DetailsResult<T> result) => (result.Status, result.Value);

    /// <summary>
    /// Publishes queried data on the UI thread.
    /// </summary>
    /// <param name="tab">The tab the data belongs to.</param>
    /// <param name="data">The data, or <see langword="null"/> if the query failed.</param>
    private void ApplyTab(DetailsTab tab, object? data)
    {
        switch (tab)
        {
            case DetailsTab.General:
                General = data as ProcessGeneralInfo;
                break;
            case DetailsTab.Threads:
                Threads = data as IReadOnlyList<ThreadDetails> ?? [];
                break;
            case DetailsTab.Modules:
                Modules = data as IReadOnlyList<ModuleDetails> ?? [];
                break;
            case DetailsTab.Handles:
                _handles = data as IReadOnlyList<HandleDetails> ?? [];
                HandlesView = CreateHandlesView(_handles);
                break;
        }
    }

    /// <summary>
    /// Shows <paramref name="row"/> with no data loaded yet.
    /// </summary>
    /// <param name="row">The process, or <see langword="null"/>.</param>
    private void Clear(ProcessRowViewModel? row)
    {
        Process = row;
        Parent = row is null ? null : DescribeParent(row, _processes.Rows);
        General = null;
        Threads = [];
        Modules = [];
        _handles = [];
        HandlesView = CreateHandlesView([]);
        Message = null;
        IsLoading = false;
    }

    /// <summary>
    /// Creates the filtered view over a handle list.
    /// </summary>
    /// <param name="handles">The handles.</param>
    /// <returns>The view.</returns>
    private ListCollectionView CreateHandlesView(IReadOnlyList<HandleDetails> handles) =>
        new(handles.ToList()) { Filter = item => MatchesHandleFilter((HandleDetails)item, HandleFilter) };

    /// <summary>
    /// Determines whether a handle matches the filter text.
    /// </summary>
    /// <param name="handle">The handle.</param>
    /// <param name="filter">The filter text.</param>
    /// <returns><see langword="true"/> if shown.</returns>
    internal static bool MatchesHandleFilter(HandleDetails handle, string? filter) =>
        string.IsNullOrWhiteSpace(filter)
        || handle.TypeName.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase)
        || (handle.Name?.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>
    /// Re-applies the handle filter.
    /// </summary>
    /// <param name="value">The new filter text.</param>
    partial void OnHandleFilterChanged(string value) => HandlesView.Refresh();

    /// <summary>
    /// Loads the newly selected tab.
    /// </summary>
    /// <param name="value">The tab.</param>
    partial void OnSelectedTabChanged(DetailsTab value)
    {
        OnPropertyChanged(nameof(SelectedTabIndex));
        _ = LoadAsync(TimeSpan.Zero);
    }

    /// <summary>
    /// Loads when the panel is shown.
    /// </summary>
    /// <param name="value">Whether the panel is shown.</param>
    partial void OnIsActiveChanged(bool value) => _ = LoadAsync(TimeSpan.Zero);

    /// <summary>
    /// Loads the newly selected process after a short delay.
    /// </summary>
    /// <param name="sender">The process list.</param>
    /// <param name="e">The event arguments.</param>
    private void OnProcessesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProcessListViewModel.SelectedRow))
        {
            _ = LoadAsync(SelectionDelay);
        }
    }
}
