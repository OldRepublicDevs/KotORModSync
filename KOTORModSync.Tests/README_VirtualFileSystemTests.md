# Virtual File System & Dry-Run Validation Test Suite

## Overview

This test suite provides **comprehensive coverage** of the Virtual File System (VFS) and dry-run validation functionality in KOTORModSync. All tests use **real archives created with 7-Zip** (no mocking) and validate that `VirtualFileSystemProvider` and `RealFileSystemProvider` produce **identical results**.

## Prerequisites

### Required Software

- **7-Zip** must be installed on your system
  - Windows: Install from <https://www.7-zip.org/>
  - The tests will automatically search for `7z.exe` in common locations:
    - `C:\Program Files\7-Zip\7z.exe`
    - `C:\Program Files (x86)\7-Zip\7z.exe`
    - System PATH

### Why 7-Zip?

- Creates real, valid ZIP archives for testing
- No mocking = tests verify actual extraction behavior
- Cross-platform compatibility

## Test Files

### 1. `VirtualFileSystemTests.cs`

**Purpose**: Core file system operation tests

**Test Coverage**:

- ✅ Basic archive extraction
- ✅ Move archive → extract (metadata tracking)
- ✅ Copy archive → extract both copies
- ✅ Rename archive → extract
- ✅ Extract multiple archives sequentially
- ✅ Move/copy/rename/delete extracted files
- ✅ Complex multi-mod installation workflows
- ✅ Nested archive operations (move→rename→copy→extract)
- ✅ Validation failures (missing files, invalid operations)

**Key Features**:

- Each test runs operations on **BOTH** virtual and real providers
- Compares final file system state between both
- Asserts that virtual provider tracked files match real files exactly

**Example Test Pattern**:

```csharp
[Fact]
public async Task Test_MoveArchiveThenExtract()
{
    // 1. Create real archive with 7-Zip
    CreateArchive("original.zip", files);

    // 2. Run instructions on BOTH providers
    var (virtualProvider, realProvider) = await RunBothProviders(instructions, ...);

    // 3. Assert virtual matches real
    Assert.Empty(virtualProvider.GetValidationIssues());
    AssertFileSystemsMatch(virtualProvider, realDestDir);
}
```

### 2. `VirtualFileSystemWildcardTests.cs`

**Purpose**: Wildcard pattern matching tests

**Test Coverage**:

- ✅ Star pattern (`*.txt`)
- ✅ Question mark pattern (`file?.txt`)
- ✅ Complex patterns (`data_backup_*.txt`)
- ✅ Wildcards in archive names (`mod_*.zip`)
- ✅ Multiple wildcard patterns in single instruction
- ✅ No matches (should fail)

**Critical Goal**: Ensure `PathHelper.WildcardPathMatch` produces **IDENTICAL** results for both providers.

**Why This Matters**:
The user specifically emphasized:
> "The behavior of wildcards CANNOT be modified at all i rely heavily on its functionality and it's extremely important to ensure it functions correctly in the virtual filesystemprovider"

These tests verify 1:1 wildcard behavior by:

1. Expanding wildcards with real file system
2. Expanding same wildcards with virtual file system
3. Asserting expanded file lists are identical

### 3. `DryRunValidationIntegrationTests.cs`

**Purpose**: End-to-end dry-run validation pipeline tests

**Test Coverage**:

- ✅ Valid installation (should pass)
- ✅ Invalid operation order (move after delete - should fail)
- ✅ Missing archive file (should fail)
- ✅ File not in archive (should fail)
- ✅ Multiple components with dependencies
- ✅ Overwrite conflicts (should warn)
- ✅ Complex workflow with all operation types
- ✅ Wildcard operations in dry-run

**Integration Level**:
Tests the full `DryRunValidator` → `VirtualFileSystemProvider` → `Component.ExecuteInstructionsAsync` pipeline.

## Running Tests

### Run All Tests

```bash
dotnet test KOTORModSync.Tests/KOTORModSync.Tests.csproj
```

### Run Specific Test File

```bash
# Virtual file system core tests
dotnet test --filter "FullyQualifiedName~VirtualFileSystemTests"

# Wildcard tests only
dotnet test --filter "FullyQualifiedName~VirtualFileSystemWildcardTests"

# Integration tests only
dotnet test --filter "FullyQualifiedName~DryRunValidationIntegrationTests"
```

### Run Single Test

```bash
dotnet test --filter "FullyQualifiedName~Test_MoveArchiveThenExtract"
```

### Verbose Output

```bash
dotnet test -v detailed
```

## Test Architecture

### Shared Pattern: Dual Provider Testing

Every test follows this pattern:

