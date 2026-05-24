# CLI selection semantics

`[REPO]` How `install` and `validate` treat component selection vs TOML `IsSelected` flags.

Source: `ModBuildConverter.ApplySelectionFilters` and validate/install handlers in `src/KOTORModSync.Core/CLI/ModBuildConverter.cs`.

## `install` without `--select` (default)

When `--select` is omitted and **`--use-file-selection` is not set**, **every component is marked selected** (`IsSelected = true`), regardless of TOML defaults.

This matches “Select All” on `ModSelectionPage` for full-build TOMLs that ship with `IsSelected = false`.

## `install` with `--use-file-selection`

Only components already marked `IsSelected = true` in the file are installed. Use after editing a TOML or when mirroring a partial GUI selection without `--select` filters.

## `install` with `--select`

Repeatable filters: `category:Name`, `tier:Name`. Only matching components stay selected.

## `validate` without `--select` (default)

Validates **all loaded components**. TOML `IsSelected` is **not** used unless `--use-file-selection` or `--select` is provided.

## `validate` with `--use-file-selection`

Validates only components with `IsSelected = true` in the file (matches the install wizard after Mod Selection).

## `validate` with `--select`

Applies the same category/tier filters, then validates only components with `IsSelected == true` after filtering.

## `--best-effort` (install)

`[REPO]` Implies `-y`, `ContinueOnMissingSources`, and `ContinueOnModFailure`.

If no Nexus API key is configured, **Nexus-only mods are deselected** (`DeselectComponentsWithNexusUrlsWithoutApiKey`). Agents may think “everything installable” ran; check logs for deselected Nexus mods.

## Nexus API key flag names

| Verb | Flag |
|------|------|
| `convert`, `merge` | `--nexus-mods-api-key` |
| `install` | `--nexus-api-key` (or env `KOTOR_MODSYNC_NEXUS_API_KEY` / `NEXUS_MODS_API_KEY`) |

## `install_best_effort.sh`

`[REPO]` Passes `--best-effort`, `-d`, `--concurrent`, and **`--skip-validation`** (not implied by `--best-effort` alone). Does not mirror GUI `ValidatePage` unless you run `validate` separately first.

## Related

- [core-cli-reference.md](core-cli-reference.md)
- [agent-action-parity.md](agent-action-parity.md)
