---
title: Full-build serialization round-trip and CLI dry-run parity
type: docs
status: active
date: 2026-05-29
---

# Full-build serialization round-trip and CLI dry-run parity

## Summary

Close gaps between documented mod-builds workflows and Core implementation: implement missing XML serialize/deserialize, add VFS dry-run to Core `validate`, and add automated round-trip tests for canonical KOTOR1/KOTOR2 full builds across TOML/JSON/YAML/XML.

---

## Problem Frame

`mod-builds` ships human markdown (`content/k*/full.md`) and machine instruction TOMLs (`TOMLs/KOTOR*_Full.toml`). Core CLI advertises `xml` output but `ModComponentSerializationService` throws for XML. GUI `ValidatePage` runs `DryRunValidator` (VFS simulation); Core `validate` only runs per-component `ComponentValidation`. Agents and headless CI cannot prove full-build fidelity without format round-trips and dry-run CLI parity.

Full install/dry-run accuracy with real archives remains environment-dependent; tests must not be weakened when archives are absent.

---

## Requirements

- R1. Serialize and deserialize mod components as XML (parity with JSON path).
- R2. Canonical `KOTOR1_Full.toml` and `KOTOR2_Full.toml` round-trip through TOML, JSON, YAML, and XML without component-count loss.
- R3. Core `validate --dry-run` wires `DryRunValidator.ValidateInstallationAsync` with `--game-dir` and `--source-dir`.
- R4. Agent script `cli_validate.sh` forwards `--dry-run`.
- R5. Document `--dry-run` and XML support in `docs/knowledgebase/core-cli-reference.md`.
- R6. Add `FullBuildSerializationRoundTripTests` using repo-root `./mod-builds` paths.
- R7. Extend synthetic round-trip tests in `TestSerialization.cs` to include XML.

---

## Scope Boundaries

- **In scope:** Core serialization, CLI validate dry-run, tests, agent script, KB doc updates.
- **Out of scope:** Fixing markdown/TOML semantic parity (186 vs 189 K1 components), unrelated GUI wizard changes, full LongRunning install with all mod archives.
- **Deferred:** Re-enabling excluded `DocumentationRoundTripTests.cs`; full install LongRunning gate with real downloads.

---

## Implementation Units

### U1. XML serialization

**Files:** `src/KOTORModSync.Core/Services/ModComponentSerializationService.cs`

- Add `SerializeModComponentAsXmlString` / `DeserializeModComponentFromXmlString` via JSON intermediate + Newtonsoft XML helpers.
- Add `case "xml"` to serialize/deserialize switches and auto-detect fallback chain.

### U2. CLI dry-run

**Files:** `src/KOTORModSync.Core/CLI/ModBuildConverter.cs`

- Add `--dry-run` to `ValidateOptions`.
- Require `--game-dir` and `--source-dir`; set `MainConfig`; sync `IsSelected` from validation subset; call `DryRunValidator`.

### U3. Tests

**Files:**
- Create: `src/KOTORModSync.Tests/FullBuildSerializationRoundTripTests.cs`
- Modify: `src/KOTORModSync.Tests/TestSerialization.cs`

### U4. Agent script and docs

**Files:**
- Modify: `scripts/agents/cli_validate.sh`
- Modify: `docs/knowledgebase/core-cli-reference.md`

---

## Verification

```bash
dotnet build KOTORModSync.sln --configuration Debug
dotnet test src/KOTORModSync.Tests/KOTORModSync.Tests.csproj \
  --filter "FullyQualifiedName~FullBuildSerializationRoundTripTests|FullyQualifiedName~SerializationRoundTripTests" \
  --configuration Debug
./scripts/agents/cli_validate.sh --input ./mod-builds/TOMLs/KOTOR1_Full.toml \
  --game-dir ./tmp/kotor_template --source-dir ./tmp/mod_downloads --dry-run
```

---

## Success Criteria

- XML round-trip passes for canonical full-build TOMLs (K1 and K2).
- CLI `validate --dry-run` executes VFS dry-run and reports issues (exit non-zero on dry-run errors).
- KB and agent script document the new flag.
- No weakening of validation when mod archives are missing.
