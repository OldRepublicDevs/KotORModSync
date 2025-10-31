# CleanList ActionType Implementation

## Overview

The `CleanList` ActionType has been implemented to automate the process of conditionally deleting conflicting files based on which mods are selected for installation. This is specifically designed to handle cases like the "Character Textures & Model Fixes" mod by Redrob41, which includes files that may conflict with other installed mods.

## Problem Statement

When installing comprehensive texture/model packs like Redrob's Character Textures & Model Fixes, certain files in the pack will overwrite or conflict with other mods the user has selected. Previously, users had to manually:

1. Download a batch script and cleanlist file
2. Run the batch script
3. Manually answer Y/N for each mod they have installed
4. Delete conflicting files accordingly

This manual process was error-prone and required users to know exactly which other mods they were using.

## Solution

The `CleanList` ActionType automates this entire process by:

1. Reading a CSV-formatted cleanlist file
2. Automatically detecting which mods are selected in the current installation
3. Conditionally deleting conflicting files based on mod selection
4. Providing detailed logging of all operations

## Cleanlist File Format

The cleanlist file is a CSV format where each line contains:

- **First field**: Mod name (human-readable, e.g., "HD Astromechs by Dark Hope")
- **Subsequent fields**: Comma-separated list of files to delete if that mod is selected

### Example Cleanlist Entry

```csv
HD Astromechs by Dark Hope,C_DrdAstro01.tpc,C_DrdAstro02.tpc,C_DrdAstro03.tpc,P_T3M3_01.tpc
HD Protocol Droids by Dark Hope,C_DrdProt01.tpc,C_DrdProt02.tpc,C_DrdProt03.tpc,C_DrdProt04.tpc
Mandatory Deletions: Click Y,P_BastilaH04.tpc
```

## Usage in TOML Instructions

### Basic Example

```toml
[[Instruction]]
Guid = "12345678-1234-1234-1234-123456789abc"
Action = "CleanList"
Source = ["<<modDirectory>>/cleanlist_k1.txt"]
Destination = "<<modDirectory>>/RedroBs_Character_Textures/Override"
```

### Fields

- **Action**: Must be `"CleanList"`
- **Source**: Array with one element - the path to the cleanlist CSV file
  - Supports placeholders: `<<modDirectory>>` and `<<kotorDirectory>>`
- **Destination**: The base directory where files should be deleted from
  - Typically the extracted mod folder before files are moved to the game's Override
  - Supports placeholders
- **Dependencies**: (Optional) Standard instruction dependencies
- **Restrictions**: (Optional) Standard instruction restrictions
- **Arguments**: Not used for CleanList (reserved for future use)
- **Overwrite**: Not used for CleanList

## How It Works

### Execution Flow

1. **Path Resolution**: The instruction resolves Source and Destination paths using `SetRealPaths(skipExistenceCheck: true)`

2. **Mod Name Matching**: For each line in the cleanlist, the system checks if a mod with that name is selected:
   - Exact match (case-insensitive): `"HD Astromechs by Dark Hope"` matches component named `"HD Astromechs by Dark Hope"`
   - Partial match: `"HD Astromechs by Dark Hope"` matches component named `"HD Astromechs"` or `"Dark Hope's HD Astromechs"`
   - This fuzzy matching handles variations in mod naming

3. **Conditional Deletion**: If a mod is selected, all files listed for that mod are deleted from the Destination directory

4. **Logging**: The system logs:
   - Each mod processed and how many files were deleted
   - Each file deleted successfully
   - Files that weren't found (logged as verbose)
   - Any errors encountered during deletion

### Virtual File System Support

The CleanList implementation fully supports the VirtualFileSystemProvider for dry-run validation:

- During dry runs, file deletions are simulated in the VFS
- Path validation checks if the cleanlist file exists before execution
- No actual files are deleted during validation/dry runs

## Implementation Details

### Core Components Modified

