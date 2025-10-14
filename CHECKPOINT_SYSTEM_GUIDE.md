# KOTORModSync Checkpoint System - Technical Guide

## Overview

The KOTORModSync checkpoint system provides **incremental, space-efficient backup and rollback capabilities** for mod installations using industry-standard binary differencing. It allows users to restore their game to any previous state without requiring massive full backups of the 80GB+ KOTOR directory.

This system was designed after extensive research and storage calculations to achieve optimal balance between:
- **Storage Efficiency**: ~2x original game size for full installation history
- **Restore Speed**: 10-30 seconds for nearby checkpoints, 1-2 minutes for distant ones
- **Reliability**: Content-addressable storage prevents corruption and enables deduplication

---

## Architecture

### Core Technologies

#### 1. **Content-Addressable Storage (CAS)**

All file contents and binary deltas are stored by their **SHA256 hash**, not by filename. This provides:

- **Automatic Deduplication**: Identical files stored once, even across different mods/sessions
- **Integrity Verification**: Hash mismatch detection prevents corruption
- **Cross-session Sharing**: Common files between installations share storage
- **Garbage Collection**: Safely remove unreferenced objects

**Storage Structure:**
```
.kotor_modsync/checkpoints/
  objects/
    ab/
      cd1234567890...  # File content or delta (first 2 chars = subdirectory)
    ef/
      gh5678901234...
```

#### 2. **Bidirectional Delta Chain with Anchors**

Each checkpoint stores **binary differences** (deltas) from adjacent checkpoints using **Octodiff**, not entire files.

**Strategy:**
- **Regular Checkpoints** (1-9, 11-19, etc.): Store forward + reverse deltas to neighbors
- **Anchor Checkpoints** (10, 20, 30, ...): Store delta from baseline + neighbor deltas
- **Baseline**: Initial snapshot of game directory before any mods installed

**Why Bidirectional?**
- Forward deltas: Efficient navigation from checkpoint N to N+1
- Reverse deltas: Fast backward navigation from N+1 to N
- Enables restoration in both directions without recomputing

#### 3. **Octodiff Binary Differencing**

Industry-standard C# library for creating and applying binary patches:
- **Rolling hash signatures**: Efficiently finds matching blocks between files
- **Streaming operations**: Handles 79GB files without loading into memory
- **Compression-friendly**: Deltas are typically 1-10% of file size for similar binaries

---

## Storage Efficiency Analysis

### Real-World Scenario

**Installation Profile:**
- **Total Size**: 80GB KOTOR directory
- **Large Files**: 10 files @ 7.9GB each = 79GB (e.g., movies, voice archives)
- **Regular Files**: Remaining 1GB (textures, scripts, 2DA files, etc.)
- **Mod Count**: 200 mods installed
- **Large File Mods**: 1 mod touches the 10 large files
- **Regular Mods**: 199 mods touch only small files

### Storage Calculation

#### Baseline (Checkpoint 0)
- **Purpose**: Initial game state before any modifications
- **Storage**: 0 bytes (files remain in game directory)
- **Metadata**: ~1MB (file paths, hashes, sizes)

#### Large File Mod (Checkpoint 1)
- **Changes**: 10 files @ 7.9GB each modified
- **Forward Deltas**: 10 × 100MB = 1GB (estimated ~1.3% delta for binary patches)
- **Reverse Deltas**: 10 × 100MB = 1GB (to restore original files)
- **Stored in CAS**: 2GB total
- **Note**: Original 79GB already exists in game directory

#### Regular Mods (Checkpoints 2-200)
Each mod changes ~50-100 small files:

**Non-Anchor Checkpoints (2-9, 11-19, etc.)** - 180 checkpoints:
- **Forward Delta**: ~5MB per checkpoint
- **Reverse Delta**: ~5MB per checkpoint
- **Total per checkpoint**: ~10MB
- **Subtotal**: 180 × 10MB = **1.8GB**

**Anchor Checkpoints (10, 20, 30, ... 200)** - 20 checkpoints:
- **Delta from Baseline**: ~75MB (cumulative changes from original)
- **Forward Delta**: ~5MB (to next checkpoint)
- **Reverse Delta**: ~5MB (to previous checkpoint)
- **Total per anchor**: ~85MB
- **Subtotal**: 20 × 85MB = **1.7GB**

