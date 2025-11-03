// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using KOTORModSync.Core.Services.FileSystem;

namespace KOTORModSync.Core.Services.Validation
{
    /// <summary>
    /// Provides validation methods for instruction paths using VirtualFileSystemProvider.
    /// Used by the UI to provide real-time feedback on instruction validity.
    /// </summary>
    public static class DryRunValidator
    {
        /// <summary>
        /// Validates a single instruction path and returns a simple status string.
        /// </summary>
        [NotNull]
        public static async Task<string> ValidateInstructionPathAsync(
            [CanBeNull] string path,
            [CanBeNull] Instruction instruction,
            [CanBeNull] ModComponent currentComponent)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "❓ Empty";
            }

            try
            {
                PathValidationResult result = await ValidateInstructionPathDetailedAsync(path, instruction, currentComponent).ConfigureAwait(false);
                return result.StatusMessage ?? "❓ Unknown";
            }
            catch (Exception ex)


            {
                await Logger.LogExceptionAsync(ex, "Error in simple path validation").ConfigureAwait(false);
                return "⚠️ Validation error";
            }
        }

        /// <summary>
        /// Validates a single instruction path with detailed information about blocking instructions.
        /// Uses VirtualFileSystemProvider to simulate the file state at the time the instruction runs.
        /// Results are automatically cached for display in the UI.
        /// </summary>
        [NotNull]
        public static async Task<PathValidationResult> ValidateInstructionPathDetailedAsync(
            [CanBeNull] string path,
            [CanBeNull] Instruction instruction,
            [CanBeNull] ModComponent currentComponent)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                var emptyResult = new PathValidationResult
                {
                    StatusMessage = "❓ Empty",
                    IsValid = false,
                };
                // Cache empty results too
                PathValidationCache.CacheResult(path, instruction, currentComponent, emptyResult);
                return emptyResult;
            }

            if (instruction is null || currentComponent is null)
            {
                var contextMissingResult = new PathValidationResult
                {
                    StatusMessage = "⚠️ Context missing",
                    DetailedMessage = "Cannot validate without instruction and component context",
                    IsValid = false,
                };
                // Cache context missing results too
                PathValidationCache.CacheResult(path, instruction, currentComponent, contextMissingResult);
                return contextMissingResult;
            }

            try
            {
                // Check if path uses required placeholders
                if (!path.StartsWith("<<modDirectory>>", StringComparison.Ordinal) && !path.StartsWith("<<kotorDirectory>>", StringComparison.Ordinal) && instruction.Action != Instruction.ActionType.Choose)
                {
                    return new PathValidationResult
                    {
                        StatusMessage = "⚠️ Invalid path",
                        DetailedMessage = "Paths must start with <<modDirectory>> or <<kotorDirectory>> for security",
                        IsValid = false,
                    };
                }

                // Initialize virtual file system with current state
                var vfs = new VirtualFileSystemProvider();

                // Load existing files from disk
                if (MainConfig.SourcePath != null && MainConfig.SourcePath.Exists)
                {
                    await vfs.InitializeFromRealFileSystemAsync(MainConfig.SourcePath.FullName).ConfigureAwait(false);
                }

                if (MainConfig.DestinationPath != null && MainConfig.DestinationPath.Exists)
                {
                    await vfs.InitializeFromRealFileSystemAsync(MainConfig.DestinationPath.FullName).ConfigureAwait(false);
                }

                // Find the instruction index
                int instructionIndex = currentComponent.Instructions.IndexOf(instruction);
                if (instructionIndex < 0)
                {
                    return new PathValidationResult
                    {
                        StatusMessage = "⚠️ Not in list",
                        DetailedMessage = "Instruction not found in component's instruction list",
                        IsValid = false,
                    };
                }

                // Execute all previous instructions to update VFS state
                System.Collections.Generic.List<ModComponent> allComponents = MainConfig.AllComponents;
                for (int i = 0; i < instructionIndex; i++)
                {
                    Instruction prevInstruction = currentComponent.Instructions[i];
                    prevInstruction.SetFileSystemProvider(vfs);

                    try
                    {
                        using (var cts = new CancellationTokenSource())


                        {
                            await SimulateInstructionAsync(prevInstruction, i, currentComponent, vfs, allComponents, cts.Token).ConfigureAwait(false);
                        }
                    }
                    catch (Exception)
                    {
                        // Continue even if previous instructions fail
                    }
                }

                // Now validate the current path against the VFS state
                string resolvedPath = ResolvePlaceholderPath(path);

                // Check based on instruction type
                PathValidationResult result;
                switch (instruction.Action)
                {
                    case Instruction.ActionType.Extract:
                        // Archive must exist
                        if (!vfs.FileExists(resolvedPath))
                        {
                            result = CheckForBlockingInstruction(path, instructionIndex, currentComponent, isExtract: true);
                        }
                        else
                        {
                            result = new PathValidationResult
                            {
                                StatusMessage = "✓ Archive found",
                                IsValid = true,
                            };
                        }
                        break;

                    case Instruction.ActionType.Move:
                    case Instruction.ActionType.Copy:
                    case Instruction.ActionType.Rename:
                        // Source file must exist
                        if (!vfs.FileExists(resolvedPath))
                        {
                            result = CheckForBlockingInstruction(path, instructionIndex, currentComponent, isExtract: false);
                        }
                        else
                        {
                            result = new PathValidationResult
                            {
                                StatusMessage = "✓ File found",
                                IsValid = true,
                            };
                        }
                        break;

                    case Instruction.ActionType.Delete:
                        // File should exist to delete (but not critical)
                        if (!vfs.FileExists(resolvedPath))
                        {
                            result = new PathValidationResult
                            {
                                StatusMessage = "⚠️ Already gone",
                                DetailedMessage = "File doesn't exist, but Delete will succeed anyway",
                                IsValid = true,
                            };
                        }
                        else
                        {
                            result = new PathValidationResult
                            {
                                StatusMessage = "✓ Will delete",
                                IsValid = true,
                            };
                        }
                        break;

                    case Instruction.ActionType.Execute:
                    case Instruction.ActionType.Run:
                        // Executable should exist
                        if (!vfs.FileExists(resolvedPath))
                        {
                            result = new PathValidationResult
                            {
                                StatusMessage = "✗ Not found",
                                DetailedMessage = "Executable file not found",
                                IsValid = false,
                            };
                        }
                        else
                        {
                            result = new PathValidationResult
                            {
                                StatusMessage = "✓ Executable found",
                                IsValid = true,
                            };
                        }
                        break;

                    case Instruction.ActionType.Choose:
                        // Choose instructions use GUIDs, not file paths
                        result = new PathValidationResult
                        {
                            StatusMessage = "✓ Option",
                            IsValid = true,
                        };
                        break;

                    case Instruction.ActionType.CleanList:
                        // Cleanlist file should exist
                        if (!vfs.FileExists(resolvedPath))
                        {
                            result = new PathValidationResult
                            {
                                StatusMessage = "✗ Cleanlist not found",
                                DetailedMessage = "Cleanlist CSV file not found",
                                IsValid = false,
                            };
                        }
                        else
                        {
                            result = new PathValidationResult
                            {
                                StatusMessage = "✓ Cleanlist found",
                                IsValid = true,
                            };
                        }
                        break;

                    default:
                        result = new PathValidationResult
                        {
                            StatusMessage = "❓ Unknown action",
                            IsValid = false,
                        };
                        break;
                }

                // Cache the result before returning
                PathValidationCache.CacheResult(path, instruction, currentComponent, result);
                return result;
            }
            catch (Exception ex)


            {
                await Logger.LogExceptionAsync(ex, "Error in detailed path validation").ConfigureAwait(false);
                var errorResult = new PathValidationResult
                {
                    StatusMessage = "⚠️ Validation error",
                    DetailedMessage = $"Error: {ex.Message}",
                    IsValid = false,
                };
                PathValidationCache.CacheResult(path, instruction, currentComponent, errorResult);
                return errorResult;
            }
        }

        private static PathValidationResult CheckForBlockingInstruction(
            string path,
            int currentInstructionIndex,
            ModComponent component,
            bool isExtract)
        {
            // Check if this file would be created by a later Extract instruction
            for (int i = currentInstructionIndex + 1; i < component.Instructions.Count; i++)
            {
                Instruction laterInstruction = component.Instructions[i];

                if (isExtract)
                {
                    // Look for Extract instructions that might provide this archive
                    // This typically means checking ModLinks
                    if (component.ResourceRegistry.Count > 0)
                    {
                        string filename = Path.GetFileName(path.Replace("<<modDirectory>>\\", "").Replace("<<modDirectory>>/", ""));

                        return new PathValidationResult
                        {
                            StatusMessage = "📥 Needs download",
                            DetailedMessage = $"Archive '{filename}' will be downloaded from ModLinks",
                            IsValid = true,
                            NeedsModLinkAdded = true,
                        };
                    }
                }
                else if (laterInstruction.Action == Instruction.ActionType.Extract)
                {
                    // Check if an Extract instruction would create this file
                    string filename = Path.GetFileName(path.Replace("<<modDirectory>>\\", "").Replace("<<modDirectory>>/", ""));

                    return new PathValidationResult
                    {
                        StatusMessage = "⚠️ Wrong order",
                        DetailedMessage = $"File '{filename}' will be created by instruction #{i + 1} (Extract). Move this instruction after that one.",
                        IsValid = false,
                        BlockingInstructionIndex = i,
                    };
                }
            }

            // File truly doesn't exist and won't be created
            string fname = Path.GetFileName(path.Replace("<<modDirectory>>\\", "").Replace("<<modDirectory>>/", ""));
            return new PathValidationResult
            {
                StatusMessage = "✗ Not found",
                DetailedMessage = $"File '{fname}' not found in mod directory or archives",
                IsValid = false,
            };
        }

        private static async Task SimulateInstructionAsync(
            Instruction instruction,
            int instructionIndex,
            ModComponent component,
            VirtualFileSystemProvider vfs,
            System.Collections.Generic.List<ModComponent> allComponents,
            CancellationToken cancellationToken = default)
        {
            // Use the unified instruction execution pipeline
            // This ensures dry-run validation matches real execution exactly
            instruction.SetFileSystemProvider(vfs);

            try
            {
                await component.ExecuteSingleInstructionAsync(
                instruction,
                instructionIndex,
                allComponents,
                vfs,
                skipDependencyCheck: true,
                cancellationToken
            ).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Silently continue - this is just for VFS state tracking
            }
        }

        private static string ResolvePlaceholderPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            string modDir = MainConfig.SourcePath?.FullName ?? string.Empty;
            string kotorDir = MainConfig.DestinationPath?.FullName ?? string.Empty;

            string resolved = path
                .Replace("<<modDirectory>>\\", modDir + "\\")
                .Replace("<<modDirectory>>/", modDir + "/")
                .Replace("<<kotorDirectory>>\\", kotorDir + "\\")
                .Replace("<<kotorDirectory>>/", kotorDir + "/");

            return Path.GetFullPath(resolved);
        }

        /// <summary>
        /// Performs a complete dry-run validation of all selected components.
        /// Uses VirtualFileSystemProvider and ExecuteInstructionsAsync to simulate the entire installation.
        /// </summary>
        [NotNull]
        public static async Task<DryRunValidationResult> ValidateInstallationAsync(
            [NotNull][ItemNotNull] System.Collections.Generic.List<ModComponent> allComponents,
            bool skipDependencyCheck = true,
            CancellationToken cancellationToken = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = new DryRunValidationResult();

            try
            {
                if (allComponents.Count == 0)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Category = "Validation",
                        Message = "No components to validate",
                    });

                    sw.Stop();
                    TelemetryService.Instance.RecordValidation(
                        validationType: "dry_run",
                        success: false,
                        issueCount: 1,
                        durationMs: sw.Elapsed.TotalMilliseconds
                    );
                    return result;
                }

                // Initialize VFS with current file state
                var vfs = new VirtualFileSystemProvider();

                try
                {
                    if (MainConfig.SourcePath != null && MainConfig.SourcePath.Exists)
                    {
                        await vfs.InitializeFromRealFileSystemAsync(MainConfig.SourcePath.FullName).ConfigureAwait(false);
                    }

                    if (MainConfig.DestinationPath != null && MainConfig.DestinationPath.Exists)
                    {
                        await vfs.InitializeFromRealFileSystemAsync(MainConfig.DestinationPath.FullName).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Category = "FileSystemInitialization",
                        Message = $"Could not fully initialize file system: {ex.Message}",
                    });
                }

                // Execute each component in order using ExecuteInstructionsAsync
                var selectedComponents = allComponents.Where(c => c.IsSelected).ToList();

                foreach (ModComponent component in selectedComponents)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        // Execute instructions using the component's built-in method with VFS
                        await component.ExecuteInstructionsAsync(
                            component.Instructions,
                            selectedComponents,
                            cancellationToken,
                            vfs,
                            skipDependencyCheck: false


                        ).ConfigureAwait(false);

                        // Collect any validation issues from VFS
                        foreach (ValidationIssue issue in vfs.ValidationIssues)
                        {
                            issue.AffectedComponent = component;
                            result.Issues.Add(issue);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Error,
                            Category = "Validation",
                            Message = $"Failed to validate component: {ex.Message}",
                            AffectedComponent = component,
                        });
                    }
                }

                sw.Stop();
                TelemetryService.Instance.RecordValidation(
                    validationType: "dry_run",
                    success: !result.Issues.Exists(i => i.Severity == ValidationSeverity.Error),
                    issueCount: result.Issues.Count,
                    durationMs: sw.Elapsed.TotalMilliseconds
                );

                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                await Logger.LogExceptionAsync(ex, "Error in ValidateInstallationAsync").ConfigureAwait(false);
                TelemetryService.Instance.RecordValidation(
                    validationType: "dry_run",
                    success: false,
                    issueCount: result.Issues.Count,
                    durationMs: sw.Elapsed.TotalMilliseconds
                );
                throw;
            }
        }
    }
}
