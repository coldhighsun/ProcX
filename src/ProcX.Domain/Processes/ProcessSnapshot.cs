namespace ProcX.Domain.Processes;

/// <summary>
/// The state of all processes on the system at one sampling instant.
/// </summary>
/// <param name="CapturedAt">The time the snapshot was taken.</param>
/// <param name="Processes">The processes; PIDs are unique within a snapshot.</param>
public sealed record ProcessSnapshot(DateTimeOffset CapturedAt, IReadOnlyList<ProcessSample> Processes);
