# ProcX

An open-source replacement for the Windows Task Manager, built with WPF and .NET 11.

## Status

Early development. Planned for the first release:

- Process list with live CPU / memory counters, sorting and search
- End process / end process tree
- Process tree view
- Process details: general info, threads, modules, handles
- Priority and CPU affinity
- Optional switch to replace the system Task Manager (Ctrl+Shift+Esc)

## Requirements

- Windows 10 1809 or later / Windows 11
- .NET 11 SDK (for building)

## Build

```powershell
dotnet build
dotnet test
dotnet run --project src/ProcX.App
```

## Project layout

| Project | Purpose |
| --- | --- |
| `ProcX.Domain` | Pure models and algorithms (no dependencies) |
| `ProcX.Application` | Service abstractions, sampling and diffing logic |
| `ProcX.Infrastructure` | Win32 / NT API implementations |
| `ProcX.App` | WPF user interface and composition root |

## License

[MIT](LICENSE)
