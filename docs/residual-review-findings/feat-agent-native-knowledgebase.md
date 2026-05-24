# Residual review findings

Branch: `feat/agent-native-knowledgebase`  
Plan: `docs/plans/2026-05-24-016-agent-native-knowledgebase-plan.md`  
Review mode: `ce-code-review mode:autofix`

## Residual Review Findings

- **RESID-001** (low) — `scripts/agents/cli_validate.sh`: Run a full `cli_validate.sh --full` smoke test when `mod-builds` and template dirs are present in the environment. Wrapper not exercised against a real TOML in CI.

- **RESID-003** (low) — Optional: add `docs/knowledgebase/README.md` link to `scripts/agents/install_best_effort.sh` in the quick commands table for discoverability.
