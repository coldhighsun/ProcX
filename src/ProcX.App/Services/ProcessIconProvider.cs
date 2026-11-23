using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ProcX.App.Native;

namespace ProcX.App.Services;

/// <summary>
/// Extracts and caches small shell icons for executables.
/// </summary>
/// <remarks>
/// Returned images are frozen and can be used from any thread. Shell APIs require an STA thread.
/// </remarks>
public sealed class ProcessIconProvider
{
    /// <summary>
    /// Icons indexed by executable path.
    /// </summary>
    private readonly ConcurrentDictionary<string, ImageSource?> _icons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The generic executable icon, used when a process has no accessible image path.
    /// </summary>
    private readonly Lazy<ImageSource?> _defaultIcon = new(static () => Extract(".exe", useFileAttributes: true));

    /// <summary>
    /// Gets the icon for an executable, or the generic executable icon.
    /// </summary>
    /// <param name="imagePath">The executable path, or <see langword="null"/>.</param>
    /// <returns>The frozen icon, or <see langword="null"/> if even the generic icon is unavailable.</returns>
    public ImageSource? GetIcon(string? imagePath) =>
        imagePath is null
            ? _defaultIcon.Value
            : _icons.GetOrAdd(imagePath, static path => Extract(path, useFileAttributes: false)) ?? _defaultIcon.Value;

    /// <summary>
    /// Extracts a small icon via <c>SHGetFileInfo</c>.
    /// </summary>
    /// <param name="path">The file path, or an extension when <paramref name="useFileAttributes"/> is set.</param>
    /// <param name="useFileAttributes">Whether to resolve the icon by file type without touching the file.</param>
    /// <returns>The frozen icon, or <see langword="null"/> on failure.</returns>
    private static unsafe BitmapSource? Extract(string path, bool useFileAttributes)
    {
        var info = default(Shell32.ShFileInfo);
        var flags = Shell32.ShgfiIcon | Shell32.ShgfiSmallIcon;
        if (useFileAttributes)
        {
            flags |= Shell32.ShgfiUseFileAttributes;
        }

        var result = Shell32.SHGetFileInfo(path, Shell32.FileAttributeNormal, ref info, (uint)sizeof(Shell32.ShFileInfo), flags);
        if (result == 0 || info.Icon == 0)
        {
            return null;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            Shell32.DestroyIcon(info.Icon);
        }
    }
}
