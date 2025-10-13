// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.TSLPatcher;
using KOTORModSync.Core.Utility;
using SharpCompress.Archives;

namespace KOTORModSync.Core.Services
{

	public static class AutoInstructionGenerator
	{
		/// <summary>
		/// Checks if an instruction is equivalent to a potential new instruction using comprehensive wildcard matching.
		/// </summary>
		/// <param name="existing">Existing instruction in the component</param>
		/// <param name="potential">Potential new instruction to be added</param>
		/// <returns>True if instructions are equivalent (considering wildcards), false otherwise</returns>
		private static bool AreInstructionsEquivalent([NotNull] Instruction existing, [NotNull] Instruction potential)
		{
			// Actions must match exactly
			if ( existing.Action != potential.Action )
				return false;

			// Check Source matching (with wildcards)
			if ( !AreSourcesEquivalent(existing.Source, potential.Source) )
				return false;

			// For actions that use Destination, check if they match
			if ( existing.ShouldSerializeDestination() )
			{
				if ( !AreDestinationsEquivalent(existing.Destination, potential.Destination) )
					return false;
			}

			// For actions that use Arguments, check if they match
			if ( existing.ShouldSerializeArguments() )
			{
				if ( !string.Equals(existing.Arguments, potential.Arguments, StringComparison.OrdinalIgnoreCase) )
					return false;
			}

			// For actions that use Overwrite, check if they match
			if ( existing.ShouldSerializeOverwrite() )
			{
				if ( existing.Overwrite != potential.Overwrite )
					return false;
			}

			// Instructions are equivalent
			return true;
		}

		/// <summary>
		/// Checks if two source lists are equivalent using wildcard matching.
		/// Handles cases where existing instructions may have wildcards (e.g., "mymod_v*.zip")
		/// and potential instructions have specific names (e.g., "mymod_v2.3.1.zip").
		/// </summary>
		private static bool AreSourcesEquivalent([NotNull] List<string> existingSources, [NotNull] List<string> potentialSources)
		{
			if ( existingSources == null || potentialSources == null )
				return false;

			// If counts don't match, they're not equivalent
			if ( existingSources.Count != potentialSources.Count )
				return false;

			// If both are empty, they're equivalent
			if ( existingSources.Count == 0 )
				return true;

			// Check each potential source against existing sources using wildcard matching
			// We need to ensure every potential source matches at least one existing source
			// and vice versa (bidirectional matching)

			foreach ( string potentialSource in potentialSources )
			{
				bool foundMatch = false;
				foreach ( string existingSource in existingSources )
				{
					if ( DoSourcesMatch(existingSource, potentialSource) )
					{
						foundMatch = true;
						break;
					}
				}
				if ( !foundMatch )
					return false;
			}

			// Also check reverse: every existing source should match at least one potential source
			foreach ( string existingSource in existingSources )
			{
				bool foundMatch = false;
				foreach ( string potentialSource in potentialSources )
				{
					if ( DoSourcesMatch(existingSource, potentialSource) )
					{
						foundMatch = true;
						break;
					}
				}
				if ( !foundMatch )
					return false;
			}

			return true;
		}

		/// <summary>
		/// Checks if two source paths match using wildcard matching.
		/// Handles both cases: existing has wildcard, potential is specific OR both are specific and match.
		/// </summary>
		private static bool DoSourcesMatch([NotNull] string existing, [NotNull] string potential)
		{
			// Normalize paths for comparison
			string existingNormalized = NormalizePathForComparison(existing);
			string potentialNormalized = NormalizePathForComparison(potential);

			// First try exact match (case-insensitive)
			if ( string.Equals(existingNormalized, potentialNormalized, StringComparison.OrdinalIgnoreCase) )
				return true;

			// Try wildcard matching: existing pattern matches potential input
			if ( ContainsWildcards(existingNormalized) )
			{
				try
				{
					if ( PathHelper.WildcardPathMatch(potentialNormalized, existingNormalized) )
						return true;
				}
				catch
				{
					// If wildcard matching fails, fall through to filename-only check
				}
			}

			// Try reverse: potential pattern matches existing input (in case user has wildcards in manual instructions)
			if ( ContainsWildcards(potentialNormalized) )
			{
				try
				{
					if ( PathHelper.WildcardPathMatch(existingNormalized, potentialNormalized) )
						return true;
				}
				catch
				{
					// If wildcard matching fails, fall through to filename-only check
				}
			}

			// Fallback: check if just the filenames match (ignore directory path differences)
			string existingFilename = Path.GetFileName(existingNormalized);
			string potentialFilename = Path.GetFileName(potentialNormalized);

			if ( string.Equals(existingFilename, potentialFilename, StringComparison.OrdinalIgnoreCase) )
				return true;

			// Check wildcard matching on filenames only
			if ( ContainsWildcards(existingFilename) )
			{
				try
				{
					if ( PathHelper.WildcardPathMatch(potentialFilename, existingFilename) )
						return true;
				}
				catch
				{
					// Wildcard matching failed
				}
			}

			if ( ContainsWildcards(potentialFilename) )
			{
				try
				{
					if ( PathHelper.WildcardPathMatch(existingFilename, potentialFilename) )
						return true;
				}
				catch
				{
					// Wildcard matching failed
				}
			}

			return false;
		}