#### Total Storage Estimate

| Component | Size |
|-----------|------|
| Baseline metadata | 1MB |
| Large file mod deltas (Checkpoint 1) | 2GB |
| Regular checkpoint deltas (2-200) | 1.8GB |
| Anchor checkpoint deltas | 1.7GB |
| Session/checkpoint metadata | ~100MB |
| **Total** | **~5.6GB** |

**Storage Efficiency**: 5.6GB / 80GB = **7% of original size** for full installation history!

### Why This Works

1. **Large files touched once**: Original 79GB stays in game directory, only 2GB deltas stored
2. **Small file deduplication**: Common files (like TSLPatcher.exe) stored once via CAS
3. **Binary deltas are tiny**: Most file changes are small modifications, not full replacements
4. **Anchor strategy**: Prevents long delta chains without storing full copies

---

## Performance Characteristics

### Checkpoint Creation Time

**Per ModComponent:**
- **File Scanning**: 1-3 seconds (recursive directory scan + SHA256 hashing)
- **Change Detection**: < 1 second (hash comparison)
- **Delta Generation**: 
  - Small files (< 10MB): < 1 second
  - Large files (> 1GB): 5-20 seconds per file
  - Uses streaming, no memory issues
- **CAS Storage**: 1-2 seconds (copy/reference files)
- **Metadata Write**: < 1 second

**Total**: 3-10 seconds for typical mods, up to 30 seconds for large mods

### Restore Speed

Restore time depends on **distance** from current state to target checkpoint:

#### Nearby Restore (±10 checkpoints)
- **Operations**: Apply 1-10 forward or reverse deltas sequentially
- **Time**: 10-30 seconds
- **Example**: Currently at checkpoint 45, restore to checkpoint 40 = apply 5 reverse deltas

#### Far Restore (> 10 checkpoints)
- **Operations**: 
  1. Rebuild from nearest anchor (max 10 operations)
  2. Apply deltas to target (max 10 operations)
- **Time**: 1-2 minutes
- **Example**: Currently at checkpoint 100, restore to checkpoint 5:
  - Restore to anchor 10 (5 reverse deltas from checkpoint 100)
  - Apply reverse deltas from anchor 10 to checkpoint 5 (5 deltas)
  - Total: ~10 operations

#### Maximum Operations
- **Worst case**: ~20 operations to any checkpoint
- **Why**: Anchors every 10 checkpoints + bidirectional navigation
- **Optimization**: System automatically chooses shortest path

### Navigation Examples

```
Current: Checkpoint 100
Target: Checkpoint 5

Strategy:
1. Move backward to anchor 10 (apply reverse deltas 100→90→80→...→10)
2. Move backward to checkpoint 5 (apply reverse deltas 10→9→...→5)
Total: ~15 operations

---

Current: Checkpoint 5
Target: Checkpoint 100

Strategy:
1. Move forward to anchor 10 (apply forward deltas 5→6→...→10)
2. Jump to anchor 100 via baseline+anchor delta
3. Done
Total: ~7 operations
```

---

## Component Architecture

### 1. CheckpointService (`KOTORModSync.Core/Services/ImmutableCheckpoint/CheckpointService.cs`)

**Primary checkpoint orchestration service.**

#### Key Methods

```csharp
// Initialize session and create baseline snapshot
Task<string> StartInstallationSessionAsync(CancellationToken cancellationToken)

// Create checkpoint after each ModComponent installation
Task<string> CreateCheckpointAsync(
    string componentName, 
    string componentGuid, 
    CancellationToken cancellationToken)

// Restore game directory to specific checkpoint
Task RestoreCheckpointAsync(
    string checkpointId, 
    CancellationToken cancellationToken)

// List all checkpoints in session
Task<List<Checkpoint>> ListCheckpointsAsync(string sessionId)

// Complete session and optionally clean up
Task CompleteSessionAsync(bool keepCheckpoints)

// Remove session and orphaned CAS objects
Task DeleteSessionAsync(string sessionId)

// Garbage collect unreferenced CAS objects
Task<int> GarbageCollectAsync()

// Validate checkpoint integrity
Task<(bool isValid, List<string> errors)> ValidateCheckpointAsync(string checkpointId)

// Attempt to repair corrupted checkpoint
Task<bool> TryRepairCheckpointAsync(string checkpointId)
```

