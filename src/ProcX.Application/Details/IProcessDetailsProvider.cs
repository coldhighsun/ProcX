using ProcX.Domain.Processes;

namespace ProcX.Application.Details;

/// <summary>
/// Retrieves on-demand details of a single process.
/// </summary>
/// <remarks>
/// Implementations verify the process identity (PID and creation time) and never throw for exited or
/// inaccessible processes; such cases are reported through <see cref="DetailsResult{T}.Status"/>.
/// Calls may be slow and must not be made on the UI thread.
/// </remarks>
public interface IProcessDetailsProvider
{
    /// <summary>
    /// Gets general information (command line, architecture, elevation, version).
    /// </summary>
    /// <param name="key">The process identity.</param>
    /// <param name="imagePath">The executable path if already known, used for version information.</param>
    /// <returns>The result.</returns>
    DetailsResult<ProcessGeneralInfo> GetGeneral(ProcessKey key, string? imagePath);

    /// <summary>
    /// Gets the threads of the process.
    /// </summary>
    /// <param name="key">The process identity.</param>
    /// <returns>The result, ordered by thread ID.</returns>
    DetailsResult<IReadOnlyList<ThreadDetails>> GetThreads(ProcessKey key);

    /// <summary>
    /// Gets the modules loaded into the process.
    /// </summary>
    /// <param name="key">The process identity.</param>
    /// <returns>The result, ordered by base address.</returns>
    DetailsResult<IReadOnlyList<ModuleDetails>> GetModules(ProcessKey key);

    /// <summary>
    /// Gets the handles held by the process.
    /// </summary>
    /// <param name="key">The process identity.</param>
    /// <param name="cancellationToken">Stops name resolution early.</param>
    /// <returns>The result, ordered by handle value.</returns>
    DetailsResult<IReadOnlyList<HandleDetails>> GetHandles(ProcessKey key, CancellationToken cancellationToken);
}
