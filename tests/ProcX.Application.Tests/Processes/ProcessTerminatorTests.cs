using ProcX.Application.Processes;
using ProcX.Domain.Processes;

namespace ProcX.Application.Tests.Processes;

public class ProcessTerminatorTests
{
    private readonly FixedSnapshotProvider _snapshots = new();
    private readonly RecordingController _controller = new();
    private readonly ProcessTerminator _terminator;

    public ProcessTerminatorTests()
    {
        _terminator = new ProcessTerminator(_snapshots, _controller);
    }

    [Fact]
    public void Terminate_Process_DelegatesToController()
    {
        // Arrange
        var key = Samples.Process(10).Key;

        // Act
        var result = _terminator.Terminate(key);

        // Assert
        result.Status.Should().Be(TerminationStatus.Succeeded);
        _controller.Terminated.Should().Equal(key);
    }

    [Fact]
    public void TerminateTree_ProcessWithDescendants_TerminatesChildrenFirst()
    {
        // Arrange
        var root = Samples.Process(10, createdAfterSeconds: 1) with { ParentPid = 1 };
        var child = Samples.Process(20, createdAfterSeconds: 2) with { ParentPid = 10 };
        var grandchild = Samples.Process(30, createdAfterSeconds: 3) with { ParentPid = 20 };
        var unrelated = Samples.Process(40, createdAfterSeconds: 4) with { ParentPid = 1 };
        _snapshots.Processes = [Samples.Process(1), root, child, grandchild, unrelated];

        // Act
        var results = _terminator.TerminateTree(root.Key);

        // Assert
        _controller.Terminated.Should().Equal(grandchild.Key, child.Key, root.Key);
        results.Should().HaveCount(3).And.OnlyContain(static r => r.Status == TerminationStatus.Succeeded);
    }

    [Fact]
    public void TerminateTree_OneChildDenied_ContinuesWithRemainingProcesses()
    {
        // Arrange
        var root = Samples.Process(10, createdAfterSeconds: 1);
        var protectedChild = Samples.Process(20, createdAfterSeconds: 2) with { ParentPid = 10 };
        _snapshots.Processes = [root, protectedChild];
        _controller.Denied.Add(protectedChild.Key);

        // Act
        var results = _terminator.TerminateTree(root.Key);

        // Assert
        results.Select(static r => r.Status).Should().Equal(TerminationStatus.AccessDenied, TerminationStatus.Succeeded);
    }

    [Fact]
    public void TerminateTree_RootAlreadyExited_ReturnsNotFoundWithoutTerminating()
    {
        // Arrange
        _snapshots.Processes = [Samples.Process(1)];
        var exited = Samples.Process(99).Key;

        // Act
        var results = _terminator.TerminateTree(exited);

        // Assert
        results.Should().ContainSingle().Which.Status.Should().Be(TerminationStatus.NotFound);
        _controller.Terminated.Should().BeEmpty();
    }

    [Theory]
    [InlineData(TerminationStatus.Succeeded, true)]
    [InlineData(TerminationStatus.NotFound, true)]
    [InlineData(TerminationStatus.AccessDenied, false)]
    [InlineData(TerminationStatus.Failed, false)]
    public void IsGone_Status_ReflectsWhetherProcessNoLongerRuns(TerminationStatus status, bool expected)
    {
        // Act
        var result = new TerminationResult(Samples.Process(1).Key, status);

        // Assert
        result.IsGone.Should().Be(expected);
    }

    private sealed class FixedSnapshotProvider : IProcessSnapshotProvider
    {
        public IReadOnlyList<ProcessSample> Processes { get; set; } = [];

        public ProcessSnapshot Capture() => new(Samples.BaseTime, Processes);
    }

    private sealed class RecordingController : IProcessController
    {
        public List<ProcessKey> Terminated { get; } = [];

        public HashSet<ProcessKey> Denied { get; } = [];

        public TerminationResult Terminate(ProcessKey key)
        {
            if (Denied.Contains(key))
            {
                return new TerminationResult(key, TerminationStatus.AccessDenied, 5);
            }

            Terminated.Add(key);
            return new TerminationResult(key, TerminationStatus.Succeeded);
        }
    }
}
