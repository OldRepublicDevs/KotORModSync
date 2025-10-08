// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Dialogs;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for loading TOML and Markdown files
	/// </summary>
	public class FileLoadingService
	{
		private readonly MainConfig _mainConfig;
		private readonly Window _parentWindow;
		private string _lastLoadedFileName;

		public string LastLoadedFileName
		{
			get => _lastLoadedFileName;
			set => _lastLoadedFileName = value;
		}

		public FileLoadingService(MainConfig mainConfig, Window parentWindow)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
			_parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
		}

		/// <summary>
		/// Loads a TOML file with merge strategy choice
		/// </summary>
		public async Task<bool> LoadTomlFileAsync(string filePath, bool editorMode, Func<Task> onComponentsLoaded, string fileType = "instruction file")
		{
			try
			{
				// Verify the file
				var fileInfo = new FileInfo(filePath);
				const int maxInstructionSize = 524288000; // 500mb max
				if ( fileInfo.Length > maxInstructionSize )
				{
					await Logger.LogAsync($"Invalid {fileType} selected: '{fileInfo.Name}' - file too large");
					return false;
				}

				// Load components from the TOML file
				List<ModComponent> newComponents = ModComponent.ReadComponentsFromFile(filePath);

				// If no existing components, just load the new ones
				if ( _mainConfig.allComponents.Count == 0 )
				{
					_mainConfig.allComponents = newComponents;
					_lastLoadedFileName = Path.GetFileName(filePath);
					await Logger.LogAsync($"Loaded {newComponents.Count} components from {fileType}.");
					await onComponentsLoaded();
					return true;
				}

				// Ask user what to do with existing components
				bool? result = await ShowConfigLoadConfirmationAsync(fileType, editorMode);

				switch ( result )
				{
					case true: // User clicked "Merge"
						{
							// Ask user which merge strategy to use
							MergeStrategy? mergeStrategy = await ShowMergeStrategyDialogAsync(fileType);
							if ( mergeStrategy == null )
								return false;

							// Show conflict resolution dialog
							var conflictDialog = new ComponentMergeConflictDialog(
								_mainConfig.allComponents,
								newComponents,
								"Currently Loaded Components",
								fileType,
								(existing, incoming) =>
								{
									if ( mergeStrategy.Value == MergeStrategy.ByGuid )
										return existing.Guid == incoming.Guid;
									return FuzzyMatcher.FuzzyMatchComponents(existing, incoming);
								});

							await conflictDialog.ShowDialog(_parentWindow);

							if ( conflictDialog.UserConfirmed && conflictDialog.MergedComponents != null )
							{
								int originalCount = _mainConfig.allComponents.Count;
								_mainConfig.allComponents = conflictDialog.MergedComponents;
								int newCount = _mainConfig.allComponents.Count;
								_lastLoadedFileName = Path.GetFileName(filePath);

								string strategyName = mergeStrategy.Value == MergeStrategy.ByGuid ? "GUID matching" : "name/author matching";
								await Logger.LogAsync($"Merged {newComponents.Count} components from {fileType} with existing {originalCount} components using {strategyName}. Total components now: {newCount}");
							}
							else
							{
								await Logger.LogAsync("Merge cancelled by user.");
								return false;
							}
							break;
						}
					case false: // User clicked "Overwrite"
						_mainConfig.allComponents = newComponents;
						_lastLoadedFileName = Path.GetFileName(filePath);
						await Logger.LogAsync($"Overwrote existing config with {newComponents.Count} components from {fileType}.");
						break;
					default: // User cancelled
						return false;
				}

				await onComponentsLoaded();
				return true;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return false;
			}
		}

		/// <summary>
		/// Loads a markdown file and parses it into components
		/// </summary>
		public async Task<bool> LoadMarkdownFileAsync(string filePath, bool editorMode, Func<Task> onComponentsLoaded, Func<List<ModComponent>, Task> tryAutoGenerate)
		{
			try
			{
				if ( string.IsNullOrEmpty(filePath) )
					return false;

				using ( var reader = new StreamReader(filePath) )
				{
					string fileContents = await reader.ReadToEndAsync();

					// Open Regex Import Dialog
					var dialog = new RegexImportDialog(fileContents, MarkdownImportProfile.CreateDefault());
					MarkdownParserResult parseResult = null;

					dialog.Closed += async (_, __) =>
					{
						if ( !dialog.LoadSuccessful || !(dialog.DataContext is RegexImportDialogViewModel vm) )
							return;

						parseResult = vm.ConfirmLoad();
						await Logger.LogAsync($"Markdown parsing completed. Found {parseResult.Components?.Count ?? 0} components with {parseResult.Components?.Sum(c => c.ModLink.Count) ?? 0} total links.");

						if ( parseResult.Warnings?.Count > 0 )
						{
							await Logger.LogWarningAsync($"Markdown parsing completed with {parseResult.Warnings.Count} warnings.");
							foreach ( string warning in parseResult.Warnings )
								await Logger.LogWarningAsync($"  - {warning}");
						}
					};

					await dialog.ShowDialog(_parentWindow);

					if ( parseResult is null )
						return false;

					// Handle merging/overwriting
					if ( _mainConfig.allComponents.Count == 0 )
					{
						_mainConfig.allComponents = new List<ModComponent>(parseResult.Components);
						await Logger.LogAsync($"Loaded {parseResult.Components.Count} components from markdown.");
						await tryAutoGenerate(parseResult.Components.ToList());
					}
					else
					{
						bool? confirmResult = await ShowConfigLoadConfirmationAsync("markdown file", editorMode);

						if ( confirmResult == true ) // Merge
						{
							var conflictDialog = new ComponentMergeConflictDialog(
								_mainConfig.allComponents,
								new List<ModComponent>(parseResult.Components),
								"Currently Loaded Components",
								"Markdown File",
								FuzzyMatcher.FuzzyMatchComponents);

							await conflictDialog.ShowDialog(_parentWindow);

							if ( conflictDialog.UserConfirmed && conflictDialog.MergedComponents != null )
							{
								int originalCount = _mainConfig.allComponents.Count;
								_mainConfig.allComponents = conflictDialog.MergedComponents;
								int newCount = _mainConfig.allComponents.Count;
								await Logger.LogAsync($"Merged {parseResult.Components.Count} parsed components with existing {originalCount} components. Total components now: {newCount}");
								await tryAutoGenerate(_mainConfig.allComponents);
							}
							else
							{
								await Logger.LogAsync("Merge cancelled by user.");
								return false;
							}
						}
						else if ( confirmResult == false ) // Overwrite
						{
							_mainConfig.allComponents = new List<ModComponent>(parseResult.Components);
							await Logger.LogAsync($"Overwrote existing config with {parseResult.Components.Count} components from markdown.");
							await tryAutoGenerate(parseResult.Components.ToList());
						}
						else
						{
							return false;
						}
					}

					await onComponentsLoaded();
					return true;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return false;
			}
		}

		/// <summary>
		/// Saves components to a TOML file
		/// </summary>
		public async Task<bool> SaveTomlFileAsync(string filePath, List<ModComponent> components)
		{
			try
			{
				if ( string.IsNullOrEmpty(filePath) )
					return false;

				await Logger.LogVerboseAsync($"Saving TOML config to {filePath}");

				using ( var writer = new StreamWriter(filePath) )
				{
					foreach ( ModComponent c in components )
					{
						string tomlContents = c.SerializeComponent();
						await writer.WriteLineAsync(tomlContents);
					}
				}

				_lastLoadedFileName = Path.GetFileName(filePath);
				return true;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return false;
			}
		}

		#region Private Helper Methods

		private async Task<bool?> ShowConfigLoadConfirmationAsync(string fileType, bool editorMode)
		{
			if ( _mainConfig.allComponents.Count == 0 )
				return true; // No existing config

			// If editor mode is disabled, always overwrite
			if ( !editorMode )
				return false;

			string confirmText = $"You already have a config loaded. Do you want to merge the {fileType} with existing components or load it as a new config?";
			return await ConfirmationDialog.ShowConfirmationDialog(_parentWindow, confirmText, yesButtonText: "Merge", noButtonText: "Load as New");
		}

		private async Task<MergeStrategy?> ShowMergeStrategyDialogAsync(string fileType = "TOML")
		{
			string confirmText = $"How would you like to merge the {fileType} components?\n\n" +
								"• GUID Matching: Matches components by their unique GUID (recommended for TOML files)\n" +
								"• Name/Author Matching: Matches components by name and author using fuzzy matching";

			bool? result = await ConfirmationDialog.ShowConfirmationDialog(_parentWindow, confirmText, yesButtonText: "GUID Matching", noButtonText: "Name/Author Matching");

			if ( result == null )
				return null;
			return result == true ? MergeStrategy.ByGuid : MergeStrategy.ByNameAndAuthor;
		}

		#endregion
	}

	/// <summary>
	/// Merge strategy for combining component lists
	/// </summary>
	public enum MergeStrategy
	{
		ByGuid,
		ByNameAndAuthor
	}
}

