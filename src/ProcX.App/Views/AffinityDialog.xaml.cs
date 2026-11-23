using System.Windows;
using ProcX.App.ViewModels;
using Wpf.Ui.Controls;

namespace ProcX.App.Views;

/// <summary>
/// Dialog for choosing the processors a process may run on.
/// </summary>
public partial class AffinityDialog : FluentWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AffinityDialog"/> class.
    /// </summary>
    /// <param name="viewModel">The dialog view model.</param>
    public AffinityDialog(AffinityDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>
    /// Closes the dialog with a positive result.
    /// </summary>
    /// <param name="sender">The OK button.</param>
    /// <param name="e">The event arguments.</param>
    private void OnOkClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
