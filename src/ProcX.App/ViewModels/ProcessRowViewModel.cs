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
        Sample = metrics.Sample;
        Update(metrics);
    }

    /// <summary>
    /// Gets the latest raw sample, used to rebuild the process tree.
    /// </summary>
    public ProcessSample Sample { get; private set; }

    /// <summary>
    /// Gets or sets the position of the row in the depth-first tree order.
    /// </summary>
    [ObservableProperty]
    public partial int TreeOrder { get; set; }

    /// <summary>
    /// Gets or sets the nesting depth in the tree; 0 outside tree mode.
    /// </summary>
    [ObservableProperty]
    public partial int Depth { get; set; }

    /// <summary>
    /// Gets or sets whether the row is presented as part of a tree.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NodeKind))]
    public partial bool IsInTree { get; set; }

    /// <summary>
    /// Gets or sets whether the process has child processes.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NodeKind))]
    public partial bool HasChildren { get; set; }

    /// <summary>
    /// Gets or sets whether the children of the process are shown in tree mode.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NodeKind))]
    public partial bool IsExpanded { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the row is shown in tree mode (not hidden by a collapsed ancestor or the search).
    /// </summary>
    [ObservableProperty]
    public partial bool IsTreeVisible { get; set; } = true;

    /// <summary>
    /// Gets or sets the parent row in the current tree, or <see langword="null"/> for roots and outside tree mode.
    /// </summary>
    public ProcessRowViewModel? TreeParent { get; set; }

    /// <summary>
    /// Gets how the row is presented in the tree.
    /// </summary>
    public TreeNodeKind NodeKind => (IsInTree, HasChildren, IsExpanded) switch
    {
        (false, _, _) => TreeNodeKind.None,
        (true, false, _) => TreeNodeKind.Leaf,
        (true, true, true) => TreeNodeKind.Expanded,
        (true, true, false) => TreeNodeKind.Collapsed,
    };

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
        Sample = sample;
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