		/// <summary>
		/// Checks if two destination paths are equivalent using wildcard matching.
		/// </summary>
		private static bool AreDestinationsEquivalent([CanBeNull] string existing, [CanBeNull] string potential)
		{
			// Both empty/null = equivalent
			if ( string.IsNullOrEmpty(existing) && string.IsNullOrEmpty(potential) )
				return true;

			// One empty, one not = not equivalent
			if ( string.IsNullOrEmpty(existing) || string.IsNullOrEmpty(potential) )
				return false;

			string existingNormalized = NormalizePathForComparison(existing);
			string potentialNormalized = NormalizePathForComparison(potential);

			// Exact match
			if ( string.Equals(existingNormalized, potentialNormalized, StringComparison.OrdinalIgnoreCase) )
				return true;

			// Wildcard matching
			if ( ContainsWildcards(existingNormalized) )
			{
				try
				{
					if ( PathHelper.WildcardPathMatch(potentialNormalized, existingNormalized) )
						return true;
				}
				catch
				{
					// Wildcard matching failed
				}
			}

			if ( ContainsWildcards(potentialNormalized) )
			{
				try
				{
					if ( PathHelper.WildcardPathMatch(existingNormalized, potentialNormalized) )
						return true;
				}
				catch
				{
					// Wildcard matching failed
				}
			}

			return false;
		}

		/// <summary>
		/// Normalizes a path for comparison by replacing custom variables and standardizing separators.
		/// </summary>
		private static string NormalizePathForComparison([NotNull] string path)
		{
			if ( string.IsNullOrEmpty(path) )
				return string.Empty;

			// Replace custom variables to their canonical form for comparison
			// (This ensures <<modDirectory>>\file.zip matches <<modDirectory>>/file.zip)
			string normalized = path
				.Replace('/', '\\')  // Standardize to backslash
				.TrimEnd('\\');      // Remove trailing slashes

			return normalized;
		}

		/// <summary>
		/// Checks if a path contains wildcard characters (* or ?).
		/// </summary>
		private static bool ContainsWildcards([NotNull] string path)
		{
			return !string.IsNullOrEmpty(path) && (path.Contains('*') || path.Contains('?'));
		}

		/// <summary>
		/// Checks if an equivalent instruction already exists in the component.
		/// </summary>
		/// <param name="component">The component to check</param>
		/// <param name="potentialInstruction">The instruction we're considering adding</param>
		/// <returns>True if an equivalent instruction already exists, false otherwise</returns>
		private static bool InstructionAlreadyExists([NotNull] ModComponent component, [NotNull] Instruction potentialInstruction)
		{
			return component.Instructions.Any(existing => AreInstructionsEquivalent(existing, potentialInstruction));
		}

		/// <summary>
		/// Finds an existing option with equivalent or overlapping instructions (ignoring Name/Description differences).
		/// Returns an option if:
		/// 1. It has exactly the same instructions as potential, OR
		/// 2. It has a subset of instructions (some or all of potential's instructions), OR
		/// 3. It has a superset of instructions (all of potential's instructions plus more)
		/// </summary>
		/// <param name="component">The component to search</param>
		/// <param name="potentialOption">The option we're considering</param>
		/// <returns>Existing related option or null if none found</returns>
		private static Option FindEquivalentOption([NotNull] ModComponent component, [NotNull] Option potentialOption)
		{
			Option bestMatch = null;
			int highestMatchScore = 0;

			foreach ( Option existingOption in component.Options )
			{
				int matchScore = CalculateOptionInstructionOverlap(existingOption, potentialOption);

				// We consider options equivalent if they have any overlapping instructions
				// or if existing has no instructions (empty option that could be populated)
				if ( matchScore > 0 || (existingOption.Instructions.Count == 0 && potentialOption.Instructions.Count > 0) )
				{
					if ( matchScore > highestMatchScore )
					{
						highestMatchScore = matchScore;
						bestMatch = existingOption;
					}
				}
			}

			// Only return a match if we found significant overlap (at least 1 matching instruction)
			// OR if the potential option would be the first to populate an empty existing option
			return bestMatch;
		}

		/// <summary>
		/// Finds all existing options that are equivalent to each other and consolidates them.
		/// This handles cases where multiple duplicate options already exist in the component.
		/// </summary>
		/// <param name="component">The component to clean up</param>
		/// <returns>Number of duplicate options removed</returns>
		private static int ConsolidateDuplicateOptions([NotNull] ModComponent component)
		{
			int removedCount = 0;
			var processedOptions = new HashSet<Guid>();

			// Create a list to avoid modifying collection during iteration
			List<Option> allOptions = component.Options.ToList();

			for ( int i = 0; i < allOptions.Count; i++ )
			{
				Option primaryOption = allOptions[i];

				// Skip if already processed as a duplicate
				if ( processedOptions.Contains(primaryOption.Guid) )
					continue;

				// Find all options equivalent to this one
				var equivalentOptions = new List<Option>();

				for ( int j = i + 1; j < allOptions.Count; j++ )
				{
					Option candidateOption = allOptions[j];

					// Skip if already processed
					if ( processedOptions.Contains(candidateOption.Guid) )
						continue;

					// Check if they have overlapping instructions
					int overlapScore = CalculateOptionInstructionOverlap(primaryOption, candidateOption);

					if ( overlapScore > 0 )
					{
						equivalentOptions.Add(candidateOption);
					}
				}

				// If we found duplicates, consolidate them
				if ( equivalentOptions.Count > 0 )
				{
					Logger.LogVerbose($"[AutoInstructionGenerator] Found {equivalentOptions.Count} duplicate option(s) equivalent to '{primaryOption.Name}'");

					// Merge all instructions from duplicates into primary
					foreach ( Option duplicate in equivalentOptions )
					{
						int addedCount = AddMissingInstructionsToOption(primaryOption, duplicate);
						if ( addedCount > 0 )
						{
							Logger.LogVerbose($"[AutoInstructionGenerator] Merged {addedCount} instruction(s) from duplicate option '{duplicate.Name}' into '{primaryOption.Name}'");
						}

						// Update all Choose instructions to reference the primary option instead of the duplicate
						ReplaceOptionGuidInChooseInstructions(component, duplicate.Guid, primaryOption.Guid);

						// Mark as processed
						processedOptions.Add(duplicate.Guid);

						// Remove the duplicate option
						component.Options.Remove(duplicate);
						removedCount++;

						Logger.LogVerbose($"[AutoInstructionGenerator] Removed duplicate option '{duplicate.Name}' (GUID: {duplicate.Guid})");
					}

					Logger.LogVerbose($"[AutoInstructionGenerator] Consolidated {equivalentOptions.Count} duplicate(s) into option '{primaryOption.Name}' (GUID: {primaryOption.Guid})");
				}

				// Mark primary as processed
				processedOptions.Add(primaryOption.Guid);
			}

			return removedCount;
		}

