## Cursor Cloud specific instructions

### Overview

KOTORModSync is a cross-platform multi-mod installer for Star Wars: KOTOR, built with C#/.NET 9.0 and AvaloniaUI. The solution (`KOTORModSync.sln`) contains three projects: `KOTORModSync.Core` (library), `KOTORModSync.GUI` (desktop app), and `KOTORModSync.Tests` (NUnit + xUnit tests).

### Prerequisites (installed via VM snapshot)

- .NET 9.0 SDK at `$HOME/.dotnet` (ensure `DOTNET_ROOT` and `PATH` include it)
- PowerShell (`pwsh`) for running tests per `.cursorrules` conventions
- X11 libraries for AvaloniaUI rendering (see README for list)
- Git submodules: `src/AvRichTextBox` and `src/RtfDomParserAvalonia` are initialized; `vendor/HoloPatcher.NET` is unavailable (private/not-found repo) but not required for building or testing the main solution

### Build, Test, Lint, Run

- **Build**: `dotnet build KOTORModSync.sln --configuration Debug` from repo root
- **Run GUI**: `dotnet run --project src/KOTORModSync.GUI/KOTORModSync.csproj --configuration Debug --framework net9.0` (must specify `--framework net9.0` since Debug can multi-target)
- **Lint**: `dotnet format KOTORModSync.sln --verify-no-changes` (pre-existing formatting diffs exist)
- **Tests**: See `.cursorrules` for the required PowerShell-based test runner pattern. Quick non-long-running test run: `dotnet test src/KOTORModSync.Tests/KOTORModSync.Tests.csproj --filter "FullyQualifiedName!~LongRunning&FullyQualifiedName!~GitHubRunnerSeeding&FullyQualifiedName!~DistributedCache" --configuration Debug`
- **Distributed cache tests**: `dotnet test KOTORModSync.Tests/KOTORModSync.Tests.csproj --filter "FullyQualifiedName~DistributedCache&FullyQualifiedName!~LongRunning&FullyQualifiedName!~GitHubRunnerSeeding"` (run from `src/` dir or adjust path)

### Non-obvious gotchas

- The `DISPLAY=:1` environment variable must be set for the AvaloniaUI GUI to render on the VM's virtual display.
- `CrossPlatformFileWatcherTests` fail in the cloud VM due to container filesystem inotify limitations; this is expected.
- Some xUnit-based UI tests may fail headlessly depending on Avalonia headless support; these are pre-existing.
- The NuGet config (`NuGet.config`) includes a GitHub Packages feed (`github-th3w1zard1`). Public packages restore without auth; if private packages are added, a GitHub PAT may be needed.
- `vendor/HoloPatcher.NET` submodule references a repo that currently returns 404. The build succeeds without it (only used in optional PostBuild copy targets).
