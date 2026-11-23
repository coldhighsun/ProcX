namespace ProcX.App.Services;

/// <summary>
/// User-facing interactions that view models request from the shell (dialogs, clipboard, Explorer, shutdown).
/// </summary>
public interface IShellInteraction
{
    /// <summary>
    /// Asks the user to confirm an action.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The question.</param>
    /// <param name="confirmText">The text of the confirming button, e.g. "End process".</param>
    /// <returns><see langword="true"/> if the user confirmed.</returns>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    /// <summary>
    /// Shows an informational or error message.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The message.</param>
    /// <returns>A task that completes when the dialog is closed.</returns>
    Task ShowMessageAsync(string title, string message);

    /// <summary>
    /// Opens Explorer with the file selected.
    /// </summary>
    /// <param name="path">The file path.</param>
    void OpenFileLocation(string path);

    /// <summary>
    /// Copies text to the clipboard.
    /// </summary>
    /// <param name="text">The text.</param>
    void CopyToClipboard(string text);

    /// <summary>
    /// Closes the application.
    /// </summary>
    void Shutdown();
}
