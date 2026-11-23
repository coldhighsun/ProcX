using System.Globalization;
using System.Windows.Data;

namespace ProcX.App.Converters;

/// <summary>
/// Formats integers as hexadecimal with a <c>0x</c> prefix, e.g. addresses and access masks.
/// </summary>
/// <remarks>The converter parameter optionally sets the minimum number of digits.</remarks>
public sealed class HexConverter : IValueConverter
{
    /// <summary>
    /// Formats a value as hexadecimal.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="digits">The minimum number of digits.</param>
    /// <returns>The formatted value.</returns>
    public static string Format(ulong value, int digits) =>
        "0x" + value.ToString("X" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var digits = parameter is string text && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var d) ? d : 1;
        return value switch
        {
            ulong v => Format(v, digits),
            uint v => Format(v, digits),
            long v => Format(unchecked((ulong)v), digits),
            int v => Format(unchecked((uint)v), digits),
            _ => null,
        };
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the conversion is one-way.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
