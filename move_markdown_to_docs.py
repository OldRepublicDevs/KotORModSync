from __future__ import annotations

import shutil
from pathlib import Path


def main() -> None:
    """Move markdown documentation into the repository's `docs` directory.

    The traversal starts from the repository root (the directory containing this script).
    Summary of rules:
    - Any `*.md` file is eligible unless it is named `README.md` (case-insensitive).
    - Files beneath the Holocron Toolset help-window documentation
      (`Tools/HolocronToolset/src/toolset/help`) are excluded.
    - Files already living under `docs/` are ignored.
    - Files are relocated into `docs/`, preserving their relative paths from
      the repository root.
    """

    repo_root = Path(__file__).resolve().parent
    docs_dir = repo_root / "docs"
    help_docs_dir = repo_root / "Tools" / "HolocronToolset" / "src" / "toolset" / "help"

    docs_dir.mkdir(parents=True, exist_ok=True)

    for markdown_path in repo_root.rglob("*.md"):
        if markdown_path.is_dir():
            continue

        # Skip files already inside docs.
        if docs_dir in markdown_path.parents:
            continue

        # Skip README.md files (case-insensitive match).
        if markdown_path.name.lower() == "readme.md":
            continue

        # Skip any files contained in directories that begin with '.'.
        if any(part.startswith(".") for part in markdown_path.parts if part not in (".", "..")):
            continue

        # Skip Holocron Toolset help-window documentation.
        if help_docs_dir in markdown_path.parents:
            continue

        relative_path = markdown_path.relative_to(repo_root)
        destination_path = docs_dir / relative_path

        destination_path.parent.mkdir(parents=True, exist_ok=True)

        # Use move instead of rename to handle cross-filesystem moves gracefully.
        shutil.move(str(markdown_path), destination_path)
        print(f"Moved: {relative_path} -> {destination_path.relative_to(repo_root)}")


if __name__ == "__main__":
    main()

