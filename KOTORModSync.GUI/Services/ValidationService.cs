// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Dialogs;
using KOTORModSync.Core.Services.FileSystem;

namespace KOTORModSync.Services
{

	public class ValidationService
	{
		private readonly MainConfig _mainConfig;

		public ValidationService(MainConfig mainConfig)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
		}


		public static async Task<bool> ValidateComponentFilesExistAsync(ModComponent component)
		{
			try
			{
				if ( component?.Instructions == null || component.Instructions.Count == 0 )

					return true;

				Logger.LogVerbose($"[ValidationService] Validating component '{component.Name}' (GUID: {component.Guid})");
				Logger.LogVerbose($"[ValidationService] ModComponent has {component.Instructions.Count} instructions");

				var validationProvider = new VirtualFileSystemProvider();
				await validationProvider.InitializeFromRealFileSystemAsync(MainConfig.SourcePath?.FullName ?? "");

				foreach ( Instruction instruction in component.Instructions )
				{
					if ( instruction.Source == null || instruction.Source.Count == 0 )
						continue;

					List<string> resolvedPaths = instruction.Source
						.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
						.Select(sourcePath => ResolvePath(sourcePath))
						.ToList();

					if ( resolvedPaths.Count == 0 )
						continue;

					Logger.LogVerbose($"[ValidationService] Checking {resolvedPaths.Count} source paths for instruction");

					List<string> foundFiles = PathHelper.EnumerateFilesWithWildcards(
						resolvedPaths,
						validationProvider,
						includeSubFolders: true
					);

					if ( foundFiles == null || foundFiles.Count == 0 )
					{
						Logger.LogVerbose($"[ValidationService] No files found for paths: {string.Join(", ", resolvedPaths)}");
						return false;
					}

					Logger.LogVerbose($"[ValidationService] Found {foundFiles.Count} files for instruction");
				}

				Logger.LogVerbose($"[ValidationService] ModComponent '{component.Name}' validation passed - all files exist");
				return true;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error validating files for component '{component?.Name}'");
				return false;
			}
		}

		public static bool ValidateComponentFilesExist(ModComponent component)
		{
			try
			{
				return ValidateComponentFilesExistAsync(component).GetAwaiter().GetResult();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error in synchronous validation for component '{component?.Name}'");
				return false;
			}
		}

		public static async Task<bool> ValidateComponentFilesExistStaticAsync(ModComponent component)
		{
			try
			{
				if ( component?.Instructions == null || component.Instructions.Count == 0 )

					return true;

				Logger.LogVerbose($"[ValidationService] Validating component '{component.Name}' (GUID: {component.Guid})");
				Logger.LogVerbose($"[ValidationService] ModComponent has {component.Instructions.Count} instructions");

				var validationProvider = new VirtualFileSystemProvider();
				await validationProvider.InitializeFromRealFileSystemAsync(MainConfig.SourcePath?.FullName ?? "");

				foreach ( Instruction instruction in component.Instructions )
				{
					if ( instruction.Source == null || instruction.Source.Count == 0 )
						continue;

					List<string> resolvedPaths = instruction.Source
						.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
						.Select(sourcePath => ResolvePath(sourcePath))
						.ToList();

					if ( resolvedPaths.Count == 0 )
						continue;

					Logger.LogVerbose($"[ValidationService] Checking {resolvedPaths.Count} source paths for instruction");

					List<string> foundFiles = PathHelper.EnumerateFilesWithWildcards(
						resolvedPaths,
						validationProvider,
						includeSubFolders: true
					);

					if ( foundFiles == null || foundFiles.Count == 0 )
					{
						Logger.LogVerbose($"[ValidationService] No files found for paths: {string.Join(", ", resolvedPaths)}");
						return false;
					}

					Logger.LogVerbose($"[ValidationService] Found {foundFiles.Count} files for instruction");
				}

				Logger.LogVerbose($"[ValidationService] ModComponent '{component.Name}' validation passed - all files exist");
				return true;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error validating files for component '{component?.Name}'");
				return false;
			}
		}


