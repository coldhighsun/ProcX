using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProcX.App.Converters;

/// <summary>
/// Converts a tree depth into a left margin.
/// </summary>
[ValueConversion(typeof(int), typeof(Thickness))]
public sealed class DepthToIndentConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets the indentation per level in device-independent pixels.
    /// </summary>
    public double IndentPerLevel { get; set; } = 16;

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int depth and > 0 ? new Thickness(depth * IndentPerLevel, 0, 0, 0) : default(Thickness);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the conversion is one-way.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
