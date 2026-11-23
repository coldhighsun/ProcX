using System.ComponentModel;
using System.Globalization;
using Microsoft.Win32.SafeHandles;
using ProcX.Application.Details;
using ProcX.Domain.Processes;
using ProcX.Infrastructure.Native;
using ProcX.Infrastructure.Processes;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace ProcX.Infrastructure.Details;

/// <summary>
/// Handle enumeration.
/// </summary>
public sealed partial class Win32ProcessDetailsProvider
{
    /// <inheritdoc />
    public DetailsResult<IReadOnlyList<HandleDetails>> GetHandles(ProcessKey key, CancellationToken cancellationToken)
    {
        // Duplicating handles is needed for names only; without it the types and access masks are still listed.
        var process = ProcessHandles.OpenVerified(
            key,
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION | PROCESS_ACCESS_RIGHTS.PROCESS_DUP_HANDLE,
            out var error);
        var canDuplicate = process is not null;
        if (process is null && error == WIN32_ERROR.ERROR_ACCESS_DENIED)
        {
            process = ProcessHandles.OpenVerified(key, PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_INFORMATION, out error);
        }

        if (process is null)
        {
            return DetailsResult.Fail<IReadOnlyList<HandleDetails>>(ToStatus(error));
        }

        using (process)
        {
            var entries = ReadHandleTable(process);
            if (entries is null)
            {
                return DetailsResult.Fail<IReadOnlyList<HandleDetails>>(DetailsStatus.AccessDenied);
            }

            using var files = new FileNameResolver();
            var result = new List<HandleDetails>(entries.Count);
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var typeName = ObjectTypeTable.GetName((int)entry.ObjectTypeIndex);
                var name = canDuplicate ? ResolveName(process, entry.HandleValue, typeName, files) : null;
                result.Add(new HandleDetails((ulong)entry.HandleValue, typeName, name, entry.GrantedAccess));
            }

            result.Sort(static (a, b) => a.Handle.CompareTo(b.Handle));
            return DetailsResult.Ok<IReadOnlyList<HandleDetails>>(result);
        }
    }

    /// <summary>
    /// Reads the handle table of a process.
    /// </summary>
    /// <param name="process">A handle with <c>PROCESS_QUERY_INFORMATION</c>.</param>
    /// <returns>The entries, or <see langword="null"/> if access was denied (e.g. protected processes).</returns>
    /// <exception cref="Win32Exception">The query failed for another reason.</exception>
    private static unsafe List<ProcessHandleTableEntryInfo>? ReadHandleTable(SafeFileHandle process)
    {
        using var buffer = new NtQueryBuffer(64 * 1024);
        var status = buffer.Fill((data, length, out required) =>
            NtDll.NtQueryInformationProcess(process, NtDll.ProcessHandleInformation, data, length, out required));

        if (status == NtDll.StatusAccessDenied)
        {
            return null;
        }

        if (!NtDll.NtSuccess(status))
        {
            throw new Win32Exception((int)NtDll.RtlNtStatusToDosError(status));
        }

        var header = (ProcessHandleSnapshotInformation*)buffer.Pointer;
        var entries = (ProcessHandleTableEntryInfo*)(buffer.Pointer + sizeof(ProcessHandleSnapshotInformation));
        var count = (int)header->NumberOfHandles;
        var result = new List<ProcessHandleTableEntryInfo>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(entries[i]);
        }

        return result;
    }

    /// <summary>
    /// Describes the object behind a handle: its name, or for processes and threads their IDs.
    /// </summary>
    /// <param name="process">The owning process, opened with <c>PROCESS_DUP_HANDLE</c>.</param>
    /// <param name="handle">The handle value in the owning process.</param>
    /// <param name="typeName">The object type.</param>
    /// <param name="files">Resolves file names safely.</param>
    /// <returns>The display name, or <see langword="null"/>.</returns>
    private static string? ResolveName(SafeFileHandle process, nint handle, string typeName, FileNameResolver files)
    {
        var access = typeName switch
        {
            "Process" => Kernel32.ProcessQueryLimitedInformation,
            "Thread" => Kernel32.ThreadQueryLimitedInformation,
            _ => 0u,
        };

        if (!Kernel32.DuplicateHandle(process, handle, Kernel32.CurrentProcess, out var duplicate, access, false, 0))
        {
            return null;
        }

        if (typeName == "File")
        {
            // Ownership of the duplicate passes to the resolver.
            var fileName = files.Resolve(duplicate);
            return fileName is null ? null : HandleNames.Format(typeName, fileName);
        }

        try
        {
            switch (typeName)
            {
                case "Process":
                    var pid = Kernel32.GetProcessId(duplicate);
                    return pid == 0 ? null : string.Create(CultureInfo.InvariantCulture, $"PID {pid}");

                case "Thread":
                    var tid = Kernel32.GetThreadId(duplicate);
                    var owner = Kernel32.GetProcessIdOfThread(duplicate);
                    return tid == 0 ? null : string.Create(CultureInfo.InvariantCulture, $"TID {tid} (PID {owner})");

                default:
                    var name = HandleNames.QueryObjectName(duplicate);
                    return name is null ? null : HandleNames.Format(typeName, name);
            }
        }
        finally
        {
            Kernel32.CloseHandle(duplicate);
        }
    }
}
