# Dry-Run Validation System

## Overview

The Dry-Run Validation System is a comprehensive solution for validating mod installation instructions **without making any physical changes to the file system**. It simulates the entire installation process in memory, tracking file locations and operations to detect issues before they occur during a real installation.

## Architecture

### Core Components

#### 1. **IFileSystemProvider** (`Services/FileSystem/IFileSystemProvider.cs`)

An abstraction layer for all file system operations. This allows the same instruction execution code to work with both real and virtual file systems.

**Key Methods:**

- `FileExists(path)` - Check if a file exists
- `DirectoryExists(path)` - Check if a directory exists
- `CopyFileAsync()` - Copy files
- `MoveFileAsync()` - Move files
- `DeleteFileAsync()` - Delete files
- `ExtractArchiveAsync()` - Extract archives
- `ExecuteProcessAsync()` - Execute programs

#### 2. **VirtualFileSystemProvider** (`Services/FileSystem/VirtualFileSystemProvider.cs`)

Simulates all file system operations in memory. Tracks:

- Which files exist at any point during validation
- Where files are located after operations
- Archive contents (pre-scanned)
- All validation issues encountered

**Key Features:**

- Initializes with current real file system state
- Pre-scans archives to know their contents
- Simulates all file operations (move, copy, delete, rename, extract)
- Collects detailed validation issues with context

#### 3. **RealFileSystemProvider** (`Services/FileSystem/RealFileSystemProvider.cs`)

Wrapper around actual file system operations. Used for real installations.

#### 4. **DryRunValidator** (`Services/Validation/DryRunValidator.cs`)

Orchestrates the validation process:

- Validates installation order
- Checks dependencies and restrictions
- Simulates each instruction's execution
- Collects and categorizes issues

#### 5. **DryRunValidationResult** (`Services/Validation/DryRunValidationResult.cs`)

Contains validation results with:

- List of all issues found (errors, warnings, info)
- User-friendly messages for end users
- Detailed technical messages for editors
- Suggestions for resolution

#### 6. **ValidationResultPresenter** (`Services/Validation/ValidationResultPresenter.cs`)

Helper class for GUI integration:

- Formats messages for dialogs
- Provides actionable steps
- Identifies components that can be auto-disabled
- Manages UI interactions

## How It Works

### Validation Flow

```
1. PreinstallValidationService.ValidatePreinstallAsync()
   ↓
2. DryRunValidator.ValidateInstallationAsync()
   ↓
3. For each selected component:
   - Check dependencies/restrictions
   - For each instruction:
     a. Resolve paths (with variable replacement)
     b. Validate instruction based on action type
     c. Simulate the operation in VirtualFileSystemProvider
     d. Track file state changes
   ↓
4. Collect all issues from VirtualFileSystemProvider
   ↓
5. Return DryRunValidationResult with detailed findings
```

### File State Tracking

The `VirtualFileSystemProvider` maintains:

- **`_virtualFiles`**: HashSet of all file paths that exist at any point
- **`_virtualDirectories`**: HashSet of all directory paths
- **`_archiveContents`**: Dictionary mapping archives to their contents
- **`_issues`**: List of validation issues encountered

### Operation Simulation Examples

**Extract Archive:**

1. Check if archive file exists
2. Scan archive contents (if not already done)
3. Add all extracted file paths to `_virtualFiles`
4. Create parent directories in `_virtualDirectories`

**Move File:**

1. Check if source file exists in `_virtualFiles`
2. Check if destination directory exists
3. Remove source path from `_virtualFiles`
4. Add destination path to `_virtualFiles`
5. If file doesn't exist: log error

**Copy File:**

1. Check if source file exists
2. Check if destination exists (overwrite handling)
3. Add destination path to `_virtualFiles` (source remains)

## Validation Categories

### Error Severity Levels

- **Critical**: Will definitely cause installation failure
- **Error**: Will likely cause installation failure
- **Warning**: May cause issues but installation might succeed
- **Info**: Informational messages

### Issue Categories

- `ArchiveValidation` - Archive file issues (missing, corrupted)
- `ExtractArchive` - Problems extracting archives
- `MoveFile` / `CopyFile` - File operation issues
- `DeleteFile` - Attempting to delete non-existent files
- `RenameFile` - Rename operation issues
- `ExecuteProcess` - Executable not found
- `Patcher` - TSLPatcher directory issues
- `PathResolution` - Cannot resolve file paths
- `DependencyValidation` - Dependency/restriction conflicts

## Integration with PreinstallValidationService

The dry-run validation is now integrated into the standard pre-install checks:

```csharp
// After existing validation checks...
DryRunValidationResult dryRunResult = await DryRunValidator.ValidateInstallationAsync(
    MainConfig.AllComponents,
    CancellationToken.None
);

if (!dryRunResult.IsValid)
{
    // Log errors and return failure
    // Show detailed issues to user
}
```

## User Experience

### For End Users

