#!/usr/bin/env bash
# Best-effort full install: download what can be fetched, install all selected mods,
# skip missing archives and individual mod failures (Nexus key from env or settings).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

TOML="${1:-$ROOT/mod-builds/TOMLs/KOTOR1_Full.toml}"
GAME_DIR="${2:-$ROOT/tmp/k1_best_game}"
MOD_DIR="${3:-$ROOT/tmp/k1_best_mods}"

if [[ ! -f "$TOML" ]]; then
  echo "Instruction file not found: $TOML" >&2
  echo "Usage: $0 [path/to/KOTOR1_Full.toml] [game_dir] [mod_workspace_dir]" >&2
  exit 1
fi

mkdir -p "$MOD_DIR"
"$ROOT/scripts/agents/create_template_kotor_install.sh" "$GAME_DIR" "$MOD_DIR"

GUI_RES="$ROOT/src/KOTORModSync.GUI/bin/Debug/net9.0/Resources"
CORE_OUT="$ROOT/src/KOTORModSync.Core/bin/Debug/net9.0"
if [[ -d "$GUI_RES" ]]; then
  ln -sfn "$GUI_RES" "$CORE_OUT/Resources"
fi

dotnet build "$ROOT/src/KOTORModSync.Core/KOTORModSync.Core.csproj" -c Debug -f net9.0 -v q

exec dotnet run --project "$ROOT/src/KOTORModSync.Core/KOTORModSync.Core.csproj" -f net9.0 --no-build -- \
  install -i "$TOML" -g "$GAME_DIR" -s "$MOD_DIR" \
  -d --concurrent \
  --best-effort \
  --skip-validation \
  --download-timeout-hours 72
