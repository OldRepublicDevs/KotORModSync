// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using KOTORModSync.Core.Services.FileSystem;

namespace KOTORModSync.Core.Services.Validation
{
    /// <summary>
    /// Single validation orchestration used by CLI and GUI. Install uses <see cref="InstallationService.InstallAllSelectedComponentsAsync"/> separately.
    /// </summary>
    public static class InstallationValidationPipeline
    {
        public delegate void ValidationProgressHandler(
            ValidationPipelineStage stage,
            int currentStep,
            int totalSteps,
            [CanBeNull] string message);

        [NotNull]
        public static async Task<ValidationPipelineResult> RunAsync(
            [NotNull][ItemNotNull] IReadOnlyList<ModComponent> allComponents,
            [NotNull] ValidationPipelineOptions options,
            [CanBeNull] ValidationProgressHandler progress = null)
        {
            if (allComponents is null)
            {
                throw new ArgumentNullException(nameof(allComponents));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var result = new ValidationPipelineResult();
            CancellationToken cancellationToken = options.CancellationToken;

            List<ModComponent> componentsToValidate = ResolveComponentsToValidate(allComponents, options);
            if (componentsToValidate.Count == 0)
            {
                result.HasCriticalErrors = true;
                result.ErrorCount = 1;
                result.IsSuccess = false;
                result.Stages.Add(new ValidationPipelineStageResult
                {
                    Stage = ValidationPipelineStage.Environment,
                    Passed = false,
                    Summary = "No components selected for validation.",
                });
                return result;
            }

            int totalSteps = CountStages(options);
            int step = 0;

            if (options.FullValidation)
            {
                if (!options.SkipEnvironmentValidation)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    step++;
                    progress?.Invoke(ValidationPipelineStage.Environment, step, totalSteps, "Validating environment...");
                    var envStage = await RunEnvironmentStageAsync(options, cancellationToken).ConfigureAwait(false);
                    result.Stages.Add(envStage);
                    if (!envStage.Passed)
                    {
                        result.HasCriticalErrors = true;
                        result.ErrorCount++;
                        result.IsSuccess = false;
                        return result;
                    }

                    result.PassedCount++;
                }

                cancellationToken.ThrowIfCancellationRequested();
                step++;
                progress?.Invoke(ValidationPipelineStage.Conflicts, step, totalSteps, "Checking mod conflicts...");
                ValidationPipelineStageResult conflictStage = RunConflictStage(componentsToValidate, allComponents);
                result.Stages.Add(conflictStage);
                if (!conflictStage.Passed)
                {
                    result.HasCriticalErrors = true;
                    result.ErrorCount += conflictStage.Messages.Count(m => m.StartsWith("ERROR:", StringComparison.Ordinal));
                }
                else if (conflictStage.HasWarnings)
                {
                    result.WarningCount += conflictStage.Messages.Count(m => m.StartsWith("WARNING:", StringComparison.Ordinal));
                }
                else
                {
                    result.PassedCount++;
                }

                cancellationToken.ThrowIfCancellationRequested();
                step++;
                progress?.Invoke(ValidationPipelineStage.InstallOrder, step, totalSteps, "Validating install order...");
                ValidationPipelineStageResult orderStage = RunInstallOrderStage(componentsToValidate);
                result.Stages.Add(orderStage);
                if (!orderStage.Passed)
                {
                    result.HasCriticalErrors = true;
                    result.ErrorCount++;
                }
                else if (orderStage.HasWarnings)
                {
                    result.WarningCount++;
                }
                else
                {
                    result.PassedCount++;
                }
            }

            int componentsWithErrors = 0;
            int componentsWithWarnings = 0;
            if (!options.DryRunOnly && !options.SkipComponentArchiveValidation)
            {
                cancellationToken.ThrowIfCancellationRequested();
                step++;
                progress?.Invoke(ValidationPipelineStage.ComponentValidation, step, totalSteps, "Validating mod archives...");
                (ValidationPipelineStageResult componentStage, int errors, int warnings) = RunComponentValidationStage(
                    componentsToValidate,
                    allComponents);
                result.Stages.Add(componentStage);
                componentsWithErrors = errors;
                componentsWithWarnings = warnings;
                if (errors > 0)
                {
                    result.HasCriticalErrors = true;
                    result.ErrorCount += errors;
                }
                else if (warnings > 0)
                {
                    result.WarningCount += warnings;
                }
                else
                {
                    result.PassedCount++;
                }
            }

            bool runDryRun = options.DryRun || options.DryRunOnly;
            if (runDryRun)
            {
                cancellationToken.ThrowIfCancellationRequested();
                step++;
                progress?.Invoke(ValidationPipelineStage.DryRun, step, totalSteps, "Running dry-run validation...");
                (ValidationPipelineStageResult dryRunStage, DryRunValidationResult dryRunResult) = await RunDryRunStageAsync(
                    allComponents,
                    componentsToValidate,
                    cancellationToken).ConfigureAwait(false);
                result.Stages.Add(dryRunStage);
                result.DryRunResult = dryRunResult;

                int dryRunErrors = dryRunResult.Issues.Count(i =>
                    i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical);
                int dryRunWarnings = dryRunResult.Issues.Count(i => i.Severity == ValidationSeverity.Warning);

                if (!dryRunResult.IsValid)
                {
                    result.HasCriticalErrors = true;
                    result.ErrorCount += dryRunErrors;
                    result.IsSuccess = false;
                }
                else if (dryRunWarnings > 0)
                {
                    result.WarningCount += dryRunWarnings;
                }
                else
                {
                    result.PassedCount++;
                }
            }

            if (result.HasCriticalErrors)
            {
                result.IsSuccess = false;
                return result;
            }

            if (!options.DryRunOnly && componentsWithErrors > 0)
            {
                result.IsSuccess = false;
                return result;
            }

            result.IsSuccess = true;
            return result;
        }

