namespace ProcX.Application.Details;

/// <summary>
/// Why a detail query did not return data.
/// </summary>
public enum DetailsStatus
{
    /// <summary>
    /// The data was retrieved.
    /// </summary>
    Ok,

    /// <summary>
    /// The process could not be opened with the required rights; elevation may help.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// The process has exited (or its PID now belongs to another process).
    /// </summary>
    NotFound,
}

/// <summary>
/// The outcome of a detail query.
/// </summary>
/// <typeparam name="T">The type of the data.</typeparam>
/// <param name="Status">Whether the data was retrieved.</param>
/// <param name="Value">The data when <paramref name="Status"/> is <see cref="DetailsStatus.Ok"/>; otherwise the default.</param>
public sealed record DetailsResult<T>(DetailsStatus Status, T? Value);

/// <summary>
/// Factory methods for <see cref="DetailsResult{T}"/>.
/// </summary>
public static class DetailsResult
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <param name="value">The data.</param>
    /// <returns>The result.</returns>
    public static DetailsResult<T> Ok<T>(T value) => new(DetailsStatus.Ok, value);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <typeparam name="T">The type of the data.</typeparam>
    /// <param name="status">The failure status.</param>
    /// <returns>The result.</returns>
    public static DetailsResult<T> Fail<T>(DetailsStatus status) => new(status, default);
}
