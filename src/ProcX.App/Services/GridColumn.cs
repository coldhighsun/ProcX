using System.Windows;
using ProcX.Application.Settings;

namespace ProcX.App.Services;

/// <summary>
/// Attached property that tags a grid column with the optional process column it shows.
/// </summary>
public static class GridColumn
{
    /// <summary>
    /// Identifies the Id attached property.
    /// </summary>
    public static readonly DependencyProperty IdProperty = DependencyProperty.RegisterAttached(
        "Id",
        typeof(ProcessColumns),
        typeof(GridColumn),
        new PropertyMetadata(ProcessColumns.None));

    /// <summary>
    /// Gets the process column a grid column shows.
    /// </summary>
    /// <param name="element">The grid column.</param>
    /// <returns>The process column, or <see cref="ProcessColumns.None"/> for the always-visible Name column.</returns>
    public static ProcessColumns GetId(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (ProcessColumns)element.GetValue(IdProperty);
    }

    /// <summary>
    /// Sets the process column a grid column shows.
    /// </summary>
    /// <param name="element">The grid column.</param>
    /// <param name="value">The process column.</param>
    public static void SetId(DependencyObject element, ProcessColumns value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IdProperty, value);
    }
}
