// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

using static KOTORModSync.Core.ModComponent;

namespace KOTORModSync.Services
{

	public class ValidationService
	{
		private readonly MainConfig _mainConfig;

		public ValidationService( MainConfig mainConfig )
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException( nameof( mainConfig ) );
		}

		public bool IsComponentValidForInstallation( ModComponent component, bool editorMode )
		{
			if (component == null)
				return false;

			if (string.IsNullOrWhiteSpace( component.Name ))
				return false;

			if (component.Dependencies.Count > 0)
			{
				List<ModComponent> dependencyComponents = ModComponent.FindComponentsFromGuidList(
					component.Dependencies,
					_mainConfig.allComponents
				);
				foreach (ModComponent dep in dependencyComponents)
				{
					if (dep == null || dep.IsSelected)
						continue;
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
					if (restriction == null || !restriction.IsSelected)
						continue;
					return false;
				}
			}

			if (component.Instructions.Count == 0)
				return false;

			return !editorMode || Core.Services.ComponentValidationService.AreModLinksValid( component.ModLinkFilenames?.Keys.ToList() );
		}

		public (string ErrorType, string Description, bool CanAutoFix) GetComponentErrorDetails( ModComponent component )
		{
			var errorReasons = new List<string>();

			if (string.IsNullOrWhiteSpace( component.Name ))
				errorReasons.Add( "Missing mod name" );

			if (component.Dependencies.Count > 0)
			{
				List<ModComponent> dependencyComponents = ModComponent.FindComponentsFromGuidList(
					component.Dependencies,
					_mainConfig.allComponents
				);
				var missingDeps = dependencyComponents.Where( dep => dep == null || !dep.IsSelected ).ToList();
				if (missingDeps.Count > 0)
					errorReasons.Add( $"Missing required dependencies ({missingDeps.Count})" );
			}

			if (component.Restrictions.Count > 0)
			{
				List<ModComponent> restrictionComponents = ModComponent.FindComponentsFromGuidList(
					component.Restrictions,
					_mainConfig.allComponents
				);
				var conflictingMods = restrictionComponents.Where( restriction => restriction != null && restriction.IsSelected ).ToList();
				if (conflictingMods.Count > 0)
					errorReasons.Add( $"Conflicting mods selected ({conflictingMods.Count})" );
			}

			if (component.Instructions.Count == 0)
				errorReasons.Add( "No installation instructions" );

			var urls = component.ModLinkFilenames?.Keys.ToList();
			if (!Core.Services.ComponentValidationService.AreModLinksValid( urls ))
			{
				List<string> invalidUrls = urls?.Where( link => !string.IsNullOrWhiteSpace( link ) && !Core.Services.ComponentValidationService.IsValidUrl( link ) ).ToList() ?? new List<string>();
				if (invalidUrls.Count > 0)
					errorReasons.Add( $"Invalid download URLs ({invalidUrls.Count})" );
				else
					errorReasons.Add( "Invalid download URLs" );
			}

			if (errorReasons.Count == 0)
				return (InstallExitCode.UnknownError.ToString(), "No specific error details available", false);

			string primaryError = errorReasons[0];
			string description = string.Join( ", ", errorReasons );

			bool canAutoFix = primaryError.ToLower().Contains( "missing required dependencies" ) ||
							  primaryError.ToLower().Contains( "conflicting mods selected" );

			return (primaryError, description, canAutoFix);
		}

		public static bool IsStep1Complete()
		{
			try
			{

				if (string.IsNullOrEmpty( MainConfig.SourcePath?.FullName ) ||
					string.IsNullOrEmpty( MainConfig.DestinationPath?.FullName ))
				{
					return false;
				}

				if (!Directory.Exists( MainConfig.SourcePath.FullName ) ||
					!Directory.Exists( MainConfig.DestinationPath.FullName ))
				{
					return false;
				}

				string kotorDir = MainConfig.DestinationPath.FullName;
				bool hasGameFiles = File.Exists( Path.Combine( kotorDir, "swkotor.exe" ) ) ||
								   File.Exists( Path.Combine( kotorDir, "swkotor2.exe" ) ) ||
								   Directory.Exists( Path.Combine( kotorDir, "data" ) ) ||
								   File.Exists( Path.Combine( kotorDir, "Knights of the Old Republic.app" ) ) ||
								   File.Exists( Path.Combine( kotorDir, "Knights of the Old Republic II.app" ) );

				return hasGameFiles;
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error checking Step 1 completion" );
				return false;
			}
		}

