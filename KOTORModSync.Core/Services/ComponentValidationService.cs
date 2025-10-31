// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Services.FileSystem;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Core validation service for component instruction validation, VFS simulation, and path verification.
	/// Handles dry-run validation and file existence checking without GUI dependencies.
	/// </summary>
	public partial class ComponentValidationService
	{
		private VirtualFileSystemProvider _virtualFileSystem;

		// Cache validation results to avoid redundant VFS operations
		private static readonly Dictionary<string, (List<string> urls, bool simulationFailed, DateTime timestamp)> _validationCache
			= new Dictionary<string, (List<string>, bool, DateTime)>(StringComparer.Ordinal);
		private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
		private static readonly object _cacheLock = new object();

		public ComponentValidationService()
		{
			_virtualFileSystem = new VirtualFileSystemProvider();
		}

		/// <summary>
		/// Clears the validation cache. Call this after file operations that might affect validation results.
		/// </summary>
		public static void ClearValidationCache()
		{
			lock (_cacheLock)
			{
				_validationCache.Clear();
			}
		}

		/// <summary>
		/// Clears the validation cache for a specific component. Call this after modifying that component's instructions.
		/// </summary>
		public static void ClearValidationCacheForComponent(string componentGuid)
		{
			lock (_cacheLock)

			{
				List<string> keysToRemove = _validationCache.Keys.Where(k => k.StartsWith(componentGuid + "_", StringComparison.Ordinal)).ToList();
				foreach (string key in keysToRemove)
				{
					_ = _validationCache.Remove(key);
				}
			}
		}

		/// <summary>
		/// Analyzes whether a component needs files downloaded by simulating installation.
		/// </summary>
		/// <returns>List of URLs that need downloading, and whether simulation failed</returns>
		public async Task<(
			List<string> urlsNeedingDownload,
			bool simulationFailed)>
		AnalyzeDownloadNecessityAsync(
			[NotNull] ModComponent component,
			[NotNull] string modArchiveDirectory,
			CancellationToken cancellationToken = default)
		{
			if (component is null)
				throw new ArgumentNullException(nameof(component));

			// Check cache first to avoid redundant VFS operations
			string cacheKey = $"{component.Guid}_{modArchiveDirectory}_{component.Instructions.Count}";
			lock (_cacheLock)
			{
				if (_validationCache.TryGetValue(cacheKey, out (List<string> urls, bool simulationFailed, DateTime timestamp) cachedResult))
				{
					if (DateTime.UtcNow - cachedResult.timestamp < _cacheExpiration)
					{
						Logger.LogVerbose($"[ComponentValidationService] Using cached validation result for component: {component.Name}");
						return (cachedResult.urls, cachedResult.simulationFailed);
					}
					else
					{
						// Cache expired, remove it
						_ = _validationCache.Remove(cacheKey);
					}
				}
			}

			await Logger.LogVerboseAsync($"[ComponentValidationService] Analyzing download necessity for component: {component.Name}").ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[ComponentValidationService] Mod archive directory: {modArchiveDirectory}").ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[ComponentValidationService] MainConfig.SourcePath: {MainConfig.SourcePath?.FullName ?? "NULL"}").ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[ComponentValidationService] MainConfig.DestinationPath: {MainConfig.DestinationPath?.FullName ?? "NULL"}").ConfigureAwait(false);

			// Initialize VFS with existing files - optimized for single component
			_virtualFileSystem = new VirtualFileSystemProvider();
			await _virtualFileSystem.InitializeFromRealFileSystemForComponentAsync(modArchiveDirectory, component).ConfigureAwait(false);

			// Track if we need to try fallback
			bool shouldTryFallback = false;
			List<string> vfsFiles = _virtualFileSystem.GetFilesInDirectory(modArchiveDirectory, "*", SearchOption.AllDirectories);
			await Logger.LogVerboseAsync($"[ComponentValidationService] VFS has {vfsFiles.Count} files loaded from: {modArchiveDirectory}").ConfigureAwait(false);

			HashSet<string> urlsNeedingDownload = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			bool initialSimulationFailed = false;

			await Logger.LogVerboseAsync("[ComponentValidationService] Simulating component installation to determine file availability...").ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[ComponentValidationService] Component has {component.Instructions.Count} instructions").ConfigureAwait(false);

			try
			{
				await Logger.LogVerboseAsync("[ComponentValidationService] Starting ExecuteInstructionsAsync...").ConfigureAwait(false);
				ModComponent.InstallExitCode exitCode = await component.ExecuteInstructionsAsync(
					component.Instructions,
					new List<ModComponent>(),
					cancellationToken,
					_virtualFileSystem,
					skipDependencyCheck: true
				).ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[ComponentValidationService] ExecuteInstructionsAsync completed with exit code: {exitCode}").ConfigureAwait(false);

				List<ValidationIssue> issues = _virtualFileSystem.GetValidationIssues();
				List<ValidationIssue> missingFileIssues = issues.Where(i =>
					(i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical) &&
					(string.Equals(i.Category, "ExtractArchive", StringComparison.Ordinal) || string.Equals(i.Category, "MoveFile", StringComparison.Ordinal) || string.Equals(i.Category, "CopyFile", StringComparison.Ordinal) || i.Message.Contains("does not exist"))
				).ToList();

				if (missingFileIssues.Count > 0)
				{
					await Logger.LogWarningAsync($"[ComponentValidationService] Simulation found {missingFileIssues.Count} missing file issue(s)").ConfigureAwait(false);

					foreach (ValidationIssue issue in missingFileIssues)
					{
						await Logger.LogVerboseAsync($"[ComponentValidationService]   • Category: {issue.Category}, Message: {issue.Message}").ConfigureAwait(false);
					}

					urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
					await Logger.LogWarningAsync($"[ComponentValidationService]   → Marking {component.ModLinkFilenames.Keys.Count} URL(s) for download").ConfigureAwait(false);
					initialSimulationFailed = true;
				}
				else if (exitCode != ModComponent.InstallExitCode.Success)
				{
					await Logger.LogWarningAsync($"[ComponentValidationService] Simulation completed with exit code: {exitCode}").ConfigureAwait(false);
					urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
					initialSimulationFailed = true;
				}
				else
				{
					await Logger.LogVerboseAsync($"[ComponentValidationService] ✓ Simulation completed successfully, all required files exist").ConfigureAwait(false);
				}
			}
			catch (Exceptions.WildcardPatternNotFoundException wildcardEx)
			{
				await Logger.LogVerboseAsync($"[ComponentValidationService] Wildcard pattern matching failed, attempting auto-fix...").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[ComponentValidationService] Failed patterns: {string.Join(", ", wildcardEx.Patterns)}").ConfigureAwait(false);
				initialSimulationFailed = true;

				int fixCount = Utility.PathFixer.FixDuplicateFolderPathsInComponent(component);

				if (fixCount > 0)
				{
					await Logger.LogAsync($"[ComponentValidationService] Auto-fixed {fixCount} duplicate folder path(s), retrying simulation...").ConfigureAwait(false);

					_virtualFileSystem = new VirtualFileSystemProvider();
					await _virtualFileSystem.InitializeFromRealFileSystemForComponentAsync(modArchiveDirectory, component).ConfigureAwait(false);

					try
					{
						ModComponent.InstallExitCode retryExitCode = await component.ExecuteInstructionsAsync(
							component.Instructions,
							new List<ModComponent>(),
							cancellationToken,
							_virtualFileSystem,
							skipDependencyCheck: true
						).ConfigureAwait(false);

						List<ValidationIssue> retryIssues = _virtualFileSystem.GetValidationIssues();
						List<ValidationIssue> retryMissingFileIssues = retryIssues.Where(i =>
							(i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical) &&
							(string.Equals(i.Category, "ExtractArchive", StringComparison.Ordinal) || string.Equals(i.Category, "MoveFile", StringComparison.Ordinal) || string.Equals(i.Category, "CopyFile", StringComparison.Ordinal) || i.Message.Contains("does not exist"))
						).ToList();

						if (retryMissingFileIssues.Count > 0)
						{
							await Logger.LogWarningAsync($"[ComponentValidationService] Retry simulation still found {retryMissingFileIssues.Count} missing file issue(s) after auto-fix").ConfigureAwait(false);
							urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
						}
						else if (retryExitCode != ModComponent.InstallExitCode.Success)
						{
							await Logger.LogWarningAsync($"[ComponentValidationService] Retry simulation completed with exit code: {retryExitCode}").ConfigureAwait(false);
							urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
						}
						else
						{
							await Logger.LogAsync($"[ComponentValidationService] ✓ First retry simulation successful after auto-fixing duplicate folder paths").ConfigureAwait(false);
							initialSimulationFailed = false;
							urlsNeedingDownload.Clear();
						}
					}
					catch (Exceptions.WildcardPatternNotFoundException wildcardRetryEx)
					{
						await Logger.LogExceptionAsync(wildcardRetryEx, $"[ComponentValidationService] Retry simulation failed with wildcard pattern error. Failed patterns: {string.Join(", ", wildcardRetryEx.Patterns)}").ConfigureAwait(false);
						await Logger.LogVerboseAsync($"[ComponentValidationService] Attempting to fix nested archive folder patterns in instructions...").ConfigureAwait(false);

						int instructionFixCount = FixNestedArchiveFolderInstructions(component, _virtualFileSystem);

						if (instructionFixCount > 0)
						{
							await Logger.LogAsync($"[ComponentValidationService] Fixed {instructionFixCount} instruction(s) with nested folder patterns, retrying simulation again...").ConfigureAwait(false);

							// Reinitialize VFS and retry
							_virtualFileSystem = new VirtualFileSystemProvider();
							await _virtualFileSystem.InitializeFromRealFileSystemForComponentAsync(modArchiveDirectory, component).ConfigureAwait(false);

							try
							{
								ModComponent.InstallExitCode secondRetryExitCode = await component.ExecuteInstructionsAsync(
									component.Instructions,
									new List<ModComponent>(),
									cancellationToken,
									_virtualFileSystem,
									skipDependencyCheck: true
								).ConfigureAwait(false);

								List<ValidationIssue> secondRetryIssues = _virtualFileSystem.GetValidationIssues();
								List<ValidationIssue> secondRetryMissingFileIssues = secondRetryIssues.Where(i =>
									(i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical) &&
									(string.Equals(i.Category, "ExtractArchive", StringComparison.Ordinal) || string.Equals(i.Category, "MoveFile", StringComparison.Ordinal) || string.Equals(i.Category, "CopyFile", StringComparison.Ordinal) || i.Message.Contains("does not exist"))
								).ToList();

								if (secondRetryMissingFileIssues.Count > 0)
								{
									await Logger.LogWarningAsync($"[ComponentValidationService] Second retry simulation still found {secondRetryMissingFileIssues.Count} missing file issue(s) after nested folder fix").ConfigureAwait(false);
									urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
								}
								else if (secondRetryExitCode != ModComponent.InstallExitCode.Success)
								{
									await Logger.LogWarningAsync($"[ComponentValidationService] Second retry simulation completed with exit code: {secondRetryExitCode}").ConfigureAwait(false);
									urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
								}
								else
								{
									await Logger.LogAsync($"[ComponentValidationService] ✓ Second retry simulation successful after nested folder fix").ConfigureAwait(false);
									initialSimulationFailed = false;
									urlsNeedingDownload.Clear();
								}
							}
							catch (Exception secondRetryEx)
							{
								await Logger.LogExceptionAsync(secondRetryEx, $"[ComponentValidationService] Second retry simulation failed after nested folder fix").ConfigureAwait(false);
								urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
							}
						}
						else
						{
							await Logger.LogWarningAsync("[ComponentValidationService] Could not auto-fix wildcard pattern errors - files may be missing. Marking URLs for download.").ConfigureAwait(false);
							await Logger.LogVerboseAsync($"[ComponentValidationService] Failed patterns: {string.Join(", ", wildcardRetryEx.Patterns)}").ConfigureAwait(false);
							urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
						}
					}
					catch (Exception retryEx)
					{
						await Logger.LogExceptionAsync(retryEx, "[ComponentValidationService] Retry simulation failed after auto-fix").ConfigureAwait(false);
						urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
					}
				}
				else
				{
					await Logger.LogWarningAsync("[ComponentValidationService] Could not auto-fix wildcard pattern errors - files may be missing. Marking URLs for download.").ConfigureAwait(false);
					await Logger.LogVerboseAsync($"[ComponentValidationService] Failed patterns: {string.Join(", ", wildcardEx.Patterns)}").ConfigureAwait(false);
					urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
				}
			}
			catch (FileNotFoundException fnfEx)
			{
				string urls = string.Join($",{Environment.NewLine}", component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
				await Logger.LogVerboseAsync("[ComponentValidationService] Caught FileNotFoundException during simulation").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[ComponentValidationService] Exception Type: {fnfEx.GetType().FullName}").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[ComponentValidationService] Exception Message: {fnfEx.Message}").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[ComponentValidationService] FileName property: {fnfEx.FileName ?? "NULL"}").ConfigureAwait(false);
				await Logger.LogExceptionAsync(fnfEx, $"[ComponentValidationService] Simulation failed. URLs: {urls}").ConfigureAwait(false);
				urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
				initialSimulationFailed = true;
			}
			catch (Exception ex)
			{
				string urls = string.Join($",{Environment.NewLine}", component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
				await Logger.LogVerboseAsync("[ComponentValidationService] Caught Exception during simulation").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[ComponentValidationService] Exception Type: {ex.GetType().FullName}").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[ComponentValidationService] Exception Message: {ex.Message}").ConfigureAwait(false);
				if (ex.InnerException != null)
				{
					await Logger.LogVerboseAsync($"[ComponentValidationService] Inner Exception Type: {ex.InnerException.GetType().FullName}").ConfigureAwait(false);
					await Logger.LogVerboseAsync($"[ComponentValidationService] Inner Exception Message: {ex.InnerException.Message}").ConfigureAwait(false);
				}
				await Logger.LogExceptionAsync(ex, $"[ComponentValidationService] Simulation failed. URLs: {urls}").ConfigureAwait(false);
				urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
				initialSimulationFailed = true;
			}

			// After the existing fix attempts, add archive name mismatch fix if still failing
			// Only run if previous attempts marked files for download (meaning they failed)
			if (initialSimulationFailed && urlsNeedingDownload.Count > 0)
			{
				// Reinitialize VFS for third attempt
				_virtualFileSystem = new VirtualFileSystemProvider();
				await _virtualFileSystem.InitializeFromRealFileSystemForComponentAsync(modArchiveDirectory, component).ConfigureAwait(false);

				try
				{
					ModComponent.InstallExitCode thirdRetryExitCode = await component.ExecuteInstructionsAsync(
						component.Instructions,
						new List<ModComponent>(),
						cancellationToken,
						_virtualFileSystem,
						skipDependencyCheck: true
					).ConfigureAwait(false);

					if (thirdRetryExitCode == ModComponent.InstallExitCode.Success)
					{
						await Logger.LogAsync("[ComponentValidationService] ✓ Third simulation successful (archive name mismatch scenario resolved)").ConfigureAwait(false);
						initialSimulationFailed = false;
						urlsNeedingDownload.Clear();
						return (new List<string>(), false);
					}
				}
				catch (Exceptions.WildcardPatternNotFoundException thirdEx)
				{
					await Logger.LogVerboseAsync("[ComponentValidationService] Third simulation failed, attempting archive name mismatch fix...").ConfigureAwait(false);

					// Attempt to fix archive name mismatches
					bool fixApplied = await TryFixArchiveNameMismatchesAsync(component, thirdEx.Patterns, _virtualFileSystem, modArchiveDirectory, cancellationToken).ConfigureAwait(false);

					if (fixApplied)
					{
						// Reinitialize VFS and try final simulation
						_virtualFileSystem = new VirtualFileSystemProvider();
						await _virtualFileSystem.InitializeFromRealFileSystemForComponentAsync(modArchiveDirectory, component).ConfigureAwait(false);

						try
						{
							ModComponent.InstallExitCode finalExitCode = await component.ExecuteInstructionsAsync(
								component.Instructions,
								new List<ModComponent>(),
								cancellationToken,
								_virtualFileSystem,
								skipDependencyCheck: true
							).ConfigureAwait(false);

							if (finalExitCode == ModComponent.InstallExitCode.Success)
							{
								await Logger.LogAsync("[ComponentValidationService] ✓ Final simulation successful after archive name fix").ConfigureAwait(false);
								initialSimulationFailed = false;
								urlsNeedingDownload.Clear();
								return (new List<string>(), false);
							}
							else
							{
								await Logger.LogWarningAsync("[ComponentValidationService] Final simulation failed after fix attempt").ConfigureAwait(false);
								urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
							}
						}
						catch (Exception finalEx)
						{
							await Logger.LogExceptionAsync(finalEx, "[ComponentValidationService] Final simulation failed").ConfigureAwait(false);
							urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
						}
					}
					else
					{
						await Logger.LogVerboseAsync("[ComponentValidationService] No archive name mismatches found to fix").ConfigureAwait(false);
						urlsNeedingDownload.UnionWith(component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)));
					}
				}
			}

			bool simulationFailed = urlsNeedingDownload.Count > 0;

			// If single-component validation failed, try fallback with all components
			if (
				simulationFailed &&
				MainConfig.AllComponents != null &&
				MainConfig.AllComponents.Count > 1 &&
				shouldTryFallback)
			{
				await Logger.LogVerboseAsync($"[ComponentValidationService] Single-component validation failed, attempting fallback with all {MainConfig.AllComponents.Count} components...").ConfigureAwait(false);

				// Try with all components
				VirtualFileSystemProvider fallbackVfs = new VirtualFileSystemProvider();
				await fallbackVfs.InitializeFromRealFileSystemForComponentsAsync(modArchiveDirectory, MainConfig.AllComponents).ConfigureAwait(false);

				try
				{
					ModComponent.InstallExitCode fallbackExitCode = await component.ExecuteInstructionsAsync(
						component.Instructions,
						new List<ModComponent>(),
						cancellationToken,
						fallbackVfs,
						skipDependencyCheck: true
					).ConfigureAwait(false);

					List<ValidationIssue> fallbackIssues = fallbackVfs.GetValidationIssues();
					List<ValidationIssue> fallbackMissingFileIssues = fallbackIssues.Where(i =>
						(i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical) &&
						(string.Equals(i.Category, "ExtractArchive", StringComparison.Ordinal) || string.Equals(i.Category, "MoveFile", StringComparison.Ordinal) || string.Equals(i.Category, "CopyFile", StringComparison.Ordinal) || i.Message.Contains("does not exist"))
					).ToList();

					if (fallbackMissingFileIssues.Count == 0 && fallbackExitCode == ModComponent.InstallExitCode.Success)
					{
						await Logger.LogAsync("[ComponentValidationService] ✓ Fallback validation succeeded! Determining which component(s) provided the missing files...").ConfigureAwait(false);

						// Determine which component(s) provided the missing files
						List<ModComponent> missingDependencies = await ComponentValidationService.FindMissingDependenciesAsync(component, modArchiveDirectory, MainConfig.AllComponents, cancellationToken).ConfigureAwait(false);

						if (missingDependencies.Count > 0)
						{
							await Logger.LogAsync($"[ComponentValidationService] Found {missingDependencies.Count} missing dependencies:").ConfigureAwait(false);
							foreach (ModComponent dep in missingDependencies)
							{
								await Logger.LogAsync($"  • {dep.Name} (GUID: {dep.Guid})").ConfigureAwait(false);

								// Add to component's dependencies if not already present
								if (!component.Dependencies.Contains(dep.Guid))
								{
									component.Dependencies.Add(dep.Guid);
									await Logger.LogAsync($"[ComponentValidationService] ✓ Added '{dep.Name}' as dependency to '{component.Name}'").ConfigureAwait(false);
								}
							}

							// Clear the URLs needing download since we found the dependencies
							urlsNeedingDownload.Clear();
							simulationFailed = false;
						}
					}
					else
					{
						await Logger.LogVerboseAsync("[ComponentValidationService] Fallback validation also failed").ConfigureAwait(false);
					}
				}
				catch (Exception fallbackEx)
				{
					await Logger.LogVerboseAsync($"[ComponentValidationService] Fallback validation threw exception: {fallbackEx.Message}").ConfigureAwait(false);
				}
			}

			// Cache the result before returning
			(List<string>, bool simulationFailed) result = (urlsNeedingDownload.ToList(), simulationFailed);
			lock (_cacheLock)
			{
				_validationCache[cacheKey] = (result.Item1, result.Item2, DateTime.UtcNow);
			}

			return result;
		}

		/// <summary>
		/// Finds which components from the available component list provide files needed by the target component.
		/// This is used to automatically detect missing dependencies.
		/// </summary>
		private static async Task<List<ModComponent>> FindMissingDependenciesAsync(
			[NotNull] ModComponent targetComponent,
			[NotNull] string modArchiveDirectory,
			[NotNull] List<ModComponent> allComponents,
			CancellationToken cancellationToken)
		{
			List<ModComponent> missingDependencies = new List<ModComponent>();

			// Test each component individually to see if it makes the target component's validation pass
			foreach (ModComponent candidateComponent in allComponents)
			{
				// Skip self and already declared dependencies
				if (candidateComponent.Guid == targetComponent.Guid ||
					 targetComponent.Dependencies.Contains(candidateComponent.Guid))
					continue;

				// Try validation with target component + candidate component
				VirtualFileSystemProvider testVfs = new VirtualFileSystemProvider();
				await testVfs.InitializeFromRealFileSystemForComponentsAsync(modArchiveDirectory,
					new List<ModComponent> { targetComponent, candidateComponent }).ConfigureAwait(false);

				try
				{
					ModComponent.InstallExitCode testExitCode = await targetComponent.ExecuteInstructionsAsync(
						targetComponent.Instructions,
						new List<ModComponent>(),
						cancellationToken,
						testVfs,
						skipDependencyCheck: true
					).ConfigureAwait(false);

					List<ValidationIssue> testIssues = testVfs.GetValidationIssues();
					List<ValidationIssue> testMissingFileIssues = testIssues.Where(i =>
						(i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical) &&
						(string.Equals(i.Category, "ExtractArchive", StringComparison.Ordinal) || string.Equals(i.Category, "MoveFile", StringComparison.Ordinal) || string.Equals(i.Category, "CopyFile", StringComparison.Ordinal) || i.Message.Contains("does not exist"))
					).ToList();

					// If validation now passes with this candidate, it's a dependency
					if (testMissingFileIssues.Count == 0 && testExitCode == ModComponent.InstallExitCode.Success)
					{
						await Logger.LogVerboseAsync($"[ComponentValidationService] Detected dependency: '{candidateComponent.Name}' provides files for '{targetComponent.Name}'").ConfigureAwait(false);
						missingDependencies.Add(candidateComponent);
					}
				}
				catch (Exception ex)
				{
					await Logger.LogVerboseAsync($"[ComponentValidationService] Error testing candidate '{candidateComponent.Name}': {ex.Message}").ConfigureAwait(false);
				}
			}

			return missingDependencies;
		}

		/// <summary>
		/// Fixes instruction Source patterns to account for nested archive folders.
		/// When an archive contains a root folder matching the archive name, the extracted structure is:
		/// workspace\ArchiveName\ArchiveName\files (double nesting)
		/// This fixes patterns like: modDirectory\ArchiveName*\Override\*
		/// To: modDirectory\ArchiveName*\ArchiveName*\Override\*
		/// </summary>
		public static int FixNestedArchiveFolderInstructions(ModComponent component, VirtualFileSystemProvider virtualFileSystem)
		{
			int fixCount = 0;

			if (component is null || virtualFileSystem is null)
				return 0;

			// Get all extracted archives and check which have nested folders
			HashSet<string> nestedArchives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (string archivePath in virtualFileSystem.GetTrackedFiles().Where(f =>
				f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
				f.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
				f.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)))
			{
				string archiveName = Path.GetFileNameWithoutExtension(archivePath);
				if (string.IsNullOrEmpty(archiveName))
					continue;

				// Check if files exist in the nested pattern: workspace\ArchiveName\ArchiveName\
				string nestedPattern = Path.DirectorySeparatorChar + archiveName + Path.DirectorySeparatorChar + archiveName + Path.DirectorySeparatorChar;
				bool hasNestedStructure = virtualFileSystem.GetTrackedFiles()
					.Exists(f => f.IndexOf(nestedPattern, StringComparison.OrdinalIgnoreCase) >= 0);

				if (hasNestedStructure)
				{
					nestedArchives.Add(archiveName);
					Logger.LogVerbose($"[ComponentValidationService] Detected nested archive structure for: {archiveName}");
				}
			}

			if (nestedArchives.Count == 0)
				return 0;

			// Fix instructions that reference these archives
			foreach (Instruction instruction in component.Instructions)
			{
				if (instruction.Source.Count == 0)
					continue;

				bool instructionModified = false;
				List<string> newSources = new List<string>();

				foreach (string source in instruction.Source)
				{
					string modifiedSource = source;
					bool wasModified = false;

					foreach (string archiveName in nestedArchives)
					{
						// Pattern: <<modDirectory>>\ArchiveName*\something\*
						// Should be: <<modDirectory>>\ArchiveName*\ArchiveName*\something\*
						string searchPattern = $"<<modDirectory>>\\{archiveName}";

						if (modifiedSource.IndexOf(searchPattern, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							// Find where the archive name pattern is
							int archivePatternIndex = modifiedSource.IndexOf(searchPattern, StringComparison.OrdinalIgnoreCase);
							int afterArchiveNameIndex = archivePatternIndex + searchPattern.Length;

							// Look for the pattern after archive name: could be "*\" or just "\"
							// Find the first path separator after the archive name
							int firstSeparatorIndex = modifiedSource.IndexOf(Path.DirectorySeparatorChar, afterArchiveNameIndex);

							if (firstSeparatorIndex > afterArchiveNameIndex)
							{
								// If it's just "*" or empty, we need to look at the next segment
								// Find the second separator to see what folder comes after
								int secondSeparatorIndex = modifiedSource.IndexOf(Path.DirectorySeparatorChar, firstSeparatorIndex + 1);

								if (secondSeparatorIndex > firstSeparatorIndex + 1)
								{
									// Get the folder name between first and second separator
									string nextFolderSegment = modifiedSource.Substring(firstSeparatorIndex + 1, secondSeparatorIndex - firstSeparatorIndex - 1);

									// Check if this folder segment is NOT the archive name (meaning we need to add it)
									if (!nextFolderSegment.Equals(archiveName, StringComparison.OrdinalIgnoreCase) &&
										 !(archiveName is null) &&
										 !nextFolderSegment.StartsWith(archiveName, StringComparison.OrdinalIgnoreCase))
									{
										// Insert the archive name pattern after the first separator
										string beforeInsert = modifiedSource.Substring(0, firstSeparatorIndex + 1);
										string afterInsert = modifiedSource.Substring(firstSeparatorIndex + 1);
										modifiedSource = beforeInsert + archiveName + "*" + Path.DirectorySeparatorChar + afterInsert;
										wasModified = true;

										Logger.LogVerbose("[ComponentValidationService] Fixed instruction source:");
										Logger.LogVerbose($"  From: {source}");
										Logger.LogVerbose($"  To:   {modifiedSource}");
									}
								}
							}
						}
					}

					newSources.Add(modifiedSource);
					if (wasModified)
					{
						instructionModified = true;
						fixCount++;
					}
				}

				if (instructionModified)
				{
					instruction.Source = newSources;
				}
			}

			return fixCount;
		}

		/// <summary>
		/// Validates that all files required by a component's instructions exist.
		/// </summary>
		public static async Task<bool> ValidateComponentFilesExistAsync(ModComponent component)
		{
			try
			{
				if (component?.Instructions is null || component.Instructions.Count == 0)
					return true;

				await Logger.LogVerboseAsync($"[ComponentValidationService] Validating component '{component.Name}' (GUID: {component.Guid})").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[ComponentValidationService] ModComponent has {component.Instructions.Count} instructions").ConfigureAwait(false);

				VirtualFileSystemProvider validationProvider = new VirtualFileSystemProvider();
				await validationProvider.InitializeFromRealFileSystemAsync(MainConfig.SourcePath?.FullName ?? "").ConfigureAwait(false);

				foreach (Instruction instruction in component.Instructions)
				{
					if (instruction.Source.Count == 0)
						continue;

					List<string> sourcePaths = instruction.Source
						.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
						.ToList();

					if (sourcePaths.Count == 0)
						continue;

					await Logger.LogVerboseAsync($"[ComponentValidationService] Checking {sourcePaths.Count} source paths for instruction").ConfigureAwait(false);

					List<string> foundFiles = PathHelper.EnumerateFilesWithWildcards(
						sourcePaths,
						validationProvider,
						includeSubFolders: true
					);

					if (foundFiles is null || foundFiles.Count == 0)
					{
						await Logger.LogVerboseAsync($"[ComponentValidationService] No files found for paths: {string.Join(", ", sourcePaths)}").ConfigureAwait(false);
						return false;
					}

					await Logger.LogVerboseAsync($"[ComponentValidationService] Found {foundFiles.Count} files for instruction").ConfigureAwait(false);
				}

				await Logger.LogVerboseAsync($"[ComponentValidationService] ModComponent '{component.Name}' validation passed - all files exist").ConfigureAwait(false);
				return true;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, $"Error validating files for component '{component?.Name}'").ConfigureAwait(false);
				return false;
			}
		}

		/// <summary>
		/// Gets a list of missing files for a component.
		/// </summary>
		public static async Task<List<string>> GetMissingFilesForComponentAsync(ModComponent component)
		{
			List<string> missingFiles = new List<string>();

			try
			{
				if (component?.Instructions is null || component.Instructions.Count == 0)
					return missingFiles;

				await Logger.LogVerboseAsync($"[ComponentValidationService] Getting missing files for component '{component.Name}' (GUID: {component.Guid})").ConfigureAwait(false);

				VirtualFileSystemProvider validationProvider = new VirtualFileSystemProvider();
				await validationProvider.InitializeFromRealFileSystemAsync(MainConfig.SourcePath?.FullName ?? "").ConfigureAwait(false);

				foreach (Instruction instruction in component.Instructions)
				{
					if (instruction.Action == Instruction.ActionType.Choose)
					{
						if (instruction.Source != null && instruction.Source.Count > 0)
						{
							foreach (string optionGuidStr in instruction.Source)
							{
								if (Guid.TryParse(optionGuidStr, out Guid optionGuid))
								{
									Option selectedOption = component.Options?.FirstOrDefault(o => o.Guid == optionGuid);
									if (selectedOption != null && selectedOption.IsSelected)
									{
										if (selectedOption.Instructions != null)
										{
											foreach (Instruction optionInstruction in selectedOption.Instructions)
											{
												if (optionInstruction.Source is null || optionInstruction.Source.Count == 0)
													continue;

												List<string> optionSourcePaths = optionInstruction.Source
													.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
													.ToList();

												if (optionSourcePaths.Count == 0)
													continue;

												List<string> foundOptionFiles = PathHelper.EnumerateFilesWithWildcards(
													optionSourcePaths,
													validationProvider,
													includeSubFolders: true
												);

												if (foundOptionFiles is null || foundOptionFiles.Count == 0)
												{
													foreach (string sourcePath in optionSourcePaths)
													{
														string fileName = Path.GetFileName(sourcePath);
														if (!string.IsNullOrEmpty(fileName) && !missingFiles.Contains(fileName, StringComparer.Ordinal))
														{
															missingFiles.Add(fileName);
															await Logger.LogVerboseAsync($"[ComponentValidationService] Missing file in option '{selectedOption.Name}': {fileName}").ConfigureAwait(false);
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

					if (instruction.Source is null || instruction.Source.Count == 0)
						continue;

					List<string> sourcePaths = instruction.Source
						.Where(sourcePath => !string.IsNullOrWhiteSpace(sourcePath))
						.ToList();

					if (sourcePaths.Count == 0)
						continue;

					await Logger.LogVerboseAsync($"[ComponentValidationService] Checking {sourcePaths.Count} source paths for instruction").ConfigureAwait(false);

					List<string> foundFiles = PathHelper.EnumerateFilesWithWildcards(
						sourcePaths,
						validationProvider,
						includeSubFolders: true
					);

					if (foundFiles is null || foundFiles.Count == 0)
					{
						foreach (string sourcePath in sourcePaths)
						{
							string fileName = Path.GetFileName(sourcePath);
							if (!string.IsNullOrEmpty(fileName) && !missingFiles.Contains(fileName, StringComparer.Ordinal))
							{
								missingFiles.Add(fileName);
								await Logger.LogVerboseAsync($"[ComponentValidationService] Missing file: {fileName}").ConfigureAwait(false);
							}
						}
					}
					else
					{
						await Logger.LogVerboseAsync($"[ComponentValidationService] Found {foundFiles.Count} files for instruction").ConfigureAwait(false);
					}
				}

				await Logger.LogVerboseAsync($"[ComponentValidationService] Found {missingFiles.Count} missing files for component '{component.Name}'").ConfigureAwait(false);
				return missingFiles;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, $"Error getting missing files for component '{component?.Name}'").ConfigureAwait(false);
				return missingFiles;
			}
		}

		/// <summary>
		/// Simulates previous Extract instructions to populate VFS for path validation.
		/// </summary>
		public static void SimulatePreviousInstructions(VirtualFileSystemProvider virtualProvider, ModComponent component, Instruction targetInstruction)
		{
			try
			{
				int targetIndex = component.Instructions.IndexOf(targetInstruction);
				if (targetIndex < 0)
					return;

				for (int i = 0; i < targetIndex; i++)
				{
					Instruction prevInstruction = component.Instructions[i];
					if (prevInstruction.Action == Instruction.ActionType.Extract)
					{
						foreach (string sourcePath in prevInstruction.Source)
						{
							List<string> archiveFiles = PathHelper.EnumerateFilesWithWildcards(
								new List<string> { sourcePath },
								virtualProvider,
								includeSubFolders: true
							);

							foreach (string archiveFile in archiveFiles)
							{
								string destination = !string.IsNullOrWhiteSpace(prevInstruction.Destination)
									? ResolvePath(prevInstruction.Destination)
									: MainConfig.SourcePath?.FullName;

								_ = virtualProvider.ExtractArchiveAsync(archiveFile, destination).GetAwaiter().GetResult();
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Error simulating previous instructions");
			}
		}

		/// <summary>
		/// Resolves placeholder paths (<<modDirectory>>, <<kotorDirectory>>) to actual paths.
		/// </summary>
		[NotNull]
		public static string ResolvePath([CanBeNull] string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return path ?? string.Empty;

			if (path.Contains("<<modDirectory>>"))
			{
				string modDir = MainConfig.SourcePath?.FullName ?? "";
				path = path.Replace("<<modDirectory>>", modDir);
			}

			if (path.Contains("<<kotorDirectory>>"))
			{
				string kotorDir = MainConfig.DestinationPath?.FullName ?? "";
				path = path.Replace("<<kotorDirectory>>", kotorDir);
			}

			return path;
		}

		/// <summary>
		/// Validates URL format.
		/// </summary>
		public static bool IsValidUrl(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				return false;

			if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
				return false;

			if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
				return false;

			if (string.IsNullOrWhiteSpace(uri.Host))
				return false;

			return true;
		}

		/// <summary>
		/// Validates all mod links in a list.
		/// </summary>
		public static bool AreModLinksValid(List<string> modLinks)
		{
			if (modLinks is null || modLinks.Count == 0)
				return true;

			foreach (string link in modLinks)
			{
				if (string.IsNullOrWhiteSpace(link))
					continue;

				if (!IsValidUrl(link))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Gets validation reason for a URL.
		/// </summary>
		public static string GetUrlValidationReason(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				return "Empty URL";

			if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
				return "Invalid URL format";

			if (!string.Equals(uri.Scheme, "http", StringComparison.Ordinal) && !string.Equals(uri.Scheme, "https", StringComparison.Ordinal))
				return $"Unsupported protocol: {uri.Scheme}";

			return "Valid URL";
		}
		/// <summary>
		/// Gets the current VirtualFileSystemProvider instance.
		/// </summary>
		[NotNull]
		public VirtualFileSystemProvider GetVirtualFileSystem() => _virtualFileSystem;

		/// <summary>
		/// Filters a list of filenames to only those that satisfy currently unsatisfied Extract instruction patterns.
		/// Uses VFS to determine which patterns are missing files and which filenames would resolve them.
		/// </summary>
		public List<string> FilterFilenamesByUnsatisfiedPatterns(
			[NotNull] ModComponent component,
			[NotNull] List<string> filenames,
			[NotNull] string modArchiveDirectory)
		{
			if (component is null)
				throw new ArgumentNullException(nameof(component));
			if (filenames is null || filenames.Count == 0)
				return new List<string>();
			if (string.IsNullOrEmpty(modArchiveDirectory))
				return filenames; // Fallback: download all

			// Initialize VFS with existing files
			_virtualFileSystem = new VirtualFileSystemProvider();
			_virtualFileSystem.InitializeFromRealFileSystem(modArchiveDirectory);

			// Collect all Extract instructions (component + options)
			List<Instruction> extractInstructions = new List<Instruction>();
			foreach (Instruction instruction in component.Instructions)
			{
				if (instruction.Action == Instruction.ActionType.Extract)
					extractInstructions.Add(instruction);
			}
			foreach (Option option in component.Options)
			{
				foreach (Instruction instruction in option.Instructions)
				{
					if (instruction.Action == Instruction.ActionType.Extract)
						extractInstructions.Add(instruction);
				}
			}

			if (extractInstructions.Count == 0)
			{
				Logger.LogVerbose($"[ComponentValidationService] No Extract instructions found, all files allowed");
				return filenames;
			}

			// Find which Extract patterns are currently unsatisfied
			List<string> unsatisfiedPatterns = new List<string>();

			foreach (Instruction instruction in extractInstructions)
			{
				if (instruction.Source is null || instruction.Source.Count == 0)
					continue;

				foreach (string sourcePath in instruction.Source)
				{
					if (string.IsNullOrWhiteSpace(sourcePath))
						continue;

					try
					{
						List<string> foundFiles = PathHelper.EnumerateFilesWithWildcards(
							new List<string> { sourcePath },
							_virtualFileSystem,
							includeSubFolders: true
						);

						if (foundFiles is null || foundFiles.Count == 0)
						{
							// Pattern is unsatisfied - need to download something for it
							unsatisfiedPatterns.Add(sourcePath);
							Logger.LogVerbose($"[ComponentValidationService] Unsatisfied Extract pattern: {sourcePath}");
						}
					}
					catch
					{
						// Pattern failed to resolve - need to download something for it
						unsatisfiedPatterns.Add(sourcePath);
						Logger.LogVerbose($"[ComponentValidationService] Failed to resolve Extract pattern: {sourcePath}");
					}
				}
			}

			if (unsatisfiedPatterns.Count == 0)
			{
				Logger.LogVerbose($"[ComponentValidationService] All Extract patterns already satisfied, no files needed");
				return new List<string>();
			}

			Logger.LogVerbose($"[ComponentValidationService] Found {unsatisfiedPatterns.Count} unsatisfied Extract pattern(s), testing {filenames.Count} filenames...");

			// Test each filename to see if it satisfies any unsatisfied patterns
			HashSet<string> neededFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (string filename in filenames)
			{
				if (string.IsNullOrWhiteSpace(filename))
					continue;

				// Add file to VFS temporarily
				string tempFilePath = Path.Combine(modArchiveDirectory, filename);
				_virtualFileSystem.WriteFileAsync(tempFilePath, "").Wait();

				// Re-check unsatisfied patterns with this file present
				foreach (string sourcePath in unsatisfiedPatterns)
				{
					try
					{
						List<string> foundFiles = PathHelper.EnumerateFilesWithWildcards(
							new List<string> { sourcePath },
							_virtualFileSystem,
							includeSubFolders: true
						);

						if (foundFiles != null && foundFiles.Count > 0)
						{
							// This file satisfies a previously unsatisfied pattern!
							neededFiles.Add(filename);
							Logger.LogVerbose($"[ComponentValidationService] ✓ File '{filename}' satisfies Extract pattern '{sourcePath}'");
							break; // No need to check other patterns for this file
						}
					}
					catch
					{
						// Pattern still fails
					}
				}

				// Remove file from VFS after testing
				_virtualFileSystem.DeleteFileAsync(tempFilePath).Wait();
			}

			if (neededFiles.Count == 0)
			{
				Logger.LogVerbose($"[ComponentValidationService] No filenames matched unsatisfied Extract patterns. Available files:");
				foreach (string fn in filenames)
				{
					Logger.LogVerbose($"  • {fn}");
				}
			}

			return neededFiles.ToList();
		}

		/// <summary>
		/// Tests if a filename is needed by any instruction pattern.
		/// </summary>
		public Task<bool> TestFilenameNeededByInstructionsAsync(ModComponent component, string filename, string modArchiveDirectory)
		{
			if (component is null || string.IsNullOrWhiteSpace(filename) || string.IsNullOrEmpty(modArchiveDirectory))
				return Task.FromResult(false);

			List<string> matchedFiles = FilterFilenamesByUnsatisfiedPatterns(component, new List<string> { filename }, modArchiveDirectory);
			return Task.FromResult(matchedFiles.Count > 0);
		}

		/// <summary>
		/// Finds the best matching filename from a list based on instruction patterns.
		/// Uses VFS to test which files would satisfy unsatisfied Extract patterns.
		/// </summary>
		public async Task<string> FindBestMatchingFilenameAsync(ModComponent component, string url, List<string> filenames, string modArchiveDirectory = null)
		{
			if (component is null || filenames is null || filenames.Count == 0)
				return null;

			if (filenames.Count == 1)
				return filenames[0];

			await Logger.LogVerboseAsync($"[ComponentValidationService] Testing {filenames.Count} filenames for URL '{url}' against instruction patterns...").ConfigureAwait(false);

			// If modArchiveDirectory not provided, use simple pattern matching as fallback
			if (string.IsNullOrEmpty(modArchiveDirectory))
			{
				modArchiveDirectory = MainConfig.SourcePath?.FullName;
			}

			if (!string.IsNullOrEmpty(modArchiveDirectory))
			{
				// Use VFS-based filtering to find files that satisfy unsatisfied patterns
				List<string> neededFiles = FilterFilenamesByUnsatisfiedPatterns(component, filenames, modArchiveDirectory);
				if (neededFiles.Count > 0)
				{
					await Logger.LogAsync($"[ComponentValidationService] ✓ Found {neededFiles.Count} file(s) that satisfy unsatisfied patterns, returning first: {neededFiles[0]}'").ConfigureAwait(false);
					return neededFiles[0];
				}
			}

			// Fallback: simple pattern matching
			List<Instruction> allInstructions = new List<Instruction>(component.Instructions);
			foreach (Option option in component.Options)
			{
				allInstructions.AddRange(option.Instructions);
			}

			foreach (Instruction instruction in allInstructions)
			{
				if (instruction.Source is null || instruction.Source.Count == 0)
					continue;

				foreach (string sourcePath in instruction.Source)
				{
					if (string.IsNullOrWhiteSpace(sourcePath))
						continue;

					string pattern = sourcePath.Replace("<<modDirectory>>\\", "").Replace("<<modDirectory>>/", "");

					foreach (string filename in filenames)
					{
						if (FileMatchesPattern(filename, pattern))
						{
							await Logger.LogAsync($"[ComponentValidationService] ✓ Matched '{filename}' to pattern '{pattern}' (fallback pattern matching)").ConfigureAwait(false);
							return filename;
						}
					}
				}
			}

			await Logger.LogWarningAsync($"[ComponentValidationService] No filename matched instruction patterns for URL '{url}'. Available files:").ConfigureAwait(false);
			foreach (string fn in filenames)
			{
				await Logger.LogWarningAsync($"  • {fn}").ConfigureAwait(false);
			}
			return null;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private static async Task<bool> TryFixArchiveNameMismatchesAsync(
			ModComponent component,
			IReadOnlyList<string> failedPatterns,
			VirtualFileSystemProvider vfs,
			string modArchiveDirectory,
			CancellationToken cancellationToken)
		{
			await Logger.LogVerboseAsync("[ComponentValidationService] Scanning for archive name mismatches...").ConfigureAwait(false);

			// Gather known archive filenames from the component's ResourceRegistry (resource-index)
			List<string> archiveFilenames = component.ResourceRegistry.Values
				.SelectMany(meta => meta.Files?.Keys ?? Enumerable.Empty<string>())
				.Where(fn => !string.IsNullOrWhiteSpace(fn) && Utility.ArchiveHelper.IsArchive(fn))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (archiveFilenames.Count == 0)
			{
				await Logger.LogVerboseAsync("[ComponentValidationService] No known archives found in ResourceRegistry").ConfigureAwait(false);
				return false;
			}

			bool anyFixApplied = false;
			List<(Instruction instruction, List<string> source, string destination)> originalInstructions = new List<(Instruction instruction, List<string> source, string destination)>();

			// Backup all instructions
			foreach (Instruction instruction in component.Instructions)
			{
				originalInstructions.Add((
					instruction,
					new List<string>(instruction.Source),
					instruction.Destination
				));
			}
			foreach (Option option in component.Options)
			{
				foreach (Instruction instruction in option.Instructions)
				{
					originalInstructions.Add((
						instruction,
						new List<string>(instruction.Source),
						instruction.Destination
					));
				}
			}

			try
			{
				foreach (string failedPattern in failedPatterns)
				{
					string expectedArchive = ExtractFilenameFromSource(failedPattern);
					if (string.IsNullOrEmpty(expectedArchive) || !Utility.ArchiveHelper.IsArchive(expectedArchive))
						continue;

					string expectedBase = Path.GetFileNameWithoutExtension(expectedArchive);
					string normalizedExpected = NormalizeModName(expectedBase);

					// Find matching archive filename
					string matchingArchive = null;
					foreach (string archiveName in archiveFilenames)
					{
						string entryBase = Path.GetFileNameWithoutExtension(archiveName);
						string normalizedEntry = NormalizeModName(entryBase);

						if (normalizedExpected.Equals(normalizedEntry, StringComparison.OrdinalIgnoreCase) ||
							 (!(entryBase is null) && expectedBase.IndexOf(entryBase, StringComparison.OrdinalIgnoreCase) >= 0) ||
							 (!(entryBase is null) && entryBase.IndexOf(expectedBase, StringComparison.OrdinalIgnoreCase) >= 0))
						{
							matchingArchive = archiveName;
							break;
						}
					}

					if (matchingArchive is null)
					{
						await Logger.LogVerboseAsync($"[ComponentValidationService] No matching cached archive for '{expectedArchive}'").ConfigureAwait(false);
						continue;
					}

					// Find the Extract instruction with this pattern
					Instruction extractInstruction = component.Instructions
						.FirstOrDefault(i => i.Action == Instruction.ActionType.Extract &&
											 i.Source.Any(s => FileMatchesPattern(expectedArchive, ExtractFilenameFromSource(s))));

					if (extractInstruction is null)
					{
						// Check options
						foreach (Option option in component.Options)
						{
							extractInstruction = option.Instructions
								.FirstOrDefault(i => i.Action == Instruction.ActionType.Extract &&
													 i.Source.Any(s => FileMatchesPattern(expectedArchive,
											 ExtractFilenameFromSource(s))));
							if (extractInstruction != null)
								break;
						}
					}

					if (extractInstruction is null)
					{
						await Logger.LogVerboseAsync($"[ComponentValidationService] No Extract instruction found for '{expectedArchive}'").ConfigureAwait(false);
						continue;
					}

					// Apply fix
					bool fixSuccess = await TryFixSingleMismatchAsync(component, extractInstruction, matchingArchive, vfs, modArchiveDirectory, cancellationToken).ConfigureAwait(false);

					if (fixSuccess)
					{
						anyFixApplied = true;
						await Logger.LogAsync($"[ComponentValidationService] ✓ Fixed mismatch for '{expectedArchive}' -> '{matchingArchive}'").ConfigureAwait(false);
					}
				}

				return anyFixApplied;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, "[ComponentValidationService] Error during mismatch fix").ConfigureAwait(false);
				RollbackInstructions(component, originalInstructions);
				return false;
			}
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private static async Task<bool> TryFixSingleMismatchAsync(
			ModComponent component,
			Instruction extractInstruction,
			string newArchiveName,
			VirtualFileSystemProvider vfs,
			string modArchiveDirectory,
			CancellationToken cancellationToken = default)
		{
			string oldArchiveName = ExtractFilenameFromSource(extractInstruction.Source[0]);

			string oldExtractedFolder = Path.GetFileNameWithoutExtension(oldArchiveName);
			string newExtractedFolder = Path.GetFileNameWithoutExtension(newArchiveName);

			// Update extract source
			int sourceIndex = -1;
			for (int i = 0; i < extractInstruction.Source.Count; i++)
			{
				if (string.Equals(ExtractFilenameFromSource(extractInstruction.Source[i]), oldArchiveName, StringComparison.Ordinal))
				{
					sourceIndex = i;
					break;
				}
			}
			if (sourceIndex >= 0)
			{
				// Create a mutable list, update, then re-assign to source as a new list.
				var sources = extractInstruction.Source.ToList();
				sources[sourceIndex] = $@"<<modDirectory>>\{newArchiveName}";
				extractInstruction.Source = sources;
			}

			void UpdateSources(IEnumerable<Instruction> instructions)
			{
				foreach (Instruction instr in instructions)
				{
					var sources = instr.Source.ToList();
					for (int i = 0; i < sources.Count; i++)
					{
						string src = sources[i];
						if (src.IndexOf(oldExtractedFolder, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							// Manual case-insensitive replace
							int index = src.IndexOf(oldExtractedFolder, StringComparison.OrdinalIgnoreCase);
							while (index >= 0)
							{
								src = src.Substring(0, index) + newExtractedFolder + src.Substring(index + oldExtractedFolder.Length);
								index = src.IndexOf(oldExtractedFolder, index + newExtractedFolder.Length, StringComparison.OrdinalIgnoreCase);
							}
							sources[i] = src;
						}
					}
					instr.Source = sources;
				}
			}

			UpdateSources(component.Instructions);
			foreach (Option opt in component.Options)
				UpdateSources(opt.Instructions);

			// Validate
			VirtualFileSystemProvider tempVfs = new VirtualFileSystemProvider();
			await tempVfs.InitializeFromRealFileSystemAsync(modArchiveDirectory).ConfigureAwait(false);

			try
			{
				ModComponent.InstallExitCode exitCode = await component.ExecuteInstructionsAsync(
					component.Instructions,
					new List<ModComponent>(),
					cancellationToken,
					tempVfs,
					skipDependencyCheck: true
				).ConfigureAwait(false);

				if (exitCode == ModComponent.InstallExitCode.Success)
					return true;

				// Rollback if fails
				return false;
			}
			catch
			{
				return false;
			}
		}

		private static void RollbackInstructions(
			ModComponent component,
			List<(Instruction instruction, List<string> source, string destination)> original)
		{
			foreach (var item in original)
			{
				var instruction = item.Item1;
				var source = item.Item2;
				var destination = item.Item3;
				instruction.Source = new List<string>(source);
				instruction.Destination = destination;
			}
		}
		private static string ExtractFilenameFromSource(string sourcePath)
		{
			if (string.IsNullOrEmpty(sourcePath))
				return string.Empty;

			string cleanedPath = sourcePath.Replace("<<modDirectory>>\\", "").Replace("<<modDirectory>>/", "");

			string filename = Path.GetFileName(cleanedPath);

			return filename;
		}

		private static string NormalizeModName(string name)
		{
			if (string.IsNullOrEmpty(name))
				return string.Empty;

			string normalized = name.ToLowerInvariant();
			normalized = MyRegex.Replace(normalized, " ");
			normalized = MyRegex_.Replace(normalized, "");
			normalized = MyRegex__.Replace(normalized, "");
			normalized = normalized.Trim();

			return normalized;
		}

		private static bool FileMatchesPattern(string filename, string pattern)
		{
			if (string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(pattern))
				return false;

			string regexPattern = "^" + Regex.Escape(pattern)
				.Replace("\\*", ".*")
				.Replace("\\?", ".") + "$";

			try
			{
				return Regex.IsMatch(filename, regexPattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(1000));
			}
			catch
			{
				return filename.IndexOf(pattern.Replace("*", ""), StringComparison.OrdinalIgnoreCase) >= 0;
			}
		}

		private static readonly Regex MyRegex = new Regex(@"[_\-\s]+", RegexOptions.Compiled, TimeSpan.FromMilliseconds(1000));
		private static readonly Regex MyRegex_ = new Regex(@"v?\d+(\.\d+)*", RegexOptions.Compiled, TimeSpan.FromMilliseconds(1000));
		private static readonly Regex MyRegex__ = new Regex(@"[^\w\s]", RegexOptions.Compiled, TimeSpan.FromMilliseconds(1000));
	}
}