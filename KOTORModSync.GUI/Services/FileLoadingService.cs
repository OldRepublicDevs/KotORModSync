// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

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

		/// <summary>
		/// Loads a config file with auto-format detection (TOML, JSON, YAML, or embedded Markdown).
		/// Uses the Core FileLoadingService which auto-detects format based on content.
		/// </summary>
		public async Task<bool> LoadInstructionFileAsync(
			[NotNull] string filePath,
			bool editorMode,
			[NotNull] Func<Task> onComponentsLoaded,
			[NotNull] string fileType = "instruction file")
		{
			try
			{

				var fileInfo = new FileInfo(filePath);
				const int maxInstructionSize = 524288000;
				if (fileInfo.Length > maxInstructionSize)


				{
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
					await Logger.LogAsync($"Invalid {fileType} selected: '{fileInfo.Name}' - file too large");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
					return false;


				}

				// Auto-detect format (TOML/JSON/YAML/embedded-Markdown)
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
				List<ModComponent> newComponents = await Core.Services.FileLoadingService.LoadFromFileAsync(filePath);
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)

				ProcessModLinks(newComponents);

				if (_mainConfig.allComponents.Count == 0)
				{
					_mainConfig.allComponents = newComponents;
					LastLoadedFileName = Path.GetFileName(filePath);


#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
					await Logger.LogAsync($"Loaded {newComponents.Count} components from {fileType}.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
					await onComponentsLoaded();
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
					return true;
				}

#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
				bool? result = await ShowConfigLoadConfirmationAsync(fileType, editorMode);
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)

				switch (result)
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
									if (existing.Guid == incoming.Guid)
										return true;
									return FuzzyMatcher.FuzzyMatchComponents(existing, incoming);
								});



#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
							await conflictDialog.ShowDialog(_parentWindow);
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)

							if (conflictDialog.UserConfirmed && conflictDialog.MergedComponents != null)
							{
								int originalCount = _mainConfig.allComponents.Count;
								_mainConfig.allComponents = conflictDialog.MergedComponents;
								int newCount = _mainConfig.allComponents.Count;
								LastLoadedFileName = Path.GetFileName(filePath);



#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
								await Logger.LogAsync($"Merged {newComponents.Count} components from {fileType} with existing {originalCount} components using hybrid matching (GUID then Name/Author). Total components now: {newCount}");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
							}
							else
							{
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
								await Logger.LogAsync("Merge cancelled by user.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
								return false;
							}
							break;
						}
					case false:
						_mainConfig.allComponents = newComponents;
						LastLoadedFileName = Path.GetFileName(filePath);
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
						await Logger.LogAsync($"Overwrote existing config with {newComponents.Count} components from {fileType}.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
						break;
					default:
						return false;
				}

