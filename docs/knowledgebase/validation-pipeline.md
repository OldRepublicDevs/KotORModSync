# Validation pipeline

`[REPO]` Unified pre-install validation used by GUI (`ValidatePage`, legacy validate) and Core CLI `validate`. Real install uses a separate path via `InstallationService` / `InstallCoordinator`.

Source: `InstallationValidationPipeline.cs`, `ValidationPipelineResult.cs`, `ValidationPipelineOptions.cs`.

## Stages (in order)

`ValidationPipelineStage` enum:

| # | Stage | What it checks |
|---|--------|----------------|
| 1 | **Environment** | Game/mod directories, HoloPatcher availability, environment probes |
| 2 | **Conflicts** | Dependency and restriction violations in the selected set |
| 3 | **InstallOrder** | InstallBefore/After and ordering constraints |
| 4 | **ComponentValidation** | Archives present, per-component archive rules |
| 5 | **DryRun** | VFS simulation of instruction execution |

## Fail-fast behavior

`[REPO]` When **FullValidation** is enabled and **Environment** fails, the pipeline **stops** — later stages do not run. Same early exit if no components are selected for validation.

If **FullValidation** is off, only component archive checks (and optional dry-run flags) run — environment/conflict/order stages are skipped.

## VFS in DryRun stage

`[REPO]` The **DryRun** stage uses `VirtualFileSystemProvider`, not real disk writes. VFS must be initialized from the real file system first. See [vfs-vs-real-fs.md](vfs-vs-real-fs.md).

Do not use `RealFileSystemProvider` when answering “what would install do?” — use validation with `--dry-run`.

## Options presets

`ValidationPipelineOptions` maps to CLI and wizard behavior:

| Preset | FullValidation | DryRun | DryRunOnly | UseFileSelection | Typical caller |
|--------|----------------|--------|------------|------------------|----------------|
| `WizardFull` | yes | yes | no | yes | Install wizard **ValidatePage**; `install` pre-check |
| `LegacyDryRunOnly` | no | yes | no | yes | Legacy Getting Started validate |
| `CliFullWithDryRun` | yes | yes | no | yes | CLI `--full --dry-run --use-file-selection` |
| `CliDryRunOnly` | no | no | yes | yes | CLI `--dry-run-only` |

Other flags:

| Option | CLI / use |
|--------|-----------|
| `SkipEnvironmentValidation` | Tests, headless fixtures without HoloPatcher |
| `SkipComponentArchiveValidation` | Graph-only tests |
| `ErrorsOnly` | `--errors-only` |
| `UseFileSelection` | `--use-file-selection` (default true in options type; CLI defaults differ — see below) |

## CLI mapping

Wizard-equivalent validate:

```bash
./scripts/agents/cli_validate.sh --input path.toml \
  --game-dir ./tmp/kotor_template \
  --source-dir ./tmp/mod_downloads \
  --full --dry-run --use-file-selection
```

| CLI flag | Effect |
|--------|--------|
| `--full` | Enables Environment, Conflicts, InstallOrder stages |
| `--dry-run` | Runs VFS DryRun after component checks |
| `--dry-run-only` | Skips archive validation; VFS only |
| `--use-file-selection` | Only `IsSelected = true` components |
| `--select category:…` / `tier:…` | Filter then validate selected |
| `--skip-environment-validation` | Skip HoloPatcher/environment probe (tests, headless fixtures) |

Without `--full`, validate is lighter (archives + optional dry-run only). See [core-cli-reference.md](core-cli-reference.md) for all verbs.

## Install pre-check

`[REPO]` `install` runs `ValidationPipelineOptions.WizardFull` on **selected** components before installing unless `--skip-validation` is set. `install_best_effort.sh` always skips validation.

## GUI mapping

| Surface | Pipeline |
|---------|----------|
| `ValidatePage` / **Run Validation** | `WizardFull` on wizard-selected mods |
| Legacy **Validate** button | Often `LegacyDryRunOnly`-style dry-run |
| `install` (after validate) | Separate install orchestration; pre-check uses same pipeline as wizard |

Progress UI: `ValidationProgress`, `StatusText`, `LogExpander`, badge counts on `ValidatePage` (`AGENTS.md`).

## Debugging tips for agents

1. Expand validation logs; capture exact stage and message before changing code.
2. Environment failures → check HoloPatcher symlink ([holopatcher-resources.md](holopatcher-resources.md)) and `--kotorPath` / `--modDirectory`.
3. ComponentValidation failures → missing archive under `<<modDirectory>>`.
4. DryRun failures → instruction path or ordering; trace with VFS rules, not live disk.

## Related

- [vfs-vs-real-fs.md](vfs-vs-real-fs.md)
- [cli-selection-semantics.md](cli-selection-semantics.md)
- [core-cli-reference.md](core-cli-reference.md)
- [agent-action-parity.md](agent-action-parity.md)
