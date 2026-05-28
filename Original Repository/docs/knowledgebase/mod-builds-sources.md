# mod-builds sources

`[REPO]` Two different “mod-builds” upstreams serve different workflows.

## Agent / local full-build clone

| Item | Value |
|------|--------|
| Remote | `https://github.com/th3w1zard1/mod-builds` |
| Location | Repo root `./mod-builds` |
| Typical TOML | `./mod-builds/TOMLs/KOTOR1_Full.toml` |
| Used by | `launch_gui_desktop.sh`, `cli_validate.sh`, `install_best_effort.sh`, agent runbooks |

Clone if missing:

```bash
git clone https://github.com/th3w1zard1/mod-builds ./mod-builds
```

## Scheduled documentation validation

| Item | Value |
|------|--------|
| Workflow | `.github/workflows/mod-build-validation.yml` |
| Content | Markdown from `KOTOR-Community-Portal/mod-builds` (not necessarily same tree as th3w1zard1 clone) |
| Tests | `DocumentationRoundTripTests` |

## Drift note

`[OPEN]` Folder layout under `th3w1zard1/mod-builds` may change; verify `TOMLs/` paths exist before scripting full-build flows.

## Related

- [local desktop agent runbook](../local_desktop_agent_runbook.md)
- [ci-test-matrix.md](ci-test-matrix.md)
