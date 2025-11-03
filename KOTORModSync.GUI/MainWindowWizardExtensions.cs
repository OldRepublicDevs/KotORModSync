// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Dialogs;

namespace KOTORModSync
{
    /// <summary>
    /// Extension methods for MainWindow to support the new Inno Setup-style wizard
    /// This file can be safely removed to revert to the old installation flow
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
    public partial class MainWindow
    {
        /// <summary>
        /// NEW wizard-based installation flow (Inno Setup style)
        /// Call this instead of StartInstall_Click to use the new wizard
        /// </summary>
        [UsedImplicitly]
        public async Task StartInstallWizardAsync()
        {
            try
            {
                // Clear validation cache
                ComponentValidationService.ClearValidationCache();
                await Logger.LogVerboseAsync("[MainWindow] Starting wizard-based installation");

                // Record telemetry
                _telemetryService?.RecordUiInteraction("click", "StartInstallWizard");
                _telemetryService?.RecordEvent("installation.wizard.started", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["selected_mod_count"] = MainConfig.AllComponents.Count(c => c.IsSelected),
                    ["total_mod_count"] = MainConfig.AllComponents.Count,
                });

                // Validate environment first (quick check)
                (bool success, string informationMessage) = await InstallationService.ValidateInstallationEnvironmentAsync(
                    MainConfigInstance,
                    async message => await ConfirmationDialog.ShowConfirmationDialogAsync(this, message) == true
                );

                if (!success)
                {
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await InformationDialog.ShowInformationDialogAsync(this, informationMessage);
                });
                    return;
                }

                // Create and show the wizard
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var wizard = new InstallWizardDialog(MainConfigInstance, MainConfig.AllComponents);

                    bool? result = await wizard.ShowDialog<bool?>(this);

                    if (wizard.InstallationCompleted && result == true)
                    {
                        await Logger.LogAsync("Installation wizard completed successfully");

                        // Update UI to reflect installation completion
                        await UpdateStepProgressAsync();

                        // Show completion message
                        await InformationDialog.ShowInformationDialogAsync(
                            this,
                            "Installation completed successfully! Check the output window for details."
                        );

                        // Record telemetry
                        _telemetryService?.RecordEvent("installation.wizard.completed", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["success"] = true,
                            ["cancelled"] = false,
                        });
                    }
                    else if (wizard.InstallationCancelled)
                    {
                        await Logger.LogAsync("Installation wizard was cancelled by user");

                        // Record telemetry
                        _telemetryService?.RecordEvent("installation.wizard.completed", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["success"] = false,
                            ["cancelled"] = true,
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, customMessage: "Error in wizard-based installation");

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await InformationDialog.ShowInformationDialogAsync(
                        this,
                        $"An error occurred during installation: {ex.Message}\n\nCheck the output window for details."
                    );
                });
            }
        }

        /// <summary>
        /// Wrapper for button click events to start the wizard
        /// </summary>
        [UsedImplicitly]
        private void StartInstallWizard_Click(
            [CanBeNull] object sender,
            [NotNull] RoutedEventArgs e)
        {
            _ = StartInstallWizardAsync();
        }

        /// <summary>
        /// Helper method to update step progress (async version)
        /// </summary>
        private Task UpdateStepProgressAsync()
        {
            return Dispatcher.UIThread.InvokeAsync(() => UpdateStepProgress(), DispatcherPriority.Normal).GetTask();
        }
    }
}

