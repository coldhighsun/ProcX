using ProcX.Infrastructure.Native;

namespace ProcX.Infrastructure.Details;

/// <summary>
/// Reads and formats kernel object names.
/// </summary>
internal static class HandleNames
{
    /// <summary>
    /// Registry roots in NT form and their familiar abbreviations.
    /// </summary>
    private static readonly (string Native, string Friendly)[] RegistryRoots =
    [
        (@"\REGISTRY\MACHINE", "HKLM"),
        (@"\REGISTRY\USER", "HKU"),
    ];

    /// <summary>
    /// Reads the name of an object with <c>NtQueryObject(ObjectNameInformation)</c>.
    /// </summary>
    /// <param name="handle">A handle to the object in the current process.</param>
    /// <returns>The name, or <see langword="null"/> if unnamed or not readable.</returns>
    /// <remarks>May block forever for some <c>File</c> objects; use <see cref="FileNameResolver"/> for those.</remarks>
    public static unsafe string? QueryObjectName(nint handle)
    {
        using var buffer = new NtQueryBuffer(512);
        var status = buffer.Fill((data, length, out required) =>
            NtDll.NtQueryObject(handle, NtDll.ObjectNameInformation, data, length, out required));

        if (!NtDll.NtSuccess(status))
        {
            return null;
        }

        var name = ((UnicodeString*)buffer.Pointer)->ToManagedString();
        return name.Length == 0 ? null : name;
    }

    /// <summary>
    /// Converts native object names into the forms users know (drive-letter paths, <c>HKLM\…</c>).
    /// </summary>
    /// <param name="typeName">The object type.</param>
    /// <param name="name">The native name.</param>
    /// <returns>The display name.</returns>
    public static string Format(string typeName, string name)
    {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(name);

        if (typeName == "File")
        {
            return DevicePathConverter.ToDosPath(name);
        }

        if (typeName == "Key")
        {
            foreach (var (native, friendly) in RegistryRoots)
            {
                if (name.StartsWith(native, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Concat(friendly, name.AsSpan(native.Length));
                }
            }
        }

        return name;
    }
}
