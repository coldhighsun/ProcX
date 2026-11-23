using ProcX.App.Services;
using ProcX.App.ViewModels;
using ProcX.Application.Details;

namespace ProcX.App.Tests.ViewModels;

public sealed class ProcessDetailsViewModelTests : IDisposable
{
    private readonly FakeDetailsProvider _provider = new();
    private readonly ProcessListViewModel _list = new(new NullEnricher());
    private readonly ProcessDetailsViewModel _details;

    public ProcessDetailsViewModelTests()
    {
        _details = new ProcessDetailsViewModel(_provider, _list) { SelectionDelay = TimeSpan.Zero };
        _list.Apply(Rows.Delta(added:
        [
            Rows.Metrics(10, "parent.exe", createdAfterSeconds: 0),
            Rows.Metrics(20, "child.exe", parentPid: 10, createdAfterSeconds: 5),
            Rows.Metrics(30, "orphan.exe", parentPid: 99, createdAfterSeconds: 5),
        ]));
    }

    public void Dispose() => _details.Dispose();

    private ProcessRowViewModel Row(int pid) => _list.Rows.Single(r => r.Pid == pid);

    [Fact]
    public async Task SelectedRow_Changed_LoadsGeneralTab()
    {
        // Act
        _list.SelectedRow = Row(20);
        await _details.RefreshAsync();

        // Assert
        _details.Process.Should().BeSameAs(Row(20));
        _details.General!.CommandLine.Should().Be("cmd-20");
        _details.Parent.Should().Be("parent.exe (10)");
        _details.Message.Should().BeNull();
        _details.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task SelectedTab_ChangedToThreads_LoadsOnlyThatTab()
    {
        // Arrange
        _list.SelectedRow = Row(20);
        await _details.RefreshAsync();
        _provider.Calls.Clear();

        // Act
        _details.SelectedTab = DetailsTab.Threads;
        await _details.RefreshAsync();

        // Assert
        _details.Threads.Should().ContainSingle().Which.ThreadId.Should().Be(21);
        _provider.Calls.Should().OnlyContain(static c => c == "Threads:20");
        _details.SelectedTabIndex.Should().Be(1);
    }

    [Fact]
    public async Task Refresh_AccessDenied_ShowsElevationHint()
    {
        // Arrange
        _provider.Status = DetailsStatus.AccessDenied;
        _list.SelectedRow = Row(20);

        // Act
        await _details.RefreshAsync();

        // Assert
        _details.General.Should().BeNull();
        _details.Message.Should().Contain("administrator");
    }

    [Fact]
    public async Task Refresh_ProcessExited_ShowsExitedMessage()
    {
        // Arrange
        _provider.Status = DetailsStatus.NotFound;
        _list.SelectedRow = Row(20);

        // Act
        await _details.RefreshAsync();

        // Assert
        _details.Message.Should().Contain("exited");
    }

    [Fact]
    public async Task IsActive_False_DoesNotQuery()
    {
        // Arrange
        _details.IsActive = false;
        _provider.Calls.Clear();

        // Act
        _list.SelectedRow = Row(20);
        await _details.RefreshAsync();

        // Assert
        _provider.Calls.Should().BeEmpty();
        _details.Process.Should().BeSameAs(Row(20));
    }

    [Fact]
    public async Task SelectedRow_Cleared_ClearsPanel()
    {
        // Arrange
        _list.SelectedRow = Row(20);
        await _details.RefreshAsync();

        // Act
        _list.SelectedRow = null;
        await _details.RefreshAsync();

        // Assert
        _details.Process.Should().BeNull();
        _details.General.Should().BeNull();
    }

    [Fact]
    public async Task HandleFilter_Set_FiltersByTypeOrName()
    {
        // Arrange
        _provider.Handles =
        [
            new HandleDetails(4, "File", @"C:\log.txt", 0x12019F),
            new HandleDetails(8, "Event", null, 0x1F0003),
            new HandleDetails(12, "Key", @"HKLM\SOFTWARE", 0x20019),
        ];
        _list.SelectedRow = Row(20);
        _details.SelectedTab = DetailsTab.Handles;
        await _details.RefreshAsync();

        // Act
        _details.HandleFilter = "log";
        var byName = _details.HandlesView.Cast<HandleDetails>().Select(static h => h.Handle).ToList();
        _details.HandleFilter = "event";
        var byType = _details.HandlesView.Cast<HandleDetails>().Select(static h => h.Handle).ToList();

        // Assert
        byName.Should().Equal(4UL);
        byType.Should().Equal(8UL);
    }

    [Theory]
    [InlineData(20, "parent.exe (10)")]
    [InlineData(30, "Non-existent process (99)")]
    [InlineData(10, null)]
    public void DescribeParent_Process_DescribesParentOrNotesItExited(int pid, string? expected)
    {
        // Act
        var result = ProcessDetailsViewModel.DescribeParent(Row(pid), _list.Rows);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void DescribeParent_ParentPidReusedByNewerProcess_ReportsNonExistentParent()
    {
        // Arrange: PID 40 now belongs to a process created after the child.
        _list.Apply(Rows.Delta(added:
        [
            Rows.Metrics(50, "child.exe", parentPid: 40, createdAfterSeconds: 10),
            Rows.Metrics(40, "newcomer.exe", createdAfterSeconds: 20),
        ]));

        // Act
        var result = ProcessDetailsViewModel.DescribeParent(Row(50), _list.Rows);

        // Assert
        result.Should().Be("Non-existent process (40)");
    }

    private sealed class NullEnricher : IProcessRowEnricher
    {
        public void Enqueue(ProcessRowViewModel row)
        {
        }
    }
}
