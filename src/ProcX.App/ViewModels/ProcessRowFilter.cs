using System.Globalization;

namespace ProcX.App.ViewModels;

/// <summary>
/// Matches process rows against a free-text search.
/// </summary>
public static class ProcessRowFilter
{
    /// <summary>
    /// Determines whether a row matches the search text.
    /// </summary>
    /// <remarks>
    /// Matches case-insensitively against name, description, user and path, or exactly against the PID.
    /// Blank search text matches every row.
    /// </remarks>
    /// <param name="row">The row.</param>
    /// <param name="searchText">The search text.</param>
    /// <returns><see langword="true"/> if the row should be shown.</returns>
    public static bool Matches(ProcessRowViewModel row, string? searchText)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        var text = searchText.Trim();
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && row.Pid == pid)
        {
            return true;
        }

        return Contains(row.Name, text)
            || Contains(row.Description, text)
            || Contains(row.UserName, text)
            || Contains(row.ImagePath, text);
    }

    /// <summary>
    /// Case-insensitive substring test that tolerates <see langword="null"/>.
    /// </summary>
    /// <param name="value">The value to search in.</param>
    /// <param name="text">The text to find.</param>
    /// <returns><see langword="true"/> if found.</returns>
    private static bool Contains(string? value, string text) =>
        value is not null && value.Contains(text, StringComparison.OrdinalIgnoreCase);
}