		/// <summary>
		/// Replaces all occurrences of oldGuid with newGuid in all Choose instructions.
		/// </summary>
		private static void ReplaceOptionGuidInChooseInstructions([NotNull] ModComponent component, Guid oldGuid, Guid newGuid)
		{
			string oldGuidStr = oldGuid.ToString();
			string newGuidStr = newGuid.ToString();
			int replacementCount = 0;

			foreach ( Instruction instruction in component.Instructions )
			{
				if ( instruction.Action != Instruction.ActionType.Choose )
					continue;

				// Check if this Choose instruction contains the old GUID
				bool found = false;
				int indexToReplace = -1;

				for ( int i = 0; i < instruction.Source.Count; i++ )
				{
					if ( string.Equals(instruction.Source[i], oldGuidStr, StringComparison.OrdinalIgnoreCase) )
					{
						indexToReplace = i;
						found = true;
						break;
					}
				}

				if ( found )
				{
					// Check if new GUID already exists in this Choose instruction
					bool newGuidExists = instruction.Source.Any(guid =>
						string.Equals(guid, newGuidStr, StringComparison.OrdinalIgnoreCase));

					if ( newGuidExists )
					{
						// New GUID already exists, just remove the old one
						instruction.Source.RemoveAt(indexToReplace);
						Logger.LogVerbose($"[AutoInstructionGenerator] Removed duplicate GUID {oldGuid} from Choose instruction (kept {newGuid})");
					}
					else
					{
						// Replace old GUID with new GUID
						instruction.Source[indexToReplace] = newGuidStr;
						replacementCount++;
						Logger.LogVerbose($"[AutoInstructionGenerator] Replaced GUID {oldGuid} with {newGuid} in Choose instruction");
					}
				}
			}

			if ( replacementCount > 0 )
			{
				Logger.LogVerbose($"[AutoInstructionGenerator] Updated {replacementCount} Choose instruction(s) to reference consolidated option");
			}
		}

		/// <summary>
		/// Calculates how many instructions overlap between two options.
		/// Returns the count of matching instructions.
		/// </summary>
		private static int CalculateOptionInstructionOverlap([NotNull] Option existing, [NotNull] Option potential)
		{
			int matchCount = 0;

			foreach ( Instruction potentialInstr in potential.Instructions )
			{
				foreach ( Instruction existingInstr in existing.Instructions )
				{
					if ( AreInstructionsEquivalent(existingInstr, potentialInstr) )
					{
						matchCount++;
						break;
					}
				}
			}

			return matchCount;
		}

		/// <summary>
		/// Checks if two options have exactly the same instructions (ignoring Name/Description).
		/// This is a stricter check than FindEquivalentOption.
		/// </summary>
		private static bool AreOptionsEquivalentByInstructions([NotNull] Option existing, [NotNull] Option potential)
		{
			// If instruction counts don't match, they're not equivalent
			if ( existing.Instructions.Count != potential.Instructions.Count )
				return false;

			// Check if every potential instruction has an equivalent in existing option
			foreach ( Instruction potentialInstr in potential.Instructions )
			{
				bool foundMatch = false;
				foreach ( Instruction existingInstr in existing.Instructions )
				{
					if ( AreInstructionsEquivalent(existingInstr, potentialInstr) )
					{
						foundMatch = true;
						break;
					}
				}
				if ( !foundMatch )
					return false;
			}

			// Check reverse: every existing instruction should have an equivalent in potential
			foreach ( Instruction existingInstr in existing.Instructions )
			{
				bool foundMatch = false;
				foreach ( Instruction potentialInstr in potential.Instructions )
				{
					if ( AreInstructionsEquivalent(existingInstr, potentialInstr) )
					{
						foundMatch = true;
						break;
					}
				}
				if ( !foundMatch )
					return false;
			}

			return true;
		}

		/// <summary>
		/// Adds missing instructions from potentialOption to existingOption.
		/// </summary>
		/// <returns>Count of instructions added</returns>
		private static int AddMissingInstructionsToOption([NotNull] Option existingOption, [NotNull] Option potentialOption)
		{
			int addedCount = 0;

			foreach ( Instruction potentialInstr in potentialOption.Instructions )
			{
				bool alreadyExists = existingOption.Instructions.Any(existingInstr =>
					AreInstructionsEquivalent(existingInstr, potentialInstr));

				if ( !alreadyExists )
				{
					// Create a copy with a new GUID and add it
					var newInstruction = new Instruction
					{
						Guid = Guid.NewGuid(),
						Action = potentialInstr.Action,
						Source = new List<string>(potentialInstr.Source),
						Destination = potentialInstr.Destination,
						Arguments = potentialInstr.Arguments,
						Overwrite = potentialInstr.Overwrite,
						Dependencies = new List<Guid>(potentialInstr.Dependencies),
						Restrictions = new List<Guid>(potentialInstr.Restrictions)
					};
					newInstruction.SetParentComponent(existingOption);
					existingOption.Instructions.Add(newInstruction);
					addedCount++;
				}
			}

			return addedCount;
		}

