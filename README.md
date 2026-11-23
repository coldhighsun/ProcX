# ProcX

An open-source replacement for the Windows Task Manager, built with WPF and .NET 11.

## Features

- Live process list: CPU, memory (private working set), commit size, threads, handles, priority, user, path
- Sorting, search (name, PID, user, path) and a parent/child **tree view**
- **End task** and **end process tree** (children first), with a warning for critical system processes
- Details panel: command line, architecture, elevation and integrity level, **threads** (with start-address
  symbols), loaded **modules** and open **handles**
- Set **priority** and **CPU affinity**
- Optional **replacement of the Windows Task Manager** (Ctrl+Shift+Esc opens ProcX)
- Light, dark or system theme and a configurable update speed
- Process identity is PID plus creation time, so a recycled PID is never mistaken for the original process

## Download

Each release has three files:

| File | Needs .NET installed? | Notes |
| --- | --- | --- |
| `ProcX-<version>-win-x64-setup.exe` | No | Installer with Start menu entry and uninstaller |
| `ProcX-<version>-win-x64-self-contained.zip` | No | Portable single `ProcX.exe`; the first start unpacks about 150 MB to `%TEMP%\.net` and takes longer |
| `ProcX-<version>-win-x64-framework-dependent.zip` | Yes, [.NET 11 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/11.0) | Portable single `ProcX.exe`, about 8 MB |

`SHA256SUMS.txt` lists the checksums. The binaries are not code-signed, so Windows SmartScreen may warn on first run.

Requirements: Windows 10 1809 or later / Windows 11, 64-bit.

## Replacing the Windows Task Manager

Turn on *Settings → Replace the Windows Task Manager*. ProcX then registers itself as the
[Image File Execution Options](https://learn.microsoft.com/windows-hardware/drivers/debugger/gflags-overview) debugger
for `taskmgr.exe`, so Ctrl+Shift+Esc and every other launch of Task Manager open ProcX.

- Changing it needs administrator rights; ProcX asks through a normal UAC prompt.
- While it is on, the original Task Manager cannot be started.
- If the registered file disappears (for example you moved or deleted ProcX), Task Manager stops opening at all.
  ProcX detects this at startup and offers to repair it. The installer's uninstaller turns the replacement off
  automatically when it points to the installed copy.
- To switch it off without ProcX, run this in an elevated PowerShell:

```powershell
Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\taskmgr.exe' -Name Debugger
```

## Settings

Stored in `%LocalAppData%\ProcX\settings.json`. Delete the file to reset everything.

## Build

Requires the .NET 11 SDK (see `global.json`).

```powershell
dotnet build
dotnet test
dotnet run --project src/ProcX.App
```

### Release files

```powershell
./build/publish.ps1                 # all three files into artifacts/<version>/
./build/publish.ps1 -SkipInstaller  # only the two zip files
```

The installer is built with [Inno Setup 7](https://jrsoftware.org/isinfo.php)
(`winget install JRSoftware.InnoSetup.7`); pass `-IsccPath` if `ISCC.exe` is not in a default location.
The version comes from `Directory.Build.props`.

## Project layout

| Project | Purpose |
| --- | --- |
| `ProcX.Domain` | Pure models and algorithms (process identity, tree building, CPU usage) |
| `ProcX.Application` | Service abstractions, sampling and diffing, settings |
| `ProcX.Infrastructure` | Win32 / NT API implementations, registry, JSON settings |
| `ProcX.App` | WPF user interface and composition root |
| `build/` | Release script and Inno Setup installer script |

## Known limitations

- CPU affinity is not supported on systems with more than 64 logical processors (several processor groups).
- No application icon yet.
- The user interface is English only.

## License

[MIT](LICENSE)