```
1. SETUP
   ├─ Create real archives with 7-Zip
   ├─ Define instructions (Extract, Move, Copy, etc.)
   └─ Create separate source/dest directories for each provider

2. EXECUTE WITH VIRTUAL PROVIDER
   ├─ Initialize VirtualFileSystemProvider
   ├─ Run Component.ExecuteInstructionsAsync
   └─ Capture tracked files and validation issues

3. EXECUTE WITH REAL PROVIDER
   ├─ Initialize RealFileSystemProvider
   ├─ Run SAME instructions on REAL file system
   └─ Capture actual file system state

4. COMPARE & ASSERT
   ├─ Assert no validation errors (if test expects success)
   ├─ Assert virtual tracked files = real files
   └─ Assert file names, paths, counts all match
```

### Why This Approach?

**Traditional Testing** (what we DON'T do):

```csharp
// ❌ Mock the file system
Mock<IFileSystem> mockFs = new Mock<IFileSystem>();
mockFs.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

// Problem: Doesn't test actual behavior, can drift from reality
```

**Our Approach** (what we DO):

```csharp
// ✅ Use REAL archives and REAL file system
CreateArchive("test.zip", realFiles); // 7-Zip creates actual archive
virtualProvider.ExtractArchive(...);  // Virtual simulates
realProvider.ExtractArchive(...);     // Real executes

// Assert virtual simulation matches reality
Assert.Equal(realFiles, virtualFiles);
```

**Benefits**:

- Tests verify actual extraction behavior
- Catches bugs that mocks would miss
- Ensures dry-run accurately predicts real installation
- No test/production code drift

## Edge Cases Covered

### Archive Operations

| Scenario | Test | Expected Behavior |
|----------|------|-------------------|
| Move archive → extract | ✅ `Test_MoveArchiveThenExtract` | Virtual tracks archive metadata through move |
| Copy archive → extract both | ✅ `Test_CopyArchiveThenExtractBoth` | Both archives extract correctly |
| Rename archive → extract | ✅ `Test_RenameArchiveThenExtract` | Archive metadata updates with new name |
| Delete archive → extract | ✅ Validation test | Should fail with error |
| Multiple moves → extract | ✅ `Test_NestedArchiveOperations` | Metadata tracked through all operations |

### File Operations

| Scenario | Test | Expected Behavior |
|----------|------|-------------------|
| Extract → move files | ✅ `Test_MoveExtractedFiles` | Files exist in virtual FS after extract |
| Extract → copy files | ✅ `Test_CopyExtractedFiles` | Files duplicated correctly |
| Extract → rename files | ✅ `Test_RenameExtractedFile` | File renamed in virtual FS |
| Extract → delete files | ✅ `Test_DeleteExtractedFile` | File removed from virtual FS |
| Move deleted file | ✅ `Test_MoveNonExistentFile_DetectedInDryRun` | Error detected in dry-run |

### Wildcard Operations

| Pattern | Test | Matches |
|---------|------|---------|
| `*.txt` | ✅ `Test_WildcardMove_StarPattern` | All .txt files |
| `file?.txt` | ✅ `Test_WildcardCopy_QuestionMarkPattern` | file1.txt, fileA.txt (not fileAB.txt) |
| `data_backup_*.txt` | ✅ `Test_WildcardDelete_ComplexPattern` | Files with prefix and suffix |
| `mod_*.zip` | ✅ `Test_WildcardInArchiveName` | Archive names matching pattern |
| Multiple patterns | ✅ `Test_WildcardMultiplePatterns` | All patterns combined |
| No matches | ✅ `Test_WildcardNoMatches_ShouldFail` | Throws exception |

### Complex Scenarios

| Scenario | Test | Components |
|----------|------|------------|
| Multi-mod installation | ✅ `Test_ComplexModInstallation_MultipleArchivesAndOperations` | 3 archives, moves, copies, deletes, patches |
| Nested operations | ✅ `Test_NestedArchiveOperations` | Rename→Copy→Move→Extract→Delete |
| Operation order validation | ✅ `Test_DryRunValidator_InvalidOperationOrder_Fails` | Extract→Delete→Move (fails) |
| File conflicts | ✅ `Test_DryRunValidator_OverwriteConflict_Warning` | Two mods writing same file |

## Validation Testing

### Success Cases

Tests that **should pass** dry-run validation:

- Valid installation with proper order
- Multiple components with dependencies
- Overwrite with `Overwrite=true` flag
- All operation types used correctly

### Failure Cases

Tests that **should fail** dry-run validation:

- Moving/copying non-existent files
- Operating on deleted files
- Missing archive files
- Files not present in archive
- Wildcard patterns with no matches

### Assertion Examples

```csharp
// 1. Virtual provider should detect issues
var issues = virtualProvider.GetValidationIssues();
Assert.Contains(issues, i =>
    i.Category == "MoveFile" &&
    i.Severity == ValidationSeverity.Error);

// 2. File system states should match
AssertFileSystemsMatch(virtualProvider, realDestDir);
// This checks:
// - Same files exist in both
// - Same file counts
// - Same directory structure

// 3. Wildcard expansion should match
Assert.Equal(realMatches, virtualMatches);
Assert.Equal(3, realMatches.Count);
Assert.All(realMatches, f => Assert.EndsWith(".txt", f));
```

## Test Isolation

Each test:

- ✅ Creates unique temporary directory (GUID-based)
- ✅ Cleans up after completion (or failure)
- ✅ Resets `MainConfig.SourcePath` and `DestinationPath`
- ✅ Uses separate directories for virtual vs real providers
- ✅ Independent of other tests (can run in parallel)

## Debugging Tests

### Enable Verbose Output

The `ITestOutputHelper` writes detailed logs:

```csharp
_output.WriteLine($"Virtual matched {count} files:");
foreach (var file in files)
    _output.WriteLine($"  {file}");
```

View output with:

```bash
dotnet test -v detailed
```

### Inspect Test Directories

Tests create directories in:

```sh
%TEMP%\KOTORModSync_VFS_Tests_<GUID>\
├── Virtual\     (virtual provider test files)
├── Real\        (real provider test files)
├── Source\      (input archives)
└── Destination\ (output directory)
```

### Common Issues

**"7-Zip not found"**:

- Install 7-Zip from <https://www.7-zip.org/>
- Ensure `7z.exe` is in PATH or default location

**Tests fail with different file counts**:

- Check wildcard expansion logic
- Verify `PathHelper.WildcardPathMatch` behavior
- Compare real vs virtual matched files in output

**Archive metadata not tracked**:

- Check `VirtualFileSystemProvider` move/copy/rename operations
- Ensure `_archiveContents` dictionary is updated

## Contributing

### Adding New Tests

1. **Use the established pattern**:

   ```csharp
   [Fact]
   public async Task Test_YourScenario()
   {
       // Arrange: Create archives with CreateArchive()
       // Act: RunBothProviders() or use DryRunValidator
       // Assert: Check issues and AssertFileSystemsMatch()
   }
   ```

2. **Test BOTH providers**:
   - Never test just virtual or just real
   - Always compare results

3. **Use real archives**:
   - Call `CreateArchive()` with file contents
   - Don't mock extraction behavior

4. **Document edge cases**:
   - Add comments explaining why test exists
   - Reference user requirements if applicable

### Test Naming Convention

```
Test_<Scenario>_<ExpectedOutcome>

Examples:
- Test_MoveArchiveThenExtract
- Test_WildcardNoMatches_ShouldFail
- Test_InvalidOperationOrder_Fails
```

## Performance

### Test Execution Time

- Single test: ~100-500ms (archive creation + execution)
- Full suite: ~20-60 seconds
- Bottleneck: 7-Zip archive creation

### Optimization Notes

- `MainConfig.UseMultiThreadedIO = false` for predictable test behavior
- Tests cleanup temp directories promptly
- Archive creation reuses single 7z.exe process per test

## Coverage Summary

| Category | Tests | Coverage |
|----------|-------|----------|
| Archive Operations | 8 | Extract, Move, Copy, Rename, Delete, Multiple |
| File Operations | 5 | Move, Copy, Rename, Delete after extract |
| Wildcard Operations | 7 | *, ?, complex patterns, multiple, archives |
| Validation | 8 | Success/failure cases, dependencies, conflicts |
| Integration | 3 | Full pipeline, complex workflows |
| **TOTAL** | **31** | **Comprehensive** |

## Success Criteria

All tests verify:

- ✅ Virtual provider tracks files correctly
- ✅ Virtual matches real file system state
- ✅ Wildcard expansion is identical
- ✅ Archive metadata tracked through operations
- ✅ Validation detects errors before real installation
- ✅ No false positives (valid installations pass)
- ✅ No false negatives (invalid installations fail)

## References

- User requirement: "Try not to mock things, like actually use 7zip CLI"
- User requirement: "Each test should test BOTH the dry run AND the real file system provider"
- User requirement: "Assert the virtual filesystem files/paths/folders matches the real"
- User requirement: "The behavior of wildcards CANNOT be modified at all"
- Design doc: `KOTORModSync.Core/Services/Validation/README_DryRunValidation.md`