1. **Instruction.cs**
   - Added `CleanList` to `ActionType` enum
   - Implemented `ExecuteCleanListAsync()` method
   - Updated `ShouldSerializeDestination()` to include CleanList

2. **ModComponent.cs**
   - Added `CleanList` case to `ExecuteSingleInstructionAsync()` switch statement
   - Implemented mod name matching logic using local function `IsModSelected()`
   - Passes componentsList to enable mod selection checking

3. **ComponentValidation.cs**
   - Added `CleanList` to validation switch statement

4. **DryRunValidator.cs**
   - Added `CleanList` case to path validation
   - Validates that cleanlist file exists during dry runs

5. **InstructionDestinationConverter.axaml.cs** (GUI)
   - Added display text for CleanList actions: "→ clean files in {destination}"

### Method Signature

```csharp
public async Task<ActionExitCode> ExecuteCleanListAsync(
    string cleanlistPath = null,              // Path to cleanlist CSV, defaults to RealSourcePaths[0]
    DirectoryInfo targetDirectory = null,     // Directory to delete files from, defaults to RealDestinationPath
    Func<string, bool> isModSelectedFunc = null  // Function to check if mod is selected, defaults to always true
)
```

### Return Values

- `ActionExitCode.Success`: All operations completed successfully
- `ActionExitCode.FileNotFoundPost`: Cleanlist file not found
- `ActionExitCode.UnknownInnerError`: Some files failed to delete (partial success)
- `ActionExitCode.UnknownError`: Critical error occurred

## Telemetry

The CleanList operation records telemetry data:

- Operation type: `"cleanlist"`
- Success/failure status
- Number of files deleted
- Duration in milliseconds
- Error messages (if any)

## Security Considerations

The CleanList action respects the application's path sandboxing model:

- All paths must use `<<modDirectory>>` or `<<kotorDirectory>>` placeholders
- Paths are resolved and validated in `SetRealPaths()`
- Cannot target arbitrary system directories
- File operations are restricted to configured source and destination paths

## Example Use Case: Redrob's Character Textures

### Installation Flow

1. Extract Redrob's texture pack to `<<modDirectory>>/RedroBs_Character_Textures/`
2. Place `cleanlist_k1.txt` in the mod directory
3. Execute CleanList instruction:

   ```toml
   [[Instruction]]
   Action = "CleanList"
   Source = ["<<modDirectory>>/cleanlist_k1.txt"]
   Destination = "<<modDirectory>>/RedroBs_Character_Textures/Override"
   ```

4. Move remaining files to game Override:

   ```toml
   [[Instruction]]
   Action = "Move"
   Source = ["<<modDirectory>>/RedroBs_Character_Textures/Override/*"]
   Destination = "<<kotorDirectory>>/Override"
   ```

### What Happens

- If user has "HD Astromechs by Dark Hope" selected:
  - `C_DrdAstro01.tpc`, `C_DrdAstro02.tpc`, `C_DrdAstro03.tpc`, `P_T3M3_01.tpc` are deleted from Redrob's folder
  - Redrob's versions of those files won't overwrite Dark Hope's mod

- If user doesn't have "HD Astromechs" selected:
  - Those files are kept
  - Redrob's versions will be installed

## Benefits

1. **Automation**: No manual user intervention required
2. **Accuracy**: System knows exactly which mods are selected
3. **Consistency**: Same behavior every time, no human error
4. **Logging**: Complete audit trail of what was deleted and why
5. **Dry-Run Compatible**: Can be validated before actual installation
6. **Cross-Platform**: Works on all platforms (Windows, Linux, macOS)

## Future Enhancements

Potential improvements for future versions:

- Support for regex patterns in file names
- Ability to specify multiple cleanlist files in one instruction
- Option to move conflicting files instead of deleting them
- Enhanced reporting of conflicts and resolutions
- GUI indicator showing which files will be affected
