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
using KOTORModSync.Core.Services.Validation;
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

            bool canAutoFix = NetFrameworkCompatibility.Contains(primaryError, "missing required dependencies", StringComparison.OrdinalIgnoreCase) ||
                              NetFrameworkCompatibility.Contains(primaryError, "conflicting mods selected", StringComparison.OrdinalIgnoreCase);

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

        public async Task AnalyzeValidationFailures(
            List<Dialogs.ValidationIssue> modIssues,
            List<string> systemIssues)
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

                Core.Services.Validation.PathValidationCache.ClearCache();

                var pipelineOptions = Core.Services.Validation.ValidationPipelineOptions.WizardFull;
                pipelineOptions.MainConfig = _mainConfig;

                Core.Services.Validation.ValidationPipelineResult pipelineResult =
                    await Core.Services.Validation.InstallationValidationPipeline.RunAsync(
                        _mainConfig.allComponents,
                        pipelineOptions).ConfigureAwait(false);

                if (pipelineResult.DryRunResult != null)
                {
                    MapDryRunIssuesToDialogIssues(modIssues, pipelineResult.DryRunResult);
                }

                foreach (ValidationPipelineStageResult stage in pipelineResult.Stages)
                {
                    if (stage.Stage == ValidationPipelineStage.Environment && !stage.Passed)
                    {
                        systemIssues.Add($"⚙️ Environment\n{stage.Summary}");
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

        private static void MapDryRunIssuesToDialogIssues(
            List<Dialogs.ValidationIssue> modIssues,
            DryRunValidationResult dryRunResult)
        {
            foreach (Core.Services.FileSystem.ValidationIssue coreIssue in dryRunResult.Issues)
            {
                if (coreIssue.Severity != Core.Services.FileSystem.ValidationSeverity.Error &&
                    coreIssue.Severity != Core.Services.FileSystem.ValidationSeverity.Critical)
                {
                    continue;
                }

                modIssues.Add(new Dialogs.ValidationIssue
                {
                    Icon = "❌",
                    ModName = coreIssue.AffectedComponent?.Name ?? "Unknown",
                    IssueType = coreIssue.Category ?? "Validation",
                    Description = coreIssue.Message ?? "No description available",
                    Solution = "Solution: Check the Output Window for details.",
                    Component = coreIssue.AffectedComponent,
                    VfsIssue = coreIssue,
                });
            }
        }
    }
}
