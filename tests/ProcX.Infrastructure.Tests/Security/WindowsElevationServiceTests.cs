using ProcX.Infrastructure.Security;

namespace ProcX.Infrastructure.Tests.Security;

public class WindowsElevationServiceTests
{
    [Fact]
    public void IsElevated_Always_MatchesRuntimePrivilegeCheck()
    {
        // Act
        var result = new WindowsElevationService().IsElevated;

        // Assert
        result.Should().Be(Environment.IsPrivilegedProcess);
    }
}
