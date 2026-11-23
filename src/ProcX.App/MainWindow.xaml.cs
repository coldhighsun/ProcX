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
    /// <param name="processList">The process list view model.</param>
    public MainWindow(ProcessListViewModel processList)
    {
        DataContext = processList;
        InitializeComponent();
    }
}
