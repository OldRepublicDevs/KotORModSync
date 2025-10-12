// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
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
	/// <summary>
	/// Service responsible for validation operations
	/// </summary>
	public class ValidationService
	{
		private readonly MainConfig _mainConfig;

		public ValidationService(MainConfig mainConfig)
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException(nameof(mainConfig));
		}


		/// <summary>
		/// Validates that all required files for a component exist using VirtualFileSystemProvider dry run
		/// </summary>
		public static async Task<bool> ValidateComponentFilesExistAsync(ModComponent component)
		{
			try
			{
				if ( component?.Instructions == null || component.Instructions.Count == 0 )
					// Components without instructions might still be valid
					return true;

				Logger.LogVerbose($"[ValidationService] Validating component '{component.Name}' (GUID: {component.Guid})");
				Logger.LogVerbose($"[ValidationService] ModComponent has {component.Instructions.Count} instructions");

				// Create a fresh virtual file system provider for this validation
				var validationProvider = new VirtualFileSystemProvider();
				await validationProvider.InitializeFromRealFileSystemAsync(MainConfig.SourcePath?.FullName ?? "");

				// Check each instruction to see if its source files exist
				foreach ( Instruction instruction in component.Instructions )
				{
					if ( instruction.Source == null || instruction.Source.Count == 0 )
						continue;

					// Resolve paths (handle <<modDirectory>> placeholder)
					List<string> resolvedPaths = instruction.Source
						.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
						.Select(sourcePath => ResolvePath(sourcePath))
						.ToList();

					if ( resolvedPaths.Count == 0 )
						continue;

					Logger.LogVerbose($"[ValidationService] Checking {resolvedPaths.Count} source paths for instruction");

					// Use PathHelper.EnumerateFilesWithWildcards to handle wildcard patterns properly
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

		/// <summary>
		/// Synchronous version of ValidateComponentFilesExistAsync
		/// </summary>
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

		/// <summary>
		/// Static version of ValidateComponentFilesExistAsync for use by ModListItem
		/// </summary>
		public static async Task<bool> ValidateComponentFilesExistStaticAsync(ModComponent component)
		{
			try
			{
				if ( component?.Instructions == null || component.Instructions.Count == 0 )
					// Components without instructions might still be valid
					return true;

				Logger.LogVerbose($"[ValidationService] Validating component '{component.Name}' (GUID: {component.Guid})");
				Logger.LogVerbose($"[ValidationService] ModComponent has {component.Instructions.Count} instructions");

				// Create a fresh virtual file system provider for this validation
				var validationProvider = new VirtualFileSystemProvider();
				await validationProvider.InitializeFromRealFileSystemAsync(MainConfig.SourcePath?.FullName ?? "");

				// Check each instruction to see if its source files exist
				foreach ( Instruction instruction in component.Instructions )
				{
					if ( instruction.Source == null || instruction.Source.Count == 0 )
						continue;

					// Resolve paths (handle <<modDirectory>> placeholder)
					List<string> resolvedPaths = instruction.Source
						.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
						.Select(sourcePath => ResolvePath(sourcePath))
						.ToList();

					if ( resolvedPaths.Count == 0 )
						continue;

					Logger.LogVerbose($"[ValidationService] Checking {resolvedPaths.Count} source paths for instruction");

					// Use PathHelper.EnumerateFilesWithWildcards to handle wildcard patterns properly
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

		/// <summary>
		/// Gets the list of specific missing files for a component (static version for ModListItem)
		/// Uses VirtualFileSystemProvider dry run to detect missing files properly
		/// </summary>
		public static List<string> GetMissingFilesForComponentStatic(ModComponent component)
		{
			var missingFiles = new List<string>();

			try
			{
				if ( component?.Instructions == null || component.Instructions.Count == 0 )
					return missingFiles;

				Logger.LogVerbose($"[ValidationService] Getting missing files for component '{component.Name}' (GUID: {component.Guid})");

				// Create a fresh virtual file system provider for this validation
				var validationProvider = new VirtualFileSystemProvider();
				validationProvider.InitializeFromRealFileSystem(MainConfig.SourcePath?.FullName ?? "");

				// Check each instruction to see if its source files exist
				foreach ( Instruction instruction in component.Instructions )
				{
					if ( instruction.Source == null || instruction.Source.Count == 0 )
						continue;

					// Resolve paths (handle <<modDirectory>> placeholder)
					List<string> resolvedPaths = instruction.Source
						.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
						.Select(sourcePath => ResolvePath(sourcePath))
						.ToList();

					if ( resolvedPaths.Count == 0 )
						continue;

					Logger.LogVerbose($"[ValidationService] Checking {resolvedPaths.Count} source paths for instruction");

					// Use PathHelper.EnumerateFilesWithWildcards to handle wildcard patterns properly
					List<string> foundFiles = PathHelper.EnumerateFilesWithWildcards(
						resolvedPaths,
						validationProvider,
						includeSubFolders: true
					);

					if ( foundFiles == null || foundFiles.Count == 0 )
					{
						// Add missing files to the list
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

		/// <summary>
		/// Gets the list of specific missing files for a component
		/// Uses VirtualFileSystemProvider dry run to detect missing files properly
		/// </summary>
		public static async Task<List<string>> GetMissingFilesForComponentAsync(ModComponent component)
		{
			var missingFiles = new List<string>();

			try
			{
				if ( component?.Instructions == null || component.Instructions.Count == 0 )
					return missingFiles;

				Logger.LogVerbose($"[ValidationService] Getting missing files for component '{component.Name}' (GUID: {component.Guid})");

				// Create a fresh virtual file system provider for this validation
				var validationProvider = new VirtualFileSystemProvider();
				await validationProvider.InitializeFromRealFileSystemAsync(MainConfig.SourcePath?.FullName ?? "");

				// Check each instruction to see if its source files exist
				foreach ( Instruction instruction in component.Instructions )
				{
					if ( instruction.Source == null || instruction.Source.Count == 0 )
						continue;

					// Resolve paths (handle <<modDirectory>> placeholder)
					List<string> resolvedPaths = instruction.Source
						.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
						.Select(sourcePath => ResolvePath(sourcePath))
						.ToList();

					if ( resolvedPaths.Count == 0 )
						continue;

					Logger.LogVerbose($"[ValidationService] Checking {resolvedPaths.Count} source paths for instruction");

					// Use PathHelper.EnumerateFilesWithWildcards to handle wildcard patterns properly
					List<string> foundFiles = PathHelper.EnumerateFilesWithWildcards(
						resolvedPaths,
						validationProvider,
						includeSubFolders: true
					);

					if ( foundFiles == null || foundFiles.Count == 0 )
					{
						// Add missing files to the list
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

		/// <summary>
		/// Synchronous version of GetMissingFilesForComponentAsync
		/// </summary>
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

		/// <summary>
		/// Resolves a path by replacing placeholders like <<modDirectory>>
		/// </summary>
		private static string ResolvePath(string path)
		{
			if ( string.IsNullOrWhiteSpace(path) )
				return path;

			// Replace <<modDirectory>> with actual mod directory path
			if ( path.Contains("<<modDirectory>>") )
			{
				string modDir = MainConfig.SourcePath?.FullName ?? "";
				path = path.Replace("<<modDirectory>>", modDir);
			}

			return path;
		}

		/// <summary>
		/// Checks if a component is valid for installation
		/// </summary>
		public bool IsComponentValidForInstallation(ModComponent component, bool editorMode)
		{
			if ( component == null )
				return false;

			// Check for critical errors
			if ( string.IsNullOrWhiteSpace(component.Name) )
				return false;

			// Check for dependency violations
			if ( component.Dependencies.Count > 0 )
			{
				List<ModComponent> dependencyComponents = ModComponent.FindComponentsFromGuidList(component.Dependencies, _mainConfig.allComponents);
				foreach ( ModComponent dep in dependencyComponents )
				{
					if ( dep == null || dep.IsSelected )
						continue;
					return false; // Missing required dependency
				}
			}

			// Check for restriction violations
			if ( component.Restrictions.Count > 0 )
			{
				List<ModComponent> restrictionComponents = ModComponent.FindComponentsFromGuidList(component.Restrictions, _mainConfig.allComponents);
				foreach ( ModComponent restriction in restrictionComponents )
				{
					if ( restriction == null || !restriction.IsSelected )
						continue;
					return false; // Conflicting mod is selected
				}
			}

			// Check for instruction issues
			if ( component.Instructions.Count == 0 )
				return false;

			// Check for valid ModLinks/URLs only when in EditorMode
			return !editorMode || ValidationService.AreModLinksValid(component.ModLink);
		}

		/// <summary>
		/// Validates that all ModLinks are valid URLs
		/// </summary>
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

		/// <summary>
		/// Checks if a string is a valid URL
		/// </summary>
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

		/// <summary>
		/// Gets detailed error information for a component
		/// </summary>
		public (string ErrorType, string Description, bool CanAutoFix) GetComponentErrorDetails(ModComponent component)
		{
			var errorReasons = new List<string>();

			// Check for missing name
			if ( string.IsNullOrWhiteSpace(component.Name) )
				errorReasons.Add("Missing mod name");

			// Check for dependency violations
			if ( component.Dependencies.Count > 0 )
			{
				List<ModComponent> dependencyComponents = ModComponent.FindComponentsFromGuidList(component.Dependencies, _mainConfig.allComponents);
				var missingDeps = dependencyComponents.Where(dep => dep == null || !dep.IsSelected).ToList();
				if ( missingDeps.Count > 0 )
					errorReasons.Add($"Missing required dependencies ({missingDeps.Count})");
			}

			// Check for restriction violations
			if ( component.Restrictions.Count > 0 )
			{
				List<ModComponent> restrictionComponents = ModComponent.FindComponentsFromGuidList(component.Restrictions, _mainConfig.allComponents);
				var conflictingMods = restrictionComponents.Where(restriction => restriction != null && restriction.IsSelected).ToList();
				if ( conflictingMods.Count > 0 )
					errorReasons.Add($"Conflicting mods selected ({conflictingMods.Count})");
			}

			// Check for instruction issues
			if ( component.Instructions.Count == 0 )
				errorReasons.Add("No installation instructions");

			// Check for invalid URLs
			if ( !ValidationService.AreModLinksValid(component.ModLink) )
			{
				List<string> invalidUrls = component.ModLink.Where(link => !string.IsNullOrWhiteSpace(link) && !ValidationService.IsValidUrl(link)).ToList();
				if ( invalidUrls.Count > 0 )
					errorReasons.Add($"Invalid download URLs ({invalidUrls.Count})");
				else
					errorReasons.Add("Invalid download URLs");
			}

			// Determine error type and description
			if ( errorReasons.Count == 0 )
				return ("Unknown Error", "No specific error details available", false);

			string primaryError = errorReasons[0];
			string description = string.Join(", ", errorReasons);

			// Determine if auto-fix is possible
			bool canAutoFix = primaryError.Contains("Missing required dependencies") ||
							  primaryError.Contains("Conflicting mods selected");

			return (primaryError, description, canAutoFix);
		}

		/// <summary>
		/// Gets URL validation reason for diagnostic purposes
		/// </summary>
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

		/// <summary>
		/// Checks if Step 1 (directory setup) is properly completed
		/// </summary>
		public static bool IsStep1Complete()
		{
			try
			{
				// Check if both directories are set and not empty
				if ( string.IsNullOrEmpty(MainConfig.SourcePath?.FullName) ||
					string.IsNullOrEmpty(MainConfig.DestinationPath?.FullName) )
				{
					return false;
				}

				// Check if both directories actually exist
				if ( !Directory.Exists(MainConfig.SourcePath.FullName) ||
					!Directory.Exists(MainConfig.DestinationPath.FullName) )
				{
					return false;
				}

				// Check if KOTOR directory contains game files
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

		/// <summary>
		/// Analyzes validation failures and generates detailed issue reports
		/// </summary>
		public async Task AnalyzeValidationFailures(List<Dialogs.ValidationIssue> modIssues, List<string> systemIssues)
		{
			try
			{
				// Check system-level issues
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

				// Virtual file system will be initialized per-component as needed

				// Check each selected component
				foreach ( ModComponent component in _mainConfig.allComponents.Where(c => c.IsSelected) )
				{
					// Check if downloaded
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

					// Check if it has instructions
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

					// Run component validation using our new logic
					bool componentValid = await ValidationService.ValidateComponentFilesExistAsync(component);
					if ( !componentValid )
					{
						// Get specific missing files list
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

				// Check for system-level issues
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

