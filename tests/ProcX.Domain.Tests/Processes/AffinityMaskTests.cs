using ProcX.Domain.Processes;

namespace ProcX.Domain.Tests.Processes;

public class AffinityMaskTests
{
    [Theory]
    [InlineData(1, 0x1UL)]
    [InlineData(4, 0xFUL)]
    [InlineData(64, ulong.MaxValue)]
    public void All_ValidCount_SetsLowestBits(int processorCount, ulong expected)
    {
        // Act
        var mask = AffinityMask.All(processorCount);

        // Assert
        mask.Value.Should().Be(expected);
        mask.Count.Should().Be(processorCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void All_CountOutOfRange_Throws(int processorCount)
    {
        // Act
        var act = () => AffinityMask.All(processorCount);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FromProcessors_Indices_RoundTripsThroughGetProcessors()
    {
        // Act
        var mask = AffinityMask.FromProcessors([5, 0, 63, 5]);

        // Assert
        mask.GetProcessors().Should().Equal(0, 5, 63);
        mask.Contains(5).Should().BeTrue();
        mask.Contains(1).Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(64)]
    public void FromProcessors_IndexOutOfRange_Throws(int processor)
    {
        // Act
        var act = () => AffinityMask.FromProcessors([processor]);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IsEmpty_NoProcessors_ReturnsTrue()
    {
        // Act
        var mask = AffinityMask.FromProcessors([]);

        // Assert
        mask.IsEmpty.Should().BeTrue();
    }
}
