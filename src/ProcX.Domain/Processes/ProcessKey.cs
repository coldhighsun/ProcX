namespace ProcX.Domain.Processes;

/// <summary>
/// Uniquely identifies a process instance across its lifetime.
/// </summary>
/// <remarks>
/// Windows recycles process IDs, so the PID alone is not a stable identity.
/// Combining it with the creation time distinguishes a new process that reuses an old PID.
/// </remarks>
/// <param name="Pid">The process identifier.</param>
/// <param name="CreateTime">The time the process was created.</param>
public readonly record struct ProcessKey(int Pid, DateTimeOffset CreateTime)
{
    /// <summary>
    /// Returns a readable representation, e.g. <c>1234@2026-11-23T08:00:00.0000000+00:00</c>.
    /// </summary>
    /// <returns>The formatted key.</returns>
    public override string ToString() => $"{Pid}@{CreateTime:O}";
}
