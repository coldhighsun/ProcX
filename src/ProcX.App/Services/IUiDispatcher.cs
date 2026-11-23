namespace ProcX.App.Services;

/// <summary>
/// Schedules work on the UI thread.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Queues an action to run on the UI thread without waiting for it.
    /// </summary>
    /// <param name="action">The action.</param>
    void Post(Action action);
}
