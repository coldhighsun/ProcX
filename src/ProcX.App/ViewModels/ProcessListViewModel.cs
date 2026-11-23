using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using ProcX.App.Services;
using ProcX.Application.Processes;
using ProcX.Domain.Processes;

namespace ProcX.App.ViewModels;

/// <summary>
/// The live, sortable and filterable list of processes.
/// </summary>
public sealed partial class ProcessListViewModel : ObservableObject
{
    /// <summary>
    /// Mutable properties whose changes re-sort the view while a sort on them is active.
    /// </summary>
    private static readonly string[] LiveSortProperties =
    [
        nameof(ProcessRowViewModel.CpuPercent),
        nameof(ProcessRowViewModel.MemoryBytes),
        nameof(ProcessRowViewModel.CommitBytes),
        nameof(ProcessRowViewModel.ThreadCount),
        nameof(ProcessRowViewModel.HandleCount),
        nameof(ProcessRowViewModel.Description),
        nameof(ProcessRowViewModel.UserName),
        nameof(ProcessRowViewModel.ImagePath),
    ];

    /// <summary>
    /// The backing collection of all rows.
    /// </summary>
    private readonly ObservableCollection<ProcessRowViewModel> _rows = [];

    /// <summary>
    /// Rows indexed by process identity.
    /// </summary>
    private readonly Dictionary<ProcessKey, ProcessRowViewModel> _rowsByKey = [];

    /// <summary>
    /// Fills in path, user, description and icon of new rows.
    /// </summary>
    private readonly IProcessRowEnricher _enricher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessListViewModel"/> class.
    /// </summary>
    /// <param name="enricher">Fills in path, user, description and icon of new rows.</param>
    public ProcessListViewModel(IProcessRowEnricher enricher)
    {
        _enricher = enricher;

        View = new ListCollectionView(_rows)
        {
            Filter = item => ProcessRowFilter.Matches((ProcessRowViewModel)item, SearchText),
            IsLiveSorting = true,
        };

        ((INotifyCollectionChanged)View.SortDescriptions).CollectionChanged += (_, _) => SyncLiveSortingProperties();
        View.SortDescriptions.Add(new SortDescription(nameof(ProcessRowViewModel.Name), ListSortDirection.Ascending));
    }

    /// <summary>
    /// Gets the sorted and filtered view bound to the grid.
    /// </summary>
    public ListCollectionView View { get; }

    /// <summary>
    /// Gets all rows, regardless of the current filter.
    /// </summary>
    public IReadOnlyCollection<ProcessRowViewModel> Rows => _rows;

    /// <summary>
    /// Gets or sets the search text used to filter rows.
    /// </summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selected row.
    /// </summary>
    [ObservableProperty]
    public partial ProcessRowViewModel? SelectedRow { get; set; }

    /// <summary>
    /// Gets the total number of processes.
    /// </summary>
    [ObservableProperty]
    public partial int ProcessCount { get; private set; }

    /// <summary>
    /// Gets the overall CPU usage of the system in percent.
    /// </summary>
    [ObservableProperty]
    public partial double TotalCpuPercent { get; private set; }

    /// <summary>
    /// Applies a sampling delta. Must be called on the UI thread.
    /// </summary>
    /// <param name="delta">The delta.</param>
    public void Apply(ProcessDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        foreach (var key in delta.Removed)
        {
            if (_rowsByKey.Remove(key, out var row))
            {
                _rows.Remove(row);
            }
        }

        foreach (var metrics in delta.Updated)
        {
            if (_rowsByKey.TryGetValue(metrics.Key, out var row))
            {
                row.Update(metrics);
            }
        }

        foreach (var metrics in delta.Added)
        {
            var row = new ProcessRowViewModel(metrics);
            if (_rowsByKey.TryAdd(row.Key, row))
            {
                _rows.Add(row);
                _enricher.Enqueue(row);
            }
        }

        ProcessCount = _rows.Count;
        TotalCpuPercent = delta.TotalCpuPercent;
    }

    /// <summary>
    /// Restricts live sorting to the properties currently sorted on that can change over time.
    /// </summary>
    /// <remarks>
    /// Live shaping re-evaluates every listed property on every change notification, so watching
    /// properties that are not part of the sort wastes UI thread time with hundreds of rows updating each second.
    /// </remarks>
    private void SyncLiveSortingProperties()
    {
        View.LiveSortingProperties.Clear();
        foreach (var sort in View.SortDescriptions)
        {
            if (LiveSortProperties.Contains(sort.PropertyName))
            {
                View.LiveSortingProperties.Add(sort.PropertyName);
            }
        }
    }

    /// <summary>
    /// Re-applies the filter when the search text changes.
    /// </summary>
    /// <param name="value">The new search text.</param>
    partial void OnSearchTextChanged(string value) => View.Refresh();
}
