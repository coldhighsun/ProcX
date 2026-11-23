using System.Text.Json.Serialization;
using ProcX.Application.Processes;

namespace ProcX.Application.Settings;

/// <summary>
/// The color theme of the application.
/// </summary>
public enum AppTheme
{
    /// <summary>
    /// Follow the Windows app mode setting.
    /// </summary>
    System,

    /// <summary>
    /// Always light.
    /// </summary>
    Light,

    /// <summary>
    /// Always dark.
    /// </summary>
    Dark,
}

/// <summary>
/// User preferences persisted between runs.
/// </summary>
public sealed record AppSettings
{
    /// <summary>
    /// Gets the color theme.
    /// </summary>
    public AppTheme Theme { get; init; } = AppTheme.System;

    /// <summary>
    /// Gets how often process data is sampled.
    /// </summary>
    public TimeSpan RefreshInterval { get; init; } = ProcessMonitor.DefaultInterval;

    /// <summary>
    /// Gets whether the details panel is shown.
    /// </summary>
    public bool ShowDetails { get; init; } = true;

    /// <summary>
    /// Gets whether processes are shown as a tree.
    /// </summary>
    public bool TreeView { get; init; }

    /// <summary>
    /// Gets the columns of the process list the user has hidden; new columns are therefore visible by default.
    /// </summary>
    [JsonConverter(typeof(ProcessColumnsJsonConverter))]
    public ProcessColumns HiddenColumns { get; init; } = ProcessColumns.None;

    /// <summary>
    /// Returns a copy whose values are within their valid ranges, e.g. after loading a hand-edited file.
    /// </summary>
    /// <returns>The sanitized settings.</returns>
    public AppSettings Sanitize() => this with
    {
        Theme = Enum.IsDefined(Theme) ? Theme : AppTheme.System,
        RefreshInterval = TimeSpan.FromTicks(Math.Clamp(
            RefreshInterval.Ticks,
            ProcessMonitor.MinimumInterval.Ticks,
            ProcessMonitor.MaximumInterval.Ticks)),
        HiddenColumns = HiddenColumns & ProcessColumns.All,
    };
}
