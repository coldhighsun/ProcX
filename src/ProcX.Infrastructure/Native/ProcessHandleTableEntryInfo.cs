using System.Runtime.InteropServices;

namespace ProcX.Infrastructure.Native;

/// <summary>
/// One entry of <c>PROCESS_HANDLE_SNAPSHOT_INFORMATION</c>, returned by
/// <c>NtQueryInformationProcess(ProcessHandleInformation)</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ProcessHandleTableEntryInfo
{
    /// <summary>
    /// The handle value in the owning process.
    /// </summary>
    public nint HandleValue;

    /// <summary>
    /// The number of handles to the object.
    /// </summary>
    public nuint HandleCount;

    /// <summary>
    /// The number of references to the object.
    /// </summary>
    public nuint PointerCount;

    /// <summary>
    /// The access mask granted to the handle.
    /// </summary>
    public uint GrantedAccess;

    /// <summary>
    /// The index of the object type (see <c>ObjectTypesInformation</c>).
    /// </summary>
    public uint ObjectTypeIndex;

    /// <summary>
    /// The handle attributes.
    /// </summary>
    public uint HandleAttributes;

    /// <summary>
    /// Reserved.
    /// </summary>
    public uint Reserved;
}

/// <summary>
/// The header of <c>PROCESS_HANDLE_SNAPSHOT_INFORMATION</c>; the entries follow it.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ProcessHandleSnapshotInformation
{
    /// <summary>
    /// The number of entries.
    /// </summary>
    public nuint NumberOfHandles;

    /// <summary>
    /// Reserved.
    /// </summary>
    public nuint Reserved;
}
