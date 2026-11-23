using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProcX.App.Services;
using ProcX.Application.Processes;
using ProcX.Domain.Processes;

namespace ProcX.App.ViewModels;

/// <summary>
/// The live, sortable and filterable list of processes, shown either flat or as a parent/child tree.
/// </summary>
/// <remarks>
/// Both modes share one <see cref="ListCollectionView"/>. Tree mode sorts by <see cref="ProcessRowViewModel.TreeOrder"/>
/// and filters by <see cref="ProcessRowViewModel.IsTreeVisible"/>; live shaping applies changes to those properties
/// without a full refresh, so the scroll position and selection survive updates.
/// </remarks>
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
        nameof(ProcessRowViewModel.TreeOrder),
    ];

    /// <summary>
    /// The sort applied in tree mode.
    /// </summary>
    private static readonly SortDescription TreeSort = new(nameof(ProcessRowViewModel.TreeOrder), ListSortDirection.Ascending);

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
    /// All rows in depth-first tree order; empty outside tree mode.
    /// </summary>
    private List<ProcessRowViewModel> _treeOrder = [];

    /// <summary>
    /// The flat-mode sort to restore when leaving tree mode.
    /// </summary>
    private SortDescription[] _flatSort = [new(nameof(ProcessRowViewModel.Name), ListSortDirection.Ascending)];

    /// <summary>
    /// Set while this class changes <see cref="ListCollectionView.SortDescriptions"/> itself.
    /// </summary>
    private bool _isApplyingSort;

    /// <summary>
    /// Set when tree mode is left because the user sorted by a column, so that sort is kept.
    /// </summary>
    private bool _isLeavingTreeForUserSort;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessListViewModel"/> class.
    /// </summary>
    /// <param name="enricher">Fills in path, user, description and icon of new rows.</param>
    public ProcessListViewModel(IProcessRowEnricher enricher)
    {
        _enricher = enricher;

        View = new ListCollectionView(_rows)
        {
            Filter = item => IsVisible((ProcessRowViewModel)item),
            IsLiveSorting = true,
            IsLiveFiltering = true,
        };

        View.LiveFilteringProperties.Add(nameof(ProcessRowViewModel.IsTreeVisible));
        View.LiveFilteringProperties.Add(nameof(ProcessRowViewModel.Description));
        View.LiveFilteringProperties.Add(nameof(ProcessRowViewModel.UserName));
        View.LiveFilteringProperties.Add(nameof(ProcessRowViewModel.ImagePath));

        ((INotifyCollectionChanged)View.SortDescriptions).CollectionChanged += OnSortDescriptionsChanged;
        ApplySort(_flatSort);
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
    /// Gets or sets whether processes are shown as a parent/child tree.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTreeView { get; set; }

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

        if (delta.Added.Count > 0 || delta.Removed.Count > 0)
        {
            RebuildTree();
        }

        ProcessCount = _rows.Count;
        TotalCpuPercent = delta.TotalCpuPercent;
    }

    /// <summary>
    /// Expands or collapses a row in tree mode.
    /// </summary>
    /// <param name="row">The row.</param>
    [RelayCommand]
    private void ToggleExpand(ProcessRowViewModel? row)
    {
        if (row is null || !row.HasChildren)
        {
            return;
        }

        row.IsExpanded = !row.IsExpanded;
        UpdateTreeVisibility();
    }

    /// <summary>
    /// Expands every row in tree mode.
    /// </summary>
    [RelayCommand]
    private void ExpandAll() => SetAllExpanded(true);

    /// <summary>
    /// Collapses every row in tree mode.
    /// </summary>
    [RelayCommand]
    private void CollapseAll() => SetAllExpanded(false);

    /// <summary>
    /// Sets the expanded state of all rows and refreshes visibility.
    /// </summary>
    /// <param name="expanded">The state.</param>
    private void SetAllExpanded(bool expanded)
    {
        foreach (var row in _rows)
        {
            row.IsExpanded = expanded;
        }

        UpdateTreeVisibility();
    }

    /// <summary>
    /// The view filter.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns><see langword="true"/> if the row is shown.</returns>
    private bool IsVisible(ProcessRowViewModel row) =>
        IsTreeView ? row.IsTreeVisible : ProcessRowFilter.Matches(row, SearchText);

    /// <summary>
    /// Recomputes tree order, depth and parent of every row. No-op outside tree mode.
    /// </summary>
    private void RebuildTree()
    {
        if (!IsTreeView)
        {
            return;
        }

        var nodes = ProcessTreeBuilder.Flatten(ProcessTreeBuilder.Build(_rows.Select(static r => r.Sample)), static _ => true);
        var order = new List<ProcessRowViewModel>(nodes.Count);
        var ancestors = new List<ProcessRowViewModel>();

        foreach (var node in nodes)
        {
            var row = _rowsByKey[node.Process.Key];

            // In pre-order the parent of a node at depth d is the last node seen at depth d - 1.
            if (ancestors.Count > node.Depth)
            {
                ancestors.RemoveRange(node.Depth, ancestors.Count - node.Depth);
            }

            row.TreeParent = node.Depth > 0 ? ancestors[^1] : null;
            row.TreeOrder = order.Count;
            row.Depth = node.Depth;
            row.HasChildren = node.HasChildren;
            row.IsInTree = true;

            ancestors.Add(row);
            order.Add(row);
        }

        _treeOrder = order;
        UpdateTreeVisibility();
    }

    /// <summary>
    /// Recomputes <see cref="ProcessRowViewModel.IsTreeVisible"/> from the collapsed state, or, while searching,
    /// shows matching rows together with their ancestors regardless of collapsed state.
    /// </summary>
    private void UpdateTreeVisibility()
    {
        if (!IsTreeView)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var visible = new HashSet<ProcessRowViewModel>();
            foreach (var row in _treeOrder.Where(r => ProcessRowFilter.Matches(r, SearchText)))
            {
                for (var current = row; current is not null && visible.Add(current); current = current.TreeParent)
                {
                }
            }

            foreach (var row in _treeOrder)
            {
                row.IsTreeVisible = visible.Contains(row);
            }

            return;
        }

        // Rows deeper than a collapsed ancestor are hidden until the walk returns to that ancestor's depth.
        var hiddenBelowDepth = int.MaxValue;
        foreach (var row in _treeOrder)
        {
            if (row.Depth > hiddenBelowDepth)
            {
                row.IsTreeVisible = false;
                continue;
            }

            hiddenBelowDepth = int.MaxValue;
            row.IsTreeVisible = true;
            if (row.HasChildren && !row.IsExpanded)
            {
                hiddenBelowDepth = row.Depth;
            }
        }
    }

    /// <summary>
    /// Switches between flat and tree presentation.
    /// </summary>
    /// <param name="value">Whether tree mode is now on.</param>
    partial void OnIsTreeViewChanged(bool value)
    {
        if (value)
        {
            _flatSort = [.. View.SortDescriptions];
            ApplySort([TreeSort]);
            RebuildTree();
        }
        else
        {
            foreach (var row in _rows)
            {
                row.IsInTree = false;
                row.Depth = 0;
                row.TreeParent = null;
                row.IsTreeVisible = true;
            }

            _treeOrder = [];

            if (!_isLeavingTreeForUserSort)
            {
                ApplySort(_flatSort);
            }
        }

        RefreshFilter();
    }

    /// <summary>
    /// Re-applies the filter when the search text changes.
    /// </summary>
    /// <param name="value">The new search text.</param>
    partial void OnSearchTextChanged(string value)
    {
        UpdateTreeVisibility();
        RefreshFilter();
    }

    /// <summary>
    /// Re-evaluates the filter unless a deferred refresh (e.g. a column sort in progress) will do it anyway;
    /// refreshing during a deferral throws.
    /// </summary>
    private void RefreshFilter()
    {
        if (!View.NeedsRefresh)
        {
            View.Refresh();
        }
    }

    /// <summary>
    /// Replaces the sort without treating it as a user action.
    /// </summary>
    /// <param name="sorts">The new sort.</param>
    private void ApplySort(IEnumerable<SortDescription> sorts)
    {
        _isApplyingSort = true;
        try
        {
            using (View.DeferRefresh())
            {
                View.SortDescriptions.Clear();
                foreach (var sort in sorts)
                {
                    View.SortDescriptions.Add(sort);
                }
            }
        }
        finally
        {
            _isApplyingSort = false;
        }

        SyncLiveSortingProperties();
    }

    /// <summary>
    /// Keeps live sorting in sync with the sort and leaves tree mode when the user sorts by a column.
    /// </summary>
    /// <param name="sender">The sort description collection.</param>
    /// <param name="e">The event arguments.</param>
    private void OnSortDescriptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isApplyingSort)
        {
            return;
        }

        SyncLiveSortingProperties();

        if (IsTreeView && !(View.SortDescriptions.Count == 1 && View.SortDescriptions[0] == TreeSort))
        {
            _isLeavingTreeForUserSort = true;
            try
            {
                IsTreeView = false;
            }
            finally
            {
                _isLeavingTreeForUserSort = false;
            }
        }
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
}
