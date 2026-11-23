using System.Windows.Threading;

namespace ProcX.App.Services;

/// <summary>
/// <see cref="IUiDispatcher"/> backed by the WPF dispatcher of the thread that created it.
/// </summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    /// <summary>
    /// The dispatcher of the UI thread.
    /// </summary>
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    /// <inheritdoc />
    public void Post(Action action) => _dispatcher.BeginInvoke(action, DispatcherPriority.Background);
}
