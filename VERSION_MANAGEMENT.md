# KOTORModSync Version Management

## Version Sources

### 1. MainConfig.cs (Source of Truth for Code)

**Location**: `KOTORModSync.Core/MainConfig.cs`

```csharp
public static string CurrentVersion => "2.0.0b1";
```

This is the **canonical version** for the codebase. All development builds use this version.

### 2. .release-please-manifest.json (Tracks Releases)

**Location**: `.release-please-manifest.json`

```json
{
  ".": "2.0.0-beta.1"
}
```

This is **ONLY used by release-please** to:

- Track what version was last released
- Determine what the next version should be
- Create release PRs that update MainConfig.cs

**Note**: This should match MainConfig.cs version (in semver format). Release-please will keep them in sync.

### 3. GitHub Release Tags (For Release Builds)

When a GitHub release is created (either manually or by release-please), the tag name becomes the version for that build.

Example: Release tag `v2.0.0` → builds are named `KOTORModSync-v2.0.0-*.zip`

## Build Version Logic

The `build-and-release.yml` workflow determines version with this priority:

### 1. GitHub Release Event (Highest Priority)

```yaml
if github.event_name == 'release':
  version = github.event.release.tag_name  # e.g., "v2.0.0"
```

### 2. MainConfig.cs (Default)

```yaml
else:
  version = extract_from_mainconfig()  # e.g., "2.0.0b1" → "v2.0.0b1"
```

### 3. Manual Override (workflow_dispatch)

```yaml
if manual_trigger AND version_input_provided:
  version = user_input  # e.g., "v2.0.0-rc.1"
```

## Version Format Conversion

| Format | Example | Used By |
|--------|---------|---------|
| MainConfig.cs | `"2.0.0b1"` | Source code |
| Semver (manifest) | `"2.0.0-beta.1"` | release-please |
| Git tag | `"v2.0.0-beta.1"` | GitHub releases |
| Build artifacts | `"v2.0.0b1"` | ZIP filenames |

## Release Process

### Automated (Recommended)

1. **Make changes** with conventional commits:

   ```bash
   git commit -m "feat: add new feature"
   git commit -m "fix: resolve bug"
   ```

2. **Push to main**:

   ```bash
   git push origin main
   ```

3. **release-please creates PR** automatically:
   - Updates `MainConfig.cs` version
   - Updates `.release-please-manifest.json`
   - Generates `CHANGELOG.md` entries

4. **Review and merge** the release PR

5. **GitHub creates release** automatically with proper tag

6. **Build workflow runs** automatically:
   - Injects telemetry secret
   - Builds for all platforms
   - Uploads archives to the release

### Manual (If Needed)

1. **Update MainConfig.cs** manually:

   ```csharp
   public static string CurrentVersion => "2.1.0";
   ```

2. **Update manifest** manually:

   ```json
   {
     ".": "2.1.0"
   }
   ```

3. **Create release** on GitHub with tag `v2.1.0`

4. **Build workflow runs** automatically

## Current State (Zero Releases)

- **MainConfig.cs**: `"2.0.0b1"`
- **Manifest**: `"2.0.0-beta.1"` ✅ (in sync)
- **GitHub Releases**: None yet
- **Next release-please action**: Will create PR for first release

## Conventional Commit Types

Release-please uses conventional commits to determine version bumps:

| Commit Type | Version Bump | Example |
|-------------|--------------|---------|
| `feat:` | Minor (2.0.0 → 2.1.0) | `feat: add telemetry` |
| `fix:` | Patch (2.0.0 → 2.0.1) | `fix: resolve crash` |
| `perf:` | Patch | `perf: optimize loading` |
| `BREAKING CHANGE:` | Major (2.0.0 → 3.0.0) | See below |
| `docs:`, `chore:`, `style:` | No bump | Changelog only |

### Breaking Change Example

```bash
git commit -m "feat: redesign API

BREAKING CHANGE: Old API endpoints removed"
```

## FAQ

### Q: Why do we have two version files?

**A**:

- `MainConfig.cs` = Code version (used by app and dev builds)
- `.release-please-manifest.json` = Release tracking (used by automation)

### Q: Which file should I update manually?

**A**: **MainConfig.cs only**. Release-please will update both when creating release PRs.

### Q: What if they get out of sync?

**A**: Release-please will use the manifest value and update MainConfig.cs in the next release PR. Just manually sync them if needed.

### Q: Can I delete .release-please-manifest.json?

**A**: No, release-please requires it to track version history.

### Q: What about pre-releases (beta, rc)?

**A**: Add to MainConfig.cs as `"2.0.0-beta.1"` or `"2.0.0b1"`. Build workflow handles both formats.

## Version Sync Check

To verify versions are in sync:

```bash
# Extract from MainConfig.cs
grep -oP 'CurrentVersion\s*=>\s*"\K[^"]+' KOTORModSync.Core/MainConfig.cs

# Extract from manifest
jq -r '."."' .release-please-manifest.json

# They should match (accounting for format differences)
# "2.0.0b1" ←→ "2.0.0-beta.1"
```