        [NotNull]
        private static List<ModComponent> ResolveComponentsToValidate(
            [NotNull][ItemNotNull] IReadOnlyList<ModComponent> allComponents,
            [NotNull] ValidationPipelineOptions options)
        {
            if (options.UseFileSelection)
            {
                return allComponents.Where(c => c.IsSelected).ToList();
            }

            return allComponents.ToList();
        }

        private static int CountStages(ValidationPipelineOptions options)
        {
            int count = 0;
            if (options.FullValidation)
            {
                count += 3;
            }

            if (!options.DryRunOnly)
            {
                count++;
            }

            if (options.DryRun || options.DryRunOnly)
            {
                count++;
            }

            return Math.Max(count, 1);
        }

        [NotNull]
        private static async Task<ValidationPipelineStageResult> RunEnvironmentStageAsync(
            [NotNull] ValidationPipelineOptions options,
            CancellationToken cancellationToken)
        {
            var stage = new ValidationPipelineStageResult { Stage = ValidationPipelineStage.Environment };
            MainConfig config = options.MainConfig ?? MainConfig.Instance;
            cancellationToken.ThrowIfCancellationRequested();

            (bool success, string message) = await InstallationService.ValidateInstallationEnvironmentAsync(
                config,
                options.ConfirmationCallback).ConfigureAwait(false);

            stage.Passed = success;
            stage.Summary = message;
            if (!success)
            {
                stage.Messages.Add($"ERROR: {message}");
            }

            return stage;
        }

        [NotNull]
        private static ValidationPipelineStageResult RunConflictStage(
            [NotNull][ItemNotNull] List<ModComponent> componentsToValidate,
            [NotNull][ItemNotNull] IReadOnlyList<ModComponent> allComponents)
        {
            var stage = new ValidationPipelineStageResult
            {
                Stage = ValidationPipelineStage.Conflicts,
                Passed = true,
            };

            foreach (ModComponent component in componentsToValidate)
            {
                Dictionary<string, List<ModComponent>> conflicts = ModComponent.GetConflictingComponents(
                    component.Dependencies,
                    component.Restrictions,
                    allComponents.ToList());

                if (conflicts.ContainsKey("Dependency"))
                {
                    string depNames = string.Join(", ", conflicts["Dependency"].Select(d => d.Name ?? string.Empty));
                    stage.Messages.Add($"WARNING: {component.Name}: missing dependencies: {depNames}");
                    stage.HasWarnings = true;
                }

                if (conflicts.ContainsKey("Restriction"))
                {
                    string restrictionNames = string.Join(", ", conflicts["Restriction"].Select(r => r.Name ?? string.Empty));
                    stage.Messages.Add($"ERROR: {component.Name}: incompatible with: {restrictionNames}");
                    stage.Passed = false;
                }
            }

            if (stage.Messages.Count == 0)
            {
                stage.Summary = "No dependency or restriction conflicts.";
            }

            return stage;
        }

