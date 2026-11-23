using ProcX.App.ViewModels;
using ProcX.Domain.Processes;

namespace ProcX.App.Tests.ViewModels;

public class AffinityDialogViewModelTests
{
    [Fact]
    public void Constructor_CurrentSubset_TicksOnlyCurrentProcessors()
    {
        // Act
        var viewModel = new AffinityDialogViewModel("app.exe", AffinityMask.FromProcessors([1, 3]), AffinityMask.All(4));

        // Assert
        viewModel.Processors.Select(static p => p.IsSelected).Should().Equal(false, true, false, true);
        viewModel.Mask.Should().Be(AffinityMask.FromProcessors([1, 3]));
        viewModel.Title.Should().Contain("app.exe");
    }

    [Fact]
    public void Constructor_SparseAvailableProcessors_OffersOnlyThose()
    {
        // Act
        var viewModel = new AffinityDialogViewModel("app.exe", AffinityMask.FromProcessors([2]), AffinityMask.FromProcessors([2, 5]));

        // Assert
        viewModel.Processors.Select(static p => p.Index).Should().Equal(2, 5);
    }

    [Fact]
    public void SelectNone_Executed_MakesDialogInvalid()
    {
        // Arrange
        var viewModel = new AffinityDialogViewModel("app.exe", AffinityMask.All(4), AffinityMask.All(4));

        // Act
        viewModel.SelectNoneCommand.Execute(null);

        // Assert
        viewModel.IsValid.Should().BeFalse();
        viewModel.Mask.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ProcessorToggled_Changed_RaisesMaskAndValidityNotifications()
    {
        // Arrange
        var viewModel = new AffinityDialogViewModel("app.exe", AffinityMask.FromProcessors([0]), AffinityMask.All(2));
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        viewModel.Processors[1].IsSelected = true;

        // Assert
        changed.Should().Contain([nameof(AffinityDialogViewModel.Mask), nameof(AffinityDialogViewModel.IsValid)]);
        viewModel.Mask.Should().Be(AffinityMask.All(2));
    }

    [Fact]
    public void SelectAll_AfterNone_TicksEveryProcessor()
    {
        // Arrange
        var viewModel = new AffinityDialogViewModel("app.exe", AffinityMask.All(3), AffinityMask.All(3));
        viewModel.SelectNoneCommand.Execute(null);

        // Act
        viewModel.SelectAllCommand.Execute(null);

        // Assert
        viewModel.Mask.Should().Be(AffinityMask.All(3));
    }
}
