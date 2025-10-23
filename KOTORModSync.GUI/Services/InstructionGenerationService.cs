// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Utility;
using KOTORModSync.Dialogs;

namespace KOTORModSync.Services
{

	public class InstructionGenerationService
	{
		private readonly MainConfig _mainConfig;
		private readonly Window _parentWindow;
		private readonly DownloadOrchestrationService _downloadOrchestrationService;

		public InstructionGenerationService(
			MainConfig mainConfig,
			Window parentWindow,
			DownloadOrchestrationService downloadOrchestrationService)
		{
			_mainConfig = mainConfig
						  ?? throw new ArgumentNullException(nameof(mainConfig));
			_parentWindow = parentWindow
							?? throw new ArgumentNullException(nameof(parentWindow));
			_downloadOrchestrationService = downloadOrchestrationService
											?? throw new ArgumentNullException(nameof(downloadOrchestrationService));
		}

		public async Task<int> GenerateInstructionsFromModLinksAsync(ModComponent component)
		{
			try
			{
				await Logger.LogVerboseAsync("[InstructionGenerationService] START GenerateInstructionsFromModLinks");

				// Validation (GUI-specific)
				if ( component.ModLinkFilenames == null || component.ModLinkFilenames.Count == 0 )
				{
					await InformationDialog.ShowInformationDialogAsync(
						_parentWindow,
						"No mod link filenames available for this component");
					return 0;
				}

				if ( _mainConfig.sourcePath == null || !_mainConfig.sourcePath.Exists )
				{
					await InformationDialog.ShowInformationDialogAsync(
						_parentWindow,
						"Source path is not set. Please configure the mod directory first.");
					return 0;
				}

				component.Instructions.Clear();
				component.Options.Clear();

				// Step 1: Analyze component files (Core logic)
				var downloadCacheService = new DownloadCacheService();
				downloadCacheService.SetDownloadManager();

				using ( var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10)) )
				{
					AutoInstructionGenerator.FileAnalysisResult analysis = await AutoInstructionGenerator.AnalyzeComponentFilesAsync(
						component,
						downloadCacheService,
						_mainConfig.sourcePath.FullName,
						cts.Token);

					// Step 2: Handle missing files (GUI-specific)
					if ( analysis.MissingUrls.Count > 0 )
					{
						bool? shouldDownload = await ConfirmationDialog.ShowConfirmationDialogAsync(
							_parentWindow,
							confirmText: $"This component has {analysis.MissingUrls.Count} file(s) that aren't downloaded yet.\n\nWould you like to download them now?",
							yesButtonText: "Download Now",
							noButtonText: "Skip (Generate Placeholder Instructions)",
							yesButtonTooltip: "Download the missing files now.",
							noButtonTooltip: "Skip (Generate Placeholder Instructions).",
							closeButtonTooltip: "Cancel the download process.");

						if ( shouldDownload is null )
						{
							// User cancelled
							await Logger.LogVerboseAsync("[InstructionGenerationService] User cancelled download confirmation");
							return 0;
						}

						if ( shouldDownload is true )
						{
							// Download missing files using single-component dialog (GUI-specific)
							await Logger.LogAsync($"[InstructionGenerationService] Opening download dialog for {analysis.MissingUrls.Count} missing file(s)...");

							var downloadDialog = new SingleModDownloadDialog(component, downloadCacheService);
							_ = downloadDialog.StartDownloadAsync();
							await downloadDialog.ShowDialog(_parentWindow);

							// Re-analyze after download to pick up new files
							if ( downloadDialog.WasSuccessful && downloadDialog.DownloadedFiles.Count > 0 )
							{
								await Logger.LogAsync($"[InstructionGenerationService] Download successful, re-analyzing files...");

								analysis = await AutoInstructionGenerator.AnalyzeComponentFilesAsync(
									component,
									downloadCacheService,
									_mainConfig.sourcePath.FullName,
									cts.Token);
							}
							else
							{
								await Logger.LogWarningAsync("[InstructionGenerationService] Download was not successful");
							}
						}
						else
						{
							await Logger.LogVerboseAsync("[InstructionGenerationService] User chose to skip download");
						}
					}

					// Step 3: Generate instructions from analyzed files (Core logic)
					int totalInstructionsGenerated = await AutoInstructionGenerator.GenerateInstructionsFromAnalyzedFilesAsync(
						component,
						analysis,
						_mainConfig.sourcePath.FullName);

					// Step 4: Update component state and show results (GUI-specific)
					if ( totalInstructionsGenerated > 0 )
					{
						component.IsDownloaded = analysis.MissingUrls.Count == 0;

						string message = $"Successfully generated {totalInstructionsGenerated} instructions";
						if ( analysis.InvalidLinks.Count > 0 )
						{
							message += $"\n\nNote: {analysis.InvalidLinks.Count} mod link(s) could not be processed:\n" +
									   string.Join("\n", analysis.InvalidLinks.Take(5));
							if ( analysis.InvalidLinks.Count > 5 )
								message += $"\n... and {analysis.InvalidLinks.Count - 5} more";
						}

						await InformationDialog.ShowInformationDialogAsync(
							_parentWindow,
							message);
					}
					else
					{
						await InformationDialog.ShowInformationDialogAsync(
							_parentWindow,
							"Could not generate any instructions from the available mod links. Please check that the files exist and are in the correct format.");
					}

					return totalInstructionsGenerated;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				await InformationDialog.ShowInformationDialogAsync(
					_parentWindow,
					$"Error generating instructions from mod links: {ex.Message}");
				return 0;
			}
		}