#### Checkpoint Creation Algorithm

1. **Scan Game Directory**
   - Recursively enumerate all files
   - Compute SHA256 hash for each file
   - Record file metadata (size, last modified)

2. **Detect Changes**
   - Compare current file states with previous checkpoint
   - Identify: Added files, Modified files (hash changed), Deleted files

3. **Determine Checkpoint Type**
   - If `(sequenceNumber % 10) == 0`: Anchor checkpoint
   - Else: Regular checkpoint

4. **Generate Deltas**
   - **Regular**: Create forward + reverse deltas from previous checkpoint
   - **Anchor**: Create delta from baseline + neighbor deltas
   - **Large files**: Stream-based processing to avoid memory issues

5. **Store in CAS**
   - Store new file contents by hash
   - Store generated deltas by hash
   - Automatic deduplication (existing hashes not re-stored)

6. **Save Metadata**
   - Create checkpoint JSON with:
     - File states (path → hash mapping)
     - Change summary (added/modified/deleted counts)
     - Delta references (CAS hashes)
     - Component info (name, GUID, timestamp)

#### Restoration Algorithm

1. **Load Target Checkpoint**
   - Read checkpoint metadata from JSON
   - Validate CAS references exist

2. **Find Optimal Path**
   - Current checkpoint → Target checkpoint
   - Choose shortest path using anchors and bidirectional deltas

3. **Apply Deltas**
   - Restore files from CAS by hash
   - Apply binary deltas in sequence (forward or reverse)
   - Delete files marked as added after target
   - Stream large file operations

4. **Verify State**
   - Compute hashes of restored files
   - Confirm match with target checkpoint metadata

### 2. ContentAddressableStore (`KOTORModSync.Core/Services/ImmutableCheckpoint/ContentAddressableStore.cs`)

**Manages content-addressed file storage.**

#### Methods

```csharp
// Store file and return its SHA256 hash
Task<string> StoreFileAsync(string filePath)

// Retrieve file from CAS by hash to destination
Task<string> RetrieveFileAsync(string hash, string destinationPath)

// Check if object exists in CAS
bool HasObject(string hash)

// Get path to CAS object (for streaming)
string GetObjectPath(string hash)

// Compute SHA256 hash without storing
Task<string> ComputeHashAsync(string filePath)
```

#### Storage Layout

```
.kotor_modsync/checkpoints/objects/
  ab/
    cd1234567890abcdef...  # Full file content or delta
    cd9999888777666555...
  ef/
    gh1111222233334444...
```

**Why two-level directories?**
- Prevents millions of files in one directory (filesystem performance)
- First 2 chars of hash = subdirectory name
- Remaining chars = filename

### 3. BinaryDiffService (`KOTORModSync.Core/Services/ImmutableCheckpoint/BinaryDiffService.cs`)

**Handles binary differencing using Octodiff.**

#### Methods

```csharp
// Create forward and reverse deltas between two files
Task<(string forwardDeltaHash, string reverseDeltaHash, long forwardSize, long reverseSize)>
CreateBidirectionalDeltaAsync(
    string sourceFilePath, 
    string targetFilePath,
    CancellationToken cancellationToken)

// Apply delta to recreate target file
Task ApplyDeltaAsync(
    string sourceFilePath,
    string deltaHash,
    string outputPath,
    CancellationToken cancellationToken)
```

#### How Octodiff Works

1. **Signature Generation**: Create rolling hash signature of source file
2. **Delta Generation**: Compare target with signature, output differences
3. **Delta Application**: Apply differences to source to recreate target

**Key Benefits:**
- Handles binary files (not just text)
- Streaming operations (low memory usage)
- Efficient for large files with small changes

### 4. InstallationCoordinatorService (`KOTORModSync.Core/Services/InstallationCoordinatorService.cs`)

**Orchestrates mod installation with automatic checkpointing.**

#### Integration Points

```csharp
// Start checkpoint session before installation
_currentSessionId = await _checkpointService.StartInstallationSessionAsync(cancellationToken);

// After each successful ModComponent installation
string checkpointId = await _checkpointService.CreateCheckpointAsync(
    component.Name,
    component.Guid.ToString(),
    cancellationToken);

// On error, offer rollback
if (errorOccurred && userWantsRollback) {
    await RollbackInstallationAsync(progress, cancellationToken);
}

// Complete session at end
await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);
```

