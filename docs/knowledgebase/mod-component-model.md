# Mod component model

`[REPO]` Fields on `ModComponent` and `Instruction`, plus how selection works in GUI vs CLI.

Sources: `ModComponent.cs`, `Instruction.cs`, [cli-selection-semantics.md](cli-selection-semantics.md).

## ModComponent overview

Each **mod component** is one installable mod (or mod group) in an instruction file. Components are identified by **Guid** and ordered by the dependency resolver using graph fields and user selection.

### Identity and presentation

| Field | Purpose |
|-------|---------|
| `Guid` | Stable identifier; used in Dependencies, Restrictions, InstallBefore/After |
| `Name` | Display name (may contain spoiler markup) |
| `NameSpoilerFree` | Spoiler-free display variant |
| `Heading`, `Author` | Metadata for UI and docs |
| `Tier` | Sort/filter tier (e.g. Essential, Recommended) |
| `Category` | List of category tags for filtering |
| `Description`, `Directions`, `DownloadInstructions`, … | Rich text fields (+ `*SpoilerFree` variants) |
| `UsageWarning`, `KnownBugs`, `CompatibilityWarning`, `SteamNotes`, … | Warnings shown in UI |

### Install graph

| Field | Type | Meaning |
|-------|------|---------|
| `Dependencies` | `List<Guid>` | Required mods must be selected and installed first |
| `Restrictions` | `List<Guid>` | Incompatible mods — cannot be selected together |
| `InstallBefore` | `List<Guid>` | Ordering: this mod runs before listed GUIDs |
| `InstallAfter` | `List<Guid>` | Ordering: this mod runs after listed GUIDs |
| `DependencyNames` | `List<string>` | Human-readable dependency names (serialization aid) |

`[SYNTH]` **Dependencies** = hard requires. **Restrictions** = mutual exclusion. **InstallBefore/After** = ordering hints beyond strict dependencies — see README/pastebin for author guidance.

### Selection and install state

| Field | Purpose |
|-------|---------|
| `IsSelected` | Whether the mod is chosen for install/validate (GUI and optional CLI) |
| `IsDownloaded` | Download step completed |
| `IsValidating` | UI transient flag during validation |
| `WidescreenOnly` | Triggers dynamic widescreen wizard pages when selected |
| `AspyrExclusive` | K2 Aspyr-specific content flag |

### Instructions and options

| Field | Purpose |
|-------|---------|
| `Instructions` | Ordered list of `Instruction` steps executed for this mod |
| `Options` | Optional sub-choices (`Choose` action branches into option instruction lists) |

### Runtime / cache (agents rarely edit)

| Field | Purpose |
|-------|---------|
| `ContentKey`, `MetadataHash` | Cache and content-addressing helpers |

## Instruction row model

Each entry in `ModComponent.Instructions` (`Instruction.cs`):

| Field | Purpose |
|-------|---------|
| `Action` / `ActionString` | One of `ActionType` values (see [instruction-format.md](instruction-format.md)) |
| `Source` | Path under `<<modDirectory>>` or `<<kotorDirectory>>` |
| `Destination` | Target path (same placeholder rules) |
| `Arguments` | Extra args for Execute/Patcher/Run |
| `Overwrite` | Whether to overwrite existing files |
| `Dependencies`, `Restrictions` | Optional per-instruction graph overrides |

## Selection semantics (GUI vs CLI)

`[REPO]` Full detail: [cli-selection-semantics.md](cli-selection-semantics.md).

| Context | Default behavior |
|---------|------------------|
| **GUI Mod Selection** | User toggles `IsSelected`; Validate uses selected only |
| **`install` (CLI)** | Without `--use-file-selection`: **all** components forced selected (like Select All) |
| **`install` + `--use-file-selection`** | Only TOML `IsSelected = true` |
| **`validate` (CLI)** | Default: **all** components; with `--use-file-selection`: TOML-selected only |
| **`install` pre-check** | Validates only `IsSelected == true` (wizard-equivalent) unless `--skip-validation` |

Agents running full-build TOMLs that ship with `IsSelected = false` should use default `install` (select-all) or explicit `--select` filters — not assume file flags alone.

## Widescreen block

`[REPO]` When any selected component has `WidescreenOnly = true`, the install wizard inserts extra pages after base install (`WidescreenNoticePage`, `WidescreenModSelectionPage`, `WidescreenInstallingPage`, `WidescreenCompletePage`). **No CLI equivalent** — see [agent-action-parity.md](agent-action-parity.md).

## Related

- [instruction-format.md](instruction-format.md) — action types and path rules
- [cli-selection-semantics.md](cli-selection-semantics.md) — flags and presets
- [validation-pipeline.md](validation-pipeline.md) — what gets validated for selected mods
- [agent-action-parity.md](agent-action-parity.md) — GUI-only gaps