        [NotNull]
        private static ValidationPipelineStageResult RunInstallOrderStage(
            [NotNull][ItemNotNull] List<ModComponent> componentsToValidate)
        {
            var stage = new ValidationPipelineStageResult
            {
                Stage = ValidationPipelineStage.InstallOrder,
                Passed = true,
            };

            try
            {
                (bool isCorrectOrder, List<ModComponent> _) = ModComponent.ConfirmComponentsInstallOrder(componentsToValidate);
                if (!isCorrectOrder)
                {
                    stage.HasWarnings = true;
                    stage.Summary = "Mods will be automatically reordered for installation.";
                    stage.Messages.Add("WARNING: Install order will be adjusted automatically.");
                }
                else
                {
                    stage.Summary = "Install order is correct.";
                }
            }
            catch (Exception ex)
            {
                stage.Passed = false;
                stage.Summary = $"Circular dependency: {ex.Message}";
                stage.Messages.Add($"ERROR: {ex.Message}");
            }

            return stage;
        }

        [NotNull]
        private static (ValidationPipelineStageResult stage, int errors, int warnings) RunComponentValidationStage(
            [NotNull][ItemNotNull] List<ModComponent> componentsToValidate,
            [NotNull][ItemNotNull] IReadOnlyList<ModComponent> allComponents)
        {
            var stage = new ValidationPipelineStageResult
            {
                Stage = ValidationPipelineStage.ComponentValidation,
                Passed = true,
            };

            var allList = allComponents.ToList();
            int errors = 0;
            int warnings = 0;

            foreach (ModComponent component in componentsToValidate)
            {
                var validator = new ComponentValidation(component, allList);
                bool isValid = validator.Run();
                List<string> componentErrors = validator.GetErrors();
                List<string> componentWarnings = validator.GetWarnings();

                if (componentErrors.Count > 0)
                {
                    errors += componentErrors.Count;
                    stage.Passed = false;
                    foreach (string error in componentErrors)
                    {
                        stage.Messages.Add($"ERROR: {component.Name}: {error}");
                    }
                }
                else if (componentWarnings.Count > 0)
                {
                    warnings += componentWarnings.Count;
                    stage.HasWarnings = true;
                    foreach (string warning in componentWarnings)
                    {
                        stage.Messages.Add($"WARNING: {component.Name}: {warning}");
                    }
                }
                else if (isValid)
                {
                    stage.Messages.Add($"OK: {component.Name}");
                }
            }

            stage.Summary = errors > 0
                ? $"{errors} component error(s)"
                : warnings > 0
                    ? $"{warnings} warning(s)"
                    : "All components passed archive validation.";

            return (stage, errors, warnings);
        }

        [NotNull]
        private static async Task<(ValidationPipelineStageResult stage, DryRunValidationResult dryRunResult)> RunDryRunStageAsync(
            [NotNull][ItemNotNull] IReadOnlyList<ModComponent> allComponents,
            [NotNull][ItemNotNull] List<ModComponent> componentsToValidate,
            CancellationToken cancellationToken)
        {
            var stage = new ValidationPipelineStageResult
            {
                Stage = ValidationPipelineStage.DryRun,
                Passed = true,
            };

            var selectionSnapshot = allComponents.ToDictionary(c => c.Guid, c => c.IsSelected);
            try
            {
                var targetGuids = new HashSet<Guid>(componentsToValidate.Select(c => c.Guid));
                foreach (ModComponent component in allComponents)
                {
                    component.IsSelected = targetGuids.Contains(component.Guid);
                }

                DryRunValidationResult dryRunResult = await DryRunValidator.ValidateInstallationAsync(
                    allComponents.ToList(),
                    skipDependencyCheck: false,
                    cancellationToken).ConfigureAwait(false);

                stage.Passed = dryRunResult.IsValid;
                stage.Summary = dryRunResult.GetSummaryMessage();

                foreach (ValidationIssue issue in dryRunResult.Issues)
                {
                    string prefix = issue.Severity == ValidationSeverity.Warning ? "WARNING" : "ERROR";
                    stage.Messages.Add($"{prefix}: [{issue.Category}] {issue.Message}");
                }

                return (stage, dryRunResult);
            }
            finally
            {
                foreach (ModComponent component in allComponents)
                {
                    if (selectionSnapshot.TryGetValue(component.Guid, out bool wasSelected))
                    {
                        component.IsSelected = wasSelected;
                    }
                }
            }
        }
    }
}
