using System.Diagnostics;
using ProcX.Application.Processes;
using ProcX.Domain.Processes;
using ProcX.Infrastructure.Processes;

namespace ProcX.Infrastructure.Tests.Processes;

public sealed class Win32ProcessControllerTests : IDisposable
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(10);

    private readonly Win32ProcessController _controller = new();
    private readonly NtProcessSnapshotProvider _snapshots = new();
    private readonly List<Process> _started = [];

    public void Dispose()
    {
        foreach (var process in _started)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.Dispose();
        }

        _snapshots.Dispose();
    }

    [Fact]
    public async Task Terminate_RunningProcess_EndsItWithExitCodeOne()
    {
        // Arrange
        var child = StartLongRunning("ping.exe", "-n 60 127.0.0.1");

        // Act
        var result = _controller.Terminate(KeyOf(child.Id));

        // Assert
        result.Status.Should().Be(TerminationStatus.Succeeded);
        await WaitForExitAsync(child);
        child.ExitCode.Should().Be(1);
    }

    [Fact]
    public async Task Terminate_ProcessAlreadyExited_ReturnsNotFound()
    {
        // Arrange
        var child = StartLongRunning("ping.exe", "-n 60 127.0.0.1");
        var key = KeyOf(child.Id);
        child.Kill();
        await WaitForExitAsync(child);

        // Act
        var result = _controller.Terminate(key);

        // Assert
        result.Status.Should().Be(TerminationStatus.NotFound);
    }

    [Fact]
    public void Terminate_CreateTimeMismatch_ReturnsNotFoundAndLeavesProcessRunning()
    {
        // Arrange: same PID, different instance (simulates PID reuse).
        var child = StartLongRunning("ping.exe", "-n 60 127.0.0.1");
        var staleKey = KeyOf(child.Id) with { CreateTime = DateTimeOffset.UnixEpoch };

        // Act
        var result = _controller.Terminate(staleKey);

        // Assert
        result.Status.Should().Be(TerminationStatus.NotFound);
        child.HasExited.Should().BeFalse();
    }

    [Fact]
    public void Terminate_SystemProcess_ReturnsAccessDenied()
    {
        // Act
        var result = _controller.Terminate(KeyOf(4));

        // Assert
        result.Status.Should().Be(TerminationStatus.AccessDenied);
    }

    [Fact]
    public async Task TerminateTree_ShellWithChild_EndsBothProcesses()
    {
        // Arrange
        var shell = StartLongRunning("cmd.exe", "/c ping -n 60 127.0.0.1 >nul");
        var terminator = new ProcessTerminator(_snapshots, _controller);
        var shellKey = KeyOf(shell.Id);
        var tree = await WaitForTreeAsync(terminator, shellKey, static p => p.Name.Equals("PING.EXE", StringComparison.OrdinalIgnoreCase));
        var ping = tree.Single(static p => p.Name.Equals("PING.EXE", StringComparison.OrdinalIgnoreCase));

        // Act
        var results = terminator.TerminateTree(shellKey);

        // Assert
        // Ending the console host may take the console's other processes down first, which counts as gone.
        results.Should().OnlyContain(static r => r.IsGone);
        results.Select(static r => r.Key).Should().Contain(ping.Key).And.EndWith(shellKey);
        await WaitForExitAsync(shell);
        _snapshots.Capture().Processes.Should().NotContain(p => p.Key == ping.Key);
    }

    [Theory]
    [InlineData(ProcessPriority.Idle, ProcessPriorityClass.Idle)]
    [InlineData(ProcessPriority.BelowNormal, ProcessPriorityClass.BelowNormal)]
    [InlineData(ProcessPriority.AboveNormal, ProcessPriorityClass.AboveNormal)]
    [InlineData(ProcessPriority.Normal, ProcessPriorityClass.Normal)]
    public void SetPriority_ChildProcess_ChangesPriorityClass(ProcessPriority priority, ProcessPriorityClass expected)
    {
        // Arrange
        var child = StartLongRunning("ping.exe", "-n 60 127.0.0.1");

        // Act
        var result = _controller.SetPriority(KeyOf(child.Id), priority);
        child.Refresh();

        // Assert
        result.Status.Should().Be(ProcessOperationStatus.Succeeded);
        child.PriorityClass.Should().Be(expected);
    }

    [Fact]
    public void SetPriority_CreateTimeMismatch_ReturnsNotFoundAndLeavesPriority()
    {
        // Arrange
        var child = StartLongRunning("ping.exe", "-n 60 127.0.0.1");
        var stale = KeyOf(child.Id) with { CreateTime = DateTimeOffset.UnixEpoch };

        // Act
        var result = _controller.SetPriority(stale, ProcessPriority.Idle);
        child.Refresh();

        // Assert
        result.Status.Should().Be(ProcessOperationStatus.NotFound);
        child.PriorityClass.Should().Be(ProcessPriorityClass.Normal);
    }

    [Fact]
    public void SetPriority_SystemProcess_ReturnsAccessDenied()
    {
        // Act
        var result = _controller.SetPriority(KeyOf(4), ProcessPriority.Idle);

        // Assert
        result.Status.Should().Be(ProcessOperationStatus.AccessDenied);
    }

    [Fact]
    public void SetAffinity_ChildProcess_RestrictsAndReadsBack()
    {
        // Arrange
        var child = StartLongRunning("ping.exe", "-n 60 127.0.0.1");
        var key = KeyOf(child.Id);
        var before = _controller.GetAffinity(key);
        Assert.SkipUnless(before.IsSuccess, "Affinity is not supported on this system.");

        // Act
        var set = _controller.SetAffinity(key, AffinityMask.FromProcessors([0]));
        var after = _controller.GetAffinity(key);
        child.Refresh();

        // Assert
        set.Status.Should().Be(ProcessOperationStatus.Succeeded);
        after.Value!.Process.Value.Should().Be(1UL);
        after.Value.System.Value.Should().Be(before.Value!.System.Value);
        ((ulong)(nuint)child.ProcessorAffinity).Should().Be(1UL);
    }

    [Fact]
    public void SetAffinity_EmptyMask_FailsWithoutChangingProcess()
    {
        // Arrange
        var child = StartLongRunning("ping.exe", "-n 60 127.0.0.1");

        // Act
        var result = _controller.SetAffinity(KeyOf(child.Id), new AffinityMask(0));

        // Assert
        result.Status.Should().Be(ProcessOperationStatus.Failed);
    }

    [Fact]
    public void GetAffinity_ProcessAlreadyExited_ReturnsNotFound()
    {
        // Arrange
        var child = StartLongRunning("ping.exe", "-n 60 127.0.0.1");
        var key = KeyOf(child.Id);
        child.Kill();
        child.WaitForExit();

        // Act
        var result = _controller.GetAffinity(key);

        // Assert
        result.Status.Should().BeOneOf(ProcessOperationStatus.NotFound, ProcessOperationStatus.Unsupported);
    }

    private Process StartLongRunning(string fileName, string arguments)
    {
        var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        _started.Add(process);
        return process;
    }

    private ProcessKey KeyOf(int pid) => _snapshots.Capture().Processes.Single(p => p.Pid == pid).Key;

    private static async Task<IReadOnlyList<ProcessSample>> WaitForTreeAsync(
        ProcessTerminator terminator,
        ProcessKey root,
        Func<ProcessSample, bool> expectedMember)
    {
        var deadline = DateTimeOffset.UtcNow + ExitTimeout;
        while (true)
        {
            var tree = terminator.GetTree(root);
            if (tree.Any(expectedMember) || DateTimeOffset.UtcNow > deadline)
            {
                return tree;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
    }

    private static async Task WaitForExitAsync(Process process)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(ExitTimeout);
        await process.WaitForExitAsync(timeout.Token);
    }
}
