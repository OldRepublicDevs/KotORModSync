#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
input_path=""
game_dir=""
source_dir=""
full_validation=false
dry_run=false
dry_run_only=false
use_file_selection=false
extra_args=()

usage() {
  cat <<'EOF'
Usage: cli_validate.sh --input <file.toml> [options]

Options:
  --input PATH           Instruction file (required)
  --game-dir PATH        Game install directory
  --source-dir PATH      Mod workspace directory
  --full                 Pass --full to Core validate (needs game + source dirs)
  --dry-run              Pass --dry-run to Core validate (VFS simulation; needs game + source dirs)
  --dry-run-only         Pass --dry-run-only (skip archive checks; VFS dry-run only; needs game + source dirs)
  --use-file-selection   Only validate mods with IsSelected=true in the TOML (matches GUI)
  --select VALUE         Repeatable; passed to Core as --select
  -h, --help             Show this help

Forwards unknown options to ModBuildConverter validate after built-in flags.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --input)
      input_path="$2"
      shift 2
      ;;
    --game-dir)
      game_dir="$2"
      shift 2
      ;;
    --source-dir)
      source_dir="$2"
      shift 2
      ;;
    --full)
      full_validation=true
      shift
      ;;
    --dry-run)
      dry_run=true
      shift
      ;;
    --dry-run-only)
      dry_run_only=true
      shift
      ;;
    --use-file-selection)
      use_file_selection=true
      shift
      ;;
    --select)
      extra_args+=(--select "$2")
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      extra_args+=("$1")
      shift
      ;;
  esac
done

if [[ -z "$input_path" ]]; then
  echo "error: --input is required" >&2
  usage >&2
  exit 1
fi

if [[ ! -f "$input_path" ]]; then
  echo "error: instruction file not found: $input_path" >&2
  exit 1
fi

cmd=(validate -i "$input_path")

if [[ -n "$game_dir" ]]; then
  cmd+=(-g "$game_dir")
fi
if [[ -n "$source_dir" ]]; then
  cmd+=(-s "$source_dir")
fi
if [[ "$full_validation" == true ]]; then
  cmd+=(--full)
fi
if [[ "$dry_run" == true ]]; then
  cmd+=(--dry-run)
fi
if [[ "$dry_run_only" == true ]]; then
  cmd+=(--dry-run-only)
fi
if [[ "$use_file_selection" == true ]]; then
  cmd+=(--use-file-selection)
fi
if [[ ${#extra_args[@]} -gt 0 ]]; then
  cmd+=("${extra_args[@]}")
fi

if [[ "$full_validation" == true || "$dry_run" == true || "$dry_run_only" == true ]]; then
  # shellcheck source=common.sh
  source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
  ensure_core_resources_symlink "$repo_root"
  if [[ -x "$repo_root/scripts/agents/ensure_linux_holopatcher.sh" ]]; then
    "$repo_root/scripts/agents/ensure_linux_holopatcher.sh" || true
  fi
fi

dotnet run --project "$repo_root/src/KOTORModSync.Core/KOTORModSync.Core.csproj" \
  -f net9.0 -- "${cmd[@]}"
