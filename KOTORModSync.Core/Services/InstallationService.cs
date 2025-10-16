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
using KOTORModSync.Core.Installation;
using KOTORModSync.Core.Utility;
using Python.Included;
using Python.Runtime;

namespace KOTORModSync.Core.Services
{

	public class InstallationService
	{
		private readonly ComponentManagerService _componentManager = new ComponentManagerService();
		private readonly FileOperationService _fileOperation = new FileOperationService();
		private static bool _pythonInitialized = false;
		private static readonly SemaphoreSlim _pythonSemaphore = new SemaphoreSlim(1, 1);

		/// <summary>
		/// Initializes the embedded Python environment if not already initialized.
		/// </summary>
		private static async Task EnsurePythonInitializedAsync()
		{
			if ( _pythonInitialized )
				return;

			await _pythonSemaphore.WaitAsync();
			try
			{
				if ( _pythonInitialized )
					return;

				await Installer.SetupPython();
				PythonEngine.Initialize();

				// Install required Python packages for HoloPatcher
				await Logger.LogVerboseAsync("Installing HoloPatcher Python dependencies...");
				await Installer.TryInstallPip();
				await Installer.PipInstallModule("loggerplus");
				await Installer.PipInstallModule("ply");

				_pythonInitialized = true;
			}
			finally
			{
				_pythonSemaphore.Release();
			}
		}

		/// <summary>
		/// Locates holopatcher, checking for platform-specific executable first, then Python source.
		/// </summary>
		/// <param name="resourcesDir">Resources directory path</param>
		/// <param name="baseDir">Base directory path</param>
		/// <returns>Tuple of (holopatcherPath, usePythonVersion, found)</returns>
		public static async Task<(string holopatcherPath, bool usePythonVersion, bool found)> FindHolopatcherAsync(
			string resourcesDir,
			string baseDir)
		{
			FileSystemInfo patcherCliPath = null;

			// Try to find platform-specific holopatcher executable first
			if ( Utility.Utility.GetOperatingSystem() == OSPlatform.Windows )
			{
				patcherCliPath = new FileInfo(Path.Combine(resourcesDir, "holopatcher.exe"));
			}
			else
			{
				string[] possibleOsxPaths = {
					Path.Combine(resourcesDir, "HoloPatcher.app", "Contents", "MacOS", "holopatcher"),
					Path.Combine(resourcesDir, "holopatcher"),
					Path.Combine(baseDir, "Resources", "HoloPatcher.app", "Contents", "MacOS", "holopatcher"),
					Path.Combine(baseDir, "Resources", "holopatcher")
				};
				OSPlatform thisOperatingSystem = Utility.Utility.GetOperatingSystem();
				foreach ( string path in possibleOsxPaths )
				{
					patcherCliPath = thisOperatingSystem == OSPlatform.OSX && path.ToLowerInvariant().EndsWith(".app")
						? PathHelper.GetCaseSensitivePath(new DirectoryInfo(path))
						: (FileSystemInfo)PathHelper.GetCaseSensitivePath(new FileInfo(path));
					if ( patcherCliPath.Exists )
					{
						await Logger.LogVerboseAsync($"Found holopatcher executable at '{patcherCliPath.FullName}'...");
						break;
					}
					await Logger.LogVerboseAsync($"Holopatcher executable not found at '{patcherCliPath.FullName}'...");
				}
			}

			// If executable found, return it
			if ( patcherCliPath != null && patcherCliPath.Exists )
			{
				return (patcherCliPath.FullName, false, true);
			}

			// Fall back to Python version
			await Logger.LogVerboseAsync("Platform-specific holopatcher not found, using embedded Python version...");
			string holopatcherPyPath = Path.Combine(resourcesDir, "PyKotor", "Tools", "HoloPatcher", "src", "holopatcher");

			if ( Directory.Exists(holopatcherPyPath) )
			{
				await Logger.LogVerboseAsync($"Found holopatcher Python source at '{holopatcherPyPath}'");
				return (holopatcherPyPath, true, true);
			}

			// Not found anywhere
			return (null, false, false);
		}

