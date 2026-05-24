# Drift audit snapshot (2026-05-24)

`[SYNTH]` Repo research (`ce-repo-research-analyst`) cross-check of docs vs code. Remediation applied in same pass; use this page for the audit trail.

## High severity (fixed)

| Topic | Issue | Fix |
|-------|-------|-----|
| Distributed cache tests | `cloud-agents-starter` referenced removed `DistributedCache` filters | Removed; [removed-features.md](removed-features.md) |
| Duplicate cloud skill | `cloud_agent_starter` wrong test path | Deprecated stub → `cloud-agents-starter` |
| CI vs local tests | Docs implied CI runs full `!~LongRunning` | [ci-test-matrix.md](ci-test-matrix.md) |
| NuGet feed | README/AGENTS claimed GitHub Packages | Updated to nuget.org only |

## Medium severity (fixed)

| Topic | Issue | Fix |
|-------|-------|-----|
| CLI install selection | Undocumented “select all” without `--select` | [cli-selection-semantics.md](cli-selection-semantics.md), parity + CLI ref |
| `--best-effort` Nexus | Deselects Nexus mods without API key | Documented in CLI ref + semantics |
| `install_best_effort.sh` | Adds `--skip-validation` | scripts README + semantics |
| Nexus flag names | `convert` vs `install` key names | `core-cli-reference.md` |
| HoloPatcher Resources | Core/GUI symlink easy to miss | [holopatcher-resources.md](holopatcher-resources.md) |
| mod-builds upstreams | Two repos for different workflows | [mod-builds-sources.md](mod-builds-sources.md) |
| Runbook test name | `RealModIntegrationTests` obsolete | → `DocumentationRoundTripTests` |

## Open / runtime verification

| Topic | Status |
|-------|--------|
| `th3w1zard1/mod-builds` TOML paths | `[OPEN]` verify before full-build automation |
| telemetry-auth workflows on `main` vs `master` | `[OPEN]` |
| `CrossPlatformFileWatcherTests` on cloud runners | `[OPEN]` may still fail in containers |

## Stale branches (not merged)

14 remote feature branches remain hundreds of commits behind `master`. Bulk merge was **not** attempted. Revive individually with rebase + CI if still needed.

## Related

- [README.md](README.md) — topic index
