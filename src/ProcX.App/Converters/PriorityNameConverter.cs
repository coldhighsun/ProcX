using System.Globalization;
using System.Windows.Data;
using ProcX.Domain.Processes;

namespace ProcX.App.Converters;

/// <summary>
/// Shows a <see cref="ProcessPriority"/> with the name Windows tools use, e.g. <c>Low</c> instead of <c>Idle</c>.
/// </summary>
[ValueConversion(typeof(ProcessPriority), typeof(string))]
public sealed class PriorityNameConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ProcessPriority priority ? priority.ToDisplayName() : null;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Always; the conversion is one-way.</exception>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
