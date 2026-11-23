using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcX.App.Services;
using ProcX.App.Startup;
using ProcX.App.ViewModels;
using ProcX.Application.Details;
using ProcX.Application.Processes;
using ProcX.Application.Replacement;
using ProcX.Application.Security;
using ProcX.Application.Settings;
using ProcX.Infrastructure.Details;
using ProcX.Infrastructure.Processes;
using ProcX.Infrastructure.Replacement;
using ProcX.Infrastructure.Security;
using ProcX.Infrastructure.Settings;

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
            services.AddSingleton<IProcessController, Win32ProcessController>();
            services.AddSingleton<IElevationService, WindowsElevationService>();
            services.AddSingleton<IProcessDetailsProvider, Win32ProcessDetailsProvider>();
            services.AddSingleton<ITaskManagerReplacementService, IfeoTaskManagerReplacementService>();
            services.AddSingleton<ISettingsStore>(static provider =>
                new JsonSettingsStore(JsonSettingsStore.DefaultPath, provider.GetRequiredService<ILogger<JsonSettingsStore>>()));

            // Application
            services.AddSingleton<ProcessMonitor>();
            services.AddSingleton<ProcessInfoCache>();
            services.AddSingleton<ProcessTerminator>();
            services.AddSingleton<AppSettingsService>();

            // Presentation
            services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
            services.AddSingleton<IThemeService, WpfThemeService>();
            services.AddSingleton<IShellInteraction, WpfShellInteraction>();
            services.AddSingleton<ProcessDetailsViewModel>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<SettingsBinder>();
            services.AddSingleton<ProcessIconProvider>();
            services.AddSingleton<IProcessRowEnricher, ProcessRowEnricher>();
            services.AddSingleton<ProcessListViewModel>();
            services.AddSingleton<MainWindow>();
            services.AddHostedService<ProcessMonitorService>();
        })
        .Build();

    /// <summary>
    /// Runs a headless replacement command, or starts the host and shows the main window.
    /// </summary>
    /// <param name="e">The startup event arguments.</param>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var options = StartupOptions.Parse(e.Args);
        if (options.Mode != StartupMode.Normal)
        {
            // Elevated helper instance started by the settings window: change the registry and exit, no UI.
            Shutdown(ReplacementCommand.Execute(
                options.Mode,
                _host.Services.GetRequiredService<ITaskManagerReplacementService>(),
                Environment.ProcessPath ?? string.Empty));
            return;
        }

        // Resolve UI-affine services on the UI thread before background services start.
        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        _host.Services.GetRequiredService<SettingsBinder>().Start();
        MainWindow.Show();

        await _host.StartAsync();
        await _host.Services.GetRequiredService<SettingsViewModel>().CheckReplacementAtStartupAsync();
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
