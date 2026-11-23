using Microsoft.Extensions.Hosting;
using ProcX.App.ViewModels;
using ProcX.Application.Processes;

namespace ProcX.App.Services;

/// <summary>
/// Runs the <see cref="ProcessMonitor"/> for the lifetime of the application and forwards its updates to the UI.
/// </summary>
/// <param name="monitor">The process monitor.</param>
/// <param name="processList">The list view model receiving updates.</param>
/// <param name="infoCache">The static information cache to prune when processes exit.</param>
/// <param name="dispatcher">Marshals updates to the UI thread.</param>
public sealed class ProcessMonitorService(
    ProcessMonitor monitor,
    ProcessListViewModel processList,
    ProcessInfoCache infoCache,
    IUiDispatcher dispatcher) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        monitor.Updated += OnUpdated;
        try
        {
            // Sample off the UI thread; the first sample runs synchronously inside RunAsync.
            await Task.Run(() => monitor.RunAsync(stoppingToken), stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            monitor.Updated -= OnUpdated;
        }
    }

    /// <summary>
    /// Forwards a delta to the UI thread.
    /// </summary>
    /// <param name="sender">The monitor.</param>
    /// <param name="delta">The delta.</param>
    private void OnUpdated(object? sender, ProcessDelta delta)
    {
        infoCache.Evict(delta.Removed);
        dispatcher.Post(() => processList.Apply(delta));
    }
}
