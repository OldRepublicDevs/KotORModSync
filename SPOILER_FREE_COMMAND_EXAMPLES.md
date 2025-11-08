# Spoiler-Free Feature - Command Examples

## Your Original Command - Enhanced with Spoiler-Free

Your original command was:

```bash
dotnet run --project KOTORModSync.Core --framework net8.0 convert `
  --source-path="$Env:USERPROFILE\OneDrive\Documents\rev12_modbuild_workspace" `
  --format=toml `
  --auto `
  --output=".\mod-builds\TOMLs\KOTOR1_Full.toml" `
  --plaintext `
  --verbose `
  --download `
  --merge=1
  --exclude-existing-only `
  --existing=".\KOTOR.Modbuilds.Rev10\KOTOR1_Full.toml" `
  --incoming=".\mod-builds\content\k1\full.md"
```

### Enhanced with Spoiler-Free Support

**Using the `convert` command with `--spoiler-free`:**

```bash
dotnet run --project KOTORModSync.Core --framework net8.0 convert `
  --source-path="$Env:USERPROFILE\OneDrive\Documents\rev12_modbuild_workspace" `
  --format=toml `
  --auto `
  --output=".\mod-builds\TOMLs\KOTOR1_Full.toml" `
  --plaintext `
  --verbose `
  --download `
  --merge=1
  --exclude-existing-only `
  --existing=".\KOTOR.Modbuilds.Rev10\KOTOR1_Full.toml" `
  --incoming=".\mod-builds\content\k1\full.md" `
  --spoiler-free=".\mod-builds\content\k1\spoiler-free.md"
```

**Or using the `merge` command (recommended for clarity):**

```bash
dotnet run --project KOTORModSync.Core --framework net8.0 merge `
  --source-path="$Env:USERPROFILE\OneDrive\Documents\rev12_modbuild_workspace" `
  --format=toml `
  --auto `
  --output=".\mod-builds\TOMLs\KOTOR1_Full.toml" `
  --plaintext `
  --verbose `
  --download `
  --merge=1
  --exclude-existing-only `
  --existing=".\KOTOR.Modbuilds.Rev10\KOTOR1_Full.toml" `
  --spoiler-free=".\mod-builds\content\k1\spoiler-free.md"
```

## Explanation of the New Flag

| Argument | Value | Purpose |
|----------|-------|---------|
| `--spoiler-free` | `.\mod-builds\content\k1\spoiler-free.md` | Path to the markdown file containing spoiler-free content |

## Expected Output

The resulting TOML file will contain both original and spoiler-free versions of fields:

```toml
[[components]]
Name = "KOTOR Dialogue Fixes"
Description = "[original description]"
DescriptionSpoilerFree = "[spoiler-free description]"
Directions = "[original directions]"
DirectionsSpoilerFree = "[spoiler-free directions]"
DownloadInstructions = "[original instructions]"
DownloadInstructionsSpoilerFree = "[spoiler-free instructions]"
UsageWarning = "[original warning]"
UsageWarningSpoilerFree = "[spoiler-free warning]"
Screenshots = "[original screenshots description]"
ScreenshotsSpoilerFree = "[spoiler-free screenshots description]"

# ... rest of component configuration ...
```

## What the Feature Does

1. **Loads your spoiler-free markdown file** from the provided path
2. **Parses component sections** (### Component Name format)
3. **Matches components by name** (case-insensitive) from your instruction file
4. **Applies spoiler-free content** to matching components for:
   - Description
   - Directions
   - DownloadInstructions
   - UsageWarning
   - Screenshots
5. **Includes both versions** in the output (original stays intact, spoiler-free version added)

## Markdown File Format

Create `.\mod-builds\content\k1\spoiler-free.md` with content like:

```markdown
### KOTOR Dialogue Fixes
**Description:** Fixes dialogue typos while maintaining spoiler-free content
**Directions:** Extract to Override folder as instructed
**DownloadInstructions:** Available from Deadly Stream without spoilers
**UsageWarning:** No spoilers in this modification
**Screenshots:** Shows improved dialogue quality

___

### Thematic KOTOR Companions
**Description:** Rebalances companion mechanics without plot changes
**Directions:** Apply patch then extract to Override
**DownloadInstructions:** Download from GitHub for spoiler-free version

___

### [More Components...]
```

## Verbosity and Logging

With `--verbose`, you'll see:

```
Applied spoiler-free content to component: KOTOR Dialogue Fixes
Applied spoiler-free content to component: Thematic KOTOR Companions
Applied spoiler-free content to 2 component(s)
```

## Notes

- The `--spoiler-free` argument works with **both** `convert` and `merge` commands
- The argument is **optional** - omit it if you don't have a spoiler-free file
- Component names must **match exactly** (but matching is case-insensitive)
- The file path can be **relative or absolute**
- If the file **doesn't exist**, it's silently skipped (no error)
- **All five fields are optional** - only include the ones you want to override
- Spoiler-free fields can be **multi-line** in the markdown file

## Quick Copy-Paste Template

```bash
dotnet run --project KOTORModSync.Core --framework net8.0 merge \
  --existing [path-to-existing-toml_yaml_json_md] \
  --incoming [path-to-incoming--toml_yaml_json_md] \
  --output [path-to-output-toml] \
  --format toml \
  --source-path [your-mods-folder] \
  --download \
  --verbose \
  --spoiler-free [path-to-spoiler-free-md]
```
