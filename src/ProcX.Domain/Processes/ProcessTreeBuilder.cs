namespace ProcX.Domain.Processes;

/// <summary>
/// Builds parent/child process hierarchies from flat process samples.
/// </summary>
public static class ProcessTreeBuilder
{
    /// <summary>
    /// Builds the process forest.
    /// </summary>
    /// <remarks>
    /// A process is attached to the process whose PID equals its parent PID only when that candidate
    /// was created before it. Otherwise the original parent has exited and its PID was reused, so the
    /// process becomes a root. Ties on creation time are broken by PID, which yields a strict order and
    /// therefore guarantees the result is acyclic.
    /// </remarks>
    /// <param name="processes">The processes of one snapshot; PIDs must be unique.</param>
    /// <returns>Root nodes ordered by creation time, then PID.</returns>
    /// <exception cref="ArgumentException"><paramref name="processes"/> contains duplicate PIDs.</exception>
    public static IReadOnlyList<ProcessTreeNode> Build(IEnumerable<ProcessSample> processes)
    {
        ArgumentNullException.ThrowIfNull(processes);

        var byPid = new Dictionary<int, ProcessSample>();
        foreach (var process in processes)
        {
            if (!byPid.TryAdd(process.Pid, process))
            {
                throw new ArgumentException($"Duplicate PID {process.Pid} in snapshot.", nameof(processes));
            }
        }

        var childrenByPid = new Dictionary<int, List<ProcessSample>>();
        var roots = new List<ProcessSample>();

        foreach (var process in byPid.Values)
        {
            if (byPid.TryGetValue(process.ParentPid, out var parent) && IsCreatedBefore(parent, process))
            {
                if (!childrenByPid.TryGetValue(parent.Pid, out var siblings))
                {
                    siblings = [];
                    childrenByPid[parent.Pid] = siblings;
                }

                siblings.Add(process);
            }
            else
            {
                roots.Add(process);
            }
        }

        return CreateNodes(roots, childrenByPid);
    }

    /// <summary>
    /// Projects a process forest onto a depth-first flat list.
    /// </summary>
    /// <param name="roots">The root nodes.</param>
    /// <param name="isExpanded">Returns whether the children of the given process should be included.</param>
    /// <returns>The visible nodes in display order.</returns>
    public static IReadOnlyList<FlattenedProcessNode> Flatten(
        IReadOnlyList<ProcessTreeNode> roots,
        Func<ProcessKey, bool> isExpanded)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(isExpanded);

        var result = new List<FlattenedProcessNode>();
        var stack = new Stack<(ProcessTreeNode Node, int Depth)>();
        for (var i = roots.Count - 1; i >= 0; i--)
        {
            stack.Push((roots[i], 0));
        }

        while (stack.TryPop(out var item))
        {
            var (node, depth) = item;
            result.Add(new FlattenedProcessNode(node.Process, depth, node.Children.Count > 0));

            if (node.Children.Count > 0 && isExpanded(node.Process.Key))
            {
                for (var i = node.Children.Count - 1; i >= 0; i--)
                {
                    stack.Push((node.Children[i], depth + 1));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets a process and all its descendants, children before parents.
    /// </summary>
    /// <remarks>
    /// The post-order is the safe order for terminating a tree: descendants are ended before the
    /// process that could otherwise spawn replacements.
    /// </remarks>
    /// <param name="processes">The processes of one snapshot.</param>
    /// <param name="root">The process whose subtree to return.</param>
    /// <returns>The subtree in post-order, ending with <paramref name="root"/>; empty if the root is not present.</returns>
    public static IReadOnlyList<ProcessSample> GetSubtreePostOrder(IEnumerable<ProcessSample> processes, ProcessKey root)
    {
        var node = Find(Build(processes), root);
        if (node is null)
        {
            return [];
        }

        var result = new List<ProcessSample>();
        AppendPostOrder(node, result);
        return result;
    }

    /// <summary>
    /// Finds the node of a process in a forest.
    /// </summary>
    /// <param name="nodes">The nodes to search, including their descendants.</param>
    /// <param name="key">The process identity.</param>
    /// <returns>The node, or <see langword="null"/> if not found.</returns>
    private static ProcessTreeNode? Find(IReadOnlyList<ProcessTreeNode> nodes, ProcessKey key)
    {
        var stack = new Stack<ProcessTreeNode>(nodes);
        while (stack.TryPop(out var node))
        {
            if (node.Process.Key == key)
            {
                return node;
            }

            foreach (var child in node.Children)
            {
                stack.Push(child);
            }
        }

        return null;
    }

    /// <summary>
    /// Appends a subtree to a list in post-order.
    /// </summary>
    /// <param name="node">The subtree root.</param>
    /// <param name="result">The list to append to.</param>
    private static void AppendPostOrder(ProcessTreeNode node, List<ProcessSample> result)
    {
        foreach (var child in node.Children)
        {
            AppendPostOrder(child, result);
        }

        result.Add(node.Process);
    }

    /// <summary>
    /// Determines whether <paramref name="candidate"/> precedes <paramref name="process"/> in the strict
    /// (creation time, PID) order, which is required for it to be a valid parent.
    /// </summary>
    /// <param name="candidate">The potential parent.</param>
    /// <param name="process">The child process.</param>
    /// <returns><see langword="true"/> if the candidate can be the parent.</returns>
    private static bool IsCreatedBefore(ProcessSample candidate, ProcessSample process) =>
        candidate.CreateTime < process.CreateTime
        || (candidate.CreateTime == process.CreateTime && candidate.Pid < process.Pid);

    /// <summary>
    /// Recursively creates sorted tree nodes for the given processes.
    /// </summary>
    /// <param name="processes">The processes at one level.</param>
    /// <param name="childrenByPid">Child lists indexed by parent PID.</param>
    /// <returns>The sorted nodes.</returns>
    private static List<ProcessTreeNode> CreateNodes(
        IEnumerable<ProcessSample> processes,
        Dictionary<int, List<ProcessSample>> childrenByPid) =>
        [
            .. processes
                .OrderBy(static p => p.CreateTime)
                .ThenBy(static p => p.Pid)
                .Select(p => new ProcessTreeNode(
                    p,
                    childrenByPid.TryGetValue(p.Pid, out var children) ? CreateNodes(children, childrenByPid) : [])),
        ];
}
