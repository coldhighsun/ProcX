using System.Collections.Concurrent;
using ProcX.Application.Details;
using ProcX.Domain.Processes;

namespace ProcX.App.Tests;

/// <summary>
/// Returns configurable details and records every query.
/// </summary>
internal sealed class FakeDetailsProvider : IProcessDetailsProvider
{
    /// <summary>
    /// Gets the queries made, as <c>Tab:PID</c>.
    /// </summary>
    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>
    /// Gets or sets the status returned by every query.
    /// </summary>
    public DetailsStatus Status { get; set; } = DetailsStatus.Ok;

    /// <summary>
    /// Gets or sets the handles returned.
    /// </summary>
    public IReadOnlyList<HandleDetails> Handles { get; set; } = [];

    /// <inheritdoc />
    public DetailsResult<ProcessGeneralInfo> GetGeneral(ProcessKey key, string? imagePath)
    {
        Calls.Enqueue($"General:{key.Pid}");
        return Result(new ProcessGeneralInfo($"cmd-{key.Pid}", ProcessArchitecture.X64, false, IntegrityLevel.Medium, null, null));
    }

    /// <inheritdoc />
    public DetailsResult<IReadOnlyList<ThreadDetails>> GetThreads(ProcessKey key)
    {
        Calls.Enqueue($"Threads:{key.Pid}");
        return Result<IReadOnlyList<ThreadDetails>>(
            [new ThreadDetails(key.Pid + 1, key.CreateTime, TimeSpan.Zero, TimeSpan.Zero, 8, 8, 0, ThreadSchedulingState.Waiting, "UserRequest", 0, null)]);
    }

    /// <inheritdoc />
    public DetailsResult<IReadOnlyList<ModuleDetails>> GetModules(ProcessKey key)
    {
        Calls.Enqueue($"Modules:{key.Pid}");
        return Result<IReadOnlyList<ModuleDetails>>([new ModuleDetails("a.dll", @"C:\a.dll", 0x1000, 0x1000, null, null, null)]);
    }

    /// <inheritdoc />
    public DetailsResult<IReadOnlyList<HandleDetails>> GetHandles(ProcessKey key, CancellationToken cancellationToken)
    {
        Calls.Enqueue($"Handles:{key.Pid}");
        return Result(Handles);
    }

    /// <summary>
    /// Wraps data in a result with the configured status.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="value">The data.</param>
    /// <returns>The result.</returns>
    private DetailsResult<T> Result<T>(T value) =>
        Status == DetailsStatus.Ok ? DetailsResult.Ok(value) : DetailsResult.Fail<T>(Status);
}
