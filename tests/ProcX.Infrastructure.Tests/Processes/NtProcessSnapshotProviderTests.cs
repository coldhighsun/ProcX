using System.Diagnostics;
using ProcX.Infrastructure.Processes;

namespace ProcX.Infrastructure.Tests.Processes;

public sealed class NtProcessSnapshotProviderTests : IDisposable
{
    private readonly NtProcessSnapshotProvider _provider = new();

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void Capture_CurrentProcess_MatchesSystemDiagnosticsProcess()
    {
        // Arrange
        using var current = Process.GetCurrentProcess();

        // Act
        var snapshot = _provider.Capture();

        // Assert
        var sample = snapshot.Processes.Should().ContainSingle(p => p.Pid == current.Id).Subject;
        sample.Name.Should().BeEquivalentTo($"{current.ProcessName}.exe");
        sample.CreateTime.Should().BeCloseTo(current.StartTime.ToUniversalTime(), TimeSpan.FromSeconds(1));
        sample.ThreadCount.Should().BePositive();
        sample.HandleCount.Should().BePositive();
        sample.WorkingSetBytes.Should().BePositive();
        sample.PrivateWorkingSetBytes.Should().BePositive().And.BeLessThanOrEqualTo(sample.WorkingSetBytes);
        sample.PrivateBytes.Should().BePositive();
        sample.SessionId.Should().Be(current.SessionId);
        sample.TotalCpuTime.Should().BePositive();
    }

    [Fact]
    public void Capture_Always_ContainsIdleAndSystemProcesses()
    {
        // Act
        var snapshot = _provider.Capture();

        // Assert
        snapshot.Processes.Should().Contain(p => p.Pid == 0 && p.Name == "System Idle Process");
        snapshot.Processes.Should().Contain(p => p.Pid == 4 && p.Name == "System");
    }

    [Fact]
    public void Capture_Always_ReturnsUniquePids()
    {
        // Act
        var snapshot = _provider.Capture();

        // Assert
        snapshot.Processes.Should().HaveCountGreaterThan(10);
        snapshot.Processes.Select(static p => p.Pid).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Capture_ChildProcessStarted_ReportsItWithCurrentProcessAsParent()
    {
        // Arrange
        using var child = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 30 127.0.0.1 >nul")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;

        try
        {
            // Act
            var snapshot = _provider.Capture();

            // Assert
            var sample = snapshot.Processes.Should().ContainSingle(p => p.Pid == child.Id).Subject;
            sample.Name.Should().BeEquivalentTo("cmd.exe");
            sample.ParentPid.Should().Be(Environment.ProcessId);
        }
        finally
        {
            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void Capture_CalledRepeatedly_ReusesBufferAndKeepsCountersMonotonic()
    {
        // Arrange
        var first = _provider.Capture().Processes.Single(static p => p.Pid == Environment.ProcessId);

        // Act
        var second = _provider.Capture().Processes.Single(static p => p.Pid == Environment.ProcessId);

        // Assert
        second.Key.Should().Be(first.Key);
        second.TotalCpuTime.Should().BeGreaterThanOrEqualTo(first.TotalCpuTime);
    }

    [Fact]
    public void Capture_AfterDispose_Throws()
    {
        // Arrange
        _provider.Dispose();

        // Act
        var act = _provider.Capture;

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }
}
