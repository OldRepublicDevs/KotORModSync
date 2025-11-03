# Mod Build Validation Workflow

This workflow validates and converts mod build documentation files from the KOTOR Community Portal mod-builds repository.

## Overview

The `mod-build-validation.yml` workflow:

1. **Runs in the mod-builds repository** (KOTOR-Community-Portal/mod-builds)
2. **Checks out KOTORModSync** to access validation tools
3. **Validates local markdown files** from `content/k1/` and `content/k2/`
4. **Updates markdown files in-place** with validated/normalized content
5. **Generates TOML files** for KOTORModSync consumption
6. **Commits all changes** back to the mod-builds repository

## Triggers

- **Manual**: via workflow_dispatch
- **Automatic**:
  - Daily at midnight UTC
  - On push to any markdown files in `content/k1/` or `content/k2/`

## Validated Builds

The workflow processes 8 mod builds:

### KOTOR 1

- Full (`content/k1/full.md` → `TOMLs/KOTOR1_Full.toml`)
- Full Mobile (`content/k1/full_mobile.md` → `TOMLs/KOTOR1_Mobile_Full.toml`)
- Spoiler-Free (`content/k1/spoiler-free.md` → `TOMLs/KOTOR1_Spoiler_Free.toml`)
- Spoiler-Free Mobile (`content/k1/spoiler-free_mobile.md` → `TOMLs/KOTOR1_Mobile_Spoiler_Free.toml`)

### KOTOR 2

- Full (`content/k2/full.md` → `TOMLs/KOTOR2_Full.toml`)
- Full Mobile (`content/k2/full_mobile.md` → `TOMLs/KOTOR2_Mobile_Full.toml`)
- Spoiler-Free (`content/k2/spoiler-free.md` → `TOMLs/KOTOR2_Spoiler_Free.toml`)
- Spoiler-Free Mobile (`content/k2/spoiler-free_mobile.md` → `TOMLs/KOTOR2_Mobile_Spoiler_Free.toml`)

## How It Works

### Repository Structure

- **This repo (mod-builds)**: Contains source markdown files
- **KOTORModSync**: Checked out temporarily, provides conversion tools via Core library

### Process Flow

1. Checkout both repositories
2. Calculate SHA256 hash of source markdown
3. Compare with stored hash (`.checksums/`) to detect changes
4. If changed:
   - Build KOTORModSync Core library with .NET 8
   - Convert markdown → TOML using `KOTORModSync.Core` CLI (saved to `TOMLs/`)
   - Generate validated markdown back to original path using `KOTORModSync.Core` CLI (round-trip conversion)
   - Create validation report (`.validation-reports/`)
   - Update hash file (`.checksums/`)
   - Commit all changes back to repo

### CLI Commands Used

The workflow uses the KOTORModSync Core library's CLI interface:

```bash
# Convert markdown to TOML
dotnet run --project KOTORModSync.Core/KOTORModSync.Core.csproj \
  --framework net8.0 \
  -- convert-to-toml \
  --input <source.md> \
  --output <destination.toml>

# Regenerate normalized markdown documentation
dotnet run --project KOTORModSync.Core/KOTORModSync.Core.csproj \
  --framework net8.0 \
  -- generate-docs \
  --input <source.md> \
  --output <destination.md>
```

### File Structure

```
mod-builds/
├── content/
│   ├── k1/
│   │   ├── full.md (updated in-place)
│   │   ├── full_mobile.md (updated in-place)
│   │   └── ...
│   └── k2/
│       └── (same structure)
├── TOMLs/
│   ├── KOTOR1_Full.toml
│   ├── KOTOR1_Mobile_Full.toml
│   └── ...
├── .checksums/
│   ├── KOTOR1_Full.toml.sha256
│   └── ...
└── .validation-reports/
    ├── KOTOR1_Full.toml_report.txt
    └── ...
```

## Migration Note

This workflow was migrated from the KOTORModSync repository where it previously downloaded mod-builds files via raw.githubusercontent.com URLs. Now it runs directly in the mod-builds repository and pulls KOTORModSync for validation tools.

## Requirements

- .NET 8.0 SDK
- KOTORModSync repository (automatically checked out)
- Ubuntu runner (Linux environment)
