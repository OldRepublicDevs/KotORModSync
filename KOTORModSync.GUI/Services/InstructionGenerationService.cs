// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
				await Logger.LogVerboseAsync("[GenerateInstructionsFromModLinks] START");

				if ( component.ModLinkFilenames == null || component.ModLinkFilenames.Count == 0 )
				{
					await InformationDialog.ShowInformationDialogAsync(_parentWindow, "No mod links available for this component");
					return 0;
				}

				component.Instructions.Clear();
				component.Options.Clear();

				var validArchives = new List<string>();
				var invalidLinks = new List<string>();
				var nonArchiveFiles = new List<string>();

				if ( _mainConfig.sourcePath == null || !_mainConfig.sourcePath.Exists )
				{
					await InformationDialog.ShowInformationDialogAsync(_parentWindow, "Mod directory is not set. Please configure the mod directory first.");
					return 0;
				}

				foreach ( string modLink in component.ModLinkFilenames.Keys )
				{
					if ( string.IsNullOrWhiteSpace(modLink) )
						continue;

					await Logger.LogVerboseAsync($"[GenerateInstructionsFromModLinks] Processing link: {modLink}");

					if ( IsValidUrl(modLink) )
					{
						string downloadedFilePath = await DownloadOrchestrationService.DownloadModFromUrlAsync(modLink, component);
						if ( !string.IsNullOrEmpty(downloadedFilePath) && File.Exists(downloadedFilePath) )
						{
							if ( IsArchive(downloadedFilePath) )
								validArchives.Add(downloadedFilePath);
							else
								nonArchiveFiles.Add(downloadedFilePath);
						}
						else
						{
							invalidLinks.Add(modLink);
						}
					}
					else
					{

						string fullPath = Path.IsPathRooted(modLink) ? modLink : Path.Combine(_mainConfig.sourcePath.FullName, modLink);

						if ( File.Exists(fullPath) )
						{
							if ( IsArchive(fullPath) )
								validArchives.Add(fullPath);
							else
								nonArchiveFiles.Add(fullPath);
						}
						else
						{
							invalidLinks.Add(modLink);
						}
					}
				}

				int totalInstructionsGenerated = 0;
				foreach ( string archivePath in validArchives )
				{
					await Logger.LogVerboseAsync($"[GenerateInstructionsFromModLinks] Generating instructions for: {archivePath}");
					bool success = AutoInstructionGenerator.GenerateInstructions(component, archivePath);
					if ( success )
					{
						totalInstructionsGenerated += component.Instructions.Count;
						await Logger.LogVerboseAsync($"[GenerateInstructionsFromModLinks] Successfully generated instructions for: {archivePath}");

						component.IsDownloaded = true;
					}
				}

				foreach ( string filePath in nonArchiveFiles )
				{
					string fileName = Path.GetFileName(filePath);
					string relativePath = GetRelativePath(_mainConfig.sourcePath.FullName, filePath);

					var moveInstruction = new Instruction
					{
						Guid = Guid.NewGuid(),
						Action = Instruction.ActionType.Move,
						Source = new List<string> { $@"<<modDirectory>>\{relativePath}" },
						Destination = @"<<kotorDirectory>>\Override",
						Overwrite = true
					};
					moveInstruction.SetParentComponent(component);
					component.Instructions.Add(moveInstruction);
					totalInstructionsGenerated++;

					await Logger.LogVerboseAsync($"[GenerateInstructionsFromModLinks] Added Move instruction for file: {fileName}");
				}

				if ( totalInstructionsGenerated > 0 )
				{

					component.IsDownloaded = true;
					string message = $"Successfully generated {totalInstructionsGenerated} instructions";
					if ( invalidLinks.Count > 0 )
					{
						message += $"\n\nNote: {invalidLinks.Count} mod link(s) could not be processed:\n" + string.Join("\n", invalidLinks.Take(5));
						if ( invalidLinks.Count > 5 )
							message += $"\n... and {invalidLinks.Count - 5} more";
					}

					await InformationDialog.ShowInformationDialogAsync(_parentWindow, message);
				}
				else
				{
					await InformationDialog.ShowInformationDialogAsync(_parentWindow, "Could not generate any instructions from the available mod links. Please check that the files exist and are in the correct format.");
				}

				return totalInstructionsGenerated;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				await InformationDialog.ShowInformationDialogAsync(_parentWindow, $"Error processing mod links: {ex.Message}");
				return 0;
			}
		}

		public async Task<bool> GenerateInstructionsFromArchiveAsync(ModComponent component, Func<Task<string[]>> showFileDialog)
		{
			try
			{
				await Logger.LogVerboseAsync("[GenerateInstructionsFromArchive] START");

				string[] filePaths = await showFileDialog();

				if ( filePaths is null || filePaths.Length == 0 )
				{
					await Logger.LogVerboseAsync("[GenerateInstructionsFromArchive] User cancelled file selection");
					return false;
				}

				string archivePath = filePaths[0];
				await Logger.LogVerboseAsync($"[GenerateInstructionsFromArchive] Selected archive: {archivePath}");

				if ( !File.Exists(archivePath) )
				{
					await InformationDialog.ShowInformationDialogAsync(_parentWindow, "Selected file does not exist");
					return false;
				}

				if ( !IsArchive(archivePath) )
				{
					await InformationDialog.ShowInformationDialogAsync(_parentWindow, "Please select a supported archive format (.zip, .rar, .7z)");
					return false;
				}

				await Logger.LogVerboseAsync("[GenerateInstructionsFromArchive] Calling AutoInstructionGenerator.GenerateInstructions");
				bool success = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

				if ( success )
				{
					await Logger.LogVerboseAsync($"[GenerateInstructionsFromArchive] Successfully generated {component.Instructions.Count} instructions");
					await InformationDialog.ShowInformationDialogAsync(_parentWindow,
						$"Successfully generated {component.Instructions.Count} instructions from the archive.\n\nInstallation Method: {component.InstallationMethod}");
					return true;
				}
				else
				{
					await Logger.LogVerboseAsync("[GenerateInstructionsFromArchive] AutoInstructionGenerator returned false");
					await InformationDialog.ShowInformationDialogAsync(_parentWindow,
						"Could not generate instructions from the selected archive. The archive may not contain recognizable game files or TSLPatcher components.");
					return false;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				await InformationDialog.ShowInformationDialogAsync(_parentWindow, $"Error processing archive: {ex.Message}");
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

		#region Private Helper Methods

		private static bool IsValidUrl(string url)
		{
			if ( string.IsNullOrWhiteSpace(url) )
				return false;

			if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri uri) )
				return false;

			return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
		}

		private static bool IsArchive(string filePath)
		{
			return ArchiveHelper.IsArchive(filePath);
		}

		private static string GetRelativePath(string basePath, string targetPath)
		{
			if ( string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(targetPath) )
				return targetPath;

			basePath = Path.GetFullPath(basePath);
			targetPath = Path.GetFullPath(targetPath);

			if ( !targetPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase) )
				return Path.GetFileName(targetPath);

			string relativePath = targetPath.Substring(basePath.Length);
			if ( relativePath.StartsWith(Path.DirectorySeparatorChar.ToString()) )
				relativePath = relativePath.Substring(1);

			return relativePath;
		}

		#endregion
	}
}

