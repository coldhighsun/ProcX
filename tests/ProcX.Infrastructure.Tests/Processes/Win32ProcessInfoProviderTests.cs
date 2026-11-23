using System.Diagnostics;
using ProcX.Application.Processes;
using ProcX.Domain.Processes;
using ProcX.Infrastructure.Processes;

namespace ProcX.Infrastructure.Tests.Processes;

public sealed class Win32ProcessInfoProviderTests : IDisposable
{
    private readonly Win32ProcessInfoProvider _provider = new();
    private readonly NtProcessSnapshotProvider _snapshots = new();

    public void Dispose() => _snapshots.Dispose();

    [Fact]
    public void GetStaticInfo_CurrentProcess_ReturnsPathAndUser()
    {
        // Arrange
        var key = KeyOf(Environment.ProcessId);

        // Act
        var info = _provider.GetStaticInfo(key);

        // Assert
        info.IsAccessDenied.Should().BeFalse();
        info.ImagePath.Should().BeEquivalentTo(Environment.ProcessPath);
        info.UserName.Should().BeEquivalentTo(Environment.UserName);
    }

    [Fact]
    public async Task GetStaticInfo_WindowsExecutable_ReturnsFileDescription()
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
            var info = _provider.GetStaticInfo(KeyOf(child.Id));

            // Assert
            info.ImagePath.Should().EndWithEquivalentOf(@"\cmd.exe");
            info.Description.Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void GetStaticInfo_CreateTimeMismatch_ReturnsUnavailable()
    {
        // Arrange: same PID, different instance (simulates PID reuse).
        var key = KeyOf(Environment.ProcessId) with { CreateTime = DateTimeOffset.UnixEpoch };

        // Act
        var info = _provider.GetStaticInfo(key);

        // Assert
        info.Should().Be(ProcessStaticInfo.Unavailable);
    }

    [Fact]
    public void GetStaticInfo_NonexistentProcess_ReturnsUnavailable()
    {
        // Arrange: PIDs are multiples of 4, so an odd PID never exists.
        var key = new ProcessKey(Pid: 3, DateTimeOffset.UnixEpoch);

        // Act
        var info = _provider.GetStaticInfo(key);

        // Assert
        info.Should().Be(ProcessStaticInfo.Unavailable);
    }

    [Fact]
    public void GetStaticInfo_EverySnapshotProcess_DoesNotThrow()
    {
        // Arrange
        var keys = _snapshots.Capture().Processes.Select(static p => p.Key).ToList();

        // Act
        var act = () => keys.ForEach(key => _provider.GetStaticInfo(key));

        // Assert
        act.Should().NotThrow();
    }

    private ProcessKey KeyOf(int pid) => _snapshots.Capture().Processes.Single(p => p.Pid == pid).Key;
}