When validation fails, users see:

- Clear explanation of what's wrong
- Which mods have issues
- Actionable steps to resolve:
  - Download missing mods
  - Disable problematic mods (auto-resolve available)
  - Contact support for unfixable issues

Example message:

```
✗ Validation Failed

Issues by component:
━━━ PartySwap ━━━
✗ Archive file does not exist: PartySwap_v1.4.zip
  → This archive may be missing, corrupted, or incompatible.
     Try re-downloading it from the mod link.
```

### For Mod Build Editors

When validation fails in editor mode, creators see:

- Detailed technical information
- Instruction-level details (action, source, destination)
- Specific guidance on how to fix instructions
- Line numbers and affected components

Example message:

```
✗ Validation Failed

━━━ Component: PartySwap (GUID: 12345678-...) ━━━

  Instruction #3:
    Action: Move
    Source: temp\PartySwap\Override\*.*
    Destination: <<kotorDirectory>>\Override

  ✗ [MoveFile] Source file does not exist: appearance.2da
     → Add an Extract instruction before this operation, or verify
        the source path is correct. Check if the file should come
        from a previous component's instructions.
```

## Benefits

### 1. **Early Error Detection**

Catches issues before any files are modified:

- Missing archives
- Incorrect file paths
- Wrong instruction order
- File conflicts

### 2. **Safe Validation**

No risk to the user's installation:

- No files are modified
- No registry changes
- Completely reversible

### 3. **Comprehensive Checking**

Validates:

- ✅ All instruction types
- ✅ File existence at each step
- ✅ Archive contents
- ✅ Directory structures
- ✅ Overwrite conflicts
- ✅ Dependency order

### 4. **Actionable Feedback**

Users know exactly what to do:

- Which mod to download
- Which mod to disable
- Whether auto-fix is available
- Where to get help

### 5. **Editor Support**

Mod build creators can:

- Validate instructions before publishing
- Get specific fix suggestions
- Identify order-of-operation issues
- Test complex dependency chains

## Usage Examples

### Basic Validation

```csharp
using KOTORModSync.Core.Services.Validation;

// Run validation
DryRunValidationResult result = await DryRunValidator.ValidateInstallationAsync(
    MainConfig.AllComponents
);

if (result.IsValid)
{
    Console.WriteLine("✓ Ready to install!");
}
else
{
    Console.WriteLine(result.GetEndUserMessage());
}
```

### With GUI Integration

```csharp
using KOTORModSync.Core.Services.Validation;

// Get actionable steps for UI
List<ActionableStep> steps = ValidationResultPresenter.GetActionableSteps(
    result,
    isEditorMode: true
);

foreach (var step in steps)
{
    // Create UI button/action
    if (step.CanAutoResolve)
    {
        // Show "Fix Automatically" button
    }
    else
    {
        // Show manual instructions
    }
}

// Auto-resolve if possible
if (ValidationResultPresenter.CanAutoResolve(result))
{
    int fixed = ValidationResultPresenter.AutoResolveIssues(result);
    Console.WriteLine($"Automatically disabled {fixed} problematic mod(s)");
}
```

### Highlighting Components in UI

```csharp
// Get components that should be highlighted in the UI
List<Component> affectedComponents = ValidationResultPresenter.GetComponentsToHighlight(result);

foreach (var component in affectedComponents)
{
    // Highlight in red or show warning icon in UI
}
```

## Future Enhancements

Potential improvements:

1. **Detailed Archive Content Validation** - Check specific files in archives match expectations
2. **Disk Space Calculation** - Estimate required disk space before installation
3. **Performance Profiling** - Estimate installation time
4. **Conflict Resolution Suggestions** - Automatically suggest instruction reordering
5. **Visual Dependency Graph** - Show component dependency relationships
6. **Undo/Rollback Planning** - Plan rollback strategy before installation

## Technical Notes

### C# Language Compatibility

The code is written for C# 7.3 compatibility (targeting .NET Standard 2.0):

- No switch expressions (converted to if-else)
- No pattern matching with `or` (converted to `||`)
- No nullable reference types

### Performance Considerations

- Archive contents are scanned asynchronously on initialization
- File operations are simulated in O(1) time using HashSets
- Validation typically completes in seconds for builds with 100+ mods

### Thread Safety

The `VirtualFileSystemProvider` is not thread-safe by design, as validation runs sequentially to match real installation order.

## Troubleshooting

### "Archive could not be scanned"

- Archive may be corrupted
- Unsupported archive format
- File permissions issue

### "Too many false positives"

- Archive contents may not be cached yet
- Check that `MainConfig.SourcePath` and `MainConfig.DestinationPath` are set correctly

### "Validation takes too long"

- Large archives are being scanned
- Many components selected
- Consider adding progress reporting for GUI

## Credits

Designed and implemented as part of KOTORModSync's comprehensive mod installation validation system. Integrates seamlessly with existing validation in `PreinstallValidationService`.
