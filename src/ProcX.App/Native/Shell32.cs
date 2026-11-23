using System.Runtime.InteropServices;

namespace ProcX.App.Native;

/// <summary>
/// P/Invoke declarations for shell icon extraction.
/// </summary>
/// <remarks>
/// Declared by hand because <c>SHFILEINFOW</c> is packed differently on 32-bit x86; ProcX ships
/// 64-bit builds only (x64 and ARM64), where natural alignment applies.
/// </remarks>
internal static partial class Shell32
{
    /// <summary>
    /// Retrieve the handle to the icon.
    /// </summary>
    public const uint ShgfiIcon = 0x000000100;

    /// <summary>
    /// Retrieve the small (16x16) icon.
    /// </summary>
    public const uint ShgfiSmallIcon = 0x000000001;

    /// <summary>
    /// Resolve by file type without accessing the file.
    /// </summary>
    public const uint ShgfiUseFileAttributes = 0x000000010;

    /// <summary>
    /// <c>FILE_ATTRIBUTE_NORMAL</c>.
    /// </summary>
    public const uint FileAttributeNormal = 0x00000080;

    /// <summary>
    /// Retrieves information about a file or file type.
    /// </summary>
    /// <param name="path">The file path or extension.</param>
    /// <param name="fileAttributes">File attributes, used with <see cref="ShgfiUseFileAttributes"/>.</param>
    /// <param name="fileInfo">Receives the information.</param>
    /// <param name="fileInfoSize">The size of <paramref name="fileInfo"/> in bytes.</param>
    /// <param name="flags">The <c>SHGFI_*</c> flags.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport("shell32.dll", EntryPoint = "SHGetFileInfoW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint SHGetFileInfo(string path, uint fileAttributes, ref ShFileInfo fileInfo, uint fileInfoSize, uint flags);

    /// <summary>
    /// Destroys an icon handle.
    /// </summary>
    /// <param name="icon">The icon handle.</param>
    /// <returns><see langword="true"/> on success.</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint icon);

    /// <summary>
    /// The <c>SHFILEINFOW</c> structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ShFileInfo
    {
        /// <summary>
        /// The icon handle; must be destroyed with <see cref="DestroyIcon"/>.
        /// </summary>
        public nint Icon;

        /// <summary>
        /// The icon index in the system image list.
        /// </summary>
        public int IconIndex;

        /// <summary>
        /// The file attributes.
        /// </summary>
        public uint Attributes;

        /// <summary>
        /// The display name.
        /// </summary>
        public fixed char DisplayName[260];

        /// <summary>
        /// The file type description.
        /// </summary>
        public fixed char TypeName[80];
    }
}
