using ProcX.App.ViewModels;
using Wpf.Ui.Controls;

namespace ProcX.App.Views;

/// <summary>
/// The settings window.
/// </summary>
public partial class SettingsWindow : FluentWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The settings view model.</param>
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
        InitializeComponent();
    }
}