---

## File Structure

### Directory Layout

```
<KOTOR_Directory>/.kotor_modsync/checkpoints/
├── objects/                              # Content-Addressable Storage
│   ├── ab/
│   │   ├── cd1234567890...              # File content or delta
│   │   └── cd9876543210...
│   └── ef/
│       └── gh5555666677...
│
├── sessions/                             # Installation sessions
│   ├── 550e8400-e29b-41d4-a716-446655440000/
│   │   ├── session.json                 # Session metadata
│   │   ├── baseline.json                # Checkpoint 0 (initial state)
│   │   ├── checkpoint_00001.json        # Checkpoint 1
│   │   ├── checkpoint_00002.json        # Checkpoint 2
│   │   ├── checkpoint_00010.json        # Anchor checkpoint
│   │   └── ...
│   └── 7c9e6679-7425-40de-944b-e07fc1f90ae7/
│       └── ...
```

### Session Metadata Format

**`session.json`:**
```json
{
  "Id": "550e8400-e29b-41d4-a716-446655440000",
  "Name": "Installation_2025-01-15_14-30-00",
  "GamePath": "C:/Program Files/Steam/steamapps/common/Knights of the Old Republic",
  "StartTime": "2025-01-15T14:30:00Z",
  "EndTime": "2025-01-15T15:45:00Z",
  "IsComplete": true,
  "TotalComponents": 200,
  "CompletedComponents": 200,
  "CheckpointIds": [
    "baseline",
    "00001",
    "00002",
    ...
  ]
}
```

### Checkpoint Metadata Format

**`checkpoint_00001.json`:**
```json
{
  "Id": "00001",
  "SessionId": "550e8400-e29b-41d4-a716-446655440000",
  "ComponentName": "K1 Community Patch",
  "ComponentGuid": "123e4567-e89b-12d3-a456-426614174000",
  "Sequence": 1,
  "Timestamp": "2025-01-15T14:35:00Z",
  "PreviousId": "baseline",
  "IsAnchor": false,
  "PreviousAnchorId": "baseline",
  "Files": {
    "Override/appearance.2da": {
      "Path": "Override/appearance.2da",
      "Hash": "abc123...",
      "CASHash": "abc123...",
      "Size": 12345,
      "LastModified": "2025-01-15T14:34:00Z"
    },
    ...
  },
  "Added": [
    "Override/new_texture.tga"
  ],
  "Modified": [
    {
      "Path": "Override/appearance.2da",
      "SourceHash": "old_hash...",
      "TargetHash": "new_hash...",
      "SourceCASHash": "old_hash...",
      "TargetCASHash": "new_hash...",
      "ForwardDeltaCASHash": "delta_forward_hash...",
      "ReverseDeltaCASHash": "delta_reverse_hash...",
      "SourceSize": 10000,
      "TargetSize": 12345,
      "ForwardDeltaSize": 500,
      "ReverseDeltaSize": 480,
      "Method": "octodiff"
    }
  ],
  "Deleted": [],
  "TotalSize": 50000000,
  "DeltaSize": 2500000,
  "FileCount": 45
}
```

### Anchor Checkpoint Format

**`checkpoint_00010.json`:**
```json
{
  "Id": "00010",
  "Sequence": 10,
  "IsAnchor": true,
  "PreviousAnchorId": "baseline",
  "Modified": [
    {
      "Path": "Override/cumulative_changes.2da",
      "SourceHash": "baseline_hash...",
      "TargetHash": "checkpoint10_hash...",
      "ForwardDeltaCASHash": "baseline_to_10_delta...",
      "ReverseDeltaCASHash": "10_to_baseline_delta...",
      "Method": "octodiff"
    }
  ],
  ...
}
```

**Note**: Anchors store deltas from baseline AND neighbor deltas, enabling fast long-distance jumps.

---

## User Guide

### During Installation

Checkpoints are created **automatically after each mod is successfully installed**. No user interaction required!

**Progress Indication:**
- Progress window shows "Creating checkpoint for '<ModName>'..."
- Displays "Scanning for file changes..." during detection
- Footer shows checkpoint creation progress

**What's Happening:**
1. Mod installs (all instructions executed)
2. System scans game directory
3. Detects file changes
4. Generates binary deltas
5. Stores in CAS
6. Saves checkpoint metadata
7. Continues to next mod

