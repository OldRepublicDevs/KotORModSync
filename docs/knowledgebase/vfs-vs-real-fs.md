# Virtual file system vs real file system

`[REPO]` When instruction dry-run uses `VirtualFileSystemProvider` vs `RealFileSystemProvider`.

## Use VFS (required for analysis)

Per `.cursorrules`:

- Dry-run validation that simulates instruction execution
- Download analysis predicting file state per instruction
- `DryRunValidator`, `ComponentValidationService` paths that walk planned installs

VFS must be initialized with `InitializeFromRealFileSystemAsync()` before executing instructions in analysis mode.

## Use real FS (intentional)

- **Real install** (`install` verb, GUI install) — writes to disk
- Some **archive enumeration** in legacy validation (`ComponentValidation.cs`) against actual paths
- Agent scripts creating template KOTOR/mod directories on disk

## Agent footgun

Do not point validation/analysis tools at `RealFileSystemProvider` when the task is “what will happen if we run these instructions” — use VFS only.

## Related

- `.cursorrules` — PATH SANDBOXING & VIRTUAL FILE SYSTEM
- [agent-action-parity.md](agent-action-parity.md)