		/// <summary>
		/// Finds an existing Choose instruction that could contain the given options.
		/// </summary>
		private static Instruction FindCompatibleChooseInstruction([NotNull] ModComponent component)
		{
			return component.Instructions.FirstOrDefault(instr => instr.Action == Instruction.ActionType.Choose);
		}

		/// <summary>
		/// Adds an option GUID to an existing Choose instruction if it's not already there.
		/// </summary>
		/// <returns>True if the GUID was added, false if it already existed</returns>
		private static bool AddOptionToChooseInstruction([NotNull] Instruction chooseInstruction, [NotNull] string optionGuid)
		{
			if ( chooseInstruction.Action != Instruction.ActionType.Choose )
			{
				Logger.LogWarning($"[AutoInstructionGenerator] Attempted to add option GUID to non-Choose instruction");
				return false;
			}

			// Check if GUID already exists in the Choose instruction
			if ( chooseInstruction.Source.Any(guid => string.Equals(guid, optionGuid, StringComparison.OrdinalIgnoreCase)) )
			{
				return false;
			}

			chooseInstruction.Source.Add(optionGuid);
			return true;
		}

		public static bool GenerateInstructions([NotNull] ModComponent component, [NotNull] string archivePath)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof(component));
			if ( string.IsNullOrWhiteSpace(archivePath) )
				throw new ArgumentException("Archive path cannot be null or empty", nameof(archivePath));
			if ( !File.Exists(archivePath) )
				return false;

			try
			{
				(IArchive archive, FileStream stream) = ArchiveHelper.OpenArchive(archivePath);
				if ( archive is null || stream is null )
					return false;

				using ( stream )
				using ( archive )
				{
					ArchiveAnalysis analysis = AnalyzeArchive(archive);
					return GenerateAllInstructions(component, archivePath, analysis);
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Failed to generate instructions for {archivePath}");

				// Check if this is a corrupted archive exception
				if ( IsCorruptedArchiveException(ex) )
				{
					Logger.LogWarning($"[AutoInstructionGenerator] Detected corrupted archive: {archivePath}");
					Logger.LogWarning($"[AutoInstructionGenerator] Deleting corrupted archive...");

					try
					{
						// Delete the corrupted file
						File.Delete(archivePath);
						Logger.LogVerbose($"[AutoInstructionGenerator] Deleted corrupted archive: {archivePath}");
						Logger.LogVerbose($"[AutoInstructionGenerator] Will create placeholder Extract instruction instead");
					}
					catch ( Exception deleteEx )
					{
						Logger.LogError($"[AutoInstructionGenerator] Failed to delete corrupted archive: {deleteEx.Message}");
					}
				}

				return false;
			}
		}

		/// <summary>
		/// Checks if an exception indicates a corrupted archive.
		/// </summary>
		private static bool IsCorruptedArchiveException(Exception ex)
		{
			// Check for known corruption indicators
			string exceptionType = ex.GetType().Name;
			string message = ex.Message?.ToLowerInvariant() ?? string.Empty;

			// SharpCompress ArchiveException indicators
			if ( exceptionType.Contains("ArchiveException") )
				return true;

			// InvalidOperationException from SharpCompress (7z corruption)
			if ( exceptionType == "InvalidOperationException" )
			{
				// Specific 7z corruption messages from SharpCompress
				if ( message.Contains("nextheaderoffset") ||
					 message.Contains("header offset") ||
					 message.Contains("invalid") )
				{
					return true;
				}
			}

			// Common corruption error messages
			if ( message.Contains("failed to locate") ||
				 message.Contains("zip header") ||
				 message.Contains("corrupt") ||
				 message.Contains("invalid archive") ||
				 message.Contains("unexpected end") ||
				 message.Contains("damaged") ||
				 message.Contains("cannot read") ||
				 message.Contains("invalid header") ||
				 message.Contains("bad archive") ||
				 message.Contains("crc mismatch") ||
				 message.Contains("data error") )
			{
				return true;
			}

			return false;
		}
		private static bool GenerateAllInstructions(ModComponent component, string archivePath, ArchiveAnalysis analysis)
		{
			string archiveFileName = Path.GetFileName(archivePath);
			string extractedPath = archiveFileName.Replace(Path.GetExtension(archiveFileName), "");

			// Generate Extract instruction if needed
			if ( analysis.HasTslPatchData || analysis.HasSimpleOverrideFiles )
			{
				var extractInstruction = new Instruction
				{
					Guid = Guid.NewGuid(),
					Action = Instruction.ActionType.Extract,
					Source = new List<string> { $@"<<modDirectory>>\{archiveFileName}" },
					Overwrite = true
				};
				extractInstruction.SetParentComponent(component);

				// Only add if it doesn't already exist
				if ( !InstructionAlreadyExists(component, extractInstruction) )
				{
					component.Instructions.Add(extractInstruction);
					Logger.LogVerbose($"[AutoInstructionGenerator] Added Extract instruction for '{archiveFileName}'");
				}
				else
				{
					Logger.LogVerbose($"[AutoInstructionGenerator] Extract instruction for '{archiveFileName}' already exists, skipping");
				}
			}
			else
			{
				// No recognizable content in archive
				return false;
			}

			if ( analysis.HasTslPatchData )
			{
				if ( analysis.HasNamespacesIni )
					AddNamespacesChooseInstructions(component, archivePath, analysis, extractedPath);
				else if ( analysis.HasChangesIni )
					AddSimplePatcherInstruction(component, analysis, extractedPath);
			}

			if ( analysis.HasSimpleOverrideFiles )
			{
				// Find folders with files that aren't TSLPatcher folders
				var overrideFolders = analysis.FoldersWithFiles
					.Where(f => !IsTslPatcherFolder(f, analysis))
					.ToList();

				if ( overrideFolders.Count > 1 )
					// Multiple folders - create Choose instruction with options
					AddMultiFolderChooseInstructions(component, extractedPath, overrideFolders);
				else if ( overrideFolders.Count == 1 )
					// Single folder - create simple Move instruction
					AddSimpleMoveInstruction(component, extractedPath, overrideFolders[0]);
				else if ( analysis.HasFlatFiles )
					// Flat files in archive root - create simple Move instruction
					AddSimpleMoveInstruction(component, extractedPath, null);
			}

			if ( analysis.HasTslPatchData && analysis.HasSimpleOverrideFiles )
				component.InstallationMethod = "Hybrid (TSLPatcher + Loose Files)";
			else if ( analysis.HasTslPatchData )
				component.InstallationMethod = "TSLPatcher";
			else if ( analysis.HasSimpleOverrideFiles )
				component.InstallationMethod = "Loose-File Mod";

			// Consolidate any duplicate options that may have been created
			int consolidatedCount = ConsolidateDuplicateOptions(component);
			if ( consolidatedCount > 0 )
			{
				Logger.LogVerbose($"[AutoInstructionGenerator] Consolidated and removed {consolidatedCount} duplicate option(s)");
			}

			return component.Instructions.Count > 0;
		}

