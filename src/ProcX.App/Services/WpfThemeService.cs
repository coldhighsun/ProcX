using Microsoft.Win32;
using ProcX.Application.Settings;
using Wpf.Ui.Appearance;

namespace ProcX.App.Services;

/// <summary>
/// <see cref="IThemeService"/> backed by WPF-UI's theme manager.
/// </summary>
public sealed class WpfThemeService : IThemeService
{
    /// <inheritdoc />
    public void Apply(AppTheme theme)
    {
        var applied = theme switch
        {
            AppTheme.Light => ApplicationTheme.Light,
            AppTheme.Dark => ApplicationTheme.Dark,
            _ => IsSystemLight() ? ApplicationTheme.Light : ApplicationTheme.Dark,
        };

        ApplicationThemeManager.Apply(applied);
    }

    /// <summary>
    /// Reads the Windows "app mode" setting.
    /// </summary>
    /// <returns><see langword="true"/> if apps should be light; dark is assumed if the setting cannot be read.</returns>
    private static bool IsSystemLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
    }
}
