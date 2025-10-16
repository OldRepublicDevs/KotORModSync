#!/usr/bin/env python3
"""
Analyzes holopatcher dependencies and removes unused files from vendor/holopatcher_py
"""

import ast
import shutil
from pathlib import Path
from typing import Set


def find_imports_in_file(file_path: Path) -> Set[str]:
    """Extract all import statements from a Python file."""
    imports = set()

    try:
        with open(file_path, "r", encoding="utf-8") as f:
            content = f.read()

        tree = ast.parse(content)

        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                for alias in node.names:
                    imports.add(alias.name.split(".")[0])
            elif isinstance(node, ast.ImportFrom):
                if node.module:
                    imports.add(node.module.split(".")[0])
    except Exception as e:
        print(f"Warning: Could not parse {file_path}: {e}")

    return imports


def find_all_dependencies(start_file: Path, root_dir: Path) -> Set[Path]:
    """Recursively find all Python files that are imported."""
    visited = set()
    to_process = {start_file}
    all_files = set()

    while to_process:
        current = to_process.pop()
        if current in visited:
            continue

        visited.add(current)
        if current.exists() and current.suffix == ".py":
            all_files.add(current)
            imports = find_imports_in_file(current)

            # Map imports to actual file paths within our vendor directory
            for imp in imports:
                if imp in ["pykotor", "utility", "holopatcher", "loggerplus"]:
                    # Look for this module in vendor
                    module_dir = root_dir / imp
                    if module_dir.exists():
                        for py_file in module_dir.rglob("*.py"):
                            if py_file not in visited:
                                to_process.add(py_file)

    return all_files


def main():
    vendor_dir = Path("vendor/holopatcher_py")
    if not vendor_dir.exists():
        print(f"ERROR: {vendor_dir} does not exist")
        return 1

    # Start from holopatcher/__main__.py
    entry_point = vendor_dir / "holopatcher" / "__main__.py"
    if not entry_point.exists():
        print(f"ERROR: Entry point {entry_point} not found")
        return 1

    print("Analyzing holopatcher dependencies...")
    print(f"Entry point: {entry_point}")

    # Find all required files
    required_files = find_all_dependencies(entry_point, vendor_dir)
    print(f"\nFound {len(required_files)} required Python files")

    # Find all existing Python files
    all_py_files = set(vendor_dir.rglob("*.py"))
    print(f"Total Python files in vendor: {len(all_py_files)}")

    # Files that can be removed
    removable = all_py_files - required_files
    print(f"\nRemovable files: {len(removable)}")

    # Calculate size savings
    removable_size = sum(f.stat().st_size for f in removable if f.exists())
    total_size = sum(f.stat().st_size for f in all_py_files if f.exists())

    print("\nSize analysis:")
    print(f"  Total size: {total_size / 1024 / 1024:.2f} MB")
    print(
        f"  Removable: {removable_size / 1024 / 1024:.2f} MB ({removable_size / total_size * 100:.1f}%)"
    )
    print(f"  After cleanup: {(total_size - removable_size) / 1024 / 1024:.2f} MB")

    # Also check for obviously unused directories
    unused_dirs = [
        "utility/gui/qt",  # Qt GUI stuff not needed
        "utility/ui_libraries",  # UI libraries not needed
        "pykotor/resource/formats/tpc/convert",  # TPC conversion not needed for patching
        "pykotor/resource/formats/mdl",  # Model files not needed for patching
    ]

    print("\nRemoving obviously unused directories...")
    for dir_name in unused_dirs:
        dir_path = vendor_dir / dir_name
        if dir_path.exists():
            size = sum(f.stat().st_size for f in dir_path.rglob("*") if f.is_file())
            print(f"  Removing {dir_name} ({size / 1024:.1f} KB)")
            shutil.rmtree(dir_path)

    print("\nRemoving unused Python files...")
    removed_count = 0
    for file_path in sorted(removable):
        _ = file_path.relative_to(vendor_dir)
        # print(f"  Removing {relative}")
        file_path.unlink()
        removed_count += 1

        # Remove empty parent directories
        parent = file_path.parent
        while parent != vendor_dir and parent.exists():
            try:
                if not any(parent.iterdir()):
                    parent.rmdir()
                    parent = parent.parent
                else:
                    break
            except Exception:
                break

    print(f"Removed {removed_count} unused Python files")

    # Remove other non-Python files that aren't needed
    print("\nRemoving non-Python artifacts...")
    for pattern in ["*.pyi", "*.pyc", "*.pyo", "__pycache__", "*.out", "tests"]:
        for item in vendor_dir.rglob(pattern):
            if item.is_file():
                item.unlink()
                print(f"  Removed {item.relative_to(vendor_dir)}")
            elif item.is_dir():
                shutil.rmtree(item)
                print(f"  Removed directory {item.relative_to(vendor_dir)}")

    # Final size
    final_size = sum(f.stat().st_size for f in vendor_dir.rglob("*") if f.is_file())
    print(f"\nFinal size: {final_size / 1024 / 1024:.2f} MB")
    print(
        f"Savings: {(total_size - final_size) / 1024 / 1024:.2f} MB ({(total_size - final_size) / total_size * 100:.1f}%)"
    )

    return 0


if __name__ == "__main__":
    import sys

    sys.exit(main())