		/// <summary>
		/// Pre-resolves URLs and generates instructions with comprehensive analysis if files exist on disk.
		/// Does NOT download files - only generates instructions for files that already exist.
		/// </summary>
		public static async Task<bool> GenerateInstructionsFromUrlsAsync(
			[NotNull] ModComponent component,
			[NotNull] DownloadCacheService downloadCache,
			CancellationToken cancellationToken = default)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof(component));
			if ( downloadCache is null )
				throw new ArgumentNullException(nameof(downloadCache));

			if ( component.ModLink == null || component.ModLink.Count == 0 )
			{
				Logger.LogVerbose($"[AutoInstructionGenerator] Component '{component.Name}' has no URLs to process");
				return false;
			}

			try
			{
				Logger.LogVerbose($"[AutoInstructionGenerator] Pre-resolving URLs for component: {component.Name}");

				var resolvedUrls = await downloadCache.PreResolveUrlsAsync(component, null, cancellationToken);

				if ( resolvedUrls.Count == 0 )
				{
					Logger.LogVerbose($"[AutoInstructionGenerator] No URLs resolved for component: {component.Name}");
					return false;
				}

				Logger.LogVerbose($"[AutoInstructionGenerator] Resolved {resolvedUrls.Count} URLs");

				// Check if we have a source directory to look for files
				if ( MainConfig.SourcePath == null || !MainConfig.SourcePath.Exists )
				{
					Logger.LogVerbose($"[AutoInstructionGenerator] No source directory configured, creating placeholder instructions");

					// Create placeholder instructions for each resolved filename (only if not already present)
					foreach ( var kvp in resolvedUrls )
					{
						List<string> filenames = kvp.Value;
						if ( filenames.Count == 0 )
							continue;

						string fileName = filenames[0];

						// Create the potential instruction to check if it already exists
						Instruction potentialInstruction = CreatePlaceholderInstructionObject(component, fileName);

						// Only add if it doesn't already exist
						if ( !InstructionAlreadyExists(component, potentialInstruction) )
						{
							component.Instructions.Add(potentialInstruction);
							Logger.LogVerbose($"[AutoInstructionGenerator] Added placeholder instruction for '{fileName}'");
						}
						else
						{
							Logger.LogVerbose($"[AutoInstructionGenerator] Placeholder instruction for '{fileName}' already exists, skipping");
						}
					}

					return component.Instructions.Count > 0;
				}

				// Try to find files on disk and do comprehensive analysis
				foreach ( var kvp in resolvedUrls )
				{
					List<string> filenames = kvp.Value;
					if ( filenames.Count == 0 )
						continue;

					string fileName = filenames[0];
					string filePath = Path.Combine(MainConfig.SourcePath.FullName, fileName);

					if ( File.Exists(filePath) )
					{
						// File exists on disk - do comprehensive analysis
						Logger.LogVerbose($"[AutoInstructionGenerator] Found '{fileName}' on disk, performing comprehensive analysis");

						bool isArchive = DownloadCacheService.IsArchive(fileName);
						if ( isArchive )
						{
							// Do comprehensive archive analysis (it will check for existing instructions before adding)
							bool generated = GenerateInstructions(component, filePath);
							if ( !generated )
							{
								// Re-check if file still exists (it may have been deleted if corrupted)
								bool fileStillExists = File.Exists(filePath);

								if ( !fileStillExists )
								{
									Logger.LogVerbose($"[AutoInstructionGenerator] Corrupted file '{fileName}' has been deleted, creating placeholder instruction");
								}
								else
								{
									Logger.LogVerbose($"[AutoInstructionGenerator] Comprehensive analysis failed for '{fileName}', creating placeholder Extract instruction");
								}

								// Create potential instruction and check if it exists
								Instruction potentialInstruction = CreatePlaceholderInstructionObject(component, fileName);
								if ( !InstructionAlreadyExists(component, potentialInstruction) )
								{
									component.Instructions.Add(potentialInstruction);
									Logger.LogVerbose($"[AutoInstructionGenerator] Added placeholder Extract instruction for '{fileName}'");
								}
								else
								{
									Logger.LogVerbose($"[AutoInstructionGenerator] Placeholder instruction for '{fileName}' already exists, skipping");
								}
							}
						}
						else
						{
							// Not an archive - just create Move instruction
							Logger.LogVerbose($"[AutoInstructionGenerator] '{fileName}' is not an archive, creating Move instruction");

							// Create potential instruction and check if it exists
							Instruction potentialInstruction = CreatePlaceholderInstructionObject(component, fileName);
							if ( !InstructionAlreadyExists(component, potentialInstruction) )
							{
								component.Instructions.Add(potentialInstruction);
								Logger.LogVerbose($"[AutoInstructionGenerator] Added Move instruction for '{fileName}'");
							}
							else
							{
								Logger.LogVerbose($"[AutoInstructionGenerator] Move instruction for '{fileName}' already exists, skipping");
							}
						}
					}
					else
					{
						// File doesn't exist yet - create placeholder instruction
						Logger.LogVerbose($"[AutoInstructionGenerator] '{fileName}' not found on disk, creating placeholder instruction");

						// Create potential instruction and check if it exists
						Instruction potentialInstruction = CreatePlaceholderInstructionObject(component, fileName);
						if ( !InstructionAlreadyExists(component, potentialInstruction) )
						{
							component.Instructions.Add(potentialInstruction);
							Logger.LogVerbose($"[AutoInstructionGenerator] Added placeholder instruction for '{fileName}'");
						}
						else
						{
							Logger.LogVerbose($"[AutoInstructionGenerator] Placeholder instruction for '{fileName}' already exists, skipping");
						}
					}
				}

				// Consolidate any duplicate options that may have been created or already existed
				int consolidatedCount = ConsolidateDuplicateOptions(component);
				if ( consolidatedCount > 0 )
				{
					Logger.LogVerbose($"[AutoInstructionGenerator] Consolidated and removed {consolidatedCount} duplicate option(s)");
				}

				Logger.LogVerbose($"[AutoInstructionGenerator] Generated {component.Instructions.Count} instructions for component: {component.Name}");
				return component.Instructions.Count > 0;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Failed to generate instructions from URLs for component: {component.Name}");
				return false;
			}
		}

		/// <summary>
		/// Creates a placeholder Extract or Move instruction object (does NOT add it to the component).
		/// </summary>
		/// <param name="component">The component this instruction belongs to</param>
		/// <param name="fileName">The filename for the instruction</param>
		/// <returns>A new Instruction object with parent component set</returns>
		private static Instruction CreatePlaceholderInstructionObject([NotNull] ModComponent component, [NotNull] string fileName)
		{
			bool isArchive = DownloadCacheService.IsArchive(fileName);

			var instruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = isArchive ? Instruction.ActionType.Extract : Instruction.ActionType.Move,
				Source = new List<string> { $@"<<modDirectory>>\{fileName}" },
				Destination = isArchive ? string.Empty : @"<<kotorDirectory>>\Override",
				Overwrite = true
			};
			instruction.SetParentComponent(component);

			return instruction;
		}

		private static readonly char[] pathSeparators = new[] { '/', '\\' };

		private static bool IsTslPatcherFolder(string folderName, ArchiveAnalysis analysis)
		{
			if ( string.IsNullOrEmpty(folderName) )
				return false;

			if ( folderName.Equals("tslpatchdata", StringComparison.OrdinalIgnoreCase) )
				return true;

			if ( string.IsNullOrEmpty(analysis.TslPatcherPath) )
				return false;
			string[] pathParts = analysis.TslPatcherPath.Split(pathSeparators, StringSplitOptions.RemoveEmptyEntries);
			if ( pathParts.Length > 0 && pathParts[0].Equals(folderName, StringComparison.OrdinalIgnoreCase) )
				return true;

			return false;
		}

		private static void AddNamespacesChooseInstructions(ModComponent component, string archivePath, ArchiveAnalysis analysis, string extractedPath)
		{
			Dictionary<string, Dictionary<string, string>> namespaces =
				IniHelper.ReadNamespacesIniFromArchive(archivePath);

			if ( namespaces == null ||
				 !namespaces.TryGetValue("Namespaces", out Dictionary<string, string> value) )
			{
				return;
			}

			var optionGuidsToAdd = new List<string>();

			foreach ( string ns in value.Values )
			{
				if ( !namespaces.TryGetValue(ns, out Dictionary<string, string> namespaceData) )
					continue;

				// Create potential option with its instructions
				var potentialOption = new Option
				{
					Guid = Guid.NewGuid(),
					Name = namespaceData.TryGetValue("Name", out string value2) ? value2 : ns,
					Description = namespaceData.TryGetValue("Description", out string value3) ? value3 : string.Empty,
					IsSelected = false
				};

				string iniName = namespaceData.TryGetValue("IniName", out string value4) ? value4 : "changes.ini";
				string patcherPath = string.IsNullOrEmpty(analysis.TslPatcherPath)
					? extractedPath
					: analysis.TslPatcherPath;

				string executableName = string.IsNullOrEmpty(analysis.PatcherExecutable)
					? "TSLPatcher.exe"
					: Path.GetFileName(analysis.PatcherExecutable);

				var patcherInstruction = new Instruction
				{
					Guid = Guid.NewGuid(),
					Action = Instruction.ActionType.Patcher,
					Source = new List<string> { $@"<<modDirectory>>\{patcherPath}\{executableName}" },
					Destination = "<<kotorDirectory>>",
					Arguments = iniName,
					Overwrite = true
				};
				patcherInstruction.SetParentComponent(potentialOption);
				potentialOption.Instructions.Add(patcherInstruction);

				// Check if an equivalent option already exists
				Option existingOption = FindEquivalentOption(component, potentialOption);

				if ( existingOption != null )
				{
					// Option with same instructions exists - add missing instructions if any
					int addedCount = AddMissingInstructionsToOption(existingOption, potentialOption);
					if ( addedCount > 0 )
					{
						Logger.LogVerbose($"[AutoInstructionGenerator] Added {addedCount} missing instruction(s) to existing option '{existingOption.Name}'");
					}
					else
					{
						Logger.LogVerbose($"[AutoInstructionGenerator] Option equivalent to '{potentialOption.Name}' already exists as '{existingOption.Name}' with all instructions present");
					}

					// Use the existing option's GUID
					optionGuidsToAdd.Add(existingOption.Guid.ToString());
				}
				else
				{
					// No equivalent option exists - add the new option
					component.Options.Add(potentialOption);
					optionGuidsToAdd.Add(potentialOption.Guid.ToString());
					Logger.LogVerbose($"[AutoInstructionGenerator] Added new option '{potentialOption.Name}' for namespace");
				}
			}

			// Handle Choose instruction
			if ( optionGuidsToAdd.Count > 0 )
			{
				Instruction existingChoose = FindCompatibleChooseInstruction(component);

				if ( existingChoose != null )
				{
					// Add option GUIDs to existing Choose instruction
					int addedGuidCount = 0;
					foreach ( string optionGuid in optionGuidsToAdd )
					{
						if ( AddOptionToChooseInstruction(existingChoose, optionGuid) )
							addedGuidCount++;
					}

					if ( addedGuidCount > 0 )
					{
						Logger.LogVerbose($"[AutoInstructionGenerator] Added {addedGuidCount} option GUID(s) to existing Choose instruction");
					}
					else
					{
						Logger.LogVerbose($"[AutoInstructionGenerator] All namespace option GUIDs already present in existing Choose instruction");
					}
				}
				else
				{
					// No Choose instruction exists - create one
					var chooseInstruction = new Instruction
					{
						Guid = Guid.NewGuid(),
						Action = Instruction.ActionType.Choose,
						Source = optionGuidsToAdd,
						Overwrite = true
					};
					chooseInstruction.SetParentComponent(component);
					component.Instructions.Add(chooseInstruction);
					Logger.LogVerbose($"[AutoInstructionGenerator] Created new Choose instruction with {optionGuidsToAdd.Count} namespace option(s)");
				}
			}

			// Clean up any duplicate options that may have been created
			int consolidatedCount = ConsolidateDuplicateOptions(component);
			if ( consolidatedCount > 0 )
			{
				Logger.LogVerbose($"[AutoInstructionGenerator] Consolidated {consolidatedCount} duplicate namespace option(s)");
			}
		}

		private static void AddSimplePatcherInstruction(ModComponent component, ArchiveAnalysis analysis, string extractedPath)
		{
			string patcherPath = string.IsNullOrEmpty(analysis.TslPatcherPath)
				? extractedPath
				: analysis.TslPatcherPath;

			string executableName = string.IsNullOrEmpty(analysis.PatcherExecutable)
				? "TSLPatcher.exe"
				: Path.GetFileName(analysis.PatcherExecutable);

			var patcherInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.Patcher,
				Source = new List<string> { $@"<<modDirectory>>\{patcherPath}\{executableName}" },
				Destination = "<<kotorDirectory>>",
				Overwrite = true
			};
			patcherInstruction.SetParentComponent(component);

			// Only add if it doesn't already exist
			if ( !InstructionAlreadyExists(component, patcherInstruction) )
			{
				component.Instructions.Add(patcherInstruction);
				Logger.LogVerbose($"[AutoInstructionGenerator] Added Patcher instruction for '{patcherPath}'");
			}
			else
			{
				Logger.LogVerbose($"[AutoInstructionGenerator] Patcher instruction for '{patcherPath}' already exists, skipping");
			}
		}

		private static void AddMultiFolderChooseInstructions(ModComponent component, string extractedPath, List<string> folders)
		{
			var optionGuidsToAdd = new List<string>();

			foreach ( string folder in folders )
			{
				// Create potential option with its instructions
				var potentialOption = new Option
				{
					Guid = Guid.NewGuid(),
					Name = folder,
					Description = $"Install files from {folder} folder",
					IsSelected = false
				};

				var moveInstruction = new Instruction
				{
					Guid = Guid.NewGuid(),
					Action = Instruction.ActionType.Move,
					Source = new List<string> { $@"<<modDirectory>>\{extractedPath}\{folder}\*" },
					Destination = @"<<kotorDirectory>>\Override",
					Overwrite = true
				};
				moveInstruction.SetParentComponent(potentialOption);
				potentialOption.Instructions.Add(moveInstruction);

				// Check if an equivalent option already exists
				Option existingOption = FindEquivalentOption(component, potentialOption);

				if ( existingOption != null )
				{
					// Option with same instructions exists - add missing instructions if any
					int addedCount = AddMissingInstructionsToOption(existingOption, potentialOption);
					if ( addedCount > 0 )
					{
						Logger.LogVerbose($"[AutoInstructionGenerator] Added {addedCount} missing instruction(s) to existing option '{existingOption.Name}'");
					}
					else
					{
						Logger.LogVerbose($"[AutoInstructionGenerator] Option equivalent to '{potentialOption.Name}' already exists as '{existingOption.Name}' with all instructions present");
					}

					// Use the existing option's GUID
					optionGuidsToAdd.Add(existingOption.Guid.ToString());
				}
				else
				{
					// No equivalent option exists - add the new option
					component.Options.Add(potentialOption);
					optionGuidsToAdd.Add(potentialOption.Guid.ToString());
					Logger.LogVerbose($"[AutoInstructionGenerator] Added new option '{potentialOption.Name}' for folder");
				}
			}

			// Handle Choose instruction
			if ( optionGuidsToAdd.Count > 0 )
			{
				Instruction existingChoose = FindCompatibleChooseInstruction(component);

				if ( existingChoose != null )
				{
					// Add option GUIDs to existing Choose instruction
					int addedGuidCount = 0;
					foreach ( string optionGuid in optionGuidsToAdd )
					{
						if ( AddOptionToChooseInstruction(existingChoose, optionGuid) )
							addedGuidCount++;
					}

					if ( addedGuidCount > 0 )
					{
						Logger.LogVerbose($"[AutoInstructionGenerator] Added {addedGuidCount} option GUID(s) to existing Choose instruction");
					}
					else
					{
						Logger.LogVerbose($"[AutoInstructionGenerator] All folder option GUIDs already present in existing Choose instruction");
					}
				}
				else
				{
					// No Choose instruction exists - create one
					var chooseInstruction = new Instruction
					{
						Guid = Guid.NewGuid(),
						Action = Instruction.ActionType.Choose,
						Source = optionGuidsToAdd,
						Overwrite = true
					};
					chooseInstruction.SetParentComponent(component);
					component.Instructions.Add(chooseInstruction);
					Logger.LogVerbose($"[AutoInstructionGenerator] Created new Choose instruction with {optionGuidsToAdd.Count} folder option(s)");
				}
			}

			// Clean up any duplicate options that may have been created
			int consolidatedCount = ConsolidateDuplicateOptions(component);
			if ( consolidatedCount > 0 )
			{
				Logger.LogVerbose($"[AutoInstructionGenerator] Consolidated {consolidatedCount} duplicate folder option(s)");
			}
		}


		private static void AddSimpleMoveInstruction(ModComponent component, string extractedPath, string folderName)
		{
			string sourcePath = string.IsNullOrEmpty(folderName)
				? $@"<<modDirectory>>\{extractedPath}\*"
				: $@"<<modDirectory>>\{extractedPath}\{folderName}\*";

			var moveInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.Move,
				Source = new List<string> { sourcePath },
				Destination = @"<<kotorDirectory>>\Override",
				Overwrite = true
			};
			moveInstruction.SetParentComponent(component);

			// Only add if it doesn't already exist
			if ( !InstructionAlreadyExists(component, moveInstruction) )
			{
				component.Instructions.Add(moveInstruction);
				Logger.LogVerbose($"[AutoInstructionGenerator] Added Move instruction for '{sourcePath}'");
			}
			else
			{
				Logger.LogVerbose($"[AutoInstructionGenerator] Move instruction for '{sourcePath}' already exists, skipping");
			}
		}

		private static ArchiveAnalysis AnalyzeArchive(IArchive archive)
		{
			var analysis = new ArchiveAnalysis();

			foreach ( IArchiveEntry entry in archive.Entries )
			{
				if ( entry.IsDirectory )
					continue;

				string path = entry.Key.Replace('\\', '/');
				string[] pathParts = path.Split('/');

				if ( pathParts.Any(p => p.Equals("tslpatchdata", StringComparison.OrdinalIgnoreCase)) )
				{
					analysis.HasTslPatchData = true;

					string fileName = Path.GetFileName(path);
					if ( fileName.Equals("namespaces.ini", StringComparison.OrdinalIgnoreCase) )
					{
						analysis.HasNamespacesIni = true;
						analysis.TslPatcherPath = GetTslPatcherPath(path);
					}
					else if ( fileName.Equals("changes.ini", StringComparison.OrdinalIgnoreCase) )
					{
						analysis.HasChangesIni = true;
						if ( string.IsNullOrEmpty(analysis.TslPatcherPath) )
							analysis.TslPatcherPath = GetTslPatcherPath(path);
					}
					else if ( fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) )
					{
						if ( string.IsNullOrEmpty(analysis.PatcherExecutable) )
							analysis.PatcherExecutable = path;
					}
				}
				else
				{

					string extension = Path.GetExtension(path).ToLowerInvariant();
					if ( !IsGameFile(extension) )
						continue;
					analysis.HasSimpleOverrideFiles = true;

					if ( pathParts.Length == 1 )
					{

						analysis.HasFlatFiles = true;
					}
					else if ( pathParts.Length >= 2 )
					{

						string topLevelFolder = pathParts[0];
						if ( !analysis.FoldersWithFiles.Contains(topLevelFolder) )
							analysis.FoldersWithFiles.Add(topLevelFolder);
					}
				}
			}
			return analysis;
		}

		private static string GetTslPatcherPath(string iniPath)
		{

			string[] parts = iniPath.Split(pathSeparators, StringSplitOptions.RemoveEmptyEntries);
			for ( int i = 0; i < parts.Length - 1; i++ )
			{
				if ( parts[i].Equals("tslpatchdata", StringComparison.OrdinalIgnoreCase) )
				{

					return string.Join("/", parts.Take(i));
				}
			}
			return string.Empty;
		}

		private static bool IsGameFile(string extension)
		{

			var gameExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				".2da", ".are", ".bik",
				".dds", ".dlg", ".erf",
				".git", ".gui", ".ifo",
				".mod", ".jrl", ".lip",
				".lyt", ".mdl", ".mdx",
				".ncs", ".pth", ".rim",
				".ssf", ".tga", ".tlk",
				".txi", ".tpc", ".utc",
				".utd", ".ute", ".uti",
				".utm", ".utp", ".uts",
				".utw", ".vis", ".wav"
			};

			return gameExtensions.Contains(extension);
		}

		private class ArchiveAnalysis
		{
			public bool HasTslPatchData { get; set; }
			public bool HasNamespacesIni { get; set; }
			public bool HasChangesIni { get; set; }
			public bool HasSimpleOverrideFiles { get; set; }
			public bool HasFlatFiles { get; set; }
			public List<string> FoldersWithFiles { get; set; } = new List<string>();
			public string TslPatcherPath { get; set; } = string.Empty;
			public string PatcherExecutable { get; set; } = string.Empty;
		}
	}
}

