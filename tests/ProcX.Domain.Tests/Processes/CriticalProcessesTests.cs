using ProcX.Domain.Processes;

namespace ProcX.Domain.Tests.Processes;

public class CriticalProcessesTests
{
    [Theory]
    [InlineData(0, "System Idle Process")]
    [InlineData(4, "System")]
    [InlineData(600, "csrss.exe")]
    [InlineData(700, "WININIT.EXE")]
    [InlineData(800, "lsass.exe")]
    public void IsCritical_SystemProcess_ReturnsTrue(int pid, string name)
    {
        // Arrange
        var process = SampleFactory.Create(pid, parentPid: 4, createdAfterSeconds: 0) with { Name = name };

        // Act
        var result = CriticalProcesses.IsCritical(process);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("explorer.exe")]
    [InlineData("svchost.exe")]
    [InlineData("csrss.exe.bak")]
    public void IsCritical_RegularProcess_ReturnsFalse(string name)
    {
        // Arrange
        var process = SampleFactory.Create(pid: 1000, parentPid: 4, createdAfterSeconds: 0) with { Name = name };

        // Act
        var result = CriticalProcesses.IsCritical(process);

        // Assert
        result.Should().BeFalse();
    }
}