		public async Task AnalyzeValidationFailures( List<Dialogs.ValidationIssue> modIssues, List<string> systemIssues )
		{
			try
			{

				if (MainConfig.DestinationPath == null || MainConfig.SourcePath == null)
				{
					systemIssues.Add( "⚙️ Directories not configured\n" +
									"Both Mod Directory and KOTOR Install Directory must be set.\n" +
									"Solution: Click Settings and configure both directories." );
					return;
				}

				if (!_mainConfig.allComponents.Any())
				{
					systemIssues.Add( "📋 No mods loaded\n" +
									"No mod configuration file has been loaded.\n" +
									"Solution: Click 'File > Open File' to load a mod list." );
					return;
				}

				if (!_mainConfig.allComponents.Any( c => c.IsSelected ))
				{
					systemIssues.Add( "☑️ No mods selected\n" +
									"At least one mod must be selected for installation.\n" +
									"Solution: Check the boxes next to mods you want to install." );
					return;
				}

				foreach (ModComponent component in _mainConfig.allComponents.Where( c => c.IsSelected ))
				{

					if (!component.IsDownloaded)
					{
						var issue = new Dialogs.ValidationIssue
						{
							Icon = "📥",
							ModName = component.Name,
							IssueType = "Missing Download",
							Description = "The mod archive file is not in your Mod Directory.",
							Solution = component.ModLinkFilenames != null && component.ModLinkFilenames.Count > 0
						? $"Solution: Click 'Fetch Downloads' or manually download from: {component.ModLinkFilenames.Keys.First()}"
						: "Solution: Click 'Fetch Downloads' or manually download and place in Mod Directory."
						};
						modIssues.Add( issue );
						continue;
					}

					if (component.Instructions.Count == 0 && component.Options.Count == 0)
					{
						var issue = new Dialogs.ValidationIssue
						{
							Icon = "❌",
							ModName = component.Name,
							IssueType = "Missing Instructions",
							Description = "This mod has no installation instructions defined.",
							Solution = "Solution: Contact the mod list creator or disable this mod."
						};
						modIssues.Add( issue );
					}

					bool componentValid = await Core.Services.ComponentValidationService.ValidateComponentFilesExistAsync( component ).ConfigureAwait( false );
					if (!componentValid)


					{

						List<string> missingFiles = await Core.Services.ComponentValidationService.GetMissingFilesForComponentAsync( component ).ConfigureAwait( false );
						string missingFilesDescription = missingFiles.Count > 0
							? $"Missing file(s): {string.Join( ", ", missingFiles )}"
							: "One or more required files for this mod are missing from your Mod Directory.";

						var issue = new Dialogs.ValidationIssue
						{
							Icon = "🔧",
							ModName = component.Name,
							IssueType = "Missing Files",
							Description = missingFilesDescription,
							Solution = "Solution: Click 'Fetch Downloads' to download missing files or check the Output Window for details."
						};
						modIssues.Add( issue );
					}
				}

				if (!UtilityHelper.IsDirectoryWritable( MainConfig.DestinationPath ))
				{
					systemIssues.Add( "🔒 KOTOR Directory Not Writable\n" +
									"The installer cannot write to your KOTOR installation directory.\n" +
									"Solution: Run as Administrator or install to a different location." );
				}

				if (!UtilityHelper.IsDirectoryWritable( MainConfig.SourcePath ))
				{
					systemIssues.Add( "🔒 Mod Directory Not Writable\n" +
									"The installer cannot write to your Mod Directory.\n" +
									"Solution: Ensure you have write permissions." );
				}
			}
			catch (Exception ex)


			{
				await Logger.LogExceptionAsync( ex ).ConfigureAwait( false );
				systemIssues.Add( "❌ Unexpected Error\n" +
								"An error occurred during validation analysis.\n" +
								"Solution: Check the Output Window for details." );
			}
		}
	}
}