# Agent Instructions for KOTORModSync

This document provides guidance for AI agents working on the KOTORModSync codebase.

## Project Overview

KOTORModSync is a multi-mod installer for KOTOR (Knights of the Old Republic) games. It automates mod installation with dependency resolution, TSLPatcher support (including Mac/Linux without Wine), and a TOML-based instruction format. The project is currently undergoing a large-scale rework.

## Architecture

| Project | Purpose |
|---------|---------|
| **KOTORModSync.GUI** | Main application; uses AvaloniaUI v11 for UI. Buttons, windows, dialogs, and controls. |
| **KOTORModSync.Core** | Core logic; targets .NET Standard 2.0. |
| **KOTORModSync.Tests** | All tests live here. Do not create additional test projects. |
| **KOTORModSync.ConsoleApp** | Developer tools for quick feature testing. |
| **HoloPatcher** | Vendor dependency (submodule at `vendor/HoloPatcher.NET`). Used for patching mods. |

**Build:** Run from solution root: `dotnet build` then `dotnet run` inside `KOTORModSync.GUI`, or build `KOTORModSync.GUI` directly.

**Vendor deps:** Run `git submodule update --init --recursive` after cloning.

---

## Critical Conventions

### UI / XAML (AvaloniaUI)

- **Never** specify font, style, or color directly on UI elements. Omit them so theme defaults apply.
- **Do not** use styling classes unless absolutely necessary.
- **ZIndex is NOT valid in AvaloniaUI** — anything related to z-index is incorrect and unusable. Do not use it.

### Path Sandboxing & Security

- All instruction Source/Destination fields **must** start with `<<modDirectory>>` or `<<kotorDirectory>>`.
- No absolute paths in instruction definitions (prevents malicious TOML from targeting system dirs).
- Use placeholders: `<<modDirectory>>\filename*.zip` or `<<kotorDirectory>>/Override`.
- Exceptions: Choose actions may not require these prefixes; internal code can resolve to absolute paths as needed.

### Virtual File System

- **VirtualFileSystemProvider** tracks file state during instruction execution (create, move, delete, rename).
- Used for dry-run validation and download analysis.
- **Must** be initialized with `InitializeFromRealFileSystemAsync()` before use.
- For validation/analysis: use **VirtualFileSystemProvider only**, never RealFileSystemProvider.
- See `ExecuteInstructionsAsync` for the instruction execution flow.

### Path Resolution

- `<<modDirectory>>` / `<<kotorDirectory>>` are replaced at install or dry-run time via `SetRealPaths()`.
- `EnumerateFilesWithWildcards()` resolves `*` and `?` via the active file system provider.

---

## Testing

### Test Project

- All tests go in **KOTORModSync.Tests**. Do not create additional test projects.

### Test Naming Conventions

| Suffix | Purpose | When to Use |
|--------|---------|-------------|
| **GitHubRunnerSeeding** | 5–6 hour seeding tests for GitHub Actions | Only for CI seeding workflows |
| **LongRunning** | Tests >2 min, not for GitHub | Regular long tests; never combine with "Seeding" |
| (none) | Normal tests | Complete in under 2 minutes |

**Rules:**

- Do **not** use `GitHubRunnerSeeding` for non-seeding tests.
- Do **not** use `LongRunning` with "Seeding" in the name.
- Workflow filter for seeding: `FullyQualifiedName~GitHubRunnerSeeding`.

### Running Tests

**Distributed cache tests** (use exactly this command):

```
dotnet test KOTORModSync.Tests/KOTORModSync.Tests.csproj --filter "FullyQualifiedName~DistributedCache&FullyQualifiedName!~LongRunning&FullyQualifiedName!~GitHubRunnerSeeding"
```

**General tests** (prefer this PowerShell pattern for timeouts):

```pwsh
pwsh -Command '& {
    $proj = ''KOTORModSync.Tests/KOTORModSync.Tests.csproj''
    $args = ''test {0} --filter "FullyQualifiedName~<test_name>" --list-tests'' -f $proj
    # ... (see .cursorrules for full timeout/process handling)
}'
```

- 120000 ms: normal vs slightly longer tests; use for a **single** test when profiling.
- 600000 ms: slightly long vs long tests.

---

## Key Concepts

### Instruction File Format (TOML)

- Fields: `InstallBefore`, `InstallAfter`, `Dependencies`, `Restrictions` (for dependency/compatibility).
- Examples: Ultimate Character Overhaul, Handmaiden/Disciple Same-Gender Romance Mod.
- See <https://pastebin.com/7gML3zCJ> for field explanations.

### NuGet

- Sources in `NuGet.config` at repo root (nuget.org + GitHub Packages for `th3w1zard1`).

---

## Common Workflows

1. **Adding a new instruction type:** Extend core logic in `KOTORModSync.Core`, keep paths sandboxed.
2. **UI changes:** Edit Avalonia XAML/controls; avoid font/style/color and ZIndex.
3. **Dry-run / validation:** Ensure VirtualFileSystemProvider is used, not RealFileSystemProvider.
4. **New tests:** Add to `KOTORModSync.Tests`; use correct suffix for long or seeding tests.

