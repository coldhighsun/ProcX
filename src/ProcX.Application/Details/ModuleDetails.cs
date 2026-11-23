namespace ProcX.Application.Details;

/// <summary>
/// A module (EXE or DLL) loaded into a process.
/// </summary>
/// <param name="Name">The module file name.</param>
/// <param name="Path">The full path.</param>
/// <param name="BaseAddress">The load address.</param>
/// <param name="Size">The size of the mapped image in bytes.</param>
/// <param name="Description">The file description from the version resource.</param>
/// <param name="CompanyName">The company name from the version resource.</param>
/// <param name="FileVersion">The file version from the version resource.</param>
public sealed record ModuleDetails(
    string Name,
    string Path,
    ulong BaseAddress,
    long Size,
    string? Description,
    string? CompanyName,
    string? FileVersion)
{
    /// <summary>
    /// Determines whether an address falls inside this module.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns><see langword="true"/> if the address is within the module image.</returns>
    public bool Contains(ulong address) => address >= BaseAddress && address - BaseAddress < (ulong)Size;
}
