using ProcX.Application.Settings;

namespace ProcX.App.Services;

/// <summary>
/// Applies the application color theme.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Applies a theme to all windows.
    /// </summary>
    /// <param name="theme">The theme; <see cref="AppTheme.System"/> follows the Windows app mode.</param>
    void Apply(AppTheme theme);
}