### Handling Installation Errors

If a mod installation fails, you'll see an **Installation Error Dialog** with options:

#### Option 1: Rollback Installation (Recommended)
- Restores game to state **before the installation began**
- Undoes all mods installed in this session
- Uses baseline checkpoint
- **Time**: 30 seconds - 2 minutes depending on changes

#### Option 2: Skip This Mod
- Continues installation with remaining mods
- Failed mod is skipped
- Checkpoints continue normally
- **Risk**: Installation may be incomplete

#### Option 3: Abort Installation
- Stops installation immediately
- Keeps all changes made so far
- No rollback performed
- **Use**: When you want to investigate manually

### Managing Checkpoints

#### Access Checkpoint Management

**GUI:**
1. Click **Tools** → **Manage Checkpoints**
2. Or click the checkpoint icon in toolbar

**Dialog Features:**
- **Left Panel**: List of installation sessions
  - Shows: Session name, date, checkpoint count, status
  - Status indicators: ✅ Completed | ⏳ In Progress
- **Right Panel**: Checkpoints for selected session
  - Shows: Component name, timestamp, changes, delta size
  - 📍 icon for anchor checkpoints

#### Restore to Previous Checkpoint

1. Select installation session from left panel
2. Browse checkpoints in right panel
3. Click **Restore** on desired checkpoint
4. Review confirmation dialog:
   - Shows number of subsequent mods that will be undone
   - Lists file changes at that checkpoint
   - Indicates if it's an anchor (faster restore)
5. Click **Restore** to confirm
6. Wait for restoration to complete

**Restoration Details:**
- Game directory will match state **after** the selected mod was installed
- All mods installed **after** will be removed
- Process is **logged** for troubleshooting
- Takes 10 seconds - 2 minutes depending on distance

#### Clean Up Old Sessions

Checkpoint data accumulates over time. Clean up when no longer needed:

1. Open Checkpoint Management Dialog
2. Click **Clean Up Old Sessions**
3. Confirm deletion
4. System will:
   - Delete all **completed** sessions
   - Remove **orphaned CAS objects** (garbage collection)
   - Keep **in-progress** sessions
   - Report freed disk space

**Storage Info:**
- Footer shows: `💾 X session(s), Y checkpoint(s), Z.Z GB total storage`
- Updated in real-time

#### Manual Cleanup

If you need to manually remove checkpoint data:

**Location**: `<KOTOR_Directory>/.kotor_modsync/checkpoints/`

**Safe to Delete:**
- Entire `.kotor_modsync` directory (removes all checkpoints)
- Individual session directories (removes specific sessions)
- Run garbage collection afterward to clean orphaned CAS objects

**DO NOT delete** while installation is in progress!

---

## Advanced Features

### Validation and Corruption Detection

The checkpoint system includes robust validation:

#### Automatic Validation
- **On Restore**: Verifies all CAS references exist
- **On Load**: Checks checkpoint metadata integrity
- **Hash Verification**: Confirms file contents match expected hash

#### Manual Validation

```csharp
// Validate single checkpoint
var (isValid, errors) = await checkpointService.ValidateCheckpointAsync(checkpointId);
if (!isValid) {
    foreach (var error in errors) {
        Logger.LogError(error);
    }
}

// Validate entire session
var (sessionValid, errorsByCheckpoint) = await checkpointService.ValidateSessionAsync(sessionId);
```

#### Automatic Repair

If corruption detected, the system can attempt repair:

```csharp
bool repaired = await checkpointService.TryRepairCheckpointAsync(checkpointId);
```

**Repair Process:**
1. Identify missing CAS objects
2. Check if files still exist in game directory
3. Recompute hashes and restore to CAS
4. Validate repair succeeded

**Limitations**: Only works if original files still exist in game directory.

### Garbage Collection

Removes CAS objects no longer referenced by any checkpoint:

```csharp
int orphanedObjects = await checkpointService.GarbageCollectAsync();
Logger.LogInfo($"Removed {orphanedObjects} orphaned objects");
```

**When to Run:**
- After deleting sessions
- After failed installations
- Periodically for housekeeping

**How It Works:**
1. Scan all checkpoint metadata for CAS references
2. Identify CAS objects not referenced
3. Safely delete orphaned objects
4. Report freed space