		public static List<string> GetMissingFilesForComponentStatic(ModComponent component)
		{
			var missingFiles = new List<string>();

			try
			{
				if ( component?.Instructions == null || component.Instructions.Count == 0 )
					return missingFiles;

				Logger.LogVerbose($"[ValidationService] Getting missing files for component '{component.Name}' (GUID: {component.Guid})");

				var validationProvider = new VirtualFileSystemProvider();
				validationProvider.InitializeFromRealFileSystem(MainConfig.SourcePath?.FullName ?? "");

				foreach ( Instruction instruction in component.Instructions )
				{
					// Handle Choose instructions - validate the selected option's instructions
					if ( instruction.Action == Instruction.ActionType.Choose )
					{
						if ( instruction.Source != null && instruction.Source.Count > 0 )
						{
							// Source contains Option GUIDs
							foreach ( string optionGuidStr in instruction.Source )
							{
								if ( Guid.TryParse(optionGuidStr, out Guid optionGuid) )
								{
									Option selectedOption = component.Options?.FirstOrDefault(o => o.Guid == optionGuid);
									if ( selectedOption != null && selectedOption.IsSelected )
									{
										// Recursively validate the selected option's instructions
										if ( selectedOption.Instructions != null )
										{
											foreach ( Instruction optionInstruction in selectedOption.Instructions )
											{
												if ( optionInstruction.Source == null || optionInstruction.Source.Count == 0 )
													continue;

												List<string> optionResolvedPaths = optionInstruction.Source
													.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
													.Select(sourcePath => ResolvePath(sourcePath))
													.ToList();

												if ( optionResolvedPaths.Count == 0 )
													continue;

												List<string> foundOptionFiles = PathHelper.EnumerateFilesWithWildcards(
													optionResolvedPaths,
													validationProvider,
													includeSubFolders: true
												);

												if ( foundOptionFiles == null || foundOptionFiles.Count == 0 )
												{
													foreach ( string resolvedPath in optionResolvedPaths )
													{
														string fileName = Path.GetFileName(resolvedPath);
														if ( !string.IsNullOrEmpty(fileName) && !missingFiles.Contains(fileName) )
														{
															missingFiles.Add(fileName);
															Logger.LogVerbose($"[ValidationService] Missing file in option '{selectedOption.Name}': {fileName}");
														}
													}
												}
											}
										}
									}
								}
							}
						}
						continue;
					}

					if ( instruction.Source == null || instruction.Source.Count == 0 )
						continue;

					List<string> resolvedPaths = instruction.Source
						.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
						.Select(sourcePath => ResolvePath(sourcePath))
						.ToList();

					if ( resolvedPaths.Count == 0 )
						continue;

					Logger.LogVerbose($"[ValidationService] Checking {resolvedPaths.Count} source paths for instruction");

					List<string> foundFiles = PathHelper.EnumerateFilesWithWildcards(
						resolvedPaths,
						validationProvider,
						includeSubFolders: true
					);

					if ( foundFiles == null || foundFiles.Count == 0 )
					{

						foreach ( string resolvedPath in resolvedPaths )
						{
							string fileName = Path.GetFileName(resolvedPath);
							if ( !string.IsNullOrEmpty(fileName) && !missingFiles.Contains(fileName) )
							{
								missingFiles.Add(fileName);
								Logger.LogVerbose($"[ValidationService] Missing file: {fileName}");
							}
						}
					}
					else
					{
						Logger.LogVerbose($"[ValidationService] Found {foundFiles.Count} files for instruction");
					}
				}

				Logger.LogVerbose($"[ValidationService] Found {missingFiles.Count} missing files for component '{component.Name}'");
				return missingFiles;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error getting missing files for component '{component?.Name}'");
				return missingFiles;
			}
		}


