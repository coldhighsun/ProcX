using ProcX.Application.Processes;

namespace ProcX.Application.Tests.Processes;

public class ProcessDeltaCalculatorTests
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [Fact]
    public void Compute_FirstSnapshot_ReportsAllAsAddedWithZeroCpu()
    {
        // Arrange
        var calculator = new ProcessDeltaCalculator(processorCount: 4);

        // Act
        var delta = calculator.Compute(Samples.Snapshot(Samples.Process(1, cpuSeconds: 5), Samples.Process(2)), OneSecond);

        // Assert
        delta.Added.Select(static m => m.Sample.Pid).Should().BeEquivalentTo([1, 2]);
        delta.Added.Should().AllSatisfy(static m => m.CpuPercent.Should().Be(0));
        delta.Updated.Should().BeEmpty();
        delta.Removed.Should().BeEmpty();
    }

    [Fact]
    public void Compute_SecondSnapshot_ComputesCpuForUpdatedProcesses()
    {
        // Arrange
        var calculator = new ProcessDeltaCalculator(processorCount: 2);
        calculator.Compute(Samples.Snapshot(Samples.Process(1, cpuSeconds: 1)), OneSecond);

        // Act
        var delta = calculator.Compute(Samples.Snapshot(Samples.Process(1, cpuSeconds: 1.5)), OneSecond);

        // Assert
        delta.Added.Should().BeEmpty();
        delta.Updated.Should().ContainSingle().Which.CpuPercent.Should().BeApproximately(25, 1e-9);
    }

    [Fact]
    public void Compute_ProcessExited_ReportsRemoved()
    {
        // Arrange
        var calculator = new ProcessDeltaCalculator(processorCount: 1);
        var exiting = Samples.Process(2);
        calculator.Compute(Samples.Snapshot(Samples.Process(1), exiting), OneSecond);

        // Act
        var delta = calculator.Compute(Samples.Snapshot(Samples.Process(1)), OneSecond);

        // Assert
        delta.Removed.Should().Equal(exiting.Key);
    }

    [Fact]
    public void Compute_PidReused_ReportsRemovalAndAddition()
    {
        // Arrange
        var calculator = new ProcessDeltaCalculator(processorCount: 1);
        var original = Samples.Process(7, createdAfterSeconds: 0);
        var reused = Samples.Process(7, createdAfterSeconds: 30);
        calculator.Compute(Samples.Snapshot(original), OneSecond);

        // Act
        var delta = calculator.Compute(Samples.Snapshot(reused), OneSecond);

        // Assert
        delta.Removed.Should().Equal(original.Key);
        delta.Added.Should().ContainSingle().Which.Key.Should().Be(reused.Key);
        delta.Updated.Should().BeEmpty();
    }

    [Fact]
    public void Compute_IdleProcessPresent_ReportsTotalCpuAsComplementOfIdle()
    {
        // Arrange: 4 processors, idle consumed 3 of 4 available CPU-seconds.
        var calculator = new ProcessDeltaCalculator(processorCount: 4);
        calculator.Compute(Samples.Snapshot(Samples.Process(0, cpuSeconds: 100)), OneSecond);

        // Act
        var delta = calculator.Compute(Samples.Snapshot(Samples.Process(0, cpuSeconds: 103)), OneSecond);

        // Assert
        delta.TotalCpuPercent.Should().BeApproximately(25, 1e-9);
    }

    [Fact]
    public void Compute_NoIdleProcess_ReportsZeroTotalCpu()
    {
        // Arrange
        var calculator = new ProcessDeltaCalculator(processorCount: 1);
        calculator.Compute(Samples.Snapshot(Samples.Process(1)), OneSecond);

        // Act
        var delta = calculator.Compute(Samples.Snapshot(Samples.Process(1, cpuSeconds: 1)), OneSecond);

        // Assert
        delta.TotalCpuPercent.Should().Be(0);
    }

    [Fact]
    public void Reset_AfterSnapshot_ReportsAllAsAddedAgain()
    {
        // Arrange
        var calculator = new ProcessDeltaCalculator(processorCount: 1);
        calculator.Compute(Samples.Snapshot(Samples.Process(1)), OneSecond);

        // Act
        calculator.Reset();
        var delta = calculator.Compute(Samples.Snapshot(Samples.Process(1)), OneSecond);

        // Assert
        delta.Added.Should().ContainSingle();
        delta.Removed.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ZeroProcessors_Throws()
    {
        // Act
        var act = () => new ProcessDeltaCalculator(processorCount: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
