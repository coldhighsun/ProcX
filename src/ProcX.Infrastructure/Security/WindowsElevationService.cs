using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using ProcX.Application.Security;

namespace ProcX.Infrastructure.Security;

/// <summary>
/// UAC-based elevation.
/// </summary>
public sealed class WindowsElevationService : IElevationService
{
    /// <summary>
    /// <c>ERROR_CANCELLED</c>: the user declined the UAC prompt.
    /// </summary>
    private const int ErrorCancelled = 1223;

    /// <inheritdoc />
    public bool IsElevated { get; } = DetermineIsElevated();

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The path of the current executable cannot be determined.</exception>
    /// <exception cref="Win32Exception">The process could not be started for a reason other than the user declining.</exception>
    public bool TryStartElevated(string arguments = "")
    {
        var path = Environment.ProcessPath
            ?? throw new InvalidOperationException("The path of the current executable cannot be determined.");

        try
        {
            using var process = Process.Start(new ProcessStartInfo(path, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether the current token is a member of the Administrators group; with UAC this is only
    /// true for an elevated token.
    /// </summary>
    /// <returns><see langword="true"/> if elevated.</returns>
    private static bool DetermineIsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
