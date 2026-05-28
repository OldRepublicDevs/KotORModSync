# telemetry-auth routing

`[REPO]` Python/Docker sidecar — **not** the Avalonia/.NET installer path.

## When to use this doc

- Task touches `telemetry-auth/`
- HMAC relay, signing secrets, Docker compose for telemetry service
- Do **not** use `AGENTS.md` GUI wizard or `ModBuildConverter` for sidecar work

## Entry points

| Resource | Path |
|----------|------|
| Sidecar README | `telemetry-auth/README.md` |
| Client signing | `KOTORMODSYNC_SIGNING_SECRET`, `telemetry_config.json`, `OFFICIAL_BUILD` embedded key |
| Relay env | `KOTORMODSYNC_RELAY_CREDENTIAL` (production); local Docker may use documented test defaults |

## Workflow branch note

`[OPEN]` Some telemetry workflows in `.github/workflows/` trigger on `main`; this repository’s default branch is `master`. Confirm branch filters when debugging CI.

## Related

- [cloud-agents-starter SKILL](../../.cursor/skills/cloud-agents-starter/SKILL.md) — § Auth / Telemetry (GUI client)
- `AGENTS.md` — routes telemetry-auth separately from Avalonia
