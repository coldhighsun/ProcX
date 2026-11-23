namespace ProcX.Domain.Processes;

/// <summary>
/// A process tree node projected onto a flat list for display in a grid.
/// </summary>
/// <param name="Process">The process sample.</param>
/// <param name="Depth">Nesting depth; roots have depth 0.</param>
/// <param name="HasChildren">Whether the node has child processes, regardless of whether it is expanded.</param>
public sealed record FlattenedProcessNode(ProcessSample Process, int Depth, bool HasChildren);
