using System.Globalization;
using System.Windows.Data;

namespace ProcX.App.Converters;

/// <summary>
/// Formats a byte count as a human-readable size, e.g. <c>12.3 MB</c>.
/// </summary>
[ValueConversion(typeof(long), typeof(string))]
public sealed class ByteSizeConverter : IValueConverter
{
    /// <summary>
    /// Unit suffixes in ascending order of magnitude (powers of 1024).
    /// </summary>
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    /// Formats a byte count.
    /// </summary>
    /// <param name="bytes">The byte count.</param>
    /// <param name="culture">The culture used for number formatting.</param>
    /// <returns>The formatted size.</returns>
    public static string Format(long bytes, CultureInfo culture)
    {
        if (bytes < 1024)
        {
            return string.Create(culture, $"{Math.Max(bytes, 0)} B");
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(culture, $"{value:N1} {Units[unit]}");
    }

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        long bytes => Format(bytes, culture),
        int bytes => Format(bytes, culture),
        _ => null,
    };

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the conversion is one-way.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
