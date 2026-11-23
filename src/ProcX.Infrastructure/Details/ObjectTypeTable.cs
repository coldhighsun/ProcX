using System.ComponentModel;
using ProcX.Infrastructure.Native;

namespace ProcX.Infrastructure.Details;

/// <summary>
/// Maps kernel object type indices to names (e.g. 37 → <c>File</c>).
/// </summary>
/// <remarks>Object types are fixed for the lifetime of the OS boot, so the table is read once.</remarks>
internal static class ObjectTypeTable
{
    /// <summary>
    /// Size of <c>OBJECT_TYPE_INFORMATION</c> on 64-bit Windows.
    /// </summary>
    private const int ObjectTypeInformationSize = 104;

    /// <summary>
    /// Offset of the <c>TypeIndex</c> field in <c>OBJECT_TYPE_INFORMATION</c> (Windows 8.1+).
    /// </summary>
    private const int TypeIndexOffset = 90;

    /// <summary>
    /// The table, read on first use.
    /// </summary>
    private static readonly Lazy<IReadOnlyDictionary<int, string>> Table = new(Load);

    /// <summary>
    /// Gets the name of an object type.
    /// </summary>
    /// <param name="index">The type index.</param>
    /// <returns>The name, or <c>Type #n</c> if unknown.</returns>
    public static string GetName(int index) =>
        Table.Value.TryGetValue(index, out var name) ? name : $"Type #{index}";

    /// <summary>
    /// Reads all object types with <c>NtQueryObject(ObjectTypesInformation)</c>.
    /// </summary>
    /// <returns>Type names indexed by type index.</returns>
    /// <exception cref="Win32Exception">The query failed.</exception>
    private static unsafe Dictionary<int, string> Load()
    {
        using var buffer = new NtQueryBuffer(64 * 1024);
        var status = buffer.Fill(static (data, length, out required) =>
            NtDll.NtQueryObject(0, NtDll.ObjectTypesInformation, data, length, out required));
        if (!NtDll.NtSuccess(status))
        {
            throw new Win32Exception((int)NtDll.RtlNtStatusToDosError(status));
        }

        // OBJECT_TYPES_INFORMATION: ULONG NumberOfTypes, then pointer-aligned OBJECT_TYPE_INFORMATION entries,
        // each followed by its name buffer.
        var count = *(uint*)buffer.Pointer;
        var entry = buffer.Pointer + AlignUp(sizeof(uint));
        var result = new Dictionary<int, string>((int)count);

        for (var i = 0; i < count; i++)
        {
            var name = ((UnicodeString*)entry)->ToManagedString();
            var index = entry[TypeIndexOffset];
            result[index] = name;

            entry += ObjectTypeInformationSize + AlignUp(((UnicodeString*)entry)->MaximumLength);
        }

        return result;
    }

    /// <summary>
    /// Rounds a size up to pointer alignment.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns>The aligned size.</returns>
    private static int AlignUp(int size) => (size + nint.Size - 1) & ~(nint.Size - 1);
}