		/// <summary>
		/// Runs holopatcher directly using Python.NET with the embedded Python interpreter.
		/// </summary>
		/// <param name="holopatcherPath">Path to the holopatcher Python source directory</param>
		/// <param name="args">Arguments to pass to holopatcher</param>
		/// <returns>Tuple of (exit code, stdout, stderr)</returns>
		public static async Task<(int exitCode, string stdout, string stderr)> RunHolopatcherPyAsync(string holopatcherPath, string args)
		{
			await EnsurePythonInitializedAsync();

			return await Task.Run(() =>
			{
				using ( Py.GIL() )
				{
					try
					{
						// Get the PyKotor root directory from holopatcher path
						// Path is: Resources/PyKotor/Tools/HoloPatcher/src/holopatcher
						string pyKotorRoot = Path.GetFullPath(Path.Combine(holopatcherPath, "..", "..", "..", "..", ".."));
						string holopatcherSrc = Path.GetDirectoryName(holopatcherPath);
						string pyKotorLibSrc = Path.Combine(pyKotorRoot, "Libraries", "PyKotor", "src");
						string utilityLibSrc = Path.Combine(pyKotorRoot, "Libraries", "Utility", "src");

						// Add all necessary paths to Python's sys.path
						dynamic sys = Py.Import("sys");
						sys.path.insert(0, holopatcherSrc);
						sys.path.insert(0, pyKotorLibSrc);
						sys.path.insert(0, utilityLibSrc);

						// Parse the arguments
						dynamic sysArgv = new PyList();
						sysArgv.append("holopatcher");
						foreach ( string arg in args.Split(' ', StringSplitOptions.RemoveEmptyEntries) )
						{
							sysArgv.append(arg.Trim('"'));
						}
						sys.argv = sysArgv;

						// Import and run holopatcher's main function
						dynamic holopatcher = Py.Import("holopatcher.__main__");
						holopatcher.main();

						return (0, "", "");
					}
					catch ( PythonException ex )
					{
						return (1, "", ex.Message);
					}
					catch ( Exception ex )
					{
						return (1, "", ex.Message);
					}
				}
			});
		}


		public static async Task<(bool success, string informationMessage)> ValidateInstallationEnvironmentAsync(
			[NotNull] MainConfig mainConfig,
			[CanBeNull] Func<string, Task<bool?>> confirmationCallback = null)
		{
			if ( mainConfig == null )
				throw new ArgumentNullException(nameof(mainConfig));

			try
			{
				if ( MainConfig.DestinationPath is null || MainConfig.SourcePath is null )
					return (false, "Please set your directories first");

				bool holopatcherIsExecutable = true;
				bool holopatcherTestExecute = false;
				string baseDir = Utility.Utility.GetBaseDirectory();
				string resourcesDir = Utility.Utility.GetResourcesDirectory(baseDir);

				// Use helper method to find holopatcher
				(string holopatcherPath, bool usePythonVersion, bool found) = await FindHolopatcherAsync(resourcesDir, baseDir);

				if ( !found )
				{
					return (false,
						"HoloPatcher could not be found in the Resources directory. Please ensure your AV isn't quarantining it and the files exist.");
				}

				if ( usePythonVersion )
				{
					// Initialize Python environment and test holopatcher
					await Logger.LogVerboseAsync("Initializing embedded Python environment...");
					try
					{
						await EnsurePythonInitializedAsync();
					}
					catch ( Exception e )
					{
						await Logger.LogExceptionAsync(e);
						holopatcherIsExecutable = false;
					}

					// Test holopatcher execution with Python
					(int, string, string) result = await RunHolopatcherPyAsync(holopatcherPath, "--install");
					if ( result.Item1 == 2 )
						holopatcherTestExecute = true;
				}
				else
				{
					// Use platform-specific executable
					await Logger.LogVerboseAsync("Ensuring the holopatcher binary has executable permissions...");
					try
					{
						await PlatformAgnosticMethods.MakeExecutableAsync(new FileInfo(holopatcherPath));
					}
					catch ( Exception e )
					{
						await Logger.LogExceptionAsync(e);
						holopatcherIsExecutable = false;
					}

					// Test holopatcher execution with executable
					(int, string, string) result = await PlatformAgnosticMethods.ExecuteProcessAsync(
						holopatcherPath,
						args: "--install"
					);
					if ( result.Item1 == 2 )
						holopatcherTestExecute = true;
				}

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
				bool noDuplicateComponents = await ComponentManagerService.FindDuplicateComponentsAsync(MainConfig.AllComponents);

				await Logger.LogAsync("Ensuring both the mod directory and the install directory are writable...");
				bool isInstallDirectoryWritable = Utility.Utility.IsDirectoryWritable(MainConfig.DestinationPath);
				bool isModDirectoryWritable = Utility.Utility.IsDirectoryWritable(MainConfig.SourcePath);
				if ( !isInstallDirectoryWritable )
				{
					if ( confirmationCallback != null && await confirmationCallback("The Install directory is not writable! Would you like to attempt to gain access now?") == true )
					{
						await FilePermissionHelper.FixPermissionsAsync(MainConfig.DestinationPath);
						isInstallDirectoryWritable = Utility.Utility.IsDirectoryWritable(MainConfig.DestinationPath);
					}
				}
				if ( !isModDirectoryWritable )
				{
					if ( confirmationCallback != null && await confirmationCallback("Your mod directory is not writable! Would you like to attempt to gain access now?") == true )
					{
						await FilePermissionHelper.FixPermissionsAsync(MainConfig.SourcePath);
						isModDirectoryWritable = Utility.Utility.IsDirectoryWritable(MainConfig.SourcePath);
					}
				}

				await Logger.LogAsync("Validating individual components, this might take a while...");
				bool individuallyValidated = true;
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
					individuallyValidated &= validator.Run();
				}

				await Logger.LogVerboseAsync("Finished validating all components.");

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

				if ( fileSystemInfos.Count != 0 )
				{
					informationMessage =
						"You have duplicate files/folders in your installation directory in a case-insensitive environment."
						+ "Please resolve these before continuing. Check the output window for the specific files to resolve.";
					await Logger.LogErrorAsync(informationMessage);
				}

				return !informationMessage.Equals(string.Empty)
					? ((bool success, string informationMessage))(false, informationMessage)
					: ((bool success, string informationMessage))(true,
						"No issues found. If you encounter any problems during the installation, please submit a bug report.");
			}
			catch ( Exception e )
			{
				await Logger.LogExceptionAsync(e);
				return (false, "Unknown error, check the output window for more information.");
			}
		}



