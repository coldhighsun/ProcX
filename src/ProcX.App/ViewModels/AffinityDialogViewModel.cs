using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProcX.Domain.Processes;

namespace ProcX.App.ViewModels;

/// <summary>
/// One processor in the affinity dialog.
/// </summary>
public sealed partial class ProcessorItemViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessorItemViewModel"/> class.
    /// </summary>
    /// <param name="index">The zero-based processor index.</param>
    /// <param name="isSelected">Whether the processor is initially allowed.</param>
    public ProcessorItemViewModel(int index, bool isSelected)
    {
        Index = index;
        IsSelected = isSelected;
    }

    /// <summary>
    /// Gets the zero-based processor index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the label, e.g. <c>CPU 3</c>.
    /// </summary>
    public string Label => $"CPU {Index}";

    /// <summary>
    /// Gets or sets whether the process may run on this processor.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// The affinity dialog: a set of processors to tick.
/// </summary>
public sealed partial class AffinityDialogViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AffinityDialogViewModel"/> class.
    /// </summary>
    /// <param name="processName">The process name, for the title.</param>
    /// <param name="current">The processors the process currently uses.</param>
    /// <param name="available">The processors available on the system.</param>
    public AffinityDialogViewModel(string processName, AffinityMask current, AffinityMask available)
    {
        Title = $"Processor affinity: {processName}";
        Processors = new ObservableCollection<ProcessorItemViewModel>(
            available.GetProcessors().Select(index => new ProcessorItemViewModel(index, current.Contains(index))));

        foreach (var processor in Processors)
        {
            processor.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Mask));
        }
    }

    /// <summary>
    /// Gets the dialog title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the processors offered.
    /// </summary>
    public ObservableCollection<ProcessorItemViewModel> Processors { get; }

    /// <summary>
    /// Gets the mask of the ticked processors.
    /// </summary>
    public AffinityMask Mask => AffinityMask.FromProcessors(Processors.Where(static p => p.IsSelected).Select(static p => p.Index));

    /// <summary>
    /// Gets whether the selection is valid; a process needs at least one processor.
    /// </summary>
    public bool IsValid => !Mask.IsEmpty;

    /// <summary>
    /// Ticks every processor.
    /// </summary>
    [RelayCommand]
    private void SelectAll() => SetAll(true);

    /// <summary>
    /// Unticks every processor.
    /// </summary>
    [RelayCommand]
    private void SelectNone() => SetAll(false);

    /// <summary>
    /// Sets every processor.
    /// </summary>
    /// <param name="selected">Whether processors are ticked.</param>
    private void SetAll(bool selected)
    {
        foreach (var processor in Processors)
        {
            processor.IsSelected = selected;
        }
    }

    /// <summary>
    /// Raises <see cref="IsValid"/> change notifications along with the mask.
    /// </summary>
    /// <param name="e">The event arguments.</param>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(Mask))
        {
            OnPropertyChanged(nameof(IsValid));
        }
    }
}
