namespace ProcX.Application.Processes;

/// <summary>
/// The outcome of an operation that changes or reads the scheduling state of a process.
/// </summary>
public enum ProcessOperationStatus
{
    /// <summary>
    /// The operation succeeded.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The process has exited (or its PID now belongs to another process).
    /// </summary>
    NotFound,

    /// <summary>
    /// The caller lacks the rights; elevation may help.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// The operation is not supported on this system (e.g. affinity with more than one processor group).
    /// </summary>
    Unsupported,

    /// <summary>
    /// The operation failed for another reason; see <see cref="ProcessOperationResult{T}.ErrorCode"/>.
    /// </summary>
    Failed,
}

/// <summary>
/// The result of a process operation that returns data.
/// </summary>
/// <typeparam name="T">The type of the data.</typeparam>
/// <param name="Status">The outcome.</param>
/// <param name="Value">The data when <paramref name="Status"/> is <see cref="ProcessOperationStatus.Succeeded"/>.</param>
/// <param name="ErrorCode">The Win32 error code on failure; otherwise 0.</param>
public sealed record ProcessOperationResult<T>(ProcessOperationStatus Status, T? Value, int ErrorCode = 0)
{
    /// <summary>
    /// Gets whether the operation succeeded.
    /// </summary>
    public bool IsSuccess => Status == ProcessOperationStatus.Succeeded;
}

/// <summary>
/// Factory methods for <see cref="ProcessOperationResult{T}"/>.
/// </summary>
public static class ProcessOperationResult
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <param name="value">The data.</param>
    /// <returns>The result.</returns>
    public static ProcessOperationResult<T> Ok<T>(T value) => new(ProcessOperationStatus.Succeeded, value);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <param name="status">The failure status.</param>
    /// <param name="errorCode">The Win32 error code.</param>
    /// <returns>The result.</returns>
    public static ProcessOperationResult<T> Fail<T>(ProcessOperationStatus status, int errorCode = 0) => new(status, default, errorCode);
}