		public static async Task<ModComponent.InstallExitCode> InstallSingleComponentAsync(
			[NotNull] ModComponent component,
			[NotNull][ItemNotNull] List<ModComponent> allComponents,
			CancellationToken cancellationToken = default)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( allComponents == null )
				throw new ArgumentNullException(nameof(allComponents));

			var validator = new ComponentValidation(component, allComponents);
			await Logger.LogVerboseAsync($" == Validating '{component.Name}' == ");
			if ( !validator.Run() )
				return ModComponent.InstallExitCode.InvalidOperation;

			return await component.InstallAsync(allComponents, cancellationToken);
		}



		public static async Task<ModComponent.InstallExitCode> InstallAllSelectedComponentsAsync(
			[NotNull][ItemNotNull] List<ModComponent> allComponents,
			[CanBeNull] Action<int, int, string> progressCallback = null,
			CancellationToken cancellationToken = default)
		{
			if ( allComponents == null )
				throw new ArgumentNullException(nameof(allComponents));

			var coordinator = new InstallCoordinator();
			DirectoryInfo destination = MainConfig.DestinationPath
										?? throw new InvalidOperationException("DestinationPath must be set before installing.");
			ResumeResult resume = await coordinator.InitializeAsync(allComponents, destination, cancellationToken);
			var orderedComponents = resume.OrderedComponents.Where(component => component.IsSelected).ToList();
			int total = orderedComponents.Count;
			ModComponent.InstallExitCode exitCode = ModComponent.InstallExitCode.Success;

			for ( int index = 0; index < orderedComponents.Count; index++ )
			{
				cancellationToken.ThrowIfCancellationRequested();
				ModComponent component = orderedComponents[index];

				progressCallback?.Invoke(index, total, component.Name);

				switch ( component.InstallState )
				{
					case ModComponent.ComponentInstallState.Completed:
						await Logger.LogAsync($"Skipping '{component.Name}' (already completed).");
						coordinator.SessionManager.UpdateComponentState(component);
						await coordinator.SessionManager.SaveAsync();
						continue;
					case ModComponent.ComponentInstallState.Skipped:
					case ModComponent.ComponentInstallState.Blocked:
						await Logger.LogAsync($"Skipping '{component.Name}' (blocked by dependency).");
						coordinator.SessionManager.UpdateComponentState(component);
						await coordinator.SessionManager.SaveAsync();
						continue;
				}

				await Logger.LogAsync($"Start install of '{component.Name}'...");
				exitCode = await component.InstallAsync(allComponents, cancellationToken);
				coordinator.SessionManager.UpdateComponentState(component);
				await coordinator.SessionManager.SaveAsync();

				if ( exitCode == ModComponent.InstallExitCode.Success )
				{
					await Logger.LogAsync($"Install of '{component.Name}' succeeded.");
					await coordinator.BackupManager.PromoteSnapshotAsync(destination, cancellationToken);
				}
				else
				{
					await Logger.LogErrorAsync($"Install of '{component.Name}' failed with exit code {exitCode}");
					InstallCoordinator.MarkBlockedDescendants(orderedComponents, component.Guid);
					foreach ( ModComponent blocked in orderedComponents.Where(c => c.InstallState == ModComponent.ComponentInstallState.Blocked) )
					{
						coordinator.SessionManager.UpdateComponentState(blocked);
					}
					await coordinator.SessionManager.SaveAsync();
					break;
				}
			}

			return exitCode;
		}

	}
}
