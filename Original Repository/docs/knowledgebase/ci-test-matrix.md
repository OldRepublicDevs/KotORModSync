# CI test matrix vs local runs

`[REPO]` GitHub Actions does **not** run the same filter as `./scripts/agents/run_headless_tests.sh`.

## Local default (agents)

```bash
./scripts/agents/run_headless_tests.sh
# Equivalent filter: FullyQualifiedName!~LongRunning
```

Runs the **full** test project (minus `LongRunning` suffix if any exist). Longer and broader than CI.

## CI smoke subsets

### `build-and-test.yml` / `push-integration.yml` (Linux)

| Step filter | Classes exercised |
|-------------|-------------------|
| `FullyQualifiedName~InstallCoordinatorTests` | Install coordination |
| `FullyQualifiedName~CliInstallIntegrationTests` | CLI install pipeline |
| `FullyQualifiedName~DownloadQueueHeadlessTests` | Download queue (push-integration only) |
| `FullyQualifiedName~InstallingPageHeadlessTests\|FullyQualifiedName~WizardFlowHeadlessTests` | Wizard headless (push-integration only) |
| `FullyQualifiedName~MarkdownTomlParityTests` | Markdown/TOML parity (push-integration only) |
| `FullyQualifiedName~AutoUpdateServiceTests` | Auto-update service |
| `FullyQualifiedName~AutoUpdateClientHeadlessTests\|FullyQualifiedName~AutoUpdateServiceHeadlessTests\|FullyQualifiedName~HeadlessBootstrapTests` | Headless bootstrap / client |
| `FullyQualifiedName~SelfUpdateIntegrationTests` | Self-update integration |

### `code-cleanup.yml`

- `InstallCoordinatorTests`, `DownloadQueueHeadlessTests`

### `mod-build-validation.yml`

- `FullyQualifiedName~DocumentationRoundTripTests` (after cloning community mod-builds markdown)

## Implications for agents

| Claim | Accurate? |
|-------|-----------|
| “CI passed” = every test in the project ran | **No** — subset only |
| `run_headless_tests.sh` matches CI | **No** — local is wider |
| Green CI + green local script = strong signal | **Yes** — run both before large merges |

## Related

- [removed-features.md](removed-features.md) — no `DistributedCache` CI job
- [scripts/agents/README.md](../../scripts/agents/README.md)
