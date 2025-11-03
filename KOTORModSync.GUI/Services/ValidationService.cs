// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using KOTORModSync.Core;
using KOTORModSync.Core.Services.FileSystem;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Services
{

    public class ValidationService
    {
        private readonly MainConfig _mainConfig;

        public ValidationService(MainConfig mainConfig)
        {
            _mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
        }

        public bool IsComponentValidForInstallation(ModComponent component, bool editorMode)
        {
            if (component is null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(component.Name))
            {
                return false;
            }

            if (component.Dependencies.Count > 0)
            {
                List<ModComponent> dependencyComponents = ModComponent.FindComponentsFromGuidList(
                    component.Dependencies,
                    _mainConfig.allComponents
                );
                foreach (ModComponent dep in dependencyComponents)
                {
                    if (dep is null || dep.IsSelected)
                    {
                        continue;
                    }

                    return false;
                }
            }

            if (component.Restrictions.Count > 0)
            {
                List<ModComponent> restrictionComponents = ModComponent.FindComponentsFromGuidList(
                    component.Restrictions,
                    _mainConfig.allComponents
                );
                foreach (ModComponent restriction in restrictionComponents)
                {
                    if (restriction is null || !restriction.IsSelected)
                    {
                        continue;
                    }

                    return false;
                }
            }

            if (component.Instructions.Count == 0)
            {
                return false;
            }

            return !editorMode || Core.Services.ComponentValidationService.AreModLinksValid(component.ResourceRegistry?.Keys.ToList());
        }

        public (string ErrorType, string Description, bool CanAutoFix) GetComponentErrorDetails(ModComponent component)
        {
            var errorReasons = new List<string>();

            if (string.IsNullOrWhiteSpace(component.Name))
            {
                errorReasons.Add("Missing mod name");
            }

            if (component.Dependencies.Count > 0)
            {
                List<ModComponent> dependencyComponents = ModComponent.FindComponentsFromGuidList(
                    component.Dependencies,
                    _mainConfig.allComponents
                );
                var missingDeps = dependencyComponents.Where(dep => dep is null || !dep.IsSelected).ToList();
                if (missingDeps.Count > 0)
                {
                    errorReasons.Add($"Missing required dependencies ({missingDeps.Count})");
                }
            }

            if (component.Restrictions.Count > 0)
            {
                List<ModComponent> restrictionComponents = ModComponent.FindComponentsFromGuidList(
                    component.Restrictions,
                    _mainConfig.allComponents
                );
                var conflictingMods = restrictionComponents.Where(restriction => restriction != null && restriction.IsSelected).ToList();
                if (conflictingMods.Count > 0)
                {
                    errorReasons.Add($"Conflicting mods selected ({conflictingMods.Count})");
                }
            }

            if (component.Instructions.Count == 0)
            {
                errorReasons.Add("No installation instructions");
            }

            var urls = component.ResourceRegistry?.Keys.ToList();
            if (!Core.Services.ComponentValidationService.AreModLinksValid(urls))
            {
                List<string> invalidUrls = urls?.Where(link => !string.IsNullOrWhiteSpace(link) && !Core.Services.ComponentValidationService.IsValidUrl(link)).ToList() ?? new List<string>();
                if (invalidUrls.Count > 0)
                {
                    errorReasons.Add($"Invalid download URLs ({invalidUrls.Count})");
                }
                else
                {
                    errorReasons.Add("Invalid download URLs");
                }
            }

            if (errorReasons.Count == 0)
            {
                return ("UnknownError", "No specific error details available", false);
            }

            string primaryError = errorReasons[0];
            string description = string.Join(", ", errorReasons);

            bool canAutoFix = primaryError.ToLowerInvariant().Contains("missing required dependencies") ||
                              primaryError.ToLowerInvariant().Contains("conflicting mods selected");

            return (primaryError, description, canAutoFix);
        }

        public static bool IsStep1Complete()
        {
            try
            {

                if (string.IsNullOrEmpty(MainConfig.SourcePath?.FullName) ||
                    string.IsNullOrEmpty(MainConfig.DestinationPath?.FullName))
                {
                    return false;
                }

                if (!Directory.Exists(MainConfig.SourcePath.FullName) ||
                    !Directory.Exists(MainConfig.DestinationPath.FullName))
                {
                    return false;
                }

                string kotorDir = MainConfig.DestinationPath.FullName;
                bool hasGameFiles = File.Exists(Path.Combine(kotorDir, "swkotor.exe")) ||
                                   File.Exists(Path.Combine(kotorDir, "swkotor2.exe")) ||
                                   Directory.Exists(Path.Combine(kotorDir, "data")) ||
                                   File.Exists(Path.Combine(kotorDir, "Knights of the Old Republic.app")) ||
                                   File.Exists(Path.Combine(kotorDir, "Knights of the Old Republic II.app"));

                return hasGameFiles;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error checking Step 1 completion");
                return false;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        public async Task AnalyzeValidationFailures(List<Dialogs.ValidationIssue> modIssues, List<string> systemIssues)
        {
            try
            {

                if (MainConfig.DestinationPath is null || MainConfig.SourcePath is null)
                {
                    systemIssues.Add("⚙️ Directories not configured\n" +
                                    "Both Mod Directory and KOTOR Install Directory must be set.\n" +
                                    "Solution: Click Settings and configure both directories.");
                    return;
                }

                if (!_mainConfig.allComponents.Any())
                {
                    systemIssues.Add("📋 No mods loaded\n" +
                                    "No mod configuration file has been loaded.\n" +
                                    "Solution: Click 'File > Open File' to load a mod list.");
                    return;
                }

                if (!_mainConfig.allComponents.Exists(c => c.IsSelected))
                {
                    systemIssues.Add("☑️ No mods selected\n" +
                                    "At least one mod must be selected for installation.\n" +
                                    "Solution: Check the boxes next to mods you want to install.");
                    return;
                }

                // Clear validation cache before running new validation
                Core.Services.Validation.PathValidationCache.ClearCache();

                // Use proper VFS dry-run validation with ExecuteInstructionsAsync
                var selectedComponents = _mainConfig.allComponents.Where(c => c.IsSelected).ToList();

                // Execute each component using ExecuteInstructionsAsync to simulate installation
                foreach (ModComponent component in selectedComponents)
                {
                    if (component.Instructions.Count == 0 && component.Options.Count == 0)
                    {
                        var issue = new Dialogs.ValidationIssue
                        {
                            Icon = "❌",
                            ModName = component.Name,
                            IssueType = "Missing Instructions",
                            Description = "This mod has no installation instructions defined.",
                            Solution = "Solution: Contact the mod list creator or disable this mod.",
                            Component = component,
                        };
                        modIssues.Add(issue);
                        continue;
                    }

                    try
                    {
                        // Create a fresh VFS for each component to track its issues separately
                        var vfs = new Core.Services.FileSystem.VirtualFileSystemProvider();

                        // Initialize VFS with current file state
                        if (MainConfig.SourcePath != null && MainConfig.SourcePath.Exists)
                        {
                            await vfs.InitializeFromRealFileSystemAsync(MainConfig.SourcePath.FullName);
                        }

                        if (MainConfig.DestinationPath != null && MainConfig.DestinationPath.Exists)
                        {
                            await vfs.InitializeFromRealFileSystemAsync(MainConfig.DestinationPath.FullName);
                        }

                        // Validate all paths in this component and cache results
                        await PopulatePathValidationCacheAsync(component);

                        // Execute instructions using the component's built-in method with VFS
                        ModComponent.InstallExitCode exitCode = await component.ExecuteInstructionsAsync(
                            component.Instructions,
                            selectedComponents,
                            default,
                            vfs,
                            skipDependencyCheck: false
                        );

                        // Collect validation issues from VFS and mark them with this component
                        List<Core.Services.FileSystem.ValidationIssue> vfsIssues = vfs.GetValidationIssues();
                        foreach (var issue in vfsIssues)
                        {
                            if (issue.AffectedComponent == null)
                            {
                                issue.AffectedComponent = component;
                            }
                        }

                        // Filter for critical errors that would prevent installation
                        var criticalIssues = vfsIssues.Where(i =>
                            (i.Severity == Core.Services.FileSystem.ValidationSeverity.Error || i.Severity == Core.Services.FileSystem.ValidationSeverity.Critical) &&
                            (string.Equals(i.Category, "ExtractArchive", StringComparison.Ordinal) ||
                             string.Equals(i.Category, "ArchiveValidation", StringComparison.Ordinal) ||
                             string.Equals(i.Category, "MoveFile", StringComparison.Ordinal) ||
                             string.Equals(i.Category, "CopyFile", StringComparison.Ordinal) ||
                             i.Message.Contains("does not exist"))
                        ).ToList();

                        if (criticalIssues.Count > 0 || exitCode != ModComponent.InstallExitCode.Success)
                        {
                            // Group issues by type for better presentation
                            bool hasArchiveIssues = criticalIssues.Exists(i =>
                                string.Equals(i.Category, "ExtractArchive", StringComparison.Ordinal) ||
                                string.Equals(i.Category, "ArchiveValidation", StringComparison.Ordinal));

                            string issueType = hasArchiveIssues ? "Missing Download" : "Missing Files";
                            string icon = hasArchiveIssues ? "📥" : "🔧";

                            string description;
                            if (hasArchiveIssues)
                            {
                                description = "The mod archive file is not in your Mod Directory or cannot be extracted.";
                            }
                            else
                            {
                                var missingPaths = criticalIssues
                                    .Where(i => !string.IsNullOrEmpty(i.AffectedPath))
                                    .Select(i => Path.GetFileName(i.AffectedPath))
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .Take(3)
                                    .ToList();

                                if (missingPaths.Count > 0)
                                {
                                    string moreText = criticalIssues.Count > missingPaths.Count ? $" and {criticalIssues.Count - missingPaths.Count} more" : "";
                                    description = $"Missing file(s): {string.Join(", ", missingPaths)}{moreText}";
                                }
                                else
                                {
                                    description = "One or more required files for this mod are missing from your Mod Directory.";
                                }
                            }

                            string solution;
                            if (hasArchiveIssues)
                            {
                                if (component.ResourceRegistry != null && component.ResourceRegistry.Count > 0)
                                {
                                    solution = $"Solution: Click 'Fetch Downloads' or manually download from: {component.ResourceRegistry.Keys.First()}";
                                }
                                else
                                {
                                    solution = "Solution: Click 'Fetch Downloads' or manually download and place in Mod Directory.";
                                }
                            }
                            else
                            {
                                solution = "Solution: Check the Output Window for details or click 'Fetch Downloads' to download missing files.";
                            }

                            var issue = new Dialogs.ValidationIssue
                            {
                                Icon = icon,
                                ModName = component.Name,
                                IssueType = issueType,
                                Description = description,
                                Solution = solution,
                                Component = component,
                                VfsIssue = criticalIssues.FirstOrDefault(), // Store first issue for details
                                AllVfsIssues = criticalIssues, // Store all issues for detailed view
                            };
                            modIssues.Add(issue);
                        }
                    }
                    catch (Exception ex)
                    {
                        await Logger.LogExceptionAsync(ex, $"Error validating component '{component.Name}'");
                        var issue = new Dialogs.ValidationIssue
                        {
                            Icon = "❌",
                            ModName = component.Name,
                            IssueType = "Validation Error",
                            Description = $"An error occurred during validation: {ex.Message}",
                            Solution = "Solution: Check the Output Window for details.",
                            Component = component,
                        };
                        modIssues.Add(issue);
                    }
                }

                if (!UtilityHelper.IsDirectoryWritable(MainConfig.DestinationPath))
                {
                    systemIssues.Add("🔒 KOTOR Directory Not Writable\n" +
                                    "The installer cannot write to your KOTOR installation directory.\n" +
                                    "Solution: Run as Administrator or install to a different location.");
                }

                if (!UtilityHelper.IsDirectoryWritable(MainConfig.SourcePath))
                {
                    systemIssues.Add("🔒 Mod Directory Not Writable\n" +
                                    "The installer cannot write to your Mod Directory.\n" +
                                    "Solution: Ensure you have write permissions.");
                }
            }
            catch (Exception ex)


            {
                await Logger.LogExceptionAsync(ex, "Error analyzing validation failures");
                systemIssues.Add("❌ Unexpected Error\n" +
                                "An error occurred during validation analysis.\n" +
                                "Solution: Check the Output Window for details.");
            }
        }

        /// <summary>
        /// Validates all paths in a component and caches the results for display in the UI.
        /// This runs when Validate button is pressed.
        /// </summary>
        private static async Task PopulatePathValidationCacheAsync(ModComponent component)
        {
            if (component is null || component.Instructions is null)
            {
                return;
            }

            // Validate all instruction paths and cache results
            for (int i = 0; i < component.Instructions.Count; i++)
            {
                Instruction instruction = component.Instructions[i];
                if (instruction is null)
                {
                    continue;
                }

                // Validate Source paths
                if (instruction.Source != null)
                {
                    foreach (string sourcePath in instruction.Source)
                    {
                        if (!string.IsNullOrWhiteSpace(sourcePath))
                        {
                            try
                            {
                                await Core.Services.Validation.PathValidationCache.ValidateAndCacheAsync(
                                    sourcePath, instruction, component).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                await Logger.LogVerboseAsync($"Error validating source path '{sourcePath}': {ex.Message}").ConfigureAwait(false);
                            }
                        }
                    }
                }

                // Validate Destination path
                if (!string.IsNullOrWhiteSpace(instruction.Destination))
                {
                    try
                    {
                        await Core.Services.Validation.PathValidationCache.ValidateAndCacheAsync(
                            instruction.Destination, instruction, component).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await Logger.LogVerboseAsync($"Error validating destination path '{instruction.Destination}': {ex.Message}").ConfigureAwait(false);
                    }
                }
            }

            // Validate paths in options
            if (component.Options != null)
            {
                foreach (Option option in component.Options)
                {
                    if (option?.Instructions is null)
                    {
                        continue;
                    }

                    for (int i = 0; i < option.Instructions.Count; i++)
                    {
                        Instruction instruction = option.Instructions[i];
                        if (instruction is null)
                        {
                            continue;
                        }

                        // Validate Source paths
                        if (instruction.Source != null)
                        {
                            foreach (string sourcePath in instruction.Source)
                            {
                                if (!string.IsNullOrWhiteSpace(sourcePath))
                                {
                                    try
                                    {
                                        await Core.Services.Validation.PathValidationCache.ValidateAndCacheAsync(
                                            sourcePath, instruction, component).ConfigureAwait(false);
                                    }
                                    catch (Exception ex)
                                    {
                                        await Logger.LogVerboseAsync($"Error validating option source path '{sourcePath}': {ex.Message}").ConfigureAwait(false);
                                    }
                                }
                            }
                        }

                        // Validate Destination path
                        if (!string.IsNullOrWhiteSpace(instruction.Destination))
                        {
                            try
                            {
                                await Core.Services.Validation.PathValidationCache.ValidateAndCacheAsync(
                                    instruction.Destination, instruction, component).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                await Logger.LogVerboseAsync($"Error validating option destination path '{instruction.Destination}': {ex.Message}").ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }
    }
}
