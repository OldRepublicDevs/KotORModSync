# Removed features and stale conventions

`[REPO]` Items that still appear in old branches, generated docs, or outdated skills but are **not** on current `master`.

## Distributed cache / MonoTorrent P2P

- **Removed:** `97b00c4`, `93c74e5` — distributed cache infrastructure and MonoTorrent integration.
- **Still on master:** `DownloadCacheService` and download orchestration (unrelated name; not P2P cache).
- **Drift:** Do not document `FullyQualifiedName~DistributedCache` test filters; no `DistributedCache*` tests remain under `src/KOTORModSync.Tests/`.
- **PR #70** (deobfuscation of removed files) was closed as obsolete.

## NuGet GitHub Packages feed

- **Removed:** PR #65 — `NuGet.config` now lists **nuget.org only**.
- **Drift:** `README.md` and older generated docs may still mention `github-th3w1zard1`; ignore them.

## Test suffix conventions vs reality

| Suffix | Documented purpose | `[REPO]` on master |
|--------|-------------------|-------------------|
| `LongRunning` | Exclude tests >2 min from default runs | **Convention** in `.cursorrules` / `AGENTS.md`; filter `!~LongRunning` is safe; few or no tests use the suffix yet |
| `GitHubRunnerSeeding` | GitHub-only seeding tests | **No tests** match this name after distributed cache removal |
| `DistributedCache` | Separate P2P test suite | **Removed** with feature |

Use `./scripts/agents/run_headless_tests.sh` for local “all non-LongRunning” runs. CI uses **named subsets** — see [ci-test-matrix.md](ci-test-matrix.md).

## Duplicate Cursor skill

- **Canonical:** `.cursor/skills/cloud-agents-starter/SKILL.md` (with frontmatter).
- **Deprecated:** `.cursor/skills/cloud_agent_starter/SKILL.md` — wrong test project path; do not use.

## Related

- [CI test matrix](ci-test-matrix.md)
- [Agent-native audit](agent-native-audit.md)
