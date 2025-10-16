// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Dialogs;

namespace KOTORModSync.Services
{
	public class FileLoadingService
	{
		private readonly MainConfig _mainConfig;
		private readonly Window _parentWindow;

		public string LastLoadedFileName { get; set; }

		public FileLoadingService(MainConfig mainConfig, Window parentWindow)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
			_parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
		}

		public async Task<bool> LoadTomlFileAsync(
			[NotNull] string filePath,
			bool editorMode,
			[NotNull] Func<Task> onComponentsLoaded,
			[NotNull] string fileType = "instruction file")
		{
			try
			{

				var fileInfo = new FileInfo(filePath);
				const int maxInstructionSize = 524288000;
				if ( fileInfo.Length > maxInstructionSize )
				{
					await Logger.LogAsync($"Invalid {fileType} selected: '{fileInfo.Name}' - file too large");
					return false;
				}

				List<ModComponent> newComponents = await Core.Services.FileLoadingService.LoadFromFileAsync(filePath);

				ProcessModLinks(newComponents);

				if ( _mainConfig.allComponents.Count == 0 )
				{
					_mainConfig.allComponents = newComponents;
					LastLoadedFileName = Path.GetFileName(filePath);
					await Logger.LogAsync($"Loaded {newComponents.Count} components from {fileType}.");
					await onComponentsLoaded();
					return true;
				}

				bool? result = await ShowConfigLoadConfirmationAsync(fileType, editorMode);

				switch ( result )
				{
					case true:
						{
							var conflictDialog = new ComponentMergeConflictDialog(
								_mainConfig.allComponents,
								newComponents,
								"Currently Loaded Components",
								fileType,
								(existing, incoming) =>
								{
									if ( existing.Guid == incoming.Guid )
										return true;
									return FuzzyMatcher.FuzzyMatchComponents(existing, incoming);
								});

							await conflictDialog.ShowDialog(_parentWindow);

							if ( conflictDialog.UserConfirmed && conflictDialog.MergedComponents != null )
							{
								int originalCount = _mainConfig.allComponents.Count;
								_mainConfig.allComponents = conflictDialog.MergedComponents;
								int newCount = _mainConfig.allComponents.Count;
								LastLoadedFileName = Path.GetFileName(filePath);

								await Logger.LogAsync($"Merged {newComponents.Count} components from {fileType} with existing {originalCount} components using hybrid matching (GUID then Name/Author). Total components now: {newCount}");
							}
							else
							{
								await Logger.LogAsync("Merge cancelled by user.");
								return false;
							}
							break;
						}
					case false:
						_mainConfig.allComponents = newComponents;
						LastLoadedFileName = Path.GetFileName(filePath);
						await Logger.LogAsync($"Overwrote existing config with {newComponents.Count} components from {fileType}.");
						break;
					default:
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

		public async Task<bool> LoadMarkdownFileAsync(
			[NotNull] string filePath,
			bool editorMode,
			[NotNull] Func<Task> onComponentsLoaded,
			[NotNull] Func<List<ModComponent>, Task> tryAutoGenerate,
			[CanBeNull] MarkdownImportProfile profile = null)
		{
			try
			{
				if ( string.IsNullOrEmpty(filePath) )
					return false;

				using ( var reader = new StreamReader(filePath) )
				{
					string fileContents = await reader.ReadToEndAsync();

					MarkdownParserResult parseResult = null;
					MarkdownImportProfile configuredProfile;

					if ( editorMode )
					{

						var dialog = new RegexImportDialog(fileContents, profile ?? MarkdownImportProfile.CreateDefault());

						dialog.Closed += async (_, __) =>
						{
							if ( !dialog.LoadSuccessful || !(dialog.DataContext is RegexImportDialogViewModel vm) )
								return;

							configuredProfile = vm.ConfiguredProfile;

							parseResult = vm.ConfirmLoad();

							ProcessModLinks(parseResult.Components);

							await Logger.LogAsync($"Markdown parsing completed using {(configuredProfile.Mode == RegexMode.Raw ? "raw" : "individual")} regex mode.");
							await Logger.LogAsync($"Found {parseResult.Components?.Count ?? 0} components with {parseResult.Components?.Sum(c => c.ModLinkFilenames.Count) ?? 0} total links.");

							if ( !(parseResult.Warnings?.Count > 0) )
								return;
							await Logger.LogWarningAsync($"Markdown parsing completed with {parseResult.Warnings.Count} warnings.");
							foreach ( string warning in parseResult.Warnings )
								await Logger.LogWarningAsync($"  - {warning}");
						};

						await dialog.ShowDialog(_parentWindow);

						if ( parseResult is null )
							return false;
					}
					else
					{

						configuredProfile = profile ?? MarkdownImportProfile.CreateDefault();
						var parser = new MarkdownParser(configuredProfile,
							logInfo => Logger.Log(logInfo),
							Logger.LogVerbose);
						parseResult = parser.Parse(fileContents);

						ProcessModLinks(parseResult.Components);

						await Logger.LogAsync("Markdown parsing completed using default profile.");
						await Logger.LogAsync($"Found {parseResult.Components?.Count ?? 0} components with {parseResult.Components?.Sum(c => c.ModLinkFilenames.Count) ?? 0} total links.");

						if ( parseResult.Warnings?.Count > 0 )
						{
							await Logger.LogWarningAsync($"Markdown parsing completed with {parseResult.Warnings.Count} warnings.");
							foreach ( string warning in parseResult.Warnings )
								await Logger.LogWarningAsync($"  - {warning}");
						}
					}


					_mainConfig.beforeModListContent = parseResult.BeforeModListContent ?? string.Empty;
					_mainConfig.afterModListContent = parseResult.AfterModListContent ?? string.Empty;
					_mainConfig.widescreenSectionContent = parseResult.WidescreenSectionContent ?? string.Empty;
					_mainConfig.aspyrSectionContent = parseResult.AspyrSectionContent ?? string.Empty;
					await Logger.LogAsync($"Stored {_mainConfig.beforeModListContent.Length} characters before mod list and {_mainConfig.afterModListContent.Length} characters after.");

					if ( _mainConfig.allComponents.Count == 0 )
					{
						_mainConfig.allComponents = new List<ModComponent>(
							parseResult.Components
							?? throw new InvalidOperationException("[LoadMarkdownFileAsync] parseResult.Components is null")
						);
						await Logger.LogAsync($"Loaded {parseResult.Components.Count} components from markdown.");
						await tryAutoGenerate(parseResult.Components.ToList());
					}
					else
					{
						bool? confirmResult = await ShowConfigLoadConfirmationAsync("markdown file", editorMode);

						if ( confirmResult == true )
						{
							var conflictDialog = new ComponentMergeConflictDialog(
								_mainConfig.allComponents,
								new List<ModComponent>(
									parseResult.Components
									?? throw new InvalidOperationException("[LoadMarkdownFileAsync] parseResult.Components is null")
								),
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
						else if ( confirmResult == false )
						{
							_mainConfig.allComponents = new List<ModComponent>(
								parseResult.Components
								?? throw new InvalidOperationException("[LoadMarkdownFileAsync] parseResult.Components is null")
							);
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

		public async Task<bool> SaveTomlFileAsync(string filePath, List<ModComponent> components)
		{
			try
			{
				if ( string.IsNullOrEmpty(filePath) )
					return false;

				await Logger.LogVerboseAsync($"Saving TOML config to {filePath}");

				await Core.Services.FileLoadingService.SaveToFileAsync(components, filePath);

				LastLoadedFileName = Path.GetFileName(filePath);
				return true;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return false;
			}
		}

		#region Private Helper Methods

		private static void ProcessModLinks(IList<ModComponent> components)
		{
			if ( components == null )
				return;

			const string baseUrl = "";

			foreach ( ModComponent component in components )
			{
				var urlsToFix = component.ModLinkFilenames.Keys
					.Where(url => !string.IsNullOrEmpty(url) && url[0] == '/')
					.ToList();

				foreach ( string relativeUrl in urlsToFix )
				{
					string fixedUrl = baseUrl + relativeUrl;
					Dictionary<string, bool?> filenameDict = component.ModLinkFilenames[relativeUrl];
					component.ModLinkFilenames.Remove(relativeUrl);
					component.ModLinkFilenames[fixedUrl] = filenameDict;
				}
			}
		}

		private async Task<bool?> ShowConfigLoadConfirmationAsync(string fileType, bool editorMode)
		{
			if ( _mainConfig.allComponents.Count == 0 )
				return true;

			if ( !editorMode )
				return false;

			string confirmText = $"You already have a config loaded. Do you want to merge the {fileType} with existing components or load it as a new config?";
			return await ConfirmationDialog.ShowConfirmationDialogAsync(
				_parentWindow,
				confirmText,
				yesButtonText: "Merge",
				noButtonText: "Load as New"
			);
		}

		#endregion
	}
}

