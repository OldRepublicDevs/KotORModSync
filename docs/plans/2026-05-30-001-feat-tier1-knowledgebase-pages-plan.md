---
title: Tier 1 domain knowledgebase pages
type: docs
status: completed
date: 2026-05-30
origin: ce-repo-research-analyst report (2026-05-30 session)
---

# Tier 1 domain knowledgebase pages

## Summary

Add four agent-facing knowledgebase pages that distill product intent and Core domain concepts without requiring source greps. Update the knowledgebase index so agents can route to instruction authoring, validation debugging, and onboarding tasks from `docs/knowledgebase/README.md`.

---

## Problem Frame

Plan `2026-05-24-016-agent-native-knowledgebase-plan.md` established routing, CLI parity, and audit docs. A follow-up repo research pass identified gaps: agents still grep `Instruction.cs`, `ModComponent.cs`, and `InstallationValidationPipeline.cs` for TOML schema, component fields, and validation stages. README is user-facing and marked WIP.

This slice adds **Tier 1 domain docs** only — no product code changes.

---

## Requirements

- R1. `product-overview.md` — what KOTORModSync is, audiences, primary workflows, rework disclaimer.
- R2. `instruction-format.md` — path placeholders, supported formats, action types, dependency fields, minimal examples.
- R3. `mod-component-model.md` — ModComponent/Instruction fields, options, widescreen, selection semantics (GUI vs CLI).
- R4. `validation-pipeline.md` — five stages, fail-fast rules, GUI/CLI flag mapping.
- R5. Update `docs/knowledgebase/README.md` topic index and read-order table for new pages.

---

## Scope Boundaries

- Tier 2 pages (`install-lifecycle.md`, `download-system.md`, etc.) are **deferred**.
- No edits to `.cursorrules`, `AGENTS.md`, or generated maps.
- Evidence labels (`[REPO]`, `[SYNTH]`, etc.) per existing KB convention.

---

## Key Technical Decisions

- Match existing KB page style: short sections, evidence labels, repo-relative links, tables where helpful.
- Cite canonical source paths; do not duplicate full CLI flag lists (link to `core-cli-reference.md`).
- Instruction examples use placeholder paths only (`<<modDirectory>>`, `<<kotorDirectory>>`).

---

## Implementation Units

### U1. Product overview page

**Goal:** Give agents a stable product frame without README WIP noise.

**Files:**
- Create: `docs/knowledgebase/product-overview.md`

**Approach:** Audiences table, problem statement, primary workflows (wizard, CLI, full-build), external deps summary, link to README for user FAQ only.

**Patterns to follow:** `docs/knowledgebase/vfs-vs-real-fs.md`, `docs/knowledgebase/agent-native-audit.md` (tone)

**Test expectation:** none — documentation only

**Verification:** Page links resolve; rework disclaimer present; evidence labels used.

### U2. Instruction format reference

**Goal:** Document TOML/instruction structure for authoring and fix agents.

**Files:**
- Create: `docs/knowledgebase/instruction-format.md`

**Approach:** Path sandbox rules, `ActionType` enum from `Instruction.cs`, relationship fields from README/ModComponent, minimal component+instruction examples, link to pastebin/README for advanced examples.

**Patterns to follow:** `.cursorrules` PATH SANDBOXING section, `FileLoadingService.cs` format list

**Test expectation:** none — documentation only

**Verification:** All 11 action types listed; placeholder rule stated; at least one minimal TOML example.

### U3. Mod component model

**Goal:** Glossary of ModComponent and Instruction fields plus selection semantics.

**Files:**
- Create: `docs/knowledgebase/mod-component-model.md`

**Approach:** Field tables for graph/selection/metadata; widescreen flags; CLI `--use-file-selection` vs default (link `cli-selection-semantics.md`).

**Patterns to follow:** `docs/knowledgebase/cli-selection-semantics.md`, `ModComponent.cs`

**Test expectation:** none — documentation only

**Verification:** Dependencies vs InstallBefore/After distinction clear; selection table matches `cli-selection-semantics.md`.

### U4. Validation pipeline reference

**Goal:** Stage-by-stage validation behavior for debug agents.

**Files:**
- Create: `docs/knowledgebase/validation-pipeline.md`

**Approach:** Document `ValidationPipelineStage` order, fail-fast on Environment, VFS in DryRun stage, map to `--full`, `--skip-environment-validation`, GUI `ValidatePage`.

**Patterns to follow:** `docs/knowledgebase/vfs-vs-real-fs.md`, `InstallationValidationPipeline.cs`

**Test expectation:** none — documentation only

**Verification:** All five stages documented; fail-fast rule explicit; links to VFS and CLI docs.

### U5. Index update

**Goal:** Discoverability from KB README.

**Files:**
- Modify: `docs/knowledgebase/README.md`

**Approach:** New "Domain model" subsection in topic index; extend read-order table rows for TOML authoring and validation debugging.

**Patterns to follow:** Existing topic index structure in same file

**Test expectation:** none — documentation only

**Verification:** All four new pages linked; read-order table has authoring and validation rows per research report suggestion.

---

## Verification

- Grep new files for absolute paths (must be none).
- Confirm internal markdown links resolve relative to `docs/knowledgebase/`.
- `git diff --stat` shows only `docs/knowledgebase/*` and this plan file.

---

## Success Criteria

- Agents authoring or fixing TOML can start at `instruction-format.md` without opening Core source.
- Agents debugging validation can start at `validation-pipeline.md` and follow links to VFS/CLI docs.
- KB README lists all Tier 1 pages in topic index and read-order table.
