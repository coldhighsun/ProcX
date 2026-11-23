using System.Windows.Controls;

namespace ProcX.App.Views;

/// <summary>
/// The details panel: general information, threads, modules and handles of the selected process.
/// </summary>
public partial class ProcessDetailsView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessDetailsView"/> class.
    /// </summary>
    public ProcessDetailsView()
    {
        InitializeComponent();
    }
}
