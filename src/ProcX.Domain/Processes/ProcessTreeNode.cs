namespace ProcX.Domain.Processes;

/// <summary>
/// A process together with the processes it created.
/// </summary>
/// <param name="Process">The process sample.</param>
/// <param name="Children">Child processes, ordered by creation time.</param>
public sealed record ProcessTreeNode(ProcessSample Process, IReadOnlyList<ProcessTreeNode> Children);
