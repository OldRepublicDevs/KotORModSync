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
