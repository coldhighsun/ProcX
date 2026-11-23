using ProcX.Domain.Processes;

namespace ProcX.Domain.Tests.Processes;

public class ProcessTreeBuilderTests
{
    [Fact]
    public void Build_ParentCreatedBeforeChild_NestsChildUnderParent()
    {
        // Arrange
        var parent = SampleFactory.Create(pid: 100, parentPid: 4, createdAfterSeconds: 1);
        var child = SampleFactory.Create(pid: 200, parentPid: 100, createdAfterSeconds: 2);

        // Act
        var roots = ProcessTreeBuilder.Build([child, parent]);

        // Assert
        roots.Should().ContainSingle().Which.Process.Should().Be(parent);
        roots[0].Children.Should().ContainSingle().Which.Process.Should().Be(child);
    }

    [Fact]
    public void Build_ParentPidReusedByNewerProcess_TreatsChildAsRoot()
    {
        // Arrange: the original parent exited and PID 100 now belongs to a process created after the child.
        var orphan = SampleFactory.Create(pid: 200, parentPid: 100, createdAfterSeconds: 5);
        var reusedPid = SampleFactory.Create(pid: 100, parentPid: 4, createdAfterSeconds: 10);

        // Act
        var roots = ProcessTreeBuilder.Build([orphan, reusedPid]);

        // Assert
        roots.Select(static n => n.Process.Pid).Should().Equal(200, 100);
        roots.Should().AllSatisfy(static n => n.Children.Should().BeEmpty());
    }

    [Fact]
    public void Build_MissingParent_TreatsProcessAsRoot()
    {
        // Arrange
        var process = SampleFactory.Create(pid: 300, parentPid: 999, createdAfterSeconds: 1);

        // Act
        var roots = ProcessTreeBuilder.Build([process]);

        // Assert
        roots.Should().ContainSingle().Which.Process.Should().Be(process);
    }

    [Fact]
    public void Build_ProcessIsItsOwnParent_TreatsProcessAsRoot()
    {
        // Arrange: the System Idle Process reports itself (PID 0) as its parent.
        var idle = SampleFactory.Create(pid: 0, parentPid: 0, createdAfterSeconds: 0);

        // Act
        var roots = ProcessTreeBuilder.Build([idle]);

        // Assert
        roots.Should().ContainSingle().Which.Children.Should().BeEmpty();
    }

    [Fact]
    public void Build_MutualParentsWithEqualCreateTime_ProducesAcyclicTree()
    {
        // Arrange
        var a = SampleFactory.Create(pid: 10, parentPid: 20, createdAfterSeconds: 0);
        var b = SampleFactory.Create(pid: 20, parentPid: 10, createdAfterSeconds: 0);

        // Act
        var roots = ProcessTreeBuilder.Build([a, b]);

        // Assert: the lower PID wins the tie and becomes the parent.
        roots.Should().ContainSingle().Which.Process.Should().Be(a);
        roots[0].Children.Should().ContainSingle().Which.Process.Should().Be(b);
    }

    [Fact]
    public void Build_Siblings_OrdersByCreateTimeThenPid()
    {
        // Arrange
        var parent = SampleFactory.Create(pid: 1, parentPid: 0, createdAfterSeconds: 0);
        var late = SampleFactory.Create(pid: 2, parentPid: 1, createdAfterSeconds: 9);
        var earlyHighPid = SampleFactory.Create(pid: 50, parentPid: 1, createdAfterSeconds: 3);
        var earlyLowPid = SampleFactory.Create(pid: 40, parentPid: 1, createdAfterSeconds: 3);

        // Act
        var roots = ProcessTreeBuilder.Build([late, earlyHighPid, parent, earlyLowPid]);

        // Assert
        roots[0].Children.Select(static n => n.Process.Pid).Should().Equal(40, 50, 2);
    }

    [Fact]
    public void Build_DuplicatePid_Throws()
    {
        // Arrange
        var first = SampleFactory.Create(pid: 7, parentPid: 0, createdAfterSeconds: 0);
        var second = SampleFactory.Create(pid: 7, parentPid: 0, createdAfterSeconds: 1);

        // Act
        var act = () => ProcessTreeBuilder.Build([first, second]);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetSubtreePostOrder_NestedProcesses_ReturnsChildrenBeforeParents()
    {
        // Arrange
        var root = SampleFactory.Create(pid: 2, parentPid: 1, createdAfterSeconds: 1);
        var processes = new[]
        {
            SampleFactory.Create(pid: 1, parentPid: 0, createdAfterSeconds: 0),
            root,
            SampleFactory.Create(pid: 3, parentPid: 2, createdAfterSeconds: 2),
            SampleFactory.Create(pid: 4, parentPid: 3, createdAfterSeconds: 3),
            SampleFactory.Create(pid: 5, parentPid: 2, createdAfterSeconds: 4),
            SampleFactory.Create(pid: 6, parentPid: 1, createdAfterSeconds: 5),
        };

        // Act
        var subtree = ProcessTreeBuilder.GetSubtreePostOrder(processes, root.Key);

        // Assert
        subtree.Select(static p => p.Pid).Should().Equal(4, 3, 5, 2);
    }

    [Fact]
    public void GetSubtreePostOrder_UnknownRoot_ReturnsEmpty()
    {
        // Arrange
        var processes = new[] { SampleFactory.Create(pid: 1, parentPid: 0, createdAfterSeconds: 0) };
        var unknown = SampleFactory.Create(pid: 1, parentPid: 0, createdAfterSeconds: 99).Key;

        // Act
        var subtree = ProcessTreeBuilder.GetSubtreePostOrder(processes, unknown);

        // Assert
        subtree.Should().BeEmpty();
    }

    [Fact]
    public void Flatten_AllExpanded_ReturnsDepthFirstOrderWithDepths()
    {
        // Arrange
        var roots = ProcessTreeBuilder.Build(
        [
            SampleFactory.Create(pid: 1, parentPid: 0, createdAfterSeconds: 0),
            SampleFactory.Create(pid: 2, parentPid: 1, createdAfterSeconds: 1),
            SampleFactory.Create(pid: 3, parentPid: 2, createdAfterSeconds: 2),
            SampleFactory.Create(pid: 4, parentPid: 1, createdAfterSeconds: 3),
            SampleFactory.Create(pid: 5, parentPid: 0, createdAfterSeconds: 4),
        ]);

        // Act
        var flat = ProcessTreeBuilder.Flatten(roots, static _ => true);

        // Assert
        flat.Select(static n => (n.Process.Pid, n.Depth, n.HasChildren)).Should().Equal(
            (1, 0, true),
            (2, 1, true),
            (3, 2, false),
            (4, 1, false),
            (5, 0, false));
    }

    [Fact]
    public void Flatten_NodeCollapsed_HidesDescendantsButKeepsHasChildren()
    {
        // Arrange
        var collapsed = SampleFactory.Create(pid: 2, parentPid: 1, createdAfterSeconds: 1);
        var roots = ProcessTreeBuilder.Build(
        [
            SampleFactory.Create(pid: 1, parentPid: 0, createdAfterSeconds: 0),
            collapsed,
            SampleFactory.Create(pid: 3, parentPid: 2, createdAfterSeconds: 2),
        ]);

        // Act
        var flat = ProcessTreeBuilder.Flatten(roots, key => key != collapsed.Key);

        // Assert
        flat.Select(static n => n.Process.Pid).Should().Equal(1, 2);
        flat[1].HasChildren.Should().BeTrue();
    }
}
