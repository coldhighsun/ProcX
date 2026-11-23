using System.ComponentModel;
using System.Windows;
using ProcX.App.ViewModels;
using Wpf.Ui.Controls;

namespace ProcX.App;

/// <summary>
/// The main ProcX window hosting the process list and the details panel.
/// </summary>
public partial class MainWindow : FluentWindow
{
    /// <summary>
    /// The details row height to restore when the panel is shown again (the splitter may have changed it).
    /// </summary>
    private GridLength _detailsHeight = new(280);

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The main window view model.</param>
    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
        InitializeComponent();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>
    /// Collapses or restores the details row. Done in code because the splitter replaces any binding on the
    /// row height with its own value.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">The event arguments.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.IsDetailsVisible) || sender is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsDetailsVisible)
        {
            DetailsRow.Height = _detailsHeight;
        }
        else
        {
            _detailsHeight = DetailsRow.Height;
            DetailsRow.Height = new GridLength(0);
        }
    }
}