---

## Performance Optimization Tips

### For Users

1. **Install on SSD**: Dramatically faster checkpoint creation and restoration
2. **Regular Cleanup**: Delete old sessions you don't need
3. **Sufficient Space**: Keep 10-20% free disk space for optimal performance
4. **Close Other Programs**: Reduce disk I/O contention during installation

### For Developers

1. **Async I/O**: All file operations use async patterns
2. **Streaming**: Large files processed in chunks, not loaded to memory
3. **Progress Reporting**: Updates every 100 files to avoid UI lag
4. **Parallel Hashing**: Consider multi-threaded SHA256 computation
5. **CAS Deduplication**: Automatic, no additional work needed

---

## Troubleshooting

### "Checkpoint creation is slow"

**Causes:**
- Large number of files changed (500+)
- Large individual files (> 1GB)
- Slow disk (HDD vs SSD)

**Solutions:**
- Normal for large mods, be patient
- Consider SSD upgrade
- Check disk space (> 10GB free recommended)

**Expected Times:**
- Small mod (< 100 files): 3-5 seconds
- Medium mod (100-500 files): 5-15 seconds
- Large mod (500+ files): 15-60 seconds

### "Restoration failed"

**Possible Causes:**
- Missing CAS objects (corruption)
- Insufficient disk space
- File access permissions

**Solutions:**
1. Check log file for specific error
2. Try validation: `ValidateCheckpointAsync(checkpointId)`
3. Attempt repair: `TryRepairCheckpointAsync(checkpointId)`
4. Restore to earlier checkpoint
5. If all else fails, reinstall from scratch

### "Too much disk space used"

**Expected Usage**: ~7-15% of game directory size for full installation history

**If Higher:**
1. Run cleanup: Delete old completed sessions
2. Run garbage collection
3. Check for duplicate sessions (failed installations)
4. Review checkpoint count (> 500 is unusual)

**Manual Cleanup:**
- Delete `.kotor_modsync/checkpoints/` directory entirely
- Removes ALL checkpoints and history

### "Error during checkpoint creation"

**Common Errors:**

| Error | Cause | Solution |
|-------|-------|----------|
| Insufficient disk space | < 5GB free | Free up space, delete old checkpoints |
| Access denied | Permissions issue | Run as administrator, check folder permissions |
| File in use | Another program locking file | Close other programs, retry |
| Hashing failed | Corrupted file | Verify game files via Steam/GOG |

**Debug Steps:**
1. Check log file in output window
2. Verify disk space: `> 10GB free`
3. Check permissions: Can you create files in game directory?
4. Retry installation
5. Report issue on GitHub if persistent

### "Anchor checkpoints taking long time"

**Expected**: Anchor checkpoints take 2-3x longer than regular checkpoints

**Why**: Generate deltas from baseline (more file processing)

**Normal Behavior**: 
- Regular checkpoint: 5-10 seconds
- Anchor checkpoint: 15-30 seconds

**Only concerned if**: > 2 minutes for anchor checkpoint

---

## Technical FAQ

### Q: Why bidirectional deltas instead of just forward?

**A**: Fast backward navigation. Without reverse deltas, rolling back 100 checkpoints would require:
1. Restore baseline
2. Apply 100 forward deltas to reach target

With reverse deltas: Just apply reverse deltas directly.

### Q: Why anchors every 10 checkpoints?

**A**: Balance between storage and restore speed.

**Trade-off Analysis:**
- **Every 5**: Faster long jumps, 2x more anchor storage
- **Every 10**: Good balance (chosen)
- **Every 20**: Slower long jumps, less storage

With 200 checkpoints:
- Max delta chain: 10 (acceptable performance)
- Anchor storage: ~1.7GB (reasonable)

### Q: Why SHA256 instead of MD5?

**A**: 
- **Security**: SHA256 is cryptographically secure
- **Collision Resistance**: Virtually impossible to have two different files with same hash
- **Integrity**: Detect corruption with high confidence
- **Performance**: Modern CPUs have SHA256 acceleration

### Q: How does CAS deduplication work across sessions?

**A**: If two sessions install the same mod:
1. Session 1 stores file with hash `abc123...`
2. Session 2 tries to store same file
3. Hash matches existing CAS object → skip storage
4. Both sessions reference same CAS object
5. Only deleted when neither session exists

