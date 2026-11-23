using ProcX.Infrastructure.Processes;

namespace ProcX.Infrastructure.Tests.Processes;

public class WindowsSystemInformationTests
{
    [Fact]
    public void LogicalProcessorCount_Always_IsAtLeastProcessorsAvailableToCurrentProcess()
    {
        // Act
        var count = new WindowsSystemInformation().LogicalProcessorCount;

        // Assert
        count.Should().BeGreaterThanOrEqualTo(Environment.ProcessorCount);
    }
}
