using ProcX.Domain.Processes;

namespace ProcX.Domain.Tests.Processes;

public class ProcessPriorityExtensionsTests
{
    [Theory]
    [InlineData(0, ProcessPriority.Idle)]
    [InlineData(4, ProcessPriority.Idle)]
    [InlineData(6, ProcessPriority.BelowNormal)]
    [InlineData(8, ProcessPriority.Normal)]
    [InlineData(9, ProcessPriority.Normal)]
    [InlineData(10, ProcessPriority.AboveNormal)]
    [InlineData(13, ProcessPriority.High)]
    [InlineData(24, ProcessPriority.Realtime)]
    [InlineData(31, ProcessPriority.Realtime)]
    public void FromBasePriority_Value_ReturnsExpectedClass(int basePriority, ProcessPriority expected)
    {
        // Act
        var result = ProcessPriorityExtensions.FromBasePriority(basePriority);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(ProcessPriority.Idle)]
    [InlineData(ProcessPriority.BelowNormal)]
    [InlineData(ProcessPriority.Normal)]
    [InlineData(ProcessPriority.AboveNormal)]
    [InlineData(ProcessPriority.High)]
    [InlineData(ProcessPriority.Realtime)]
    public void ToBasePriority_DefinedValue_RoundTripsThroughFromBasePriority(ProcessPriority priority)
    {
        // Act
        var result = ProcessPriorityExtensions.FromBasePriority(priority.ToBasePriority());

        // Assert
        result.Should().Be(priority);
    }

    [Fact]
    public void ToBasePriority_UndefinedValue_Throws()
    {
        // Act
        var act = () => ((ProcessPriority)42).ToBasePriority();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