### Q: What happens if I delete CAS objects manually?

**A**: Checkpoints referencing deleted objects become invalid.

**Recovery**:
1. Run `ValidateCheckpointAsync()` to detect missing objects
2. Run `TryRepairCheckpointAsync()` if files still in game directory
3. Otherwise, those checkpoints are unusable

**Prevention**: Always use built-in cleanup features!

### Q: Can I backup checkpoint data?

**A**: Yes! The `.kotor_modsync/checkpoints/` directory is self-contained.

**To Backup**:
1. Copy entire `checkpoints/` directory to external drive
2. Compress for space savings

**To Restore**:
1. Copy `checkpoints/` directory back to `.kotor_modsync/`
2. Sessions will be available in checkpoint management dialog

### Q: Why not use git internally?

**A**: 
- Git is designed for text files and code, not 79GB binary files
- Git LFS still requires server infrastructure
- Octodiff is specifically designed for binary differencing
- More control over storage format and performance

### Q: Does this work on Linux/Mac?

**A**: Yes! The checkpoint system is fully cross-platform:
- .NET Standard 2.0 compatible
- Path handling uses `Path.Combine()` for OS-agnostic paths
- Octodiff works on all platforms

---

## API Reference

### For Integration

If you're building on top of KOTORModSync:

#### Start Installation with Checkpoints

```csharp
var coordinator = new InstallationCoordinatorService();
var fileSystemProvider = new RealFileSystemProvider();

// Subscribe to events
coordinator.ComponentInstallCompleted += (sender, e) => {
    Console.WriteLine($"Checkpoint created: {e.CheckpointId}");
};

coordinator.InstallationError += async (sender, e) => {
    // Show error dialog
    var dialog = new InstallationErrorDialog(e);
    var result = await dialog.ShowDialog<ErrorAction>(window);
    
    if (result == ErrorAction.Rollback) {
        e.RollbackRequested = true;
    }
};

// Execute with automatic checkpoints
var exitCode = await coordinator.ExecuteComponentsWithCheckpointsAsync(
    selectedComponents,
    MainConfig.DestinationPath.FullName,
    fileSystemProvider,
    progress,
    cancellationToken
);
```

#### Manual Checkpoint Management

```csharp
var checkpointService = new CheckpointService(gameDirectory);

// List all sessions
var sessions = await checkpointService.ListSessionsAsync();

// Get checkpoints for session
var checkpoints = await checkpointService.ListCheckpointsAsync(sessionId);

// Restore specific checkpoint
await checkpointService.RestoreCheckpointAsync(checkpointId, cancellationToken);

// Clean up
await checkpointService.DeleteSessionAsync(sessionId);
await checkpointService.GarbageCollectAsync();
```

---

## Future Enhancements

Potential improvements being considered:

1. **Compression**: Compress deltas before CAS storage (additional 30-50% space savings)
2. **Parallel Operations**: Multi-threaded hashing and delta generation
3. **Smart Anchors**: Dynamic anchor placement based on file change volume
4. **Cloud Backup**: Optional sync to cloud storage (Google Drive, OneDrive)
5. **Checkpoint Merging**: Combine multiple checkpoints to reduce chain length
6. **Incremental Validation**: Background validation of checkpoint integrity
7. **Web UI**: Browser-based checkpoint management
8. **Checkpoint Annotations**: User notes on specific checkpoints

---

## Credits and References

### Technologies Used

- **Octodiff**: Binary differencing library by Octopus Deploy
  - GitHub: https://github.com/OctopusDeploy/Octodiff
  - License: Apache 2.0

- **Newtonsoft.Json**: JSON serialization
- **.NET SHA256**: Cryptographic hashing

### Research References

- Nexus Mods App: Immutable mod management concepts
- Git Architecture: Content-addressable storage inspiration
- Rsync Algorithm: Delta synchronization concepts
- Octopus Deploy: Binary deployment and patching strategies

---

## Support

For issues, questions, or contributions:

- **GitHub Issues**: https://github.com/th3w1zard1/KOTORModSync/issues
- **Discord**: https://discord.gg/nDkHXfc36s
- **Documentation**: Check log files in output window for detailed error messages

---

**Last Updated**: January 2025  
**System Version**: 2.0.0  
**Implementation Status**: ✅ Production Ready
