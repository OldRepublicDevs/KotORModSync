// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Services.FileSystem;
using KOTORModSync.Core.Services.Validation;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Core.Services
{

	public static class PreinstallValidationService
	{


		public static async Task<(bool success, string informationMessage)> ValidatePreinstallAsync(
			[CanBeNull] Func<string, Task<bool>> onConfirmationRequested = null)
		{
			try
			{
				if ( MainConfig.DestinationPath is null || MainConfig.SourcePath is null )
					return (false, "Please set your directories first");

				bool holopatcherIsExecutable = true;
				bool holopatcherTestExecute = true;
				string baseDir = Utility.Utility.GetBaseDirectory();
				string resourcesDir = Utility.Utility.GetResourcesDirectory(baseDir);
				FileSystemInfo patcherCliPath = null;

				if ( Utility.Utility.GetOperatingSystem() == OSPlatform.Windows )
				{
					patcherCliPath = new FileInfo(Path.Combine(resourcesDir, path2: "holopatcher.exe"));
				}
				else
				{

					string[] possibleOSXPaths =
					{
						Path.Combine(resourcesDir, "HoloPatcher.app", "Contents", "MacOS", "holopatcher"),
						Path.Combine(resourcesDir, path2: "holopatcher"),
						Path.Combine(
							baseDir,
							"Resources",
							"HoloPatcher.app",
							"Contents",
							"MacOS",
							"holopatcher"
						),
						Path.Combine(baseDir, path2: "Resources", path3: "holopatcher"),
					};

					OSPlatform thisOperatingSystem = Utility.Utility.GetOperatingSystem();
					foreach ( string path in possibleOSXPaths )
					{
						patcherCliPath = thisOperatingSystem == OSPlatform.OSX && path.ToLowerInvariant().EndsWith(".app")
							? PathHelper.GetCaseSensitivePath(new DirectoryInfo(path))
							: (FileSystemInfo)PathHelper.GetCaseSensitivePath(new FileInfo(path));

						if ( patcherCliPath.Exists )
						{
							await Logger.LogVerboseAsync($"Found holopatcher at '{patcherCliPath.FullName}'...");
							break;
						}

						await Logger.LogVerboseAsync($"Holopatcher not found at '{patcherCliPath.FullName}'...");
					}
				}

				if ( patcherCliPath is null || !patcherCliPath.Exists )
				{
					return (false,
						"HoloPatcher could not be found in the Resources directory. Please ensure your AV isn't quarantining it and the file exists.");
				}

				await Logger.LogVerboseAsync("Ensuring the holopatcher binary has executable permissions...");
				try
				{
					await PlatformAgnosticMethods.MakeExecutableAsync(patcherCliPath);
				}
				catch ( Exception e )
				{
					await Logger.LogExceptionAsync(e);
					holopatcherIsExecutable = false;
				}

				(int, string, string) result = await PlatformAgnosticMethods.ExecuteProcessAsync(
					patcherCliPath.FullName,
					args: "--install"
				);
				if ( result.Item1 == 2 )
					holopatcherTestExecute = true;

				if ( MainConfig.AllComponents.IsNullOrEmptyCollection() )
					return (false, "No instructions loaded! Press 'Load Instructions File' or create some instructions first.");
				if ( !MainConfig.AllComponents.Any(component => component.IsSelected) )
					return (false, "Select at least one mod in the left list to be installed first.");

				await Logger.LogAsync("Finding duplicate case-insensitive folders/files in the install destination...");
				IEnumerable<FileSystemInfo> duplicates = PathHelper.FindCaseInsensitiveDuplicates(MainConfig.DestinationPath.FullName);
				var fileSystemInfos = duplicates.ToList();
				foreach ( FileSystemInfo duplicate in fileSystemInfos )
				{
					await Logger.LogErrorAsync(duplicate?.FullName + " has a duplicate, please resolve before attempting an install.");
				}

				await Logger.LogAsync("Checking for duplicate components...");
				bool noDuplicateComponents = await FindDuplicateComponents(MainConfig.AllComponents);

				await Logger.LogAsync("Ensuring both the mod directory and the install directory are writable...");
				bool isInstallDirectoryWritable = Utility.Utility.IsDirectoryWritable(MainConfig.DestinationPath);
				bool isModDirectoryWritable = Utility.Utility.IsDirectoryWritable(MainConfig.SourcePath);

				if ( !isInstallDirectoryWritable )
				{
					if ( onConfirmationRequested != null &&
						await onConfirmationRequested("The Install directory is not writable! Would you like to attempt to gain access now?") )
					{
						await FilePermissionHelper.FixPermissionsAsync(MainConfig.DestinationPath);
						isInstallDirectoryWritable = Utility.Utility.IsDirectoryWritable(MainConfig.DestinationPath);
					}
				}

				if ( !isModDirectoryWritable )
				{
					if ( onConfirmationRequested != null &&
						await onConfirmationRequested("Your mod directory is not writable! Would you like to attempt to gain access now?") )
					{
						await FilePermissionHelper.FixPermissionsAsync(MainConfig.SourcePath);
						isModDirectoryWritable = Utility.Utility.IsDirectoryWritable(MainConfig.SourcePath);
					}
				}

				await Logger.LogAsync("Validating individual components, this might take a while...");
				bool individuallyValidated = true;
				var failedComponents = new List<ModComponent>();
				foreach ( ModComponent component in MainConfig.AllComponents )
				{
					if ( !component.IsSelected )
						continue;

					if ( component.Restrictions.Count > 0 && component.IsSelected )
					{
						List<ModComponent> restrictedComponentsList = ModComponent.FindComponentsFromGuidList(
							component.Restrictions,
							MainConfig.AllComponents
						);
						foreach ( ModComponent restrictedComponent in restrictedComponentsList )
						{

							if ( restrictedComponent?.IsSelected == true )
							{
								await Logger.LogErrorAsync($"Cannot install '{component.Name}' due to '{restrictedComponent.Name}' being selected for install.");
								individuallyValidated = false;
							}
						}
					}

					if ( component.Dependencies.Count > 0 && component.IsSelected )
					{
						List<ModComponent> dependencyComponentsList = ModComponent.FindComponentsFromGuidList(component.Dependencies, MainConfig.AllComponents);
						foreach ( ModComponent dependencyComponent in dependencyComponentsList )
						{

							if ( dependencyComponent?.IsSelected != true )
							{
								await Logger.LogErrorAsync($"Cannot install '{component.Name}' due to '{dependencyComponent?.Name}' not being selected for install.");
								individuallyValidated = false;
							}
						}
					}

					var validator = new ComponentValidation(component, MainConfig.AllComponents);
					await Logger.LogVerboseAsync($" == Validating '{component.Name}' == ");
					bool thisValid = validator.Run();
					individuallyValidated &= thisValid;
					if ( !thisValid )
					{
						failedComponents.Add(component);
					}
				}

				await Logger.LogVerboseAsync("Finished validating all components.");

				await Logger.LogAsync("Performing dry-run validation of installation order and instructions...");
				DryRunValidationResult dryRunResult = await DryRunValidator.ValidateInstallationAsync(
					MainConfig.AllComponents,
					CancellationToken.None
				);

				bool dryRunPassed = dryRunResult.IsValid;

				string informationMessage = string.Empty;

				if ( !holopatcherIsExecutable )
				{
					informationMessage = "The HoloPatcher binary does not seem to be executable, please see the logs in the output window for more information.";
					await Logger.LogErrorAsync(informationMessage);
				}

				if ( !holopatcherTestExecute )
				{
					informationMessage = "The holopatcher test execution did not pass, this may mean the"
					+ " binary is corrupted or has unresolved dependency problems.";
					await Logger.LogErrorAsync(informationMessage);
				}

				if ( !isInstallDirectoryWritable )
				{
					informationMessage = "The Install directory is not writable!"
						+ " Please ensure administrative privileges or reinstall KOTOR"
						+ " to a directory with write access.";
					await Logger.LogErrorAsync(informationMessage);
				}

				if ( !isModDirectoryWritable )
				{
					informationMessage = "The Mod directory is not writable!"
						+ " Please ensure administrative privileges or choose a new mod directory.";
					await Logger.LogErrorAsync(informationMessage);
				}

				if ( failedComponents.Count > 0 )
				{

					string names = string.Join(", ", failedComponents.Select(c => c.Name));
					informationMessage = $"Some components failed to validate: {names}.\nThey are highlighted in the left list. Check the Output window for exact errors.";
				}

				if ( !noDuplicateComponents )
				{
					informationMessage = "There were several duplicate components found."
						+ " Please ensure all components are unique and none have conflicting GUIDs.";
					await Logger.LogErrorAsync(informationMessage);
				}

				if ( !individuallyValidated )
				{
					informationMessage =
						$"Some components failed to validate. Check the output/console window for details.{Environment.NewLine}If you are seeing this as an end user you most"
						+ " likely need to whitelist KOTORModSync and HoloPatcher in your antivirus, or download the missing mods.";
					await Logger.LogErrorAsync(informationMessage);
				}

				if ( !dryRunPassed )
				{
					await Logger.LogErrorAsync("Dry-run validation failed! Installation would encounter errors.");
					await Logger.LogErrorAsync(dryRunResult.GetSummaryMessage());

					foreach ( ValidationIssue issue in dryRunResult.Issues.Where(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical) )
					{
						await Logger.LogErrorAsync($"[{issue.Category}] {issue.Message}");
						if ( issue.AffectedComponent != null )
						{
							await Logger.LogErrorAsync($"  ModComponent: {issue.AffectedComponent.Name}");
						}
						if ( issue.InstructionIndex > 0 )
						{
							await Logger.LogErrorAsync($"  Instruction: #{issue.InstructionIndex}");
						}
					}

					informationMessage = "Dry-run validation detected issues with the installation instructions.\n"
						+ "The installation would likely fail or produce incorrect results.\n"
						+ "Check the Output window for detailed information about each issue.";
				}
				else if ( dryRunResult.HasWarnings )
				{
					await Logger.LogWarningAsync("Dry-run validation passed with warnings.");

					foreach ( ValidationIssue issue in dryRunResult.Issues.Where(i => i.Severity == ValidationSeverity.Warning) )
					{
						await Logger.LogWarningAsync($"[{issue.Category}] {issue.Message}");
					}
				}

				if ( fileSystemInfos.Count != 0 )
				{
					informationMessage =
						"You have duplicate files/folders in your installation directory in a case-insensitive environment."
						+ "Please resolve these before continuing. Check the output window for the specific files to resolve.";
					await Logger.LogErrorAsync(informationMessage);
				}

				bool hasErrors = !string.IsNullOrEmpty(informationMessage) ||
					!holopatcherIsExecutable ||
					!holopatcherTestExecute ||
					!isInstallDirectoryWritable ||
					!isModDirectoryWritable ||
					!noDuplicateComponents ||
					!individuallyValidated ||
					!dryRunPassed ||
					fileSystemInfos.Count != 0;

				if ( hasErrors )
				{
					return (false, informationMessage);
				}

				string successMessage = "✓ All validation checks passed successfully!";
				if ( dryRunResult.HasWarnings )
				{
					successMessage += $"\n⚠ {dryRunResult.Issues.Count(i => i.Severity == ValidationSeverity.Warning)} warning(s) found - review the Output window.";
				}
				successMessage += "\n\nYou can proceed with the installation. If you encounter any problems, please submit a bug report.";

				return (true, successMessage);
			}
			catch ( Exception e )
			{
				await Logger.LogExceptionAsync(e);
				return (false, "Unknown error, check the output window for more information.");
			}
		}



		private static async Task<bool> FindDuplicateComponents([NotNull][ItemNotNull] List<ModComponent> components)
		{
			if ( components == null )
				throw new ArgumentNullException(nameof(components));

			try
			{

				var guidGroups = components.GroupBy(c => c.Guid).Where(g => g.Count() > 1).ToList();
				foreach ( IGrouping<Guid, ModComponent> group in guidGroups )
				{
					await Logger.LogErrorAsync($"Duplicate GUID found: {group.Key} in components: {string.Join(", ", group.Select(c => c.Name))}");
				}

				var nameGroups = components.GroupBy(c => c.Name.ToLowerInvariant()).Where(g => g.Count() > 1).ToList();
				foreach ( IGrouping<string, ModComponent> group in nameGroups )
				{
					await Logger.LogErrorAsync($"Duplicate component name found: '{group.Key}' in components: {string.Join(", ", group.Select(c => c.Name))}");
				}

				return guidGroups.Count == 0 && nameGroups.Count == 0;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return false;
			}
		}
	}
}
