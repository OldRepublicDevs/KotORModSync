---
name: cloud-agents-starter
description: Minimal runbook for Cloud agents - how to run, test, and configure this codebase. Use when setting up environments, executing tests, or resolving common workflow issues.
---

# KOTORModSync Cloud Agents Starter Skill

A minimal runbook for Cloud agents covering practical setup, execution, and testing. Organized by codebase area.

---

## 1. Initial Setup

**Vendor dependencies (required before first build):**
```bash
git submodule update --init --recursive
```

**NuGet sources:** `NuGet.config` at repo root. Includes nuget.org and GitHub Packages.

---

## 2. GUI Application

**Entry point:** `src/KOTORModSync.GUI/Program.cs` (Avalonia UI)

**Build and run:**
```bash
# From repo root
dotnet build src/KOTORModSync.GUI/KOTORModSync.csproj
dotnet run --project src/KOTORModSync.GUI/KOTORModSync.csproj
```

**CLI args (from `src/KOTORModSync.GUI/CLIArguments.cs`):**
- `--kotorPath="C:\Path\To\Game"`
- `--modDirectory="C:\Path\To\Mods"`
- `--instructionFile="C:\Path\To\Instructions.toml"`

**Linux/WSL:** Install X11 libs first if needed:
```bash
sudo apt install libsm6 libice6 libx11-dev libfontconfig1 libx11-6 libx11-xcb1 libxau6 libxcb1 libxdmcp6 libxcb-xkb1 libxcb-render0 libxcb-shm0 libxcb-xfixes0 libxcb-util1 libxcb-xinerama0 libxcb-randr0 libxcb-image0 libxcb-keysyms1 libxcb-sync1 libxcb-xtest0
```
Set `DISPLAY=` to avoid X11 waits in headless or CI environments.

**Launch profiles:** `src/KOTORModSync.GUI/Properties/launchSettings.json` — profiles: `dotnet`, `dotnetframework`, `WSL`.

---

## 3. Core CLI (ModBuildConverter)

**Entry point:** `src/KOTORModSync.Core/` — run via `ModBuildConverter.Run(args)` (exposed by Tests project for CLI use).

**Run Core verbs:**
```bash
dotnet run --project src/KOTORModSync.Core/KOTORModSync.Core.csproj --framework net9.0 -- <verb> [options]
```

**Common verbs:** `convert`, `merge`, `validate`, `install`, `set-nexus-api-key`, `install-python-deps`, `holopatcher`, `cache-stats`, `cache-clear`, `cache-block`, `cache-test`, `cache-seed`.

**Example:**
```bash
dotnet run --project src/KOTORModSync.Core/KOTORModSync.Core.csproj -- validate -i path/to/instructions.toml
```

---

## 4. Testing

**Project:** `src/KOTORModSync.Tests/KOTORModSync.Tests.csproj`  
**Do not create additional test projects.** All tests live here (NUnit, xUnit, Moq, Avalonia.Headless.XUnit).

**Test project paths (from repo root):**
- `src/KOTORModSync.Tests/KOTORModSync.Tests.csproj`

### Run commands

**All unit tests:**
```bash
dotnet build -c Release
dotnet test src/KOTORModSync.Tests/KOTORModSync.Tests.csproj -c Release --no-build --verbosity normal
```

**DistributedCache tests only** (exclude long and seeding):
```bash
dotnet test src/KOTORModSync.Tests/KOTORModSync.Tests.csproj \
  --filter "FullyQualifiedName~DistributedCache&FullyQualifiedName!~LongRunning&FullyQualifiedName!~GitHubRunnerSeeding"
```

**Single test (with timeout):**
```bash
dotnet test src/KOTORModSync.Tests/KOTORModSync.Tests.csproj \
  --filter "FullyQualifiedName~<test_name>" --no-build
```

**Documentation round-trip tests** (mod-build validation):
```bash
TEST_FILE_PATH=test_modbuild_current.md dotnet test src/KOTORModSync.Tests/KOTORModSync.Tests.csproj \
  --filter "FullyQualifiedName~DocumentationRoundTripTests" --no-build
```

### Test naming conventions

