using System.Globalization;
using System.Windows.Data;

namespace ProcX.App.Converters;

/// <summary>
/// Negates a boolean.
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class InverseBooleanConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
