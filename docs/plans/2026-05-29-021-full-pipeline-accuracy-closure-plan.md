---
title: Full pipeline accuracy closure
type: feat
status: completed
date: 2026-05-29
origin: docs/brainstorms/2026-05-29-mod-builds-pipeline-requirements.md
---

# Full pipeline accuracy closure

## Summary

Close remaining gaps after slices 1–4 so agents can run the mod-builds full pipeline with documented path aliases (`KOTOR1_FULL.md` → `content/k1/full.md`), prove merge `--auto-generate-local` through the Core CLI entry point, and harden the agent script for dry-run flag conflicts.

## Problem Frame

PR #91 delivered round-trip, merge, dry-run-only, and install wiring. Users still refer to `KOTOR1_FULL.md` / `KOTOR2_FULL.md` while mod-builds canonical paths are `content/k*/full.md`. Issue #93 tracks missing CLI-path coverage for `merge --auto-generate-local`. Full install with all archives remains environment-dependent but must be one documented command.

## Requirements

- R1. Document and resolve user-facing markdown aliases (`KOTOR1_FULL.md`, `KOTOR2_FULL.md`) to canonical `mod-builds/content/k*/full.md` in agent script help and KB.
- R2. Add integration test invoking `ModBuildConverter.Run` for `merge --auto-generate-local --source-path` with synthetic archive (closes #93).
- R3. `cli_full_build_pipeline.sh` warns when both `--dry-run` and `--dry-run-only` are set; `--dry-run-only` wins for validate.
- R4. Update brainstorm requirements with alias table and full-run command sequence.
- R5. (Deferred) `FullBuildInstallLongRunning` with complete archive set on CI.

## Scope Boundaries

- **In scope:** Tests, agent script, KB, brainstorm.
- **Out of scope:** Godot holocron plugin; mod-builds upstream renames.
- **Deferred:** CI download of all Nexus archives.

## Implementation Units

### U1. CLI merge auto-generate integration test

**Files:** `src/KOTORModSync.Tests/ModBuildConverterCliIntegrationTests.cs`

### U2. Agent script aliases + dry-run warning

**Files:** `scripts/agents/cli_full_build_pipeline.sh`, `scripts/agents/README.md`

### U3. KB + brainstorm

**Files:** `docs/knowledgebase/core-cli-reference.md`, `docs/brainstorms/2026-05-29-mod-builds-pipeline-requirements.md`

## Verification

```bash
dotnet test src/KOTORModSync.Tests/KOTORModSync.Tests.csproj \
  --filter "Name~ModBuildConverterCliIntegrationTests|Name~AutoGenerateLocalCliIntegrationTests|Name~FullBuildMergedDryRunTests|Name~FullBuildMarkdownMergeRoundTripTests|Name~FullBuildSerializationRoundTripTests"

./scripts/agents/cli_full_build_pipeline.sh --game k1 \
  --game-dir ./tmp/kotor_template --source-dir ./tmp/mod_downloads \
  --export-all-formats --dry-run-only
```
