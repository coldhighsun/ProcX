using System.Diagnostics;
using System.Windows;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace ProcX.App.Services;

/// <summary>
/// <see cref="IShellInteraction"/> implemented with WPF-UI dialogs and Windows shell commands.
/// </summary>
public sealed class WpfShellInteraction : IShellInteraction
{
    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var dialog = new MessageBox
        {
            Owner = System.Windows.Application.Current.MainWindow,
            Title = title,
            Content = message,
            PrimaryButtonText = confirmText,
            CloseButtonText = "Cancel",
        };

        return await dialog.ShowDialogAsync() == MessageBoxResult.Primary;
    }

    /// <inheritdoc />
    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new MessageBox
        {
            Owner = System.Windows.Application.Current.MainWindow,
            Title = title,
            Content = message,
            CloseButtonText = "OK",
        };

        await dialog.ShowDialogAsync();
    }

    /// <inheritdoc />
    public void OpenFileLocation(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        using var explorer = Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    /// <inheritdoc />
    public void CopyToClipboard(string text) => Clipboard.SetText(text);

    /// <inheritdoc />
    public void Shutdown() => System.Windows.Application.Current.Shutdown();
}