---

## File Locations

- Main GUI: `src/KOTORModSync.GUI/`
- Core logic: `src/KOTORModSync.Core/`
- Tests: `src/KOTORModSync.Tests/`
- Vendor (HoloPatcher): `vendor/HoloPatcher.NET/`
- Workflow for distributed cache tests: `.github/workflows/distributed-cache-tests.yml`

---

## Local desktop agent workflow

Use this section when a task touches the Avalonia desktop GUI, the install wizard, full-build validation against `mod-builds`, or any manual test that needs a real desktop session instead of headless tests.

Start with:

- `docs/local_desktop_agent_runbook.md`
- `.cursor/skills/local_desktop_gui_testing/SKILL.md`
- `.cursor/skills/full_build_install_validation/SKILL.md`

### Verified local desktop baseline

These steps were verified in a Linux desktop VM similar to the one used during development:

- OS: Ubuntu 24.04 x64
- .NET SDK: 9.0.x
- GUI display: X11 desktop session with `DISPLAY=:1`
- App launch style: prebuilt DLL with CLI preload args, not file pickers

Verified preload flags:

- `--instructionFile=<path>`
- `--kotorPath=<path>`
- `--modDirectory=<path>`

The GUI auto-loads the instruction file when those arguments are present.

### Use the repo scripts

Prefer these scripts over ad hoc shell commands:

- `scripts/agents/create_template_kotor_install.sh`
- `scripts/agents/ensure_linux_holopatcher.sh`
- `scripts/agents/launch_gui_desktop.sh`

Typical local desktop flow:

1. Clone `mod-builds` into the repo root if missing.
2. Create a fake/template KOTOR install and empty mod workspace.
3. Build the GUI project.
4. Ensure Linux `Resources/holopatcher` exists.
5. Launch the GUI with CLI preload args.
6. Drive the wizard manually in the desktop session.

### GUI testing rules

- Anything GUI-facing must be exercised in a real desktop session, not only headless tests.
- Prefer CLI preload args over native file-picker interaction.
- For wizard tests, use the install wizard pages instead of the legacy top-menu flow unless the task explicitly targets the legacy flow.
- Expand validation logs and capture the exact failure text before changing code.
- For full-build tests, clone `mod-builds` to the repo root. The repo expects that location.

### Project-specific wizard control map

#### Directory + onboarding flow

- `GettingStartedTab`
  - `Step1ModDirectoryPicker`
  - `Step1KotorDirectoryPicker`
  - `Step2Button` (`📄 Load Instruction File`)
  - `ScrapeDownloadsButton` (`Fetch Downloads`)
  - `DownloadStatusButton`
  - `StopDownloadsButton`
  - `ValidateButton` (`🔍 Validate`)

#### Install wizard flow

Wizard pages are created in this order:

1. `LoadInstructionPage`
2. `WelcomePage`
3. optional `PreamblePage`
4. `ModDirectoryPage`
5. `GameDirectoryPage`
6. optional `AspyrNoticePage`
7. `ModSelectionPage`
8. `DownloadsExplainPage`
9. `ValidatePage`
10. `InstallStartPage`
11. `InstallingPage`
12. `BaseInstallCompletePage`
13. `FinishedPage`

Widescreen-only pages are added dynamically after the base install when needed.

#### Key wizard controls

- `ModSelectionPage`
  - `SelectAllButton`
  - `DeselectAllButton`
  - `SelectByTierButton`
  - `SelectByCategoryButton`
  - `SearchTextBox`
  - `CategoryFilterComboBox`
  - `TierFilterComboBox`
  - `SpoilerFreeToggle`
  - `ExpandCollapseAllButton`
- `ValidatePage`
  - `ValidateButton` (`🔍 Run Validation`)
  - `ValidationProgress`
  - `LogExpander`
  - `LogText`
  - `SummaryText`
  - `ErrorCountBadge`
  - `WarningCountBadge`
  - `PassedCountBadge`
- `DownloadsExplainPage`
  - background downloads continue while the wizard advances
- `InstallStartPage`
  - review page before the real install begins

### Full-build workflow expectation

For `KOTOR1_Full.toml` / `KOTOR2_Full.toml` tests:

1. Launch the GUI with CLI preload args.
2. Go to `ModSelectionPage`.
3. Click `SelectAllButton`.
4. Go to the downloads step and click `Fetch Downloads`.
5. Open download status if needed.
6. Run validation from `ValidatePage`.
7. Only proceed to install after validation is acceptable for the task at hand.

### Linux-specific note

The plain Debug output can run the GUI, but local Linux validation/install checks may still require:

- `scripts/agents/ensure_linux_holopatcher.sh`

That script links the bundled Linux HoloPatcher binary into the `Resources` folder expected by the app.

### When new local runbook knowledge is discovered

Update all of the following together:

- `docs/local_desktop_agent_runbook.md`
- the relevant `.cursor/skills/*/SKILL.md`
- `.cursorrules` if the rule should always apply
- `.cursor/mcp.json` or the wrapper scripts if agent tooling changed
- `.vscode/tasks.json` / `.vscode/launch.json` if the launch flow changed
