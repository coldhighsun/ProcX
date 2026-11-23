using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ProcX.Application.Processes;
using ProcX.Domain.Processes;

namespace ProcX.App.ViewModels;

/// <summary>
/// One row of the process list. Properties are updated in place so selection and scroll position survive refreshes.
/// </summary>
public sealed partial class ProcessRowViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessRowViewModel"/> class.
    /// </summary>
    /// <param name="metrics">The initial metrics.</param>
    public ProcessRowViewModel(ProcessMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        Key = metrics.Key;
        Name = metrics.Sample.Name;
        Update(metrics);
    }

    /// <summary>
    /// Gets the stable identity of the process.
    /// </summary>
    public ProcessKey Key { get; }

    /// <summary>
    /// Gets the process ID.
    /// </summary>
    public int Pid => Key.Pid;

    /// <summary>
    /// Gets the image name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the time the process was started, in local time.
    /// </summary>
    public DateTimeOffset StartTime => Key.CreateTime.ToLocalTime();

    /// <summary>
    /// Gets or sets the CPU usage in percent.
    /// </summary>
    [ObservableProperty]
    public partial double CpuPercent { get; set; }

    /// <summary>
    /// Gets or sets the private working set in bytes.
    /// </summary>
    [ObservableProperty]
    public partial long MemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the commit size in bytes.
    /// </summary>
    [ObservableProperty]
    public partial long CommitBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of threads.
    /// </summary>
    [ObservableProperty]
    public partial int ThreadCount { get; set; }

    /// <summary>
    /// Gets or sets the number of handles.
    /// </summary>
    [ObservableProperty]
    public partial int HandleCount { get; set; }

    /// <summary>
    /// Gets or sets the session ID.
    /// </summary>
    [ObservableProperty]
    public partial int SessionId { get; set; }

    /// <summary>
    /// Gets or sets the account the process runs as.
    /// </summary>
    [ObservableProperty]
    public partial string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the executable path.
    /// </summary>
    [ObservableProperty]
    public partial string? ImagePath { get; set; }

    /// <summary>
    /// Gets or sets the file description.
    /// </summary>
    [ObservableProperty]
    public partial string? Description { get; set; }

    /// <summary>
    /// Gets or sets the executable icon.
    /// </summary>
    [ObservableProperty]
    public partial ImageSource? Icon { get; set; }

    /// <summary>
    /// Gets or sets whether the process could not be opened due to insufficient privileges.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAccessDenied { get; set; }

    /// <summary>
    /// Applies the latest counters.
    /// </summary>
    /// <param name="metrics">The metrics of the same process instance.</param>
    /// <exception cref="ArgumentException"><paramref name="metrics"/> belongs to a different process.</exception>
    public void Update(ProcessMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        if (metrics.Key != Key)
        {
            throw new ArgumentException($"Metrics of {metrics.Key} cannot update row {Key}.", nameof(metrics));
        }

        var sample = metrics.Sample;
        CpuPercent = metrics.CpuPercent;
        MemoryBytes = sample.PrivateWorkingSetBytes;
        CommitBytes = sample.PrivateBytes;
        ThreadCount = sample.ThreadCount;
        HandleCount = sample.HandleCount;
        SessionId = sample.SessionId;
    }

    /// <summary>
    /// Applies information retrieved asynchronously after the row was created.
    /// </summary>
    /// <param name="info">The static information.</param>
    /// <param name="icon">The executable icon.</param>
    public void ApplyStaticInfo(ProcessStaticInfo info, ImageSource? icon)
    {
        ArgumentNullException.ThrowIfNull(info);

        ImagePath = info.ImagePath;
        UserName = info.UserName;
        Description = info.Description;
        IsAccessDenied = info.IsAccessDenied;
        Icon = icon;
    }
}