		public async Task<bool> GenerateInstructionsFromArchiveAsync(ModComponent component, Func<Task<string[]>> showFileDialog)
		{
			try
			{
				await Logger.LogVerboseAsync("[InstructionGenerationService] START GenerateInstructionsFromArchive");

				// Show file dialog (GUI-specific)
				string[] filePaths = await showFileDialog();

				if ( filePaths is null || filePaths.Length == 0 )
				{
					await Logger.LogVerboseAsync("[InstructionGenerationService] User cancelled file selection");
					return false;
				}

				string archivePath = filePaths[0];
				await Logger.LogVerboseAsync($"[InstructionGenerationService] Selected archive: {archivePath}");

				// Validate file (GUI-specific validation with user feedback)
				if ( !File.Exists(archivePath) )
				{
					await InformationDialog.ShowInformationDialogAsync(
						_parentWindow,
						"The selected file does not exist");
					return false;
				}

				if ( !ArchiveHelper.IsArchive(archivePath) )
				{
					await InformationDialog.ShowInformationDialogAsync(
						_parentWindow,
						"The selected file is not a supported archive format (.zip, .rar, .7z)");
					return false;
				}

				// Generate instructions (Core logic)
				await Logger.LogVerboseAsync("[InstructionGenerationService] Calling AutoInstructionGenerator.GenerateInstructions");
				bool success = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

				// Show results (GUI-specific)
				if ( success )
				{
					await Logger.LogVerboseAsync($"[InstructionGenerationService] Successfully generated {component.Instructions.Count} instructions");
					await InformationDialog.ShowInformationDialogAsync(
						_parentWindow,
						$"Successfully generated {component.Instructions.Count} instructions from the archive.\n\nInstallation Method: {component.InstallationMethod}");
					return true;
				}
				else
				{
					await Logger.LogVerboseAsync("[InstructionGenerationService] AutoInstructionGenerator returned false");
					await InformationDialog.ShowInformationDialogAsync(
						_parentWindow,
						"Could not generate instructions from the selected archive. The archive may not contain recognizable game files or TSLPatcher components.");
					return false;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				await InformationDialog.ShowInformationDialogAsync(
					_parentWindow,
					$"Error generating instructions from archive: {ex.Message}");
				return false;
			}
		}

		public static async Task<int> TryAutoGenerateInstructionsForComponentsAsync(List<ModComponent> components)
		{
			if ( components == null || components.Count == 0 )
				return 0;

			try
			{
				int generatedCount = 0;
				int skippedCount = 0;

				foreach ( ModComponent component in components )
				{

					if ( component.Instructions.Count > 0 )
					{
						skippedCount++;
						continue;
					}

					bool success = Core.Services.AutoInstructionGenerator.TryGenerateInstructionsFromArchive(component);
					if ( !success )
						continue;

					generatedCount++;
					await Logger.LogAsync($"Auto-generated instructions for '{component.Name}': {component.InstallationMethod}");
				}

				if ( generatedCount > 0 )
					await Logger.LogAsync($"Auto-generated instructions for {generatedCount} component(s). Skipped {skippedCount} component(s) that already had instructions.");

				return generatedCount;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return 0;
			}
		}

	}
}