| Suffix | Purpose |
|--------|---------|
| `GitHubRunnerSeeding` | 5–6 hour seeding tests, only for GitHub Actions |
| `LongRunning` | >2 min, excluded from normal runs |
| (none) | Regular tests, <2 min |

### Test runsettings

`src/KOTORModSync.Tests/KOTORModSync.Tests.runsettings` — excludes `Slow` tests.

---

## 5. Feature Flags & Build Constants

**No runtime feature-flag system.** Behavior is controlled by build-time preprocessor defines.

| Define | Effect |
|--------|--------|
| `OFFICIAL_BUILD` | Enables embedded telemetry signing key in release builds |

**Enable `OFFICIAL_BUILD`:**
```bash
dotnet build -c Release /p:DefineConstants="OFFICIAL_BUILD"
```
Omit for PRs and dev builds.

**Disable telemetry (no `OFFICIAL_BUILD`):** Build without this define; telemetry falls back to env/file and can be left unset.

---

## 6. Auth / Telemetry

**No user login.** Telemetry uses HMAC auth.

**Client signing secret (order of precedence):**
1. `KOTORMODSYNC_SIGNING_SECRET` env var
2. `{ApplicationData}/KOTORModSync/telemetry.key`
3. `EmbeddedSecrets.TELEMETRY_SIGNING_KEY` (only when `OFFICIAL_BUILD`)

**Telemetry auth service** (`telemetry-auth/`): HMAC validation for OpenTelemetry.  
**Disable auth (service):** `REQUIRE_AUTH=false` when running the service.

---

## 7. Environment Variables

| Variable | Purpose |
|----------|---------|
| `KOTORMODSYNC_SIGNING_SECRET` | Telemetry HMAC signing secret |
| `NCS_INTERPRETER_DEBUG` | HoloPatcher NCS interpreter debug (`true` to enable) |
| `NCSDecomp_DEBUG_STACK` | NCS decompiler debug (Java stubs) |
| `TEST_FILE_PATH` | Path to mod-build test file (e.g. mod-build-validation workflow) |
| `PYTHON_KEYRING_BACKEND` | Set to `keyring.backends.null.Keyring` to avoid pip hangs |
| `DISPLAY` | Set empty in headless/CI to avoid X11 waits |

---

## 8. Configuration Files

| Path | Purpose |
|------|---------|
| `{ApplicationData}/KOTORModSync/settings.json` | GUI preferences, paths, Nexus API key |
| `{ApplicationData}/KOTORModSync/telemetry_config.json` | Telemetry options |
| `{ApplicationData}/KOTORModSync/telemetry.key` | Signing secret fallback |
| `testenvironments.json` | WSL/Ubuntu test environment config |

---

## 9. Quick Reference

| Task | Command |
|------|---------|
| Init submodules | `git submodule update --init --recursive` |
| Build GUI | `dotnet build src/KOTORModSync.GUI/KOTORModSync.csproj` |
| Run GUI | `dotnet run --project src/KOTORModSync.GUI/KOTORModSync.csproj` |
| Run Core CLI | `dotnet run --project src/KOTORModSync.Core/KOTORModSync.Core.csproj -- <verb> [options]` |
| Run all tests | `dotnet test src/KOTORModSync.Tests/KOTORModSync.Tests.csproj -c Release` |
| Run DistCache tests | `dotnet test src/KOTORModSync.Tests/KOTORModSync.Tests.csproj --filter "FullyQualifiedName~DistributedCache&FullyQualifiedName!~LongRunning&FullyQualifiedName!~GitHubRunnerSeeding"` |
| Release build (with telemetry) | `dotnet build -c Release /p:DefineConstants="OFFICIAL_BUILD"` |

---

## 10. Updating This Skill

When you discover new runbook steps or testing tricks:

1. **Edit this file:** `.cursor/skills/cloud-agents-starter/SKILL.md`
2. **Add to the right section** (or add a new section) with concrete commands.
3. **Keep it minimal** — only practical, runnable instructions.
4. **Use code blocks** for all commands.
5. **Commit** the change so future Cloud agent runs pick it up.

Optional subfolders: `scripts/`, `references/`, `assets/` for extra tooling or docs if needed.
