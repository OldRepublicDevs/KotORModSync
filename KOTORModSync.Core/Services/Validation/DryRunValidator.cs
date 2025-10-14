// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Services.FileSystem;

namespace KOTORModSync.Core.Services.Validation
{



	public static class DryRunValidator
	{



		[NotNull]
		public static async Task<DryRunValidationResult> ValidateInstallationAsync(
			[NotNull][ItemNotNull] List<ModComponent> components,
			CancellationToken cancellationToken = default
		)
		{
			if ( components == null )
				throw new ArgumentNullException(nameof(components));

			var result = new DryRunValidationResult();
			var virtualFileSystem = new VirtualFileSystemProvider();

			await Logger.LogAsync("Starting dry-run validation of installation...");
			await Logger.LogAsync($"Validating {components.Count(c => c.IsSelected)} selected component(s)...");


			List<ModComponent> selectedComponents = components.Where(c => c.IsSelected).ToList();

			if ( selectedComponents.Count == 0 )
			{
				await Logger.LogAsync("No components selected for validation.");
				return result;
			}


			int componentIndex = 0;
			foreach ( ModComponent component in selectedComponents )
			{
				componentIndex++;
				cancellationToken.ThrowIfCancellationRequested();

				await Logger.LogAsync($"[{componentIndex}/{selectedComponents.Count}] Validating component '{component.Name}'...");

				try
				{

					if ( !component.ShouldInstallComponent(components) )
					{
						await Logger.LogWarningAsync(
							$"ModComponent '{component.Name}' has unmet dependencies or restriction conflicts. It will be skipped."
						);

						result.Issues.Add(new ValidationIssue
						{
							Severity = ValidationSeverity.Warning,
							Category = "DependencyValidation",
							Message = "ModComponent has unmet dependencies or restriction conflicts and will be skipped during installation.",
							AffectedComponent = component,
							Timestamp = DateTimeOffset.UtcNow
						});

						continue;
					}


					await ValidateComponentInstructionsAsync(
						component,
						components,
						virtualFileSystem,
						result,
						cancellationToken
					);
				}
				catch ( Exception ex )
				{
					await Logger.LogExceptionAsync(ex);

					result.Issues.Add(new ValidationIssue
					{
						Severity = ValidationSeverity.Critical,
						Category = "ValidationException",
						Message = $"Unexpected error during validation: {ex.Message}",
						AffectedComponent = component,
						Timestamp = DateTimeOffset.UtcNow
					});
				}
			}


			foreach ( ValidationIssue issue in virtualFileSystem.ValidationIssues )
			{
				result.Issues.Add(issue);
			}

			await Logger.LogAsync("Dry-run validation completed.");
			await Logger.LogAsync($"Results: {result.Issues.Count} issue(s) found " +
				$"({result.Issues.Count(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Critical)} errors, " +
				$"{result.Issues.Count(i => i.Severity == ValidationSeverity.Warning)} warnings)");

			return result;
		}






		private static async Task ValidateComponentInstructionsAsync(
			[NotNull] ModComponent component,
			[NotNull][ItemNotNull] List<ModComponent> allComponents,
			[NotNull] VirtualFileSystemProvider fileSystem,
			[NotNull] DryRunValidationResult result,
			CancellationToken cancellationToken
		)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( allComponents == null )
				throw new ArgumentNullException(nameof(allComponents));
			if ( fileSystem == null )
				throw new ArgumentNullException(nameof(fileSystem));
			if ( result == null )
				throw new ArgumentNullException(nameof(result));

			try
			{


				ModComponent.InstallExitCode exitCode = await component.ExecuteInstructionsAsync(
					component.Instructions,
					allComponents,
					cancellationToken,
					fileSystem
				);


				if ( exitCode != ModComponent.InstallExitCode.Success )
				{
					result.Issues.Add(new ValidationIssue
					{
						Severity = ValidationSeverity.Error,
						Category = "ExecutionError",
						Message = $"ModComponent failed validation with exit code: {exitCode}",
						AffectedComponent = component,
						Timestamp = DateTimeOffset.UtcNow
					});
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);

				result.Issues.Add(new ValidationIssue
				{
					Severity = ValidationSeverity.Error,
					Category = "ValidationException",
					Message = $"Exception during validation: {ex.Message}",
					AffectedComponent = component,
					Timestamp = DateTimeOffset.UtcNow
				});
			}
		}

