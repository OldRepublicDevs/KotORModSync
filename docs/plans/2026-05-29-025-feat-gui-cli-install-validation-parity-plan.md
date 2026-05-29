---
title: "feat: GUI/CLI install validation parity"
type: feat
status: completed
date: 2026-05-29
origin: docs/knowledgebase/agent-action-parity.md
---

# feat: GUI/CLI install validation parity

## Summary

Close the install-time validation gap: CLI `install` (without `--skip-validation`) must run the same `InstallationValidationPipeline` preset as the wizard `ValidatePage`, not environment-only checks.

## Problem Frame

Architecture review and desktop testing confirmed GUI wizard validation uses `ValidationPipelineOptions.WizardFull` (environment, conflicts, order, archives, VFS dry-run on selected mods). CLI `install` only called `ValidateInstallationEnvironmentAsync`, allowing installs that the GUI would block.

## Requirements

- R1. CLI `install` without `--skip-validation` runs `InstallationValidationPipeline` with wizard-equivalent options and selection semantics.
- R2. `--skip-validation` and `install_best_effort.sh` behavior unchanged.
- R3. Regression test: restriction conflict fails install pre-check without `--skip-validation`.
- R4. Update `docs/knowledgebase/agent-action-parity.md` and `cli-selection-semantics.md`.

## Scope Boundaries

**In scope:** Install pre-check pipeline, tests, KB docs.

**Deferred:** MainWindow decomposition, duplicate wizard hosts, widescreen install batch API, validation UI mapper centralization.

## Implementation Units

### U1. Route CLI install through InstallationValidationPipeline

**Files:** `src/KOTORModSync.Core/CLI/ModBuildConverter.cs`

**Approach:** Replace environment-only block in `RunInstallAsync` with `WizardFull` pipeline; mirror validate selection flags (`UseFileSelection`, `--select`); wire `ConfirmationCallback` for HoloPatcher prompts.

### U2. Install validation parity test

**Files:** `src/KOTORModSync.Tests/ValidationPipelineParityTests.cs`

**Test scenarios:** Two selected mods with restriction conflict → `install` exit 1 without `--skip-validation`; same TOML with `--skip-validation -y` still succeeds when archives present.

### U3. KB documentation

**Files:** `docs/knowledgebase/agent-action-parity.md`, `docs/knowledgebase/cli-selection-semantics.md`

**Verification:** `dotnet test` filter `ValidationPipelineParityTests|CliInstallIntegrationTests`.
