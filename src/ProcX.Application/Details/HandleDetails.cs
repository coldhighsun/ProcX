namespace ProcX.Application.Details;

/// <summary>
/// A kernel object handle held by a process.
/// </summary>
/// <param name="Handle">The handle value in the owning process.</param>
/// <param name="TypeName">The object type, e.g. <c>File</c>, <c>Key</c>, <c>Event</c>.</param>
/// <param name="Name">The object name, or <see langword="null"/> if unnamed or not retrievable.</param>
/// <param name="GrantedAccess">The access mask granted to the handle.</param>
public sealed record HandleDetails(ulong Handle, string TypeName, string? Name, uint GrantedAccess);