		public static async Task<List<string>> GetMissingFilesForComponentAsync(ModComponent component)
		{
			var missingFiles = new List<string>();

			try
			{
				if ( component?.Instructions == null || component.Instructions.Count == 0 )
					return missingFiles;

				Logger.LogVerbose($"[ValidationService] Getting missing files for component '{component.Name}' (GUID: {component.Guid})");

				var validationProvider = new VirtualFileSystemProvider();
				await validationProvider.InitializeFromRealFileSystemAsync(MainConfig.SourcePath?.FullName ?? "");

				foreach ( Instruction instruction in component.Instructions )
				{
					// Handle Choose instructions - validate the selected option's instructions
					if ( instruction.Action == Instruction.ActionType.Choose )
					{
						if ( instruction.Source != null && instruction.Source.Count > 0 )
						{
							// Source contains Option GUIDs
							foreach ( string optionGuidStr in instruction.Source )
							{
								if ( Guid.TryParse(optionGuidStr, out Guid optionGuid) )
								{
									Option selectedOption = component.Options?.FirstOrDefault(o => o.Guid == optionGuid);
									if ( selectedOption != null && selectedOption.IsSelected )
									{
										// Recursively validate the selected option's instructions
										if ( selectedOption.Instructions != null )
										{
											foreach ( Instruction optionInstruction in selectedOption.Instructions )
											{
												if ( optionInstruction.Source == null || optionInstruction.Source.Count == 0 )
													continue;

												List<string> optionResolvedPaths = optionInstruction.Source
													.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
													.Select(sourcePath => ResolvePath(sourcePath))
													.ToList();

												if ( optionResolvedPaths.Count == 0 )
													continue;

												List<string> foundOptionFiles = PathHelper.EnumerateFilesWithWildcards(
													optionResolvedPaths,
													validationProvider,
													includeSubFolders: true
												);

												if ( foundOptionFiles == null || foundOptionFiles.Count == 0 )
												{
													foreach ( string resolvedPath in optionResolvedPaths )
													{
														string fileName = Path.GetFileName(resolvedPath);
														if ( !string.IsNullOrEmpty(fileName) && !missingFiles.Contains(fileName) )
														{
															missingFiles.Add(fileName);
															Logger.LogVerbose($"[ValidationService] Missing file in option '{selectedOption.Name}': {fileName}");
														}
													}
												}
											}
										}
									}
								}
							}
						}
						continue;
					}

					if ( instruction.Source == null || instruction.Source.Count == 0 )
						continue;

					List<string> resolvedPaths = instruction.Source
						.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
						.Select(sourcePath => ResolvePath(sourcePath))
						.ToList();

					if ( resolvedPaths.Count == 0 )
						continue;

					Logger.LogVerbose($"[ValidationService] Checking {resolvedPaths.Count} source paths for instruction");

					List<string> foundFiles = PathHelper.EnumerateFilesWithWildcards(
						resolvedPaths,
						validationProvider,
						includeSubFolders: true
					);

					if ( foundFiles == null || foundFiles.Count == 0 )
					{

						foreach ( string resolvedPath in resolvedPaths )
						{
							string fileName = Path.GetFileName(resolvedPath);
							if ( !string.IsNullOrEmpty(fileName) && !missingFiles.Contains(fileName) )
							{
								missingFiles.Add(fileName);
								Logger.LogVerbose($"[ValidationService] Missing file: {fileName}");
							}
						}
					}
					else
					{
						Logger.LogVerbose($"[ValidationService] Found {foundFiles.Count} files for instruction");
					}
				}

				Logger.LogVerbose($"[ValidationService] Found {missingFiles.Count} missing files for component '{component.Name}'");
				return missingFiles;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error getting missing files for component '{component?.Name}'");
				return missingFiles;
			}
		}

		public static List<string> GetMissingFilesForComponent(ModComponent component)
		{
			try
			{
				return ValidationService.GetMissingFilesForComponentAsync(component).GetAwaiter().GetResult();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error in synchronous GetMissingFilesForComponent for '{component?.Name}'");
				return new List<string>();
			}
		}

