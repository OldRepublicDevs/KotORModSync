# Spoiler-Free Content Feature

## Overview

The `--spoiler-free` CLI argument allows you to load spoiler-free versions of component descriptions and other content from a markdown file. This feature works with both the `convert` and `merge` commands.

## Supported Fields

The following component fields can have spoiler-free versions:

1. **Description** - Spoiler-free component description
2. **Directions** - Spoiler-free installation directions
3. **DownloadInstructions** - Spoiler-free download instructions
4. **UsageWarning** - Spoiler-free usage warning
5. **Screenshots** - Spoiler-free screenshots description

## Markdown Format

Create a markdown file with the following structure:

```markdown
### Component Name
**Description:** Spoiler-free description text here
**Directions:** Spoiler-free directions text here
**DownloadInstructions:** Spoiler-free download instructions
**UsageWarning:** Any spoiler-free warnings
**Screenshots:** Spoiler-free screenshots info

___

### Another Component Name
**Description:** Another spoiler-free description
**Directions:** Another set of directions

___
```

### Format Notes

- Component names must match exactly (case-insensitive) with component names in your instruction file
- Each component section starts with `### Component Name`
- Fields are marked with `**FieldName:**` format
- Field values can span multiple lines
- Field names are case-insensitive (Description, description, DESCRIPTION all work)
- Use `___` to separate component sections (optional but recommended)
- Only non-empty fields will be applied to components

## Usage Examples

### Convert Command

```bash
dotnet run --project KOTORModSync.Core --framework net9.0 convert \
  --source-path C:\path\to\mods\ \
  --format toml \
  --auto \
  --output .\output.toml \
  --spoiler-free .\spoiler-free.md
```

### Merge Command

```bash
dotnet run --project KOTORModSync.Core --framework net9.0 merge \
  --existing .\existing.toml \
  --incoming .\incoming.md \
  --output .\merged.toml \
  --spoiler-free .\spoiler-free.md
```

### Convert with Download and Spoiler-Free

```bash
dotnet run --project KOTORModSync.Core --framework net9.0 convert \
  --input .\full.md \
  --source-path C:\mods\ \
  --format toml \
  --download \
  --auto \
  --output .\KOTOR1_Full.toml \
  --spoiler-free .\spoiler-free.md
```

## Real-World Example

Given a spoiler-free.md file:

```markdown
### KOTOR Dialogue Fixes
**Description:** Fixes dialogue typos and improves PC responses
**Directions:** Simple loose file installation to the override folder
**DownloadInstructions:** Download from Deadly Stream, extract to a temporary folder
**UsageWarning:** No spoilers in this mod
**Screenshots:** Shows improved dialogue in action

___

### Thematic KOTOR Companions
**Description:** Rebalances companion progression without plot changes
**Directions:** Extract to override folder as normal
**DownloadInstructions:** Available from GitHub releases

___
```

And a component with name "KOTOR Dialogue Fixes", when you run:

```bash
dotnet run --project KOTORModSync.Core convert --input original.md --spoiler-free spoiler-free.md --output output.toml
```

The output TOML will include the spoiler-free fields:

```toml
[[components]]
Name = "KOTOR Dialogue Fixes"
Description = "Fixes dialogue typos and improves PC responses"
DescriptionSpoilerFree = "Fixes dialogue typos and improves PC responses"
Directions = "..."
DirectionsSpoilerFree = "Simple loose file installation to the override folder"
# ... etc
```

## Logging

When the `--spoiler-free` argument is used:

- **Verbose mode** (`--verbose`) will show: `Applied spoiler-free content to component: [ComponentName]`
- If no components match: `No matching components found for spoiler-free content`
- If file is not found: silently skips (no error, no action)
- Applied count summary: `Applied spoiler-free content to X component(s)`

## Error Handling

- If the file doesn't exist, the feature silently skips
- If parsing errors occur, a warning is logged and the feature continues
- Partial matches are skipped (component names must match exactly, but case-insensitive)
- Empty field values are ignored

## Tips

1. **Case Sensitivity**: Component names in the markdown are matched case-insensitively against instruction files
2. **Multi-line Content**: Field values can span multiple lines
3. **Optional Fields**: Only include the fields you want to override
4. **Isolation**: The spoiler-free markdown file can exist independently and doesn't affect normal operation
5. **Reusability**: The same spoiler-free file can be used with multiple instruction files as long as component names match
