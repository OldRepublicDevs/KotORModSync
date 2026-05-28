#!/usr/bin/env bash
# Shared helpers for scripts/agents/*.sh — source from other scripts, do not execute directly.

# Symlink GUI build Resources into Core output so HoloPatcher resolves during validate/install.
ensure_core_resources_symlink() {
  local root="${1:-}"
  if [[ -z "$root" ]]; then
    root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
  fi
  local gui_res="$root/src/KOTORModSync.GUI/bin/Debug/net9.0/Resources"
  local core_out="$root/src/KOTORModSync.Core/bin/Debug/net9.0"
  if [[ -d "$gui_res" ]]; then
    mkdir -p "$core_out"
    ln -sfn "$gui_res" "$core_out/Resources"
  fi
}