		private static string ResolvePath(string path)
		{
			if ( string.IsNullOrWhiteSpace(path) )
				return path;

			if ( path.Contains("<<modDirectory>>") )
			{
				string modDir = MainConfig.SourcePath?.FullName ?? "";
				path = path.Replace("<<modDirectory>>", modDir);
			}

			return path;
		}

		/// <summary>
		/// Validates a single path (with wildcards and placeholders) against the virtual file system.
		/// Returns a status message indicating whether the path will exist at installation time.
		/// </summary>
		public static string ValidateInstructionPath(string path, Instruction instruction, ModComponent component)
		{
			try
			{
				if ( string.IsNullOrWhiteSpace(path) )
					return "❓ Empty";

				// For Patcher actions with kotorDirectory destination, it's always valid
				if ( instruction != null && instruction.Action == Instruction.ActionType.Patcher
					&& path.Equals("<<kotorDirectory>>", StringComparison.OrdinalIgnoreCase) )
				{
					return "✅ Valid (Patcher destination)";
				}

				// Check if directories are configured
				if ( path.Contains("<<modDirectory>>") && MainConfig.SourcePath == null )
					return "⚠️ Mod directory not configured";
				
				if ( path.Contains("<<kotorDirectory>>") && MainConfig.DestinationPath == null )
					return "⚠️ KOTOR directory not configured";

				if ( MainConfig.SourcePath == null )
					return "⚠️ Paths not configured";

				// Resolve the path
				string resolvedPath = ResolvePath(path);

				// Create virtual filesystem and initialize from mod directory
				var virtualProvider = new VirtualFileSystemProvider();
				virtualProvider.InitializeFromRealFileSystem(MainConfig.SourcePath.FullName);

				// Simulate previous instructions if we have component context
				if ( component != null && instruction != null )
				{
					SimulatePreviousInstructions(virtualProvider, component, instruction);
				}

				// Check if the path exists (with wildcard support)
				List<string> foundFiles = PathHelper.EnumerateFilesWithWildcards(
					new List<string> { resolvedPath },
					virtualProvider,
					includeSubFolders: true
				);

				if ( foundFiles != null && foundFiles.Count > 0 )
					return $"✅ Found ({foundFiles.Count} file{(foundFiles.Count != 1 ? "s" : "")})";

				// Check if it's a directory
				if ( virtualProvider.DirectoryExists(resolvedPath) )
					return "✅ Directory exists";

				return "❌ Not found";
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error validating path: {path}");
				return "⚠️ Validation error";
			}
		}

		/// <summary>
		/// Simulates all instructions that come before the target instruction to build up the virtual filesystem state.
		/// </summary>
		private static void SimulatePreviousInstructions(VirtualFileSystemProvider virtualProvider, ModComponent component, Instruction targetInstruction)
		{
			try
			{
				int targetIndex = component.Instructions.IndexOf(targetInstruction);
				if ( targetIndex < 0 )
					return;

				// Process all instructions before the target
				for ( int i = 0; i < targetIndex; i++ )
				{
					Instruction prevInstruction = component.Instructions[i];
					if ( prevInstruction.Action == Instruction.ActionType.Extract && prevInstruction.Source != null )
					{
						// Simulate extraction
						foreach ( string sourcePath in prevInstruction.Source )
						{
							string resolvedSource = ResolvePath(sourcePath);
							List<string> archiveFiles = PathHelper.EnumerateFilesWithWildcards(
								new List<string> { resolvedSource },
								virtualProvider,
								includeSubFolders: true
							);

							foreach ( string archiveFile in archiveFiles )
							{
								string destination = !string.IsNullOrWhiteSpace(prevInstruction.Destination)
									? ResolvePath(prevInstruction.Destination)
									: MainConfig.SourcePath.FullName;

								// Simulate the extraction
								_ = virtualProvider.ExtractArchiveAsync(archiveFile, destination).GetAwaiter().GetResult();
							}
						}
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error simulating previous instructions");
			}
		}

		public bool IsComponentValidForInstallation(ModComponent component, bool editorMode)
		{
			if ( component == null )
				return false;

			if ( string.IsNullOrWhiteSpace(component.Name) )
				return false;

			if ( component.Dependencies.Count > 0 )
			{
				List<ModComponent> dependencyComponents = ModComponent.FindComponentsFromGuidList(component.Dependencies, _mainConfig.allComponents);
				foreach ( ModComponent dep in dependencyComponents )
				{
					if ( dep == null || dep.IsSelected )
						continue;
					return false;
				}
			}

			if ( component.Restrictions.Count > 0 )
			{
				List<ModComponent> restrictionComponents = ModComponent.FindComponentsFromGuidList(component.Restrictions, _mainConfig.allComponents);
				foreach ( ModComponent restriction in restrictionComponents )
				{
					if ( restriction == null || !restriction.IsSelected )
						continue;
					return false;
				}
			}

			if ( component.Instructions.Count == 0 )
				return false;

			return !editorMode || ValidationService.AreModLinksValid(component.ModLink);
		}

		public static bool AreModLinksValid(List<string> modLinks)
		{
			if ( modLinks == null || modLinks.Count == 0 )
				return true;

			foreach ( string link in modLinks )
			{
				if ( string.IsNullOrWhiteSpace(link) )
					continue;

				if ( !ValidationService.IsValidUrl(link) )
					return false;
			}

			return true;
		}

		public static bool IsValidUrl(string url)
		{
			if ( string.IsNullOrWhiteSpace(url) )
				return false;

			if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri uri) )
				return false;

			if ( uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps )
				return false;

			if ( string.IsNullOrWhiteSpace(uri.Host) )
				return false;

			return true;
		}

