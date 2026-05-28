# HoloPatcher resources layout

`[REPO]` Where HoloPatcher binaries and Python deps are resolved on each platform.

## Linux agent setup

```bash
./scripts/agents/ensure_linux_holopatcher.sh
```

Links `vendor/bin/HoloPatcher_linux` into the **GUI** build output:

`src/KOTORModSync.GUI/bin/Debug/net9.0/Resources/holopatcher`

## Core CLI vs GUI `Resources/`

Core `install` / `validate --full` load HoloPatcher from `Resources/` under the **Core** output directory. Agent scripts call `scripts/agents/common.sh` → `ensure_core_resources_symlink` when needed:

- `install_best_effort.sh` — always
- `cli_validate.sh` — when `--full` is passed (also runs `ensure_linux_holopatcher.sh` on Linux when present)

Manual equivalent:

```bash
ln -sfn src/KOTORModSync.GUI/bin/Debug/net9.0/Resources \
  src/KOTORModSync.Core/bin/Debug/net9.0/Resources
```

## Runtime lookup (Core)

`[REPO]` `InstallationService` checks bundled `Resources/holopatcher` (non-Windows) and Python fallback under `Resources/PyKotor/` when needed.

## GUI full-build

`[UI]` Use `ensure_linux_holopatcher.sh` before `launch_gui_desktop.sh` when validation reports missing HoloPatcher on Linux.

## Related

- [scripts/agents/README.md](../../scripts/agents/README.md)
- [core-cli-reference.md](core-cli-reference.md) — `holopatcher` verb
