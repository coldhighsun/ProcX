using System.ComponentModel;
using System.Windows.Threading;
using ProcX.App.Services;
using ProcX.App.ViewModels;

namespace ProcX.App.Tests.ViewModels;

public class ProcessListViewModelTests
{
    private readonly RecordingEnricher _enricher = new();
    private readonly ProcessListViewModel _viewModel;

    public ProcessListViewModelTests()
    {
        _viewModel = new ProcessListViewModel(_enricher);
    }

    [Fact]
    public void Apply_AddedProcesses_CreatesRowsAndQueuesEnrichment()
    {
        // Act
        _viewModel.Apply(Rows.Delta(added: [Rows.Metrics(1), Rows.Metrics(2)], totalCpu: 12.5));

        // Assert
        _viewModel.Rows.Select(static r => r.Pid).Should().BeEquivalentTo([1, 2]);
        _enricher.Rows.Select(static r => r.Pid).Should().BeEquivalentTo([1, 2]);
        _viewModel.ProcessCount.Should().Be(2);
        _viewModel.TotalCpuPercent.Should().Be(12.5);
    }

    [Fact]
    public void Apply_UpdatedProcess_UpdatesExistingRowInPlace()
    {
        // Arrange
        _viewModel.Apply(Rows.Delta(added: [Rows.Metrics(1)]));
        var row = _viewModel.Rows.Single();

        // Act
        _viewModel.Apply(Rows.Delta(updated: [Rows.Metrics(1, cpu: 42, memory: 2048)]));

        // Assert
        _viewModel.Rows.Single().Should().BeSameAs(row);
        row.CpuPercent.Should().Be(42);
        row.MemoryBytes.Should().Be(2048);
    }

    [Fact]
    public void Apply_RemovedProcess_RemovesRow()
    {
        // Arrange
        var gone = Rows.Metrics(2);
        _viewModel.Apply(Rows.Delta(added: [Rows.Metrics(1), gone]));

        // Act
        _viewModel.Apply(Rows.Delta(removed: [gone.Key]));

        // Assert
        _viewModel.Rows.Select(static r => r.Pid).Should().Equal(1);
        _viewModel.ProcessCount.Should().Be(1);
    }

    [Fact]
    public void Apply_UpdateForUnknownProcess_IsIgnored()
    {
        // Act
        var act = () => _viewModel.Apply(Rows.Delta(updated: [Rows.Metrics(99)]));

        // Assert
        act.Should().NotThrow();
        _viewModel.Rows.Should().BeEmpty();
    }

    [Fact]
    public void SearchText_Set_FiltersView()
    {
        // Arrange
        _viewModel.Apply(Rows.Delta(added: [Rows.Metrics(1, "explorer.exe"), Rows.Metrics(2, "notepad.exe")]));

        // Act
        _viewModel.SearchText = "NOTE";

        // Assert
        _viewModel.View.Cast<ProcessRowViewModel>().Select(static r => r.Name).Should().Equal("notepad.exe");
    }

    [Fact]
    public void View_Default_SortsByNameAscending()
    {
        // Act
        _viewModel.Apply(Rows.Delta(added: [Rows.Metrics(1, "b.exe"), Rows.Metrics(2, "a.exe")]));

        // Assert
        _viewModel.View.Cast<ProcessRowViewModel>().Select(static r => r.Name).Should().Equal("a.exe", "b.exe");
        _viewModel.View.LiveSortingProperties.Should().BeEmpty();
    }

    [Fact]
    public void View_SortedByCpu_ResortsLiveWhenCpuChanges()
    {
        // Arrange
        _viewModel.Apply(Rows.Delta(added: [Rows.Metrics(1), Rows.Metrics(2)]));
        _viewModel.View.SortDescriptions.Clear();
        _viewModel.View.SortDescriptions.Add(new SortDescription(nameof(ProcessRowViewModel.CpuPercent), ListSortDirection.Descending));

        // Act
        _viewModel.Apply(Rows.Delta(updated: [Rows.Metrics(1, cpu: 1), Rows.Metrics(2, cpu: 50)]));
        ProcessPendingDispatcherWork();

        // Assert
        _viewModel.View.LiveSortingProperties.Should().Equal(nameof(ProcessRowViewModel.CpuPercent));
        _viewModel.View.Cast<ProcessRowViewModel>().Select(static r => r.Pid).Should().Equal(2, 1);
    }

    /// <summary>
    /// Runs queued dispatcher operations (live shaping re-sorts asynchronously on the dispatcher).
    /// </summary>
    private static void ProcessPendingDispatcherWork() =>
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.ContextIdle);

    private sealed class RecordingEnricher : IProcessRowEnricher
    {
        public List<ProcessRowViewModel> Rows { get; } = [];

        public void Enqueue(ProcessRowViewModel row) => Rows.Add(row);
    }
}
