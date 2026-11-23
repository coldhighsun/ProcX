using System.Runtime.InteropServices;
using ProcX.Application.Details;
using ProcX.Domain.Processes;
using ProcX.Infrastructure.Processes;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Diagnostics.ToolHelp;
using Windows.Win32.System.Threading;

namespace ProcX.Infrastructure.Details;

/// <summary>
/// Module enumeration.
/// </summary>
public sealed partial class Win32ProcessDetailsProvider
{
    /// <summary>
    /// How many version resources are read in parallel; the work is dominated by file I/O.
    /// </summary>
    private const int VersionReadParallelism = 4;

    /// <summary>
    /// How often to retry a module snapshot that failed because the module list changed while it was taken.
    /// </summary>
    private const int SnapshotAttempts = 3;

    /// <inheritdoc />
    public DetailsResult<IReadOnlyList<ModuleDetails>> GetModules(ProcessKey key) => EnumerateModules(key, includeVersions: true);

    /// <summary>
    /// Lists the modules of a process with a single Toolhelp snapshot.
    /// </summary>
    /// <remarks>
    /// Toolhelp walks the loader list once. <c>EnumProcessModulesEx</c> plus <c>GetModuleFileNameEx</c> walks it
    /// again for every module, which is quadratic and took ~0.4 s for a process with 400 modules.
    /// </remarks>
    /// <param name="key">The process identity.</param>
    /// <param name="includeVersions">Whether to read version resources (slow on first access to each file).</param>
    /// <returns>The modules ordered by base address.</returns>
    private DetailsResult<IReadOnlyList<ModuleDetails>> EnumerateModules(ProcessKey key, bool includeVersions)
    {
        // Verifies identity and access up front: Toolhelp itself only takes a PID and reports failures vaguely.
        using (var process = ProcessHandles.OpenVerified(
            key,
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION | PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ,
            out var error))
        {
            if (process is null)
            {
                return DetailsResult.Fail<IReadOnlyList<ModuleDetails>>(ToStatus(error));
            }
        }

        var entries = SnapshotModules(key.Pid, out var snapshotError);
        if (entries is null)
        {
            // ERROR_PARTIAL_COPY: the loader data is not initialized yet (process just started) or is going away.
            return snapshotError == WIN32_ERROR.ERROR_ACCESS_DENIED
                ? DetailsResult.Fail<IReadOnlyList<ModuleDetails>>(DetailsStatus.AccessDenied)
                : DetailsResult.Ok<IReadOnlyList<ModuleDetails>>([]);
        }

        var result = new ModuleDetails[entries.Count];
        Parallel.For(
            0,
            entries.Count,
            new ParallelOptions { MaxDegreeOfParallelism = includeVersions ? VersionReadParallelism : 1 },
            i =>
            {
                var (name, path, baseAddress, size) = entries[i];
                var version = includeVersions ? GetVersion(path) : null;
                result[i] = new ModuleDetails(name, path, baseAddress, size, version?.Description, version?.CompanyName, version?.FileVersion);
            });

        Array.Sort(result, static (a, b) => a.BaseAddress.CompareTo(b.BaseAddress));
        return DetailsResult.Ok<IReadOnlyList<ModuleDetails>>(result);
    }

    /// <summary>
    /// Takes a Toolhelp module snapshot of a process.
    /// </summary>
    /// <param name="pid">The process ID.</param>
    /// <param name="error">The Win32 error on failure.</param>
    /// <returns>Name, path, base and size of each module, or <see langword="null"/> on failure.</returns>
    private static unsafe List<(string Name, string Path, ulong Base, long Size)>? SnapshotModules(int pid, out WIN32_ERROR error)
    {
        for (var attempt = 0; attempt < SnapshotAttempts; attempt++)
        {
            using var snapshot = PInvoke.CreateToolhelp32Snapshot_SafeHandle(
                CREATE_TOOLHELP_SNAPSHOT_FLAGS.TH32CS_SNAPMODULE | CREATE_TOOLHELP_SNAPSHOT_FLAGS.TH32CS_SNAPMODULE32,
                (uint)pid);

            if (snapshot.IsInvalid)
            {
                error = (WIN32_ERROR)Marshal.GetLastPInvokeError();
                if (error == WIN32_ERROR.ERROR_BAD_LENGTH)
                {
                    continue;
                }

                return null;
            }

            var result = new List<(string, string, ulong, long)>();
            var entry = new MODULEENTRY32W { dwSize = (uint)sizeof(MODULEENTRY32W) };
            for (var more = PInvoke.Module32FirstW(snapshot, ref entry); more; more = PInvoke.Module32NextW(snapshot, ref entry))
            {
                result.Add((
                    entry.szModule.ToString(),
                    entry.szExePath.ToString(),
                    (ulong)entry.modBaseAddr,
                    entry.modBaseSize));
            }

            error = WIN32_ERROR.NO_ERROR;
            return result;
        }

        error = WIN32_ERROR.ERROR_BAD_LENGTH;
        return null;
    }
}
