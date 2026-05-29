# Agent action parity

`[REPO]` Maps user-visible flows to headless agent capabilities. Label key: **Full** = achievable without desktop; **Partial** = CLI/script with gaps; **UI** = desktop session required; **N/A** = out of scope.

## Install wizard (primary flow)

Wizard order from `src/KOTORModSync.GUI/Dialogs/InstallWizardDialog.axaml.cs` and `AGENTS.md`:

| Step | Page | User action | Agent path | Parity |
|------|------|-------------|------------|--------|
| 1 | `LoadInstructionPage` | Load TOML | `-i` on CLI; `--instructionFile=` on GUI | Full |
| 2 | `WelcomePage` | Continue | N/A (informational) | Full |
| 3 | `PreamblePage` | Read preamble | Optional; content in instruction file | Full |
| 4 | `ModDirectoryPage` | Pick mod dir | `-s` / `--modDirectory=` | Full |
| 5 | `GameDirectoryPage` | Pick game dir | `-g` / `--kotorPath=` | Full |
| 6 | `AspyrNoticePage` | Acknowledge (K2) | No CLI equivalent | UI |
| 7 | `ModSelectionPage` | Select mods, filters | `install` without `--select` = select all; `install --select category:X` / `tier:X` | Full (install); Partial (subset only with `--select`) |
| 8 | `DownloadsExplainPage` | Continue (downloads may run) | `install -d` or `convert -d` | Partial |
| 9 | `ValidatePage` | Run validation | `validate --full --dry-run --use-file-selection` (same Core `InstallationValidationPipeline` as GUI) | Full |
| 10 | `InstallStartPage` | Confirm install | `install -y` (runs `InstallationValidationPipeline` / `WizardFull` pre-check unless `--skip-validation`) | Full |
| 11 | `InstallingPage` | Watch progress | `install` (console progress) | Full |
| 12 | `BaseInstallCompletePage` | Continue | N/A | Full |
| 13+ | Widescreen pages | `WidescreenNoticePage`, `WidescreenModSelectionPage`, `WidescreenInstallingPage`, `WidescreenCompletePage` (dynamic) | No dedicated CLI | UI |
| 14 | `FinishedPage` | Done | N/A | Full |

## Legacy Getting Started tab

| Control | Agent path | Parity |
|---------|------------|--------|
| `Step1ModDirectoryPicker` | `--modDirectory=` / `-s` | Full |
| `Step1KotorDirectoryPicker` | `--kotorPath=` / `-g` | Full |
| `Step2Button` (load file) | `--instructionFile=` / `-i` | Full |
| `ScrapeDownloadsButton` | `install -d` or `convert -d` | Partial |
| `ValidateButton` | `validate --full --dry-run --use-file-selection` (via `InstallationValidationPipeline`) | Full |
| `OpenModDirectoryButton` | `ls` / file tools on mod dir | Full |
| Download status / stop | No first-class CLI | UI |

## Common agent workflows

| Goal | Recommended path |
|------|------------------|
| Smoke-test repo | `./scripts/agents/run_headless_tests.sh` |
| Validate TOML structure only | `./scripts/agents/cli_validate.sh --input path.toml` |
| Full validation | `cli_validate.sh` with `--game-dir`, `--source-dir`, `--full` |
| Validate only TOML-selected mods | `cli_validate.sh` … `--use-file-selection` (matches GUI Mod Selection) |
| Template dirs | `./scripts/agents/create_template_kotor_install.sh ./tmp/kotor_template ./tmp/mod_downloads` |
| GUI full-build check | `./scripts/agents/launch_gui_desktop.sh` + runbook wizard clicks `[UI]` |
| Long headless install | `./scripts/agents/install_best_effort.sh` (uses `--best-effort` + `--skip-validation`; see [cli-selection-semantics.md](cli-selection-semantics.md)) |
| Linux HoloPatcher for GUI | `./scripts/agents/ensure_linux_holopatcher.sh` |

## Headless tests as parity proxies

| Test area | Example filter | What it proves |
|-----------|----------------|----------------|
| CLI install | `CliInstallIntegrationTests`, `ValidationPipelineParityTests` | End-to-end install; install pre-check uses same pipeline as wizard |
| VFS validation | `VirtualFileSystemDryRunValidationTests` | Dry-run matches VFS rules |
| Wizard UI | `WizardFlowHeadlessTests` | Page flow without full desktop |
| Version alignment | `ReleaseVersionAlignmentTests` | Release metadata consistency |

## Gaps to respect in plans

1. **Widescreen block** — wizard-only; document when testing K2 widescreen mods `[UI]`.
2. **Download UX** — CLI can download; no equivalent to live download status UI.
3. **Rich text / spoilers** — GUI rendering; agents edit source TOML/markdown instead.
4. **File pickers** — never automate in cloud agents; always use preload args or CLI paths.
5. **Validate vs install selection** — GUI validates **selected** mods only; CLI defaults differ unless `--use-file-selection`. `install` without flags selects all (full-build). See [cli-selection-semantics.md](cli-selection-semantics.md).
6. **CI test coverage** — green CI runs subsets only; local `run_headless_tests.sh` is broader. See [ci-test-matrix.md](ci-test-matrix.md).
7. **Validation pipeline fail-fast** — `InstallationValidationPipeline` stops after environment failure (no conflict/order/archive/dry-run stages). GUI and CLI both use `ValidationPipelineResult.IsSuccess`; do not infer pass from an empty dry-run result.
8. **Install pre-check opt-out** — `install --skip-validation` and `install_best_effort.sh` skip the wizard-equivalent pipeline; default `install` does not.

See [agent-native-audit.md](agent-native-audit.md) for scored principles and [core-cli-reference.md](core-cli-reference.md) for flags.
