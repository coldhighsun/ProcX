using ProcX.App.ViewModels;
using Wpf.Ui.Controls;

namespace ProcX.App;

/// <summary>
/// The main ProcX window hosting the process list.
/// </summary>
public partial class MainWindow : FluentWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The main window view model.</param>
    public MainWindow(MainWindowViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
