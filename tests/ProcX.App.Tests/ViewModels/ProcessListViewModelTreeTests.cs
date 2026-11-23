using System.ComponentModel;
using System.Windows.Threading;
using ProcX.App.Services;
using ProcX.App.ViewModels;
using ProcX.Application.Processes;

namespace ProcX.App.Tests.ViewModels;

public class ProcessListViewModelTreeTests
{
    private readonly ProcessListViewModel _viewModel = new(new NullEnricher());

    // Tree used by most tests:
    //   1 root.exe
    //     2 child.exe
    //       3 grandchild.exe
    //     4 sibling.exe
    //   5 other.exe
    private static readonly ProcessMetrics Root = Rows.Metrics(1, "root.exe", createdAfterSeconds: 0);
    private static readonly ProcessMetrics Child = Rows.Metrics(2, "child.exe", parentPid: 1, createdAfterSeconds: 1);
    private static readonly ProcessMetrics Grandchild = Rows.Metrics(3, "grandchild.exe", parentPid: 2, createdAfterSeconds: 2);
    private static readonly ProcessMetrics Sibling = Rows.Metrics(4, "sibling.exe", parentPid: 1, createdAfterSeconds: 3);
    private static readonly ProcessMetrics Other = Rows.Metrics(5, "other.exe", createdAfterSeconds: 4);

    public ProcessListViewModelTreeTests()
    {
        _viewModel.Apply(Rows.Delta(added: [Other, Sibling, Grandchild, Child, Root]));
    }

    private IEnumerable<(int Pid, int Depth)> Visible()
    {
        ProcessPendingDispatcherWork();
        return _viewModel.View.Cast<ProcessRowViewModel>().Select(static r => (r.Pid, r.Depth));
    }

    private ProcessRowViewModel Row(int pid) => _viewModel.Rows.Single(r => r.Pid == pid);

    [Fact]
    public void IsTreeView_TurnedOn_ShowsRowsDepthFirstWithDepths()
    {
        // Act
        _viewModel.IsTreeView = true;

        // Assert
        Visible().Should().Equal((1, 0), (2, 1), (3, 2), (4, 1), (5, 0));
        Row(1).NodeKind.Should().Be(TreeNodeKind.Expanded);
        Row(3).NodeKind.Should().Be(TreeNodeKind.Leaf);
    }

    [Fact]
    public void ToggleExpand_ExpandedRow_HidesDescendantsUntilExpandedAgain()
    {
        // Arrange
        _viewModel.IsTreeView = true;

        // Act
        _viewModel.ToggleExpandCommand.Execute(Row(2));
        var collapsed = Visible().Select(static r => r.Pid).ToList();
        _viewModel.ToggleExpandCommand.Execute(Row(2));
        var expanded = Visible().Select(static r => r.Pid).ToList();

        // Assert
        collapsed.Should().Equal(1, 2, 4, 5);
        expanded.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void CollapseAll_InTreeMode_ShowsOnlyRoots()
    {
        // Arrange
        _viewModel.IsTreeView = true;

        // Act
        _viewModel.CollapseAllCommand.Execute(null);

        // Assert
        Visible().Select(static r => r.Pid).Should().Equal(1, 5);
        Row(1).NodeKind.Should().Be(TreeNodeKind.Collapsed);
    }

    [Fact]
    public void ExpandAll_AfterCollapseAll_ShowsEveryRow()
    {
        // Arrange
        _viewModel.IsTreeView = true;
        _viewModel.CollapseAllCommand.Execute(null);

        // Act
        _viewModel.ExpandAllCommand.Execute(null);

        // Assert
        Visible().Should().HaveCount(5);
    }

    [Fact]
    public void Apply_NewChildInTreeMode_InsertsItUnderItsParent()
    {
        // Arrange
        _viewModel.IsTreeView = true;

        // Act
        _viewModel.Apply(Rows.Delta(added: [Rows.Metrics(6, "new.exe", parentPid: 2, createdAfterSeconds: 10)]));

        // Assert
        Visible().Should().Equal((1, 0), (2, 1), (3, 2), (6, 2), (4, 1), (5, 0));
    }

    [Fact]
    public void Apply_ParentExitsInTreeMode_PromotesChildrenToRoots()
    {
        // Arrange
        _viewModel.IsTreeView = true;

        // Act
        _viewModel.Apply(Rows.Delta(removed: [Root.Key]));

        // Assert
        Visible().Should().Equal((2, 0), (3, 1), (4, 0), (5, 0));
    }

    [Fact]
    public void SearchText_InTreeMode_ShowsMatchesWithTheirAncestorsEvenWhenCollapsed()
    {
        // Arrange
        _viewModel.IsTreeView = true;
        _viewModel.CollapseAllCommand.Execute(null);

        // Act
        _viewModel.SearchText = "grandchild";

        // Assert
        Visible().Select(static r => r.Pid).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void SearchText_ClearedInTreeMode_RestoresCollapsedState()
    {
        // Arrange
        _viewModel.IsTreeView = true;
        _viewModel.ToggleExpandCommand.Execute(Row(1));
        _viewModel.SearchText = "grandchild";

        // Act
        _viewModel.SearchText = string.Empty;

        // Assert
        Visible().Select(static r => r.Pid).Should().Equal(1, 5);
    }

    [Fact]
    public void IsTreeView_TurnedOff_RestoresPreviousFlatSortAndClearsIndentation()
    {
        // Arrange
        _viewModel.IsTreeView = true;
        _viewModel.CollapseAllCommand.Execute(null);

        // Act
        _viewModel.IsTreeView = false;

        // Assert: default flat sort is by name.
        Visible().Should().Equal((2, 0), (3, 0), (5, 0), (1, 0), (4, 0));
        _viewModel.Rows.Should().OnlyContain(static r => r.NodeKind == TreeNodeKind.None);
    }

    [Fact]
    public void SortByColumn_InTreeMode_LeavesTreeModeAndKeepsUserSort()
    {
        // Arrange
        _viewModel.IsTreeView = true;
        _viewModel.CollapseAllCommand.Execute(null);

        // Act: what DataGrid does when a column header is clicked.
        using (_viewModel.View.DeferRefresh())
        {
            _viewModel.View.SortDescriptions.Clear();
            _viewModel.View.SortDescriptions.Add(new SortDescription(nameof(ProcessRowViewModel.Pid), ListSortDirection.Descending));
        }

        // Assert
        _viewModel.IsTreeView.Should().BeFalse();
        Visible().Select(static r => r.Pid).Should().Equal(5, 4, 3, 2, 1);
    }

    [Fact]
    public void ToggleExpand_Leaf_DoesNothing()
    {
        // Arrange
        _viewModel.IsTreeView = true;

        // Act
        _viewModel.ToggleExpandCommand.Execute(Row(3));

        // Assert
        Row(3).IsExpanded.Should().BeTrue();
        Visible().Should().HaveCount(5);
    }

    /// <summary>
    /// Runs queued dispatcher operations (live shaping is applied asynchronously on the dispatcher).
    /// </summary>
    private static void ProcessPendingDispatcherWork() =>
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.ContextIdle);

    private sealed class NullEnricher : IProcessRowEnricher
    {
        public void Enqueue(ProcessRowViewModel row)
        {
        }
    }
}
