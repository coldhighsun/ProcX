using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ProcX.App.Services;
using ProcX.App.ViewModels;
using ProcX.Application.Settings;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

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
    /// The menu used to choose the visible columns.
    /// </summary>
    private readonly ContextMenu _columnMenu;

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
        viewModel.Processes.PropertyChanged += OnProcessesPropertyChanged;
        _columnMenu = CreateColumnMenu();
        ProcessGrid.ContextMenuOpening += OnGridContextMenuOpening;
        ProcessGrid.PreviewMouseRightButtonUp += OnGridPreviewMouseRightButtonUp;
        ApplyColumnVisibility(viewModel.Processes.HiddenColumns);
    }

    /// <summary>
    /// Gets the process list view model.
    /// </summary>
    private ProcessListViewModel Processes => ((MainWindowViewModel)DataContext).Processes;

    /// <summary>
    /// Shows the column chooser instead of the row menu when the user right-clicks a column header.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The event arguments.</param>
    private void OnGridContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (FindHeader(e.OriginalSource as DependencyObject) is { } header)
        {
            e.Handled = true;
            _columnMenu.PlacementTarget = header;
            _columnMenu.IsOpen = true;
        }
    }

    /// <summary>
    /// Opens the column chooser on a right-click of a column header, before the grid's row menu can open.
    /// </summary>
    /// <param name="sender">The grid.</param>
    /// <param name="e">The event arguments.</param>
    private void OnGridPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindHeader(e.OriginalSource as DependencyObject) is { } header)
        {
            e.Handled = true;
            _columnMenu.PlacementTarget = header;
            _columnMenu.IsOpen = true;
        }
    }

    /// <summary>
    /// Finds the column header an element is part of.
    /// </summary>
    /// <param name="node">The element.</param>
    /// <returns>The header, or <see langword="null"/> when the element is not inside one.</returns>
    private static DataGridColumnHeader? FindHeader(DependencyObject? node)
    {
        while (node is not null and not System.Windows.Controls.DataGrid)
        {
            if (node is DataGridColumnHeader header)
            {
                return header;
            }

            node = GetParent(node);
        }

        return null;
    }

    /// <summary>
    /// Gets the visual or logical parent of an element, so text runs inside headers are covered too.
    /// </summary>
    /// <param name="node">The element.</param>
    /// <returns>The parent, or <see langword="null"/> at the root.</returns>
    private static DependencyObject? GetParent(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);

    /// <summary>
    /// Creates the menu listing every optional column with a check mark.
    /// </summary>
    /// <returns>The menu.</returns>
    private ContextMenu CreateColumnMenu()
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) =>
        {
            foreach (var item in menu.Items.OfType<MenuItem>().Where(i => i.Tag is ProcessColumns))
            {
                item.IsChecked = !Processes.HiddenColumns.HasFlag((ProcessColumns)item.Tag);
            }
        };

        menu.Items.Add(new MenuItem { Header = "Name", IsCheckable = true, IsChecked = true, IsEnabled = false });
        foreach (var column in ProcessGrid.Columns.Select(GridColumn.GetId).Where(id => id != ProcessColumns.None))
        {
            var header = ProcessGrid.Columns.First(c => GridColumn.GetId(c) == column).Header?.ToString();
            var item = new MenuItem { Header = header, Tag = column, IsCheckable = true, StaysOpenOnClick = true };
            item.Click += (_, _) => Processes.HiddenColumns = item.IsChecked
                ? Processes.HiddenColumns & ~column
                : Processes.HiddenColumns | column;
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var reset = new MenuItem { Header = "Show all columns" };
        reset.Click += (_, _) => Processes.HiddenColumns = ProcessColumns.None;
        menu.Items.Add(reset);
        return menu;
    }

    /// <summary>
    /// Shows or hides the grid columns to match the hidden set.
    /// </summary>
    /// <param name="hidden">The hidden columns.</param>
    private void ApplyColumnVisibility(ProcessColumns hidden)
    {
        foreach (var column in ProcessGrid.Columns)
        {
            var id = GridColumn.GetId(column);
            if (id != ProcessColumns.None)
            {
                column.Visibility = hidden.HasFlag(id) ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }

    /// <summary>
    /// Updates the grid when the hidden columns change.
    /// </summary>
    /// <param name="sender">The process list.</param>
    /// <param name="e">The event arguments.</param>
    private void OnProcessesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProcessListViewModel.HiddenColumns))
        {
            ApplyColumnVisibility(Processes.HiddenColumns);
        }
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
