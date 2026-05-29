---
title: Ship PR #94 validation pipeline
type: feat
status: completed
date: 2026-05-29
origin: docs/plans/2026-05-29-023-unify-gui-cli-validation-pipeline-plan.md
---

# Ship PR #94 validation pipeline

## Summary

Close the validation unification slice on `feat/mod-builds-roundtrip-pipeline`: add regression coverage for environment fail-fast, document behavior in KB, mark pipeline brainstorm complete, run CI-relevant tests, and refresh PR #94 metadata.

## Problem Frame

`InstallationValidationPipeline` is wired across GUI and CLI. Residual review asked for a test proving environment failure does not report success, and explicit documentation of environment fail-fast (pipeline returns before dry-run when env fails).

## Requirements

- R1. Test: unset directories → `IsSuccess` false, environment stage failed, `DryRunResult` null.
- R2. KB note in `docs/knowledgebase/agent-action-parity.md` on environment fail-fast.
- R3. Mark `docs/brainstorms/2026-05-29-mod-builds-pipeline-requirements.md` completed.
- R4. Run plan 021 verification filter; exit 0.
- R5. Update PR #94 body residual section to reflect resolved items.

## Scope Boundaries

**In scope:** Test, docs, PR body, verification.

**Out of scope:** Download/merge GUI parity; merging PR; desktop GUI session.

## Implementation Units

### U1. Environment failure regression test

**Files:** `src/KOTORModSync.Tests/ValidationPipelineParityTests.cs`

**Test scenarios:** WizardFull without skip flags, null game/mod paths → not success, no dry-run result.

### U2. KB + brainstorm

**Files:** `docs/knowledgebase/agent-action-parity.md`, `docs/brainstorms/2026-05-29-mod-builds-pipeline-requirements.md`

### U3. Verification

```bash
dotnet test src/KOTORModSync.Tests/KOTORModSync.Tests.csproj -f net9.0 \
  --filter "FullyQualifiedName~ValidationPipelineParityTests"

dotnet test src/KOTORModSync.Tests/KOTORModSync.Tests.csproj -f net9.0 \
  --filter "Name~ModBuildConverterCliIntegrationTests|Name~AutoGenerateLocalCliIntegrationTests|Name~FullBuildMergedDryRunTests|Name~FullBuildMarkdownMergeRoundTripTests|Name~FullBuildSerializationRoundTripTests"
```