		/// <summary>
		/// Validates a single instruction path by dry-running all previous instructions.
		/// Uses VirtualFileSystemProvider to simulate the filesystem state at the instruction's execution point.
		/// </summary>
		[NotNull]
		public static async Task<string> ValidateInstructionPathAsync([CanBeNull] string path, [CanBeNull] Instruction instruction, [CanBeNull] ModComponent component)
		{
			PathValidationResult result = await ValidateInstructionPathDetailedAsync(path, instruction, component);
			return result.StatusMessage;
		}

		/// <summary>
		/// Validates a single instruction path with detailed information about failures.
		/// Returns comprehensive validation result including failure chain tracking.
		/// </summary>
		[NotNull]
		public static async Task<PathValidationResult> ValidateInstructionPathDetailedAsync([CanBeNull] string path, [CanBeNull] Instruction instruction, [CanBeNull] ModComponent component)
		{
			var result = new PathValidationResult
			{
				Path = path,
				Instruction = instruction,
				IsValid = false,
				StatusMessage = "❓ Empty"
			};

			try
			{
				if ( string.IsNullOrWhiteSpace(path) )
					return result;

				// For Patcher actions with kotorDirectory destination, it's always valid
				if ( instruction != null && instruction.Action == Instruction.ActionType.Patcher
					&& path.Equals("<<kotorDirectory>>", StringComparison.OrdinalIgnoreCase) )
				{
					result.IsValid = true;
					result.StatusMessage = "✅ Valid (Patcher destination)";
					result.DetailedMessage = "This is a special destination for TSLPatcher which will use the KOTOR directory.";
					return result;
				}

				// Check if directories are configured
				if ( path.Contains("<<modDirectory>>") && MainConfig.SourcePath == null )
				{
					result.StatusMessage = "⚠️ Mod directory not configured";
					result.DetailedMessage = "The mod directory (<<modDirectory>>) has not been configured in Settings.";
					return result;
				}

				if ( path.Contains("<<kotorDirectory>>") && MainConfig.DestinationPath == null )
				{
					result.StatusMessage = "⚠️ KOTOR directory not configured";
					result.DetailedMessage = "The KOTOR directory (<<kotorDirectory>>) has not been configured in Settings.";
					return result;
				}

				if ( MainConfig.SourcePath == null )
				{
					result.StatusMessage = "⚠️ Paths not configured";
					result.DetailedMessage = "Directories have not been configured in Settings.";
					return result;
				}

				if ( component == null || instruction == null )
				{
					result.StatusMessage = "⚠️ No component context";
					return result;
				}

				// Create virtual filesystem and initialize from mod directory
				var virtualProvider = new VirtualFileSystemProvider();
				await virtualProvider.InitializeFromRealFileSystemAsync(MainConfig.SourcePath.FullName);

				// Execute all previous instructions to simulate filesystem state
				int targetIndex = component.Instructions.IndexOf(instruction);
				if ( targetIndex < 0 )
				{
					result.StatusMessage = "⚠️ Instruction not in component";
					return result;
				}

				// Execute instructions up to (but not including) the target instruction
				for ( int i = 0; i < targetIndex; i++ )
				{
					Instruction prevInstruction = component.Instructions[i];
					try
					{
						prevInstruction.SetFileSystemProvider(virtualProvider);

						// Only execute operations that affect file paths
						switch ( prevInstruction.Action )
						{
							case Instruction.ActionType.Extract:
								prevInstruction.SetRealPaths();
								await prevInstruction.ExtractFileAsync();
								break;
							case Instruction.ActionType.Move:
								prevInstruction.SetRealPaths();
								await prevInstruction.MoveFileAsync();
								break;
							case Instruction.ActionType.Copy:
								prevInstruction.SetRealPaths();
								await prevInstruction.CopyFileAsync();
								break;
							case Instruction.ActionType.Rename:
								prevInstruction.SetRealPaths(noValidate: true);
								prevInstruction.RenameFile();
								break;
							case Instruction.ActionType.Delete:
								prevInstruction.SetRealPaths(noValidate: true);
								prevInstruction.DeleteFile();
								break;
						}
					}
					catch ( Exception ex )
					{
						// Track which instruction failed
						result.BlockingInstructionIndex = i;
						result.BlockingInstruction = prevInstruction;
						result.StatusMessage = $"⚠️ Blocked by instruction #{i + 1}";
						result.DetailedMessage = $"Instruction #{i + 1} ({prevInstruction.Action}) failed during validation: {ex.Message}\n\n" +
							$"This prevents the current instruction from being validated properly.\n" +
							$"Fix instruction #{i + 1} first.";
						return result;
					}
				}

				// Now check if the current instruction's path would resolve
				instruction.SetFileSystemProvider(virtualProvider);
				string resolvedPath = Utility.Utility.ReplaceCustomVariables(path);

				List<string> foundFiles = FileSystemUtils.PathHelper.EnumerateFilesWithWildcards(
					new List<string> { resolvedPath },
					virtualProvider,
					includeSubFolders: true
				);

				if ( foundFiles != null && foundFiles.Count > 0 )
				{
					result.IsValid = true;
					result.FoundFiles = foundFiles;
					
					// Check if file is provided by this component or its dependencies
					FileProvenance provenance = CheckFileProvenance(foundFiles[0], component, targetIndex);

					if ( !provenance.IsProvidedByInstructions && !provenance.IsProvidedByDownloadLinks && !provenance.IsProvidedByDependencies )
					{
						// Scenario 5: File exists but not provided by instructions, downloads, or dependencies
						result.StatusMessage = $"⚠️ Found ({foundFiles.Count} file{(foundFiles.Count != 1 ? "s" : "")}) - Not in ModLinks";
						result.DetailedMessage = $"Found {foundFiles.Count} file(s), but they don't appear to be provided by:\n" +
							$"• Previous Extract instructions in this component\n" +
							$"• This component's ModLinks (download URLs)\n" +
							$"• Dependencies' instructions or ModLinks\n\n" +
							$"Files found:\n" +
							string.Join("\n", foundFiles.Take(5).Select(f => $"• {System.IO.Path.GetFileName(f)}")) +
							(foundFiles.Count > 5 ? $"\n... and {foundFiles.Count - 5} more" : "") +
							$"\n\nSuggestion: Upload this file to mega.nz or another hosting service and add the URL to your ModLinks.\n" +
							$"Click 'Jump to ModLinks' to navigate to the download URLs section.";
						result.NeedsModLinkAdded = true;
					}
					else
					{
						// File is properly provided
						result.StatusMessage = $"✅ Found ({foundFiles.Count} file{(foundFiles.Count != 1 ? "s" : "")})";
						result.DetailedMessage = $"Found {foundFiles.Count} matching file(s):\n\n" +
							string.Join("\n", foundFiles.Take(10).Select(f => $"• {System.IO.Path.GetFileName(f)}")) +
							(foundFiles.Count > 10 ? $"\n... and {foundFiles.Count - 10} more" : "");

						if ( provenance.IsProvidedByDependencies )
							result.DetailedMessage += $"\n\nProvided by dependency: {provenance.ProvidingComponentName}";
					}

					return result;
				}

				// Check if it's a directory
				if ( virtualProvider.DirectoryExists(resolvedPath) )
				{
					result.IsValid = true;
					result.StatusMessage = "✅ Directory exists";
					result.DetailedMessage = $"Directory exists: {resolvedPath}";
					return result;
				}

				result.StatusMessage = "❌ Not found";
				result.DetailedMessage = $"Path does not exist: {resolvedPath}\n\n" +
					$"This path will not exist at installation time based on previous instructions.\n" +
					$"Check if:\n" +
					$"• The file/archive exists in your mod directory\n" +
					$"• Previous Extract instructions create this file\n" +
					$"• The path pattern is correct (wildcards: *, ?)";
				return result;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error validating path: {path}");
				result.StatusMessage = "⚠️ Validation error";
				result.DetailedMessage = $"An error occurred during validation:\n{ex.Message}";
				return result;
			}
		}