		public (string ErrorType, string Description, bool CanAutoFix) GetComponentErrorDetails(ModComponent component)
		{
			var errorReasons = new List<string>();

			if ( string.IsNullOrWhiteSpace(component.Name) )
				errorReasons.Add("Missing mod name");

			if ( component.Dependencies.Count > 0 )
			{
				List<ModComponent> dependencyComponents = ModComponent.FindComponentsFromGuidList(component.Dependencies, _mainConfig.allComponents);
				var missingDeps = dependencyComponents.Where(dep => dep == null || !dep.IsSelected).ToList();
				if ( missingDeps.Count > 0 )
					errorReasons.Add($"Missing required dependencies ({missingDeps.Count})");
			}

			if ( component.Restrictions.Count > 0 )
			{
				List<ModComponent> restrictionComponents = ModComponent.FindComponentsFromGuidList(component.Restrictions, _mainConfig.allComponents);
				var conflictingMods = restrictionComponents.Where(restriction => restriction != null && restriction.IsSelected).ToList();
				if ( conflictingMods.Count > 0 )
					errorReasons.Add($"Conflicting mods selected ({conflictingMods.Count})");
			}

			if ( component.Instructions.Count == 0 )
				errorReasons.Add("No installation instructions");

			if ( !ValidationService.AreModLinksValid(component.ModLink) )
			{
				List<string> invalidUrls = component.ModLink.Where(link => !string.IsNullOrWhiteSpace(link) && !ValidationService.IsValidUrl(link)).ToList();
				if ( invalidUrls.Count > 0 )
					errorReasons.Add($"Invalid download URLs ({invalidUrls.Count})");
				else
					errorReasons.Add("Invalid download URLs");
			}

			if ( errorReasons.Count == 0 )
				return ("Unknown Error", "No specific error details available", false);

			string primaryError = errorReasons[0];
			string description = string.Join(", ", errorReasons);

			bool canAutoFix = primaryError.Contains("Missing required dependencies") ||
							  primaryError.Contains("Conflicting mods selected");

			return (primaryError, description, canAutoFix);
		}

