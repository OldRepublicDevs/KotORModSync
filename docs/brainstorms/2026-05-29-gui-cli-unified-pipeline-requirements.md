# GUI/CLI unified pipeline — requirements

**Date:** 2026-05-29  
**Status:** completed

## Outcome

Every install-wizard and legacy GUI validation/install action runs through the same Core pipeline entry points as `ModBuildConverter` CLI verbs. GUI code may only handle presentation (progress, dialogs, Avalonia); it must not duplicate validation steps or call alternate simulation paths.

## Success criteria

1. One Core service orchestrates full validation: environment → dependency/restriction conflicts → install order → per-component archive validation → VFS dry-run (`DryRunValidator`).
2. `ModBuildConverter` `validate` delegates to that service; no inline duplicate step logic in the CLI handler.
3. `ValidatePage` and legacy `MainWindow` validate button delegate to the same service with equivalent options (selected mods only, full + dry-run).
4. `ValidationService.AnalyzeValidationFailures` no longer runs a parallel per-component `ExecuteInstructionsAsync` loop; it uses the unified pipeline or is removed if unused.
5. Install already uses `InstallationService.InstallAllSelectedComponentsAsync` from GUI and CLI — documented and guarded by a test or comment contract; no second install path added in GUI.
6. KB `agent-action-parity.md` updated: validate parity is **Full** when CLI flags match wizard (`--full --dry-run --use-file-selection`).
7. Automated test proves CLI `validate --full --dry-run --use-file-selection` and direct pipeline call produce equivalent pass/fail for a synthetic fixture.

## Scope boundaries

**In scope:** Validation pipeline unification, CLI/GUI wiring, tests, KB.

**Out of scope:** Widescreen wizard-only pages; merge/convert UI (editor flows); download status UI; new STRATEGY.md (optional follow-up).

**Deferred:** `cli_full_build_pipeline.sh` calling a future `pipeline validate` subcommand instead of shell-composed flags; FullBuildInstallLongRunning.

## Assumptions

- Wizard validation target = **selected** components (`IsSelected`), matching `validate --use-file-selection`.
- Legacy Getting Started validate may validate all loaded components until migrated — document behavior change if narrowed to selected-only.

## Non-goals

- Rewriting Avalonia UI or wizard page order.
- Removing `ComponentValidation` class — it becomes a pipeline stage, not a CLI-only shortcut.
