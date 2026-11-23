using ProcX.Domain.Processes;

namespace ProcX.Domain.Tests.Processes;

public class CpuUsageCalculatorTests
{
    [Fact]
    public void Calculate_HalfOfOneCoreOnFourCores_ReturnsTwelvePointFivePercent()
    {
        // Arrange
        var previous = TimeSpan.FromSeconds(10);
        var current = TimeSpan.FromSeconds(10.5);

        // Act
        var result = CpuUsageCalculator.Calculate(previous, current, TimeSpan.FromSeconds(1), processorCount: 4);

        // Assert
        result.Should().BeApproximately(12.5, 1e-9);
    }

    [Fact]
    public void Calculate_AllCoresFullyBusy_ReturnsOneHundredPercent()
    {
        // Arrange
        var elapsed = TimeSpan.FromSeconds(2);

        // Act
        var result = CpuUsageCalculator.Calculate(TimeSpan.Zero, TimeSpan.FromSeconds(16), elapsed, processorCount: 8);

        // Assert
        result.Should().BeApproximately(100, 1e-9);
    }

    [Fact]
    public void Calculate_DeltaExceedsCapacity_ClampsToOneHundred()
    {
        // Act
        var result = CpuUsageCalculator.Calculate(TimeSpan.Zero, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1), processorCount: 2);

        // Assert
        result.Should().Be(100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Calculate_NonPositiveElapsed_ReturnsZero(int elapsedSeconds)
    {
        // Act
        var result = CpuUsageCalculator.Calculate(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(elapsedSeconds),
            processorCount: 4);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void Calculate_CpuTimeDecreased_ReturnsZero()
    {
        // Act
        var result = CpuUsageCalculator.Calculate(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(1), processorCount: 4);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void Calculate_ZeroProcessors_Throws()
    {
        // Act
        var act = () => CpuUsageCalculator.Calculate(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromSeconds(1), processorCount: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
