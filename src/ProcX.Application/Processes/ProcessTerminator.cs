using ProcX.Domain.Processes;

namespace ProcX.Application.Processes;

/// <summary>
/// Terminates processes and process trees.
/// </summary>
/// <param name="snapshots">Provides the current process hierarchy for tree operations.</param>
/// <param name="controller">Terminates individual processes.</param>
public sealed class ProcessTerminator(IProcessSnapshotProvider snapshots, IProcessController controller)
{
    /// <summary>
    /// Terminates a single process.
    /// </summary>
    /// <param name="key">The process identity.</param>
    /// <returns>The result.</returns>
    public TerminationResult Terminate(ProcessKey key) => controller.Terminate(key);

    /// <summary>
    /// Gets the processes that <see cref="TerminateTree"/> would terminate, in termination order.
    /// </summary>
    /// <param name="root">The root of the tree.</param>
    /// <returns>The descendants followed by the root; empty if the root no longer exists.</returns>
    public IReadOnlyList<ProcessSample> GetTree(ProcessKey root) =>
        ProcessTreeBuilder.GetSubtreePostOrder(snapshots.Capture().Processes, root);

    /// <summary>
    /// Terminates a process and all of its descendants, children first.
    /// </summary>
    /// <remarks>
    /// The tree is re-read at call time. Failures for individual processes do not stop the operation.
    /// </remarks>
    /// <param name="root">The root of the tree.</param>
    /// <returns>One result per process; a single <see cref="TerminationStatus.NotFound"/> result if the root no longer exists.</returns>
    public IReadOnlyList<TerminationResult> TerminateTree(ProcessKey root)
    {
        var tree = GetTree(root);
        if (tree.Count == 0)
        {
            return [new TerminationResult(root, TerminationStatus.NotFound)];
        }

        return [.. tree.Select(process => controller.Terminate(process.Key))];
    }
}
