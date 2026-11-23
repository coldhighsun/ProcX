using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ProcX.App.ViewModels;
using ProcX.App.Views;
using ProcX.Domain.Processes;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace ProcX.App.Services;

/// <summary>
/// <see cref="IShellInteraction"/> implemented with WPF-UI dialogs and Windows shell commands.
/// </summary>
/// <param name="services">Resolves view models of windows created on demand.</param>
public sealed class WpfShellInteraction(IServiceProvider services) : IShellInteraction
{
    /// <inheritdoc />
    public void ShowSettings()
    {
        var window = new SettingsWindow(services.GetRequiredService<SettingsViewModel>())
        {
            Owner = System.Windows.Application.Current.MainWindow,
        };
        window.ShowDialog();
    }

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
    public Task<AffinityMask?> PickAffinityAsync(string processName, AffinityMask current, AffinityMask available)
    {
        var viewModel = new AffinityDialogViewModel(processName, current, available);
        var dialog = new AffinityDialog(viewModel) { Owner = System.Windows.Application.Current.MainWindow };
        return Task.FromResult(dialog.ShowDialog() == true ? viewModel.Mask : (AffinityMask?)null);
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
