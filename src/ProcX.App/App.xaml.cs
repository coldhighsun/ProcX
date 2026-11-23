using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            services.AddSingleton<MainWindow>();
        })
        .Build();

    /// <summary>
    /// Starts the host and shows the main window.
    /// </summary>
    /// <param name="e">The startup event arguments.</param>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync();

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
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
