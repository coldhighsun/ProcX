using ProcX.App.ViewModels;
using ProcX.Application.Processes;

namespace ProcX.App.Tests.ViewModels;

public class ProcessRowViewModelTests
{
    [Fact]
    public void Update_SameProcess_RaisesPropertyChangedOnlyForChangedValues()
    {
        // Arrange
        var row = new ProcessRowViewModel(Rows.Metrics(1, cpu: 5, memory: 100));
        var changed = new List<string?>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        row.Update(Rows.Metrics(1, cpu: 7, memory: 100));

        // Assert
        changed.Should().Equal(nameof(ProcessRowViewModel.CpuPercent));
    }

    [Fact]
    public void Update_DifferentProcess_Throws()
    {
        // Arrange
        var row = new ProcessRowViewModel(Rows.Metrics(1));

        // Act
        var act = () => row.Update(Rows.Metrics(2));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyStaticInfo_Info_SetsDetailProperties()
    {
        // Arrange
        var row = new ProcessRowViewModel(Rows.Metrics(1));

        // Act
        row.ApplyStaticInfo(new ProcessStaticInfo(@"C:\a.exe", "alice", "App A", IsAccessDenied: false), icon: null);

        // Assert
        row.ImagePath.Should().Be(@"C:\a.exe");
        row.UserName.Should().Be("alice");
        row.Description.Should().Be("App A");
        row.IsAccessDenied.Should().BeFalse();
    }
}
