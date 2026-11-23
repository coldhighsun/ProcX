using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProcX.App.Services;
using ProcX.App.ViewModels;
using ProcX.Application.Processes;
using ProcX.Infrastructure.Processes;

namespace ProcX.App;

/// <summary>
/// Application entry point; owns the generic host that composes all services.
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>
    /// The generic host providing dependency injection, logging and configuration.
    /// </summary>
    private readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureServices(static services =>
        {
            services.AddSingleton(TimeProvider.System);

            // Infrastructure
            services.AddSingleton<IProcessSnapshotProvider, NtProcessSnapshotProvider>();
            services.AddSingleton<ISystemInformation, WindowsSystemInformation>();
            services.AddSingleton<IProcessInfoProvider, Win32ProcessInfoProvider>();

            // Application
            services.AddSingleton<ProcessMonitor>();
            services.AddSingleton<ProcessInfoCache>();

            // Presentation
            services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
            services.AddSingleton<ProcessIconProvider>();
            services.AddSingleton<IProcessRowEnricher, ProcessRowEnricher>();
            services.AddSingleton<ProcessListViewModel>();
            services.AddSingleton<MainWindow>();
            services.AddHostedService<ProcessMonitorService>();
        })
        .Build();

    /// <summary>
    /// Starts the host and shows the main window.
    /// </summary>
    /// <param name="e">The startup event arguments.</param>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Resolve UI-affine services on the UI thread before background services start.
        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();

        await _host.StartAsync();
    }

    /// <summary>
    /// Stops and disposes the host when the application exits.
    /// </summary>
    /// <param name="e">The exit event arguments.</param>
    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