		public static string GetUrlValidationReason(string url)
		{
			if ( string.IsNullOrWhiteSpace(url) )
				return "Empty URL";

			if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri uri) )
				return "Invalid URL format";

			if ( uri.Scheme != "http" && uri.Scheme != "https" )
				return $"Unsupported protocol: {uri.Scheme}";

			return "Valid URL";
		}

		public static bool IsStep1Complete()
		{
			try
			{

				if ( string.IsNullOrEmpty(MainConfig.SourcePath?.FullName) ||
					string.IsNullOrEmpty(MainConfig.DestinationPath?.FullName) )
				{
					return false;
				}

				if ( !Directory.Exists(MainConfig.SourcePath.FullName) ||
					!Directory.Exists(MainConfig.DestinationPath.FullName) )
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
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error checking Step 1 completion");
				return false;
			}
		}

		public async Task AnalyzeValidationFailures(List<Dialogs.ValidationIssue> modIssues, List<string> systemIssues)
		{
			try
			{

				if ( MainConfig.DestinationPath == null || MainConfig.SourcePath == null )
				{
					systemIssues.Add("⚙️ Directories not configured\n" +
									"Both Mod Directory and KOTOR Install Directory must be set.\n" +
									"Solution: Click Settings and configure both directories.");
					return;
				}

				if ( !_mainConfig.allComponents.Any() )
				{
					systemIssues.Add("📋 No mods loaded\n" +
									"No mod configuration file has been loaded.\n" +
									"Solution: Click 'File > Open File' to load a mod list.");
					return;
				}

				if ( !_mainConfig.allComponents.Any(c => c.IsSelected) )
				{
					systemIssues.Add("☑️ No mods selected\n" +
									"At least one mod must be selected for installation.\n" +
									"Solution: Check the boxes next to mods you want to install.");
					return;
				}

				foreach ( ModComponent component in _mainConfig.allComponents.Where(c => c.IsSelected) )
				{

					if ( !component.IsDownloaded )
					{
						var issue = new Dialogs.ValidationIssue
						{
							Icon = "📥",
							ModName = component.Name,
							IssueType = "Missing Download",
							Description = "The mod archive file is not in your Mod Directory.",
							Solution = component.ModLink.Count > 0
								? $"Solution: Click 'Fetch Downloads' or manually download from: {component.ModLink[0]}"
								: "Solution: Click 'Fetch Downloads' or manually download and place in Mod Directory."
						};
						modIssues.Add(issue);
						continue;
					}

					if ( component.Instructions.Count == 0 && component.Options.Count == 0 )
					{
						var issue = new Dialogs.ValidationIssue
						{
							Icon = "❌",
							ModName = component.Name,
							IssueType = "Missing Instructions",
							Description = "This mod has no installation instructions defined.",
							Solution = "Solution: Contact the mod list creator or disable this mod."
						};
						modIssues.Add(issue);
					}

					bool componentValid = await ValidationService.ValidateComponentFilesExistAsync(component);
					if ( !componentValid )
					{

						List<string> missingFiles = await ValidationService.GetMissingFilesForComponentAsync(component);
						string missingFilesDescription = missingFiles.Count > 0
							? $"Missing file(s): {string.Join(", ", missingFiles)}"
							: "One or more required files for this mod are missing from your Mod Directory.";

						var issue = new Dialogs.ValidationIssue
						{
							Icon = "🔧",
							ModName = component.Name,
							IssueType = "Missing Files",
							Description = missingFilesDescription,
							Solution = "Solution: Click 'Fetch Downloads' to download missing files or check the Output Window for details."
						};
						modIssues.Add(issue);
					}
				}

				if ( !Utility.IsDirectoryWritable(MainConfig.DestinationPath) )
				{
					systemIssues.Add("🔒 KOTOR Directory Not Writable\n" +
									"The installer cannot write to your KOTOR installation directory.\n" +
									"Solution: Run as Administrator or install to a different location.");
				}

				if ( !Utility.IsDirectoryWritable(MainConfig.SourcePath) )
				{
					systemIssues.Add("🔒 Mod Directory Not Writable\n" +
									"The installer cannot write to your Mod Directory.\n" +
									"Solution: Ensure you have write permissions.");
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				systemIssues.Add("❌ Unexpected Error\n" +
								"An error occurred during validation analysis.\n" +
								"Solution: Check the Output Window for details.");
			}
		}
	}
}

