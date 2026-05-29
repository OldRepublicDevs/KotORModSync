---
title: Unify GUI and CLI validation pipeline
type: refactor
status: completed
date: 2026-05-29
origin: docs/brainstorms/2026-05-29-gui-cli-unified-pipeline-requirements.md
---

# Unify GUI and CLI validation pipeline

## Summary

Introduce a single Core `InstallationValidationPipeline` that performs the full validation sequence currently duplicated (and simplified) across `ValidatePage`, `MainWindow`, `ModBuildConverter.validate`, and `ValidationService`. Wire GUI and CLI exclusively through it.

## Problem Frame

Install paths already converge on `InstallationService.InstallAllSelectedComponentsAsync` (`InstallingPage`, CLI `install`). Validation does not: the wizard runs four bespoke steps then `DryRunValidator`; CLI runs `ComponentValidation` + optional dry-run with different defaults; `ValidationService` re-implements dry-run per component; legacy MainWindow skips pre-checks and calls `DryRunValidator` on all components.

## Requirements

- R1. Core pipeline service with staged results for UI rendering.
- R2. CLI `validate` calls pipeline only.
- R3. `ValidatePage` calls pipeline only (remove inline conflict/order duplication).
- R4. `MainWindow.RunValidationAsync` calls pipeline with selected-only semantics.
- R5. Remove or redirect `ValidationService` duplicate VFS loop.
- R6. Parity integration test + KB update.

## Scope Boundaries

**In scope:** `KOTORModSync.Core` pipeline, GUI validate surfaces, CLI handler, tests, `docs/knowledgebase/agent-action-parity.md`.

**Deferred:** Shell script refactor; STRATEGY.md.

## Implementation Units

### U1. Core `InstallationValidationPipeline`

**Files:** `src/KOTORModSync.Core/Services/Validation/InstallationValidationPipeline.cs`, `ValidationPipelineOptions.cs`, `ValidationPipelineResult.cs`

**Approach:** Single async `RunAsync` executing stages in wizard order; options mirror CLI flags (`FullValidation`, `DryRun`, `DryRunOnly`, `ErrorsOnly`, `CancellationToken`). Use existing `InstallationService.ValidateInstallationEnvironmentAsync`, `ModComponent.GetConflictingComponents`, `ModComponent.ConfirmComponentsInstallOrder`, `ComponentValidation`, `DryRunValidator.ValidateInstallationAsync`.

### U2. CLI wiring

**Files:** `src/KOTORModSync.Core/CLI/ModBuildConverter.cs`

**Approach:** Replace validate handler body with pipeline call; map exit codes from `ValidationPipelineResult`.

### U3. GUI wiring

**Files:** `src/KOTORModSync.GUI/Dialogs/WizardPages/ValidatePage.axaml.cs`, `src/KOTORModSync.GUI/MainWindow.axaml.cs`, `src/KOTORModSync.GUI/Services/ValidationService.cs`

**Approach:** ValidatePage maps pipeline stages to log lines; MainWindow uses pipeline with `FullValidation`+`DryRun` on selected components; ValidationService delegates to pipeline.

### U4. Tests and docs

**Files:** `src/KOTORModSync.Tests/ValidationPipelineParityTests.cs`, `docs/knowledgebase/agent-action-parity.md`, `docs/knowledgebase/cli-selection-semantics.md`

**Test scenarios:** Synthetic components — pipeline returns failure on missing dep; CLI `validate --full --dry-run --use-file-selection` same exit code as direct pipeline call.

## Verification

```bash
dotnet build KOTORModSync.sln -f net9.0
dotnet test src/KOTORModSync.Tests/KOTORModSync.Tests.csproj \
  -- RunConfiguration.TestCaseFilter="FullyQualifiedName~ValidationPipelineParityTests"
```
