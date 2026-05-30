# Product overview

`[REPO]` What KOTORModSync is, who it serves, and how work flows through the repo. For user FAQ and build steps, see `README.md` (marked WIP in places).

## What it is

KOTORModSync is a **cross-platform multi-mod installer** for *Star Wars: Knights of the Old Republic* (K1) and *The Sith Lords* (K2/TSL). It reads **instruction files** (TOML and related formats) that describe how each mod is installed, resolves **dependencies and incompatibilities**, and executes the steps (extract, patch, move, delete, and so on) against a KOTOR game directory.

`[SYNTH]` The product sits between **mod-build authors** (who encode install logic once) and **players** (who pick mods and run install/validate without hand-copying files or running TSLPatcher manually for every mod).

## Problem it solves

KOTOR modding often requires dozens of repetitive, order-sensitive steps per mod. KOTORModSync automates that pipeline and encodes compatibility rules so end users can select mods and let the tool order and execute installs. It also supports **HoloPatcher on Mac/Linux without Wine** (`README.md`).

## Audiences

| Audience | Goal | Typical entry |
|----------|------|----------------|
| **Players** | Install a curated mod list from an instruction file | GUI install wizard or legacy Getting Started tab |
| **Mod-build authors** | Author or edit instruction files with dependency graphs | GUI editor or hand-edited TOML |
| **Maintainers / agents** | Validate full builds, run CI, ship releases | Core CLI, `scripts/agents/*`, headless tests |

## Primary workflows

| Workflow | Description | Agent path |
|----------|-------------|------------|
| **Install wizard** | Load instruction file → select mods → download → validate → install | Desktop GUI + preload args; see [local desktop runbook](../local_desktop_agent_runbook.md) |
| **Legacy Getting Started** | Top-level tab: set directories, fetch downloads, validate | Same desktop session; parallel to wizard |
| **Headless validate** | Run unified validation pipeline on a TOML | `scripts/agents/cli_validate.sh` → Core `validate` |
| **Headless install** | Install selected (or all) components | Core `install`; see [cli-selection-semantics.md](cli-selection-semantics.md) |
| **Full-build testing** | `KOTOR1_Full.toml` / `KOTOR2_Full.toml` from `./mod-builds` | GUI full-build flow or `install_best_effort.sh` (long-running) |
| **Mod-builds pipeline** | Merge community markdown + TOML, export, dry-run | `scripts/agents/cli_full_build_pipeline.sh` |

`[REPO]` The app does **not** download every mod automatically in the legacy flow; users or agents must populate `<<modDirectory>>` (archives on disk or via download step in the wizard).

## External dependencies

| Dependency | Role |
|------------|------|
| **`./mod-builds`** | Agent clone of `th3w1zard1/mod-builds` for full-build TOMLs |
| **HoloPatcher** | Linux: `vendor/bin/HoloPatcher_linux` symlinked into GUI `Resources/` |
| **Nexus API key** | Many full-build downloads; env `KOTOR_MODSYNC_NEXUS_API_KEY` or CLI flags |
| **`telemetry-auth/`** | Optional Python sidecar for OTLP signing — not part of Avalonia workflows |

## Technology stack (summary)

| Layer | Stack |
|-------|--------|
| Core | C# .NET Standard 2.0 — instructions, VFS, validation, CLI |
| GUI | Avalonia UI v11, .NET 9 (and legacy net48 Windows builds) |
| Tests | Single project `KOTORModSync.Tests` (NUnit + xUnit) |
| Instruction formats | TOML, Markdown, YAML, JSON, XML (`FileLoadingService.cs`) |

## Rework disclaimer

`[REPO]` `README.md` states the project is **mid large-scale rework**. Treat README usage/FAQ as helpful but possibly stale. Prefer this knowledgebase, `AGENTS.md`, and runbooks for agent routing.

## Related

- [instruction-format.md](instruction-format.md) — authoring instruction files
- [mod-component-model.md](mod-component-model.md) — component fields and selection
- [validation-pipeline.md](validation-pipeline.md) — validate before install
- [agent-action-parity.md](agent-action-parity.md) — GUI vs CLI coverage
- [doc-hierarchy.md](doc-hierarchy.md) — which doc wins when guidance conflicts
