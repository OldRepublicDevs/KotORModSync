# Mod-builds full pipeline — requirements

**Date:** 2026-05-29  
**Status:** active  
**Related plan:** `docs/plans/2026-05-29-017-full-build-roundtrip-dryrun-plan.md`

## Outcome

Agents and CI can load canonical KOTOR full builds from `mod-builds`, round-trip them through TOML/JSON/YAML/XML without losing component fidelity, and run headless **dry-run** validation that matches GUI VFS behavior.

## Success criteria

1. `mod-builds/TOMLs/KOTOR1_Full.toml` and `KOTOR2_Full.toml` deserialize and re-serialize to TOML, JSON, YAML, and XML with stable component counts and names.
2. Core `validate --dry-run` runs `DryRunValidator` when `--game-dir` and `--source-dir` are provided.
3. `scripts/agents/cli_validate.sh` forwards `--dry-run`.
4. Automated tests cover synthetic and full-build round-trips including XML.

## Scope boundaries

**In scope:** Core serialization, CLI dry-run wiring, tests, agent script, KB docs.

**Out of scope:** Fixing markdown/TOML semantic parity (186 vs 189 K1 components); unrelated GUI wizard work; full LongRunning install with all mod archives on CI.

**Deferred:** Auto-instruction generation from markdown-only sources; full install gate with real downloads.

## Canonical sources

| User label | Path |
|------------|------|
| KOTOR1 full markdown | `mod-builds/content/k1/full.md` |
| KOTOR2 full markdown | `mod-builds/content/k2/full.md` |
| KOTOR1 full TOML | `mod-builds/TOMLs/KOTOR1_Full.toml` |
| KOTOR2 full TOML | `mod-builds/TOMLs/KOTOR2_Full.toml` |

Machine instructions and GUIDs live in TOML; markdown carries human metadata. Lossless install fidelity uses **TOML as the instruction source** for validate/dry-run/install (not markdown-only import).

## Assumptions

- `mod-builds` submodule or clone is present at repo root for local/CI tests; tests skip gracefully when files are missing.
- Dry-run with empty template directories validates structure and VFS paths; archive presence remains environment-dependent.
- XML round-trip uses JSON intermediate (same as existing Core design).

## Non-goals

- Re-enabling excluded documentation round-trip tests in one pass.
- Resolving all Nexus/download dependencies in CI.
