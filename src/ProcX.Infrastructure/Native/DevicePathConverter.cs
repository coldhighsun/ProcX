using Windows.Win32;

namespace ProcX.Infrastructure.Native;

/// <summary>
/// Converts NT device paths (<c>\Device\HarddiskVolume3\Windows</c>) to DOS paths (<c>C:\Windows</c>).
/// </summary>
internal static class DevicePathConverter
{
    /// <summary>
    /// Device prefixes indexed to their drive letters, e.g. <c>\Device\HarddiskVolume3</c> → <c>C:</c>.
    /// </summary>
    /// <remarks>
    /// Read lazily once; drives mounted later are not mapped until restart, which is acceptable for display.
    /// </remarks>
    private static readonly Lazy<IReadOnlyList<(string Device, string Drive)>> Mappings = new(LoadMappings);

    /// <summary>
    /// Converts a device path to a DOS path when it starts with a mapped device.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The DOS path, or <paramref name="path"/> unchanged if no mapping applies.</returns>
    public static string ToDosPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        foreach (var (device, drive) in Mappings.Value)
        {
            if (path.StartsWith(device, StringComparison.OrdinalIgnoreCase)
                && (path.Length == device.Length || path[device.Length] == '\\'))
            {
                return string.Concat(drive, path.AsSpan(device.Length));
            }
        }

        return path;
    }

    /// <summary>
    /// Reads the device of every drive letter with <c>QueryDosDevice</c>.
    /// </summary>
    /// <returns>The mappings, longest device first so nested devices match before their prefixes.</returns>
    private static List<(string Device, string Drive)> LoadMappings()
    {
        var result = new List<(string Device, string Drive)>();
        Span<char> buffer = stackalloc char[512];

        foreach (var drive in Environment.GetLogicalDrives())
        {
            var name = drive.TrimEnd('\\');
            var length = PInvoke.QueryDosDevice(name, buffer);
            if (length == 0)
            {
                continue;
            }

            // The result is a list of NUL-terminated strings; the first is the current mapping.
            var target = buffer[..(int)length];
            var end = target.IndexOf('\0');
            result.Add((new string(end >= 0 ? target[..end] : target), name));
        }

        result.Sort(static (a, b) => b.Device.Length.CompareTo(a.Device.Length));
        return result;
    }
}
