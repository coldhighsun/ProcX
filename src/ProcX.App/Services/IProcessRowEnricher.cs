using ProcX.App.ViewModels;

namespace ProcX.App.Services;

/// <summary>
/// Fills in the slow-to-retrieve parts of a process row (path, user, description, icon) in the background.
/// </summary>
public interface IProcessRowEnricher
{
    /// <summary>
    /// Queues a newly created row for enrichment.
    /// </summary>
    /// <param name="row">The row.</param>
    void Enqueue(ProcessRowViewModel row);
}