		/// <summary>
		/// Checks if a file is provided by the component's instructions, download links, or dependencies.
		/// </summary>
		private static FileProvenance CheckFileProvenance(
			[CanBeNull] string filePath,
			[CanBeNull] ModComponent component,
			int currentInstructionIndex)
		{
			var provenance = new FileProvenance();

			if ( string.IsNullOrWhiteSpace(filePath) || component == null )
				return provenance;

			string fileName = System.IO.Path.GetFileName(filePath);

			// Check if provided by previous Extract instructions in this component
			for ( int i = 0; i < currentInstructionIndex; i++ )
			{
				Instruction prevInstruction = component.Instructions[i];
				if ( prevInstruction.Action == Instruction.ActionType.Extract && prevInstruction.Source != null )
				{
					foreach ( string sourcePath in prevInstruction.Source )
					{
						string sourceFileName = System.IO.Path.GetFileName(sourcePath);
						string sourceFileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
						// Check if this Extract instruction creates the file we're looking for
						if ( sourceFileName.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
							 filePath.IndexOf(sourceFileNameWithoutExt, StringComparison.OrdinalIgnoreCase) >= 0 )
						{
							provenance.IsProvidedByInstructions = true;
							return provenance;
						}
					}
				}
			}

			// Check if provided by this component's ModLinks (DownloadCacheService)
			if ( component.ModLink != null && component.ModLink.Count > 0 )
			{
				foreach ( string url in component.ModLink )
				{
					if ( string.IsNullOrWhiteSpace(url) )
						continue;

					// Check against resolved filename from URL
					try
					{
						var downloadCache = new Services.DownloadCacheService();
						string cachedFileName = downloadCache.GetFileName(url);

						if ( !string.IsNullOrEmpty(cachedFileName) )
						{
							string cachedFileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(cachedFileName);
							if ( cachedFileName.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
								 filePath.IndexOf(cachedFileNameWithoutExt, StringComparison.OrdinalIgnoreCase) >= 0 )
							{
								provenance.IsProvidedByDownloadLinks = true;
								return provenance;
							}
						}
					}
					catch
					{
						// If DownloadCacheService fails, continue checking
					}
				}
			}

			// Check if provided by dependency components
			if ( component.Dependencies != null && component.Dependencies.Count > 0 && MainConfig.AllComponents != null )
			{
				foreach ( Guid depGuid in component.Dependencies )
				{
					ModComponent depComponent = MainConfig.AllComponents.FirstOrDefault(c => c.Guid == depGuid);
					if ( depComponent == null )
						continue;

					// Check dependency's Extract instructions
					if ( depComponent.Instructions != null )
					{
						foreach ( Instruction depInstruction in depComponent.Instructions )
						{
							if ( depInstruction.Action == Instruction.ActionType.Extract && depInstruction.Source != null )
							{
								foreach ( string sourcePath in depInstruction.Source )
								{
									string sourceFileName = System.IO.Path.GetFileName(sourcePath);
									string sourceFileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
									if ( sourceFileName.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
										 filePath.IndexOf(sourceFileNameWithoutExt, StringComparison.OrdinalIgnoreCase) >= 0 )
									{
										provenance.IsProvidedByDependencies = true;
										provenance.ProvidingComponentName = depComponent.Name;
										return provenance;
									}
								}
							}
						}
					}

					// Check dependency's ModLinks
					if ( depComponent.ModLink != null && depComponent.ModLink.Count > 0 )
					{
						foreach ( string url in depComponent.ModLink )
						{
							if ( string.IsNullOrWhiteSpace(url) )
								continue;

							try
							{
								var downloadCache = new Services.DownloadCacheService();
								string cachedFileName = downloadCache.GetFileName(url);

								if ( !string.IsNullOrEmpty(cachedFileName) )
								{
									string cachedFileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(cachedFileName);
									if ( cachedFileName.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
										 filePath.IndexOf(cachedFileNameWithoutExt, StringComparison.OrdinalIgnoreCase) >= 0 )
									{
										provenance.IsProvidedByDependencies = true;
										provenance.ProvidingComponentName = depComponent.Name;
										return provenance;
									}
								}
							}
							catch
							{
								// Continue checking other dependencies
							}
						}
					}
				}
			}

			return provenance;
		}
	}

	/// <summary>
	/// Tracks where a file comes from (instructions, downloads, or dependencies)
	/// </summary>
	public class FileProvenance
	{
		public bool IsProvidedByInstructions { get; set; }
		public bool IsProvidedByDownloadLinks { get; set; }
		public bool IsProvidedByDependencies { get; set; }
		public string ProvidingComponentName { get; set; }
	}

	/// <summary>
	/// Detailed validation result for instruction paths
	/// </summary>
	public class PathValidationResult
	{
		public string Path { get; set; }
		public Instruction Instruction { get; set; }
		public bool IsValid { get; set; }
		public string StatusMessage { get; set; }
		public string DetailedMessage { get; set; }
		public List<string> FoundFiles { get; set; }
		public int? BlockingInstructionIndex { get; set; }
		public Instruction BlockingInstruction { get; set; }
		public bool NeedsModLinkAdded { get; set; }
	}
}