#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
				await onComponentsLoaded();
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
				return true;
			}
			catch (Exception ex)


			{
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
				await Logger.LogExceptionAsync(ex);
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
				return false;
			}
		}

		/// <summary>
		/// Loads a Markdown file with special parsing dialog (for regex configuration).
		/// For standard embedded-Markdown TOML files, use LoadConfigFileAsync instead.
		/// </summary>
		public async Task<bool> LoadMarkdownFileAsync(
			[NotNull] string filePath,
			bool editorMode,
			[NotNull] Func<Task> onComponentsLoaded,
			[NotNull] Func<List<ModComponent>, Task> tryAutoGenerate,
			[CanBeNull] MarkdownImportProfile profile = null)
		{
			try
			{
				if (string.IsNullOrEmpty(filePath))
					return false;

				using (var reader = new StreamReader(filePath))


				{
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
					string fileContents = await reader.ReadToEndAsync();
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)

					MarkdownParserResult parseResult = null;
					MarkdownImportProfile configuredProfile;

					if (editorMode)
					{
						// UI elements must be created and shown on the UI thread
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
						await Dispatcher.UIThread.InvokeAsync(async () =>
						{
							var dialog = new RegexImportDialog(fileContents, profile ?? MarkdownImportProfile.CreateDefault());

							dialog.Closed += (_, __) =>
							{
								if (!dialog.LoadSuccessful || !(dialog.DataContext is RegexImportDialogViewModel vm))
									return;

								configuredProfile = vm.ConfiguredProfile;
								parseResult = vm.ConfirmLoad();
								ProcessModLinks(parseResult.Components);

								// Log asynchronously without blocking the UI
								_ = Task.Run(async () =>
								{
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
									await Logger.LogAsync($"Markdown parsing completed using {(configuredProfile.Mode == RegexMode.Raw ? "raw" : "individual")} regex mode.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
									await Logger.LogAsync($"Found {parseResult.Components?.Count ?? 0} components with {parseResult.Components?.Sum(c => c.ModLinkFilenames.Count) ?? 0} total links.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)

									if (parseResult.Warnings?.Count > 0)
									{
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
										await Logger.LogWarningAsync($"Markdown parsing completed with {parseResult.Warnings.Count} warnings.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
										foreach (string warning in parseResult.Warnings)
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
											await Logger.LogWarningAsync($"  - {warning}");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
									}
								});
							};

#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
							await dialog.ShowDialog(_parentWindow);
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
						});
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)

						if (parseResult is null)
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

#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
						await Logger.LogAsync("Markdown parsing completed using default profile.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
						await Logger.LogAsync($"Found {parseResult.Components?.Count ?? 0} components with {parseResult.Components?.Sum(c => c.ModLinkFilenames.Count) ?? 0} total links.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
						if (parseResult.Warnings?.Count > 0)
						{
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
							await Logger.LogWarningAsync($"Markdown parsing completed with {parseResult.Warnings.Count} warnings.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
							foreach (string warning in parseResult.Warnings)
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
								await Logger.LogWarningAsync($"  - {warning}");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
						}
					}


					_mainConfig.beforeModListContent = parseResult.BeforeModListContent ?? string.Empty;
					_mainConfig.afterModListContent = parseResult.AfterModListContent ?? string.Empty;
					_mainConfig.widescreenSectionContent = parseResult.WidescreenSectionContent ?? string.Empty;
					_mainConfig.aspyrSectionContent = parseResult.AspyrSectionContent ?? string.Empty;
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
					await Logger.LogAsync($"Stored {_mainConfig.beforeModListContent.Length} characters before mod list and {_mainConfig.afterModListContent.Length} characters after.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)

					if (_mainConfig.allComponents.Count == 0)
					{
						_mainConfig.allComponents = new List<ModComponent>(
							parseResult.Components
							?? throw new InvalidOperationException("[LoadMarkdownFileAsync] parseResult.Components is null")
						);
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
						await Logger.LogAsync($"Loaded {parseResult.Components.Count} components from markdown.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
						await tryAutoGenerate(parseResult.Components.ToList());
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
					}
					else
					{
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
						bool? confirmResult = await ShowConfigLoadConfirmationAsync("markdown file", editorMode);
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)

						if (confirmResult == true)
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

#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
							await conflictDialog.ShowDialog(_parentWindow);
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)

							if (conflictDialog.UserConfirmed && conflictDialog.MergedComponents != null)
							{
								int originalCount = _mainConfig.allComponents.Count;
								_mainConfig.allComponents = conflictDialog.MergedComponents;
								int newCount = _mainConfig.allComponents.Count;
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
								await Logger.LogAsync($"Merged {parseResult.Components.Count} parsed components with existing {originalCount} components. Total components now: {newCount}");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
								await tryAutoGenerate(_mainConfig.allComponents);
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
							}
							else
							{
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
								await Logger.LogAsync("Merge cancelled by user.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
								return false;
							}
						}
						else if (confirmResult == false)
						{
							_mainConfig.allComponents = new List<ModComponent>(
								parseResult.Components
								?? throw new InvalidOperationException("[LoadMarkdownFileAsync] parseResult.Components is null")
							);
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
							await Logger.LogAsync($"Overwrote existing config with {parseResult.Components.Count} components from markdown.");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
							await tryAutoGenerate(parseResult.Components.ToList());
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
						}
						else
						{
							return false;
						}
					}

#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
					await onComponentsLoaded();
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
					return true;
				}
			}
			catch (Exception ex)


			{
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
				await Logger.LogExceptionAsync(ex);
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
				return false;
			}
		}

		public async Task<bool> SaveTomlFileAsync(string filePath, List<ModComponent> components)
		{
			try
			{
				if (string.IsNullOrEmpty(filePath))
					return false;



#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
				await Logger.LogVerboseAsync($"Saving TOML config to {filePath}");
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)

#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
				await Core.Services.FileLoadingService.SaveToFileAsync(components, filePath);
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)

				LastLoadedFileName = Path.GetFileName(filePath);
				return true;
			}
			catch (Exception ex)


			{
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
				await Logger.LogExceptionAsync(ex);
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
				return false;
			}
		}

		#region Private Helper Methods

		private static void ProcessModLinks(IList<ModComponent> components)
		{
			if (components == null)
				return;

			const string baseUrl = "";

			foreach (ModComponent component in components)
			{
				var urlsToFix = component.ModLinkFilenames.Keys
					.Where(url => !string.IsNullOrEmpty(url) && url[0] == '/')
					.ToList();

				foreach (string relativeUrl in urlsToFix)
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
			if (_mainConfig.allComponents.Count == 0)
				return true;

			if (!editorMode)
				return false;

			string confirmText = $"You already have a config loaded. Do you want to merge the {fileType} with existing components or load it as a new config?";
#pragma warning disable MA0004 // Use Task.ConfigureAwait(false)
			return await ConfirmationDialog.ShowConfirmationDialogAsync(
				_parentWindow,
				confirmText: confirmText,
				yesButtonText: "Merge",
				noButtonText: "Load as New"
			);
#pragma warning restore MA0004 // Use Task.ConfigureAwait(false)
		}

		#endregion
	}
}