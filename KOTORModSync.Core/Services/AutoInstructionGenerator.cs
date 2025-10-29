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
using KOTORModSync.Core.TSLPatcher;
using KOTORModSync.Core.Utility;

using SharpCompress.Archives;

namespace KOTORModSync.Core.Services
{

	public static class AutoInstructionGenerator
	{
		/// <summary>
		/// Attempts to generate instructions from archive with detailed result information
		/// </summary>
		public static GenerationResult TryGenerateInstructionsFromArchiveDetailed( [NotNull] ModComponent component )
		{
			if (component is null)
				throw new ArgumentNullException( nameof( component ) );

			GenerationResult result = new GenerationResult
			{
				ComponentGuid = component.Guid,
				ComponentName = component.Name,
				Success = false,
				InstructionsGenerated = 0,
				SkipReason = string.Empty
			};

			try
			{
				if (component.Instructions.Count > 0)
				{
					result.SkipReason = "Component already has instructions";
					return result;
				}
				if (component.ModLinkFilenames.Count == 0)
				{
					result.SkipReason = "No mod links available";
					return result;
				}
				if (MainConfig.SourcePath == null || !MainConfig.SourcePath.Exists)
				{
					result.SkipReason = "Source path not configured or doesn't exist";
					return result;
				}

				string firstModLink = component.ModLinkFilenames.Keys.FirstOrDefault();
				if (string.IsNullOrWhiteSpace( firstModLink ))
				{
					result.SkipReason = "No valid mod links found";
					return result;
				}

				string searchTerm = ExtractSearchTermFromModLink( firstModLink, component.Name );
				Logger.LogVerbose( $"[TryGenerateInstructions] Component '{component.Name}': Searching for archive matching '{searchTerm}'" );

				string[] archiveExtensions = { "*.zip", "*.rar", "*.7z", "*.exe" };
				List<FileInfo> allArchives = archiveExtensions
					.SelectMany( ext => MainConfig.SourcePath.GetFiles( ext, SearchOption.TopDirectoryOnly ) )
					.Where( f => f.Exists )
					.ToList();

				if (allArchives.Count == 0)
				{
					result.SkipReason = "No archives found in source directory";
					return result;
				}

				Logger.LogVerbose( $"[TryGenerateInstructions] Component '{component.Name}': Found {allArchives.Count} archives to check" );

				string searchTermLower = searchTerm.ToLowerInvariant().Replace( "-", "" ).Replace( "_", "" ).Replace( " ", "" );
				FileInfo matchingArchive = allArchives
					.OrderByDescending( f =>
					{
						string fileWithoutExt = Path.GetFileNameWithoutExtension( f.Name );
						string fileNameNormalized = fileWithoutExt.ToLowerInvariant().Replace( "-", "" ).Replace( "_", "" ).Replace( " ", "" );
						if (fileNameNormalized.Equals( searchTermLower ))
							return 100;
						if (fileNameNormalized.Contains( searchTermLower ))
							return 50;
						if (searchTermLower.Contains( fileNameNormalized ))
							return 25;
						return 0;
					} )
					.ThenByDescending( f => f.LastWriteTime )
					.FirstOrDefault( f =>
					{
						string fileWithoutExt = Path.GetFileNameWithoutExtension( f.Name );
						string fileNameNormalized = fileWithoutExt.ToLowerInvariant().Replace( "-", "" ).Replace( "_", "" ).Replace( " ", "" );
						return fileNameNormalized.Contains( searchTermLower ) || searchTermLower.Contains( fileNameNormalized );
					} );

				if (matchingArchive == null)
				{
					result.SkipReason = $"No matching archive found for '{searchTerm}'";
					return result;
				}

				Logger.LogVerbose( $"[TryGenerateInstructions] Component '{component.Name}': Selected archive '{matchingArchive.Name}'" );

				int instructionCountBefore = component.Instructions.Count;
				bool generated = GenerateInstructions( component, matchingArchive.FullName );

				if (generated)
				{
					component.IsDownloaded = true;
					result.Success = true;
					result.InstructionsGenerated = component.Instructions.Count - instructionCountBefore;
					result.SkipReason = string.Empty;
				}
				else
				{
					result.SkipReason = "Failed to generate instructions from archive";
				}

				return result;
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, $"Failed to auto-generate instructions for component '{component.Name}'" );
				result.SkipReason = $"Error: {ex.Message}";
				return result;
			}
		}

		private static string ExtractSearchTermFromModLink( string firstModLink, string componentName )
		{
			string searchTerm;
			if (firstModLink.Contains( "://" ))
			{
				Uri uri = new Uri( firstModLink );
				string lastSegment = uri.Segments.LastOrDefault()?.TrimEnd( '/' ) ?? string.Empty;
				if (!string.IsNullOrEmpty( lastSegment ) && lastSegment.Contains( '-' ))
				{
					Match match = Regex.Match( lastSegment, @"^\d+-(.+)$" );
					searchTerm = match.Success ? match.Groups[1].Value : lastSegment;
				}
				else
				{
					searchTerm = lastSegment;
				}
				if (Path.HasExtension( searchTerm ))
				{
					searchTerm = Path.GetFileNameWithoutExtension( searchTerm );
				}
			}
			else
			{
				string fileName = Path.GetFileName( firstModLink );
				searchTerm = Path.HasExtension( fileName ) ? Path.GetFileNameWithoutExtension( fileName ) : fileName;
			}
			if (string.IsNullOrWhiteSpace( searchTerm ))
			{
				searchTerm = componentName;
			}
			return searchTerm;
		}

		public static bool TryGenerateInstructionsFromArchive( [NotNull] ModComponent component )
		{
			if (component is null)
				throw new ArgumentNullException( nameof( component ) );

			try
			{
				if (component.Instructions.Count > 0)
					return false;
				if (component.ModLinkFilenames.Count == 0)
					return false;
				if (MainConfig.SourcePath == null || !MainConfig.SourcePath.Exists)
					return false;
				string firstModLink = component.ModLinkFilenames.Keys.FirstOrDefault();
				if (string.IsNullOrWhiteSpace( firstModLink ))
					return false;
				string searchTerm;
				if (firstModLink.Contains( "://" ))
				{
					Uri uri = new Uri( firstModLink );
					string lastSegment = uri.Segments.LastOrDefault()?.TrimEnd( '/' ) ?? string.Empty;
					if (!string.IsNullOrEmpty( lastSegment ) && lastSegment.Contains( '-' ))
					{
						Match match = Regex.Match( lastSegment, @"^\d+-(.+)$" );
						searchTerm = match.Success ? match.Groups[1].Value : lastSegment;
					}
					else
					{
						searchTerm = lastSegment;
					}
					if (Path.HasExtension( searchTerm ))
					{
						searchTerm = Path.GetFileNameWithoutExtension( searchTerm );
					}
				}
				else
				{
					string fileName = Path.GetFileName( firstModLink );
					searchTerm = Path.HasExtension( fileName ) ? Path.GetFileNameWithoutExtension( fileName ) : fileName;
				}
				if (string.IsNullOrWhiteSpace( searchTerm ))
				{
					searchTerm = component.Name;
				}
				Logger.LogVerbose( $"[TryGenerateInstructions] Component '{component.Name}': Searching for archive matching '{searchTerm}'" );
				string[] archiveExtensions = { "*.zip", "*.rar", "*.7z", "*.exe" };
				List<FileInfo> allArchives = archiveExtensions
						.SelectMany( ext => MainConfig.SourcePath.GetFiles( ext, SearchOption.TopDirectoryOnly ) )
						.Where( f => f.Exists )
						.ToList();
				if (allArchives.Count == 0)
				{
					Logger.LogVerbose( $"[TryGenerateInstructions] Component '{component.Name}': No archives found in directory" );
					return false;
				}
				Logger.LogVerbose( $"[TryGenerateInstructions] Component '{component.Name}': Found {allArchives.Count} archives to check" );
				string searchTermLower = searchTerm.ToLowerInvariant().Replace( "-", "" ).Replace( "_", "" ).Replace( " ", "" );
				FileInfo matchingArchive = allArchives
					.OrderByDescending( f =>
					{
						string fileWithoutExt = Path.GetFileNameWithoutExtension( f.Name );
						string fileNameNormalized = fileWithoutExt.ToLowerInvariant().Replace( "-", "" ).Replace( "_", "" ).Replace( " ", "" );
						if (fileNameNormalized.Equals( searchTermLower ))
							return 100;
						if (fileNameNormalized.Contains( searchTermLower ))
							return 50;
						if (searchTermLower.Contains( fileNameNormalized ))
							return 25;
						return 0;
					} )
					.ThenByDescending( f => f.LastWriteTime )
					.FirstOrDefault( f =>
					{
						string fileWithoutExt = Path.GetFileNameWithoutExtension( f.Name );
						string fileNameNormalized = fileWithoutExt.ToLowerInvariant().Replace( "-", "" ).Replace( "_", "" ).Replace( " ", "" );
						return fileNameNormalized.Contains( searchTermLower ) || searchTermLower.Contains( fileNameNormalized );
					} );
				if (matchingArchive == null)
				{
					Logger.LogVerbose( $"[TryGenerateInstructions] Component '{component.Name}': No matching archive found for '{searchTerm}'" );
					return false;
				}
				Logger.LogVerbose( $"[TryGenerateInstructions] Component '{component.Name}': Selected archive '{matchingArchive.Name}'" );
				bool generated = GenerateInstructions( component, matchingArchive.FullName );
				if (generated)
				{
					component.IsDownloaded = true;
				}
				return generated;
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, $"Failed to auto-generate instructions for component '{component.Name}'" );
				return false;
			}
		}

		private static bool IsRemoveDuplicateTgaTpcMod( [NotNull] ModComponent component )
		{
			if (component.Name.Equals( "Remove Duplicate TGA/TPC", StringComparison.OrdinalIgnoreCase ))
			{
				return true;
			}

			if (!string.IsNullOrEmpty( component.Author ))
			{
				string authorLower = component.Author.ToLowerInvariant();
				if (authorLower.Contains( "flachzangen" ) && authorLower.Contains( "th3w1zard1" ))
				{
					return true;
				}
			}

			if (component.ModLinkFilenames.Count > 0)
			{
				foreach (string link in component.ModLinkFilenames.Keys)
				{
					if (string.IsNullOrEmpty( link ))
						continue;

					string linkLower = link.ToLowerInvariant();
					if (linkLower.Contains( "nexusmods.com/kotor/mods/1384" ) ||
						 linkLower.Contains( "pastebin.com/6wcn122s" ))
					{
						return true;
					}
				}
			}

			return false;
		}

		private static bool GenerateDelDuplicateInstruction( [NotNull] ModComponent component )
		{
			Instruction delDuplicateInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.DelDuplicate,
				Source = new List<string>(),
				Arguments = ".tpc",
				Overwrite = true
			};
			delDuplicateInstruction.SetParentComponent( component );

			if (!InstructionAlreadyExists( component, delDuplicateInstruction ))
			{
				component.Instructions.Add( delDuplicateInstruction );
				Logger.LogVerbose( "[AutoInstructionGenerator] Added DelDuplicate instruction for Remove Duplicate TGA/TPC mod" );
				return true;
			}
			else
			{
				Logger.LogVerbose( "[AutoInstructionGenerator] DelDuplicate instruction already exists for Remove Duplicate TGA/TPC mod" );
				return true;
			}
		}

		private static bool GenerateExecuteInstruction(
			[NotNull] ModComponent component,
			[NotNull] string exePath
		)
		{
			string fileName = Path.GetFileName( exePath );

			Instruction executeInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.Execute,
				Source = new List<string> { $@"<<modDirectory>>\{fileName}" },
				Overwrite = true
			};
			executeInstruction.SetParentComponent( component );

			if (!InstructionAlreadyExists( component, executeInstruction ))
			{
				component.Instructions.Add( executeInstruction );
				Logger.LogVerbose( $"[AutoInstructionGenerator] Added Execute instruction for '{fileName}'" );
				component.InstallationMethod = "Executable Installer";
				return true;
			}
			else
			{
				Logger.LogVerbose( $"[AutoInstructionGenerator] Execute instruction for '{fileName}' already exists, skipping" );
				return true;
			}
		}

		private static bool AreInstructionsEquivalent(
			[NotNull] Instruction existing,
			[NotNull] Instruction potential
		)
		{
			if (existing.Action != potential.Action)
				return false;

			if (!AreSourcesEquivalent( existing.Source, potential.Source ))
				return false;

			if (existing.ShouldSerializeDestination())
			{
				if (!AreDestinationsEquivalent( existing.Destination, potential.Destination ))
					return false;
			}

			if (existing.ShouldSerializeArguments())
			{
				if (!string.Equals( existing.Arguments, potential.Arguments, StringComparison.OrdinalIgnoreCase ))
					return false;
			}

			if (existing.ShouldSerializeOverwrite())
			{
				if (existing.Overwrite != potential.Overwrite)
					return false;
			}

			return true;
		}

		private static bool AreSourcesEquivalent( [NotNull] List<string> existingSources, [NotNull] List<string> potentialSources )
		{
			if (existingSources.Count == 0 && potentialSources.Count == 0)
				return true;

			if (existingSources.Count == 0 || potentialSources.Count == 0)
				return false;


			foreach (string potentialSource in potentialSources)
			{
				bool foundMatch = false;
				foreach (string existingSource in existingSources)
				{
					if (DoSourcesMatch( existingSource, potentialSource ))
					{
						foundMatch = true;
						break;
					}
				}
				if (!foundMatch)
					return false;
			}

			return true;
		}

		private static bool DoSourcesMatch(
			[NotNull] string existing,
			[NotNull] string potential
		)
		{
			string existingNormalized = NormalizePathForComparison( existing );
			string potentialNormalized = NormalizePathForComparison( potential );

			if (string.Equals( existingNormalized, potentialNormalized, StringComparison.OrdinalIgnoreCase ))
				return true;

			bool existingHasWildcards = ContainsWildcards( existingNormalized );
			bool potentialHasWildcards = ContainsWildcards( potentialNormalized );

			if (existingHasWildcards && potentialHasWildcards)
			{
				if (DoWildcardPatternsOverlap( existingNormalized, potentialNormalized ))
					return true;
			}
			else if (existingHasWildcards)
			{
				try
				{
					if (PathHelper.WildcardPathMatch( potentialNormalized, existingNormalized ))
						return true;
				}
				catch (Exception ex)
				{
					Logger.LogException( ex );
				}
			}
			else if (potentialHasWildcards)
			{
				try
				{
					if (PathHelper.WildcardPathMatch( existingNormalized, potentialNormalized ))
						return true;
				}
				catch (Exception ex)
				{
					Logger.LogException( ex );
				}
			}

			string existingFilename = Path.GetFileName( existingNormalized );
			string potentialFilename = Path.GetFileName( potentialNormalized );

			bool existingFilenameHasWildcards = ContainsWildcards( existingFilename );
			bool potentialFilenameHasWildcards = ContainsWildcards( potentialFilename );

			if (existingFilenameHasWildcards && potentialFilenameHasWildcards)
			{
				return false;
			}

			if (!existingFilenameHasWildcards && !potentialFilenameHasWildcards)
			{
				if (string.Equals( existingFilename, potentialFilename, StringComparison.OrdinalIgnoreCase ))
					return true;
			}

			if (existingFilenameHasWildcards)
			{
				try
				{
					if (PathHelper.WildcardPathMatch( potentialFilename, existingFilename ))
						return true;
				}
				catch (Exception ex)
				{
					Logger.LogException( ex );
				}
			}

			if (potentialFilenameHasWildcards)
			{
				try
				{
					if (PathHelper.WildcardPathMatch( existingFilename, potentialFilename ))
						return true;
				}
				catch (Exception ex)
				{
					Logger.LogException( ex );
				}
			}

			return false;
		}

		private static bool DoWildcardPatternsOverlap( [NotNull] string pattern1, [NotNull] string pattern2 )
		{
			string[] parts1 = pattern1.Split( new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries );
			string[] parts2 = pattern2.Split( new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries );

			int minParts = Math.Min( parts1.Length, parts2.Length );

			for (int i = 0; i < minParts - 1; i++)
			{
				string part1 = parts1[i];
				string part2 = parts2[i];

				if (string.Equals( part1, part2, StringComparison.OrdinalIgnoreCase ))
					continue;

				if (ContainsWildcards( part1 ) && ContainsWildcards( part2 ))
				{
				}
				else if (ContainsWildcards( part1 ))
				{
					try
					{
						if (!PathHelper.WildcardPathMatch( part2, part1 ))
							return false;
					}
					catch
					{
						return false;
					}
				}
				else if (ContainsWildcards( part2 ))
				{
					try
					{
						if (!PathHelper.WildcardPathMatch( part1, part2 ))
							return false;
					}
					catch
					{
						return false;
					}
				}
				else
				{
					return false;
				}
			}

			string filename1 = parts1[parts1.Length - 1];
			string filename2 = parts2[parts2.Length - 1];


			if (string.Equals( filename1, filename2, StringComparison.OrdinalIgnoreCase ))
				return true;

			if (string.Equals( filename1, "*", StringComparison.Ordinal ) || string.Equals( filename2, "*", StringComparison.Ordinal ))
				return true;

			if (ContainsWildcards( filename1 ) && ContainsWildcards( filename2 ))  // FIXME: incorrect/oversimplified/bogus logic.
			{
				return true;
			}

			if (ContainsWildcards( filename1 ))
			{
				try
				{
					return PathHelper.WildcardPathMatch( filename2, filename1 );
				}
				catch
				{
					return false;
				}
			}

			if (ContainsWildcards( filename2 ))
			{
				try
				{
					return PathHelper.WildcardPathMatch( filename1, filename2 );
				}
				catch
				{
					return false;
				}
			}

			return false;
		}

		private static bool AreDestinationsEquivalent( [CanBeNull] string existing, [CanBeNull] string potential )
		{
			if (string.IsNullOrEmpty( existing ) && string.IsNullOrEmpty( potential ))
				return true;

			if (string.IsNullOrEmpty( existing ) || string.IsNullOrEmpty( potential ))
				return false;

			string existingNormalized = NormalizePathForComparison( existing );
			string potentialNormalized = NormalizePathForComparison( potential );

			if (string.Equals( existingNormalized, potentialNormalized, StringComparison.OrdinalIgnoreCase ))
				return true;

			if (ContainsWildcards( existingNormalized ))
			{
				try
				{
					if (PathHelper.WildcardPathMatch( potentialNormalized, existingNormalized ))
						return true;
				}
				catch (Exception ex)
				{
					Logger.LogException( ex );
				}
			}

			if (ContainsWildcards( potentialNormalized ))
			{
				try
				{
					if (PathHelper.WildcardPathMatch( existingNormalized, potentialNormalized ))
						return true;
				}
				catch (Exception ex)
				{
					Logger.LogException( ex );
				}
			}

			return false;
		}

		private static string NormalizePathForComparison( [NotNull] string path )
		{
			if (string.IsNullOrEmpty( path ))
				return string.Empty;

			string normalized = path
				.Replace( '/', '\\' )
				.TrimEnd( '\\' );

			return normalized;
		}

		private static bool ContainsWildcards( [NotNull] string path )
		{
			return !string.IsNullOrEmpty( path ) && (path.Contains( '*' ) || path.Contains( '?' ));
		}

		private static bool InstructionAlreadyExists( [NotNull] ModComponent component, [NotNull] Instruction potentialInstruction )
		{
			return component.Instructions.Any( existing => AreInstructionsEquivalent( existing, potentialInstruction ) );
		}

		private static Option FindEquivalentOption( [NotNull] ModComponent component, [NotNull] Option potentialOption )
		{
			foreach (Option existingOption in component.Options)
			{
				if (AreOptionsEquivalentByInstructions( existingOption, potentialOption ))
				{
					return existingOption;
				}
			}

			return null;
		}

		private static int ConsolidateDuplicateOptions( [NotNull] ModComponent component )
		{
			int removedCount = 0;
			HashSet<Guid> processedOptions = new HashSet<Guid>();

			List<Option> allOptions = component.Options.ToList();

			for (int i = 0; i < allOptions.Count; i++)
			{
				Option primaryOption = allOptions[i];

				if (processedOptions.Contains( primaryOption.Guid ))
					continue;

				List<Option> equivalentOptions = new List<Option>();

				for (int j = i + 1; j < allOptions.Count; j++)
				{
					Option candidateOption = allOptions[j];

					if (processedOptions.Contains( candidateOption.Guid ))
						continue;

					int overlapScore = CalculateOptionInstructionOverlap( primaryOption, candidateOption );

					if (overlapScore > 0)
					{
						equivalentOptions.Add( candidateOption );
					}
				}

				if (equivalentOptions.Count > 0)
				{
					Logger.LogVerbose( $"[AutoInstructionGenerator] Found {equivalentOptions.Count} duplicate option(s) equivalent to '{primaryOption.Name}'" );

					foreach (Option duplicate in equivalentOptions)
					{
						int addedCount = AddMissingInstructionsToOption( primaryOption, duplicate );
						if (addedCount > 0)
						{
							Logger.LogVerbose( $"[AutoInstructionGenerator] Merged {addedCount} instruction(s) from duplicate option '{duplicate.Name}' into '{primaryOption.Name}'" );
						}

						ReplaceOptionGuidInChooseInstructions( component, duplicate.Guid, primaryOption.Guid );

						processedOptions.Add( duplicate.Guid );

						component.Options.Remove( duplicate );
						removedCount++;

						Logger.LogVerbose( $"[AutoInstructionGenerator] Removed duplicate option '{duplicate.Name}' (GUID: {duplicate.Guid})" );
					}

					Logger.LogVerbose( $"[AutoInstructionGenerator] Consolidated {equivalentOptions.Count} duplicate(s) into option '{primaryOption.Name}' (GUID: {primaryOption.Guid})" );
				}

				processedOptions.Add( primaryOption.Guid );
			}

			return removedCount;
		}

		private static void ReplaceOptionGuidInChooseInstructions(
			[NotNull] ModComponent component,
			Guid oldGuid,
			Guid newGuid
		)
		{
			string oldGuidStr = oldGuid.ToString();
			string newGuidStr = newGuid.ToString();
			int replacementCount = 0;

			foreach (Instruction instruction in component.Instructions)
			{
				if (instruction.Action != Instruction.ActionType.Choose)
					continue;

				bool found = false;
				int indexToReplace = -1;

				for (int i = 0; i < instruction.Source.Count; i++)
				{
					if (string.Equals( instruction.Source[i], oldGuidStr, StringComparison.OrdinalIgnoreCase ))
					{
						indexToReplace = i;
						found = true;
						break;
					}
				}

				if (found)
				{
					bool newGuidExists = instruction.Source.Exists( guid =>
						string.Equals( guid, newGuidStr, StringComparison.OrdinalIgnoreCase ) );

					if (newGuidExists)
					{
						instruction.Source.RemoveAt( indexToReplace );
						Logger.LogVerbose( $"[AutoInstructionGenerator] Removed duplicate GUID {oldGuid} from Choose instruction (kept {newGuid})" );
					}
					else
					{
						instruction.Source[indexToReplace] = newGuidStr;
						replacementCount++;
						Logger.LogVerbose( $"[AutoInstructionGenerator] Replaced GUID {oldGuid} with {newGuid} in Choose instruction" );
					}
				}
			}

			if (replacementCount > 0)
			{
				Logger.LogVerbose( $"[AutoInstructionGenerator] Updated {replacementCount} Choose instruction(s) to reference consolidated option" );
			}
		}

		private static int CalculateOptionInstructionOverlap( [NotNull] Option existing, [NotNull] Option potential )
		{
			int matchCount = 0;

			foreach (Instruction potentialInstr in potential.Instructions)
			{
				foreach (Instruction existingInstr in existing.Instructions)
				{
					if (AreInstructionsEquivalent( existingInstr, potentialInstr ))
					{
						matchCount++;
						break;
					}
				}
			}

			return matchCount;
		}

		private static bool AreOptionsEquivalentByInstructions( [NotNull] Option existing, [NotNull] Option potential )
		{
			if (existing.Instructions.Count != potential.Instructions.Count)
				return false;

			foreach (Instruction potentialInstr in potential.Instructions)
			{
				bool foundMatch = false;
				foreach (Instruction existingInstr in existing.Instructions)
				{
					if (AreInstructionsEquivalent( existingInstr, potentialInstr ))
					{
						foundMatch = true;
						break;
					}
				}
				if (!foundMatch)
					return false;
			}

			foreach (Instruction existingInstr in existing.Instructions)
			{
				bool foundMatch = false;
				foreach (Instruction potentialInstr in potential.Instructions)
				{
					if (AreInstructionsEquivalent( existingInstr, potentialInstr ))
					{
						foundMatch = true;
						break;
					}
				}
				if (!foundMatch)
					return false;
			}

			return true;
		}

		private static bool IsFolderAlreadyCoveredByInstructions(
			[NotNull] ModComponent component,
			[NotNull] string folderSourcePath )
		{
			foreach (Instruction existingInstruction in component.Instructions)
			{
				// Only check Move and Copy instructions - other instruction types (Extract, Patcher, etc.)
				// don't move files to the game directory, so they shouldn't prevent us from adding Move instructions
				if (existingInstruction.Action != Instruction.ActionType.Move &&
					 existingInstruction.Action != Instruction.ActionType.Copy)
				{
					continue;
				}

				foreach (string existingSource in existingInstruction.Source)
				{
					if (DoSourcesMatch( existingSource, folderSourcePath ))
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] Folder path '{folderSourcePath}' is covered by existing {existingInstruction.Action} instruction source '{existingSource}'" );
						return true;
					}

					if (IsParentPathCovering( existingSource, folderSourcePath ))
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] Folder path '{folderSourcePath}' is covered by parent path '{existingSource}' ({existingInstruction.Action} instruction)" );
						return true;
					}
				}
			}

			return false;
		}

		private static bool IsParentPathCovering( [NotNull] string parentPath, [NotNull] string childPath )
		{
			string parentNormalized = NormalizePathForComparison( parentPath );
			string childNormalized = NormalizePathForComparison( childPath );

			string parentWithoutWildcard = parentNormalized.TrimEnd( '*', '\\' );
			string childWithoutWildcard = childNormalized.TrimEnd( '*', '\\' );

			// Check if child path starts with parent path
			if (childWithoutWildcard.StartsWith( parentWithoutWildcard, StringComparison.OrdinalIgnoreCase ))
			{
				// Ensure the parent path ends with a wildcard pattern
				if (parentNormalized.EndsWith( "\\*", StringComparison.Ordinal ) || parentNormalized.EndsWith( "\\*\\*", StringComparison.Ordinal ))
				{
					// Additional validation: ensure the child is actually within the parent directory
					// by checking that the next character after the parent path is a path separator
					if (childWithoutWildcard.Length > parentWithoutWildcard.Length)
					{
						char nextChar = childWithoutWildcard[parentWithoutWildcard.Length];
						if (nextChar == '\\' || nextChar == '/')
						{
							return true;
						}
					}
					// If the child path is exactly the same as the parent path (without wildcard), it's covered
					else if (childWithoutWildcard.Length == parentWithoutWildcard.Length)
					{
						return true;
					}
				}
			}

			return false;
		}

		private static int AddMissingInstructionsToOption( [NotNull] Option existingOption, [NotNull] Option potentialOption )
		{
			int addedCount = 0;

			foreach (Instruction potentialInstr in potentialOption.Instructions)
			{
				bool alreadyExists = existingOption.Instructions.Any( existingInstr =>
					AreInstructionsEquivalent( existingInstr, potentialInstr ) );

				if (!alreadyExists)
				{
					Instruction newInstruction = new Instruction
					{
						Guid = Guid.NewGuid(),
						Action = potentialInstr.Action,
						Source = new List<string>( potentialInstr.Source ),
						Destination = potentialInstr.Destination,
						Arguments = potentialInstr.Arguments,
						Overwrite = potentialInstr.Overwrite,
						Dependencies = new List<Guid>( potentialInstr.Dependencies ),
						Restrictions = new List<Guid>( potentialInstr.Restrictions )
					};
					newInstruction.SetParentComponent( existingOption );
					existingOption.Instructions.Add( newInstruction );
					addedCount++;
				}
			}

			return addedCount;
		}

		private static Instruction FindCompatibleChooseInstruction( [NotNull] ModComponent component )
		{
			return component.Instructions.FirstOrDefault( instr => instr.Action == Instruction.ActionType.Choose );
		}

		private static bool AddOptionToChooseInstruction( [NotNull] Instruction chooseInstruction, [NotNull] string optionGuid )
		{
			if (chooseInstruction.Action != Instruction.ActionType.Choose)
			{
				Logger.LogWarning( "[AutoInstructionGenerator] Attempted to add option GUID to non-Choose instruction" );
				return false;
			}

			if (chooseInstruction.Source.Exists( guid => string.Equals( guid, optionGuid, StringComparison.OrdinalIgnoreCase ) ))
			{
				return false;
			}

			chooseInstruction.Source.Add( optionGuid );
			return true;
		}

		public static bool GenerateInstructions( [NotNull] ModComponent component, [NotNull] string archivePath )
		{
			if (component is null)
				throw new ArgumentNullException( nameof( component ) );
			if (string.IsNullOrWhiteSpace( archivePath ))
				throw new ArgumentException( "Archive path cannot be null or empty", nameof( archivePath ) );
			if (!File.Exists( archivePath ))
				return false;

			if (IsRemoveDuplicateTgaTpcMod( component ))
			{
				Logger.LogVerbose( "[AutoInstructionGenerator] Detected Remove Duplicate TGA/TPC mod, generating DelDuplicate instruction only" );
				return GenerateDelDuplicateInstruction( component );
			}

			string fileExtension = Path.GetExtension( archivePath ).ToLowerInvariant();
			bool isExeFile = string.Equals( fileExtension, ".exe", StringComparison.Ordinal );

			try
			{
				(IArchive archive, FileStream stream) = ArchiveHelper.OpenArchive( archivePath );
				if (archive is null || stream is null)
				{
					if (isExeFile)
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] EXE file '{Path.GetFileName( archivePath )}' is not an extractable archive, creating Execute instruction" );
						return GenerateExecuteInstruction( component, archivePath );
					}
					return false;
				}

				using (stream)
				using (archive)
				{
					ArchiveAnalysis analysis = AnalyzeArchive( archive, archivePath );
					return GenerateAllInstructions( component, archivePath, archive, analysis );
				}
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, $"Failed to generate instructions for {archivePath}" );

				if (IsCorruptedArchiveException( ex ))
				{
					if (isExeFile)
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] EXE file '{Path.GetFileName( archivePath )}' is not a valid archive, creating Execute instruction instead" );
						return GenerateExecuteInstruction( component, archivePath );
					}

					Logger.LogWarning( $"[AutoInstructionGenerator] Detected corrupted archive: {archivePath}" );
					Logger.LogWarning( "[AutoInstructionGenerator] Deleting corrupted archive..." );

					try
					{
						File.Delete( archivePath );
						Logger.LogVerbose( $"[AutoInstructionGenerator] Deleted corrupted archive: {archivePath}" );
						Logger.LogVerbose( "[AutoInstructionGenerator] Will create placeholder Extract instruction instead" );
					}
					catch (Exception deleteEx)
					{
						Logger.LogError( $"[AutoInstructionGenerator] Failed to delete corrupted archive: {deleteEx.Message}" );
					}
				}
				else if (isExeFile)
				{
					Logger.LogVerbose( $"[AutoInstructionGenerator] Failed to extract EXE file '{Path.GetFileName( archivePath )}', creating Execute instruction instead" );
					return GenerateExecuteInstruction( component, archivePath );
				}

				return false;
			}
		}

		private static bool IsCorruptedArchiveException( Exception ex )
		{
			string exceptionType = ex.GetType().Name;
			string message = ex.Message.ToLowerInvariant();

			if (exceptionType.Contains( "ArchiveException" ))
				return true;

			if (string.Equals( exceptionType, "InvalidOperationException", StringComparison.Ordinal ))
			{
				if (message.Contains( "nextheaderoffset" ) ||
					 message.Contains( "header offset" ) ||
					 message.Contains( "invalid" ))
				{
					return true;
				}
			}

			if (message.Contains( "failed to locate" ) ||
				 message.Contains( "zip header" ) ||
				 message.Contains( "corrupt" ) ||
				 message.Contains( "invalid archive" ) ||
				 message.Contains( "unexpected end" ) ||
				 message.Contains( "damaged" ) ||
				 message.Contains( "cannot read" ) ||
				 message.Contains( "invalid header" ) ||
				 message.Contains( "bad archive" ) ||
				 message.Contains( "crc mismatch" ) ||
				 message.Contains( "data error" ))
			{
				return true;
			}

			return false;
		}
		private static ArchiveAnalysis AnalyzeArchiveFromFileList( List<string> fileList )
		{
			ArchiveAnalysis analysis = new ArchiveAnalysis();

			foreach (string path in fileList)
			{
				string normalizedPath = path.Replace( '\\', '/' );
				string[] pathParts = normalizedPath.Split( '/' );

				if (pathParts.Any( p => p.Equals( "tslpatchdata", StringComparison.OrdinalIgnoreCase ) ))
				{
					analysis.HasTslPatchData = true;

					string fileName = Path.GetFileName( normalizedPath );
					if (fileName.Equals( "namespaces.ini", StringComparison.OrdinalIgnoreCase ))
					{
						analysis.HasNamespacesIni = true;
						analysis.TslPatcherPath = GetTslPatcherPath( normalizedPath );
					}
					else if (fileName.Equals( "changes.ini", StringComparison.OrdinalIgnoreCase ))
					{
						analysis.HasChangesIni = true;
						if (string.IsNullOrEmpty( analysis.TslPatcherPath ))
							analysis.TslPatcherPath = GetTslPatcherPath( normalizedPath );
					}
					else if (fileName.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ))
					{
						if (string.IsNullOrEmpty( analysis.PatcherExecutable ))
							analysis.PatcherExecutable = normalizedPath;
					}
				}
				else
				{
					string extension = Path.GetExtension( normalizedPath ).ToLowerInvariant();
					if (!IsGameFile( extension ))
						continue;
					analysis.HasSimpleOverrideFiles = true;

					if (pathParts.Length == 1)
					{
						analysis.HasFlatFiles = true;
					}
					else if (pathParts.Length >= 2)
					{
						string topLevelFolder = pathParts[0];
						if (!analysis.FoldersWithFiles.Contains( topLevelFolder, StringComparer.Ordinal ))
							analysis.FoldersWithFiles.Add( topLevelFolder );
					}
				}
			}

			return analysis;
		}

		private static bool GenerateAllInstructions(
			ModComponent component,
			string archivePath,
			IArchive archive,
			ArchiveAnalysis analysis
		)
		{
			string archiveFileName = Path.GetFileName( archivePath );
			string extractedPath = archiveFileName.Replace( Path.GetExtension( archiveFileName ), "" );

			if (analysis.HasTslPatchData || analysis.HasSimpleOverrideFiles)
			{
				Instruction extractInstruction = new Instruction
				{
					Guid = Guid.NewGuid(),
					Action = Instruction.ActionType.Extract,
					Source = new List<string> { $@"<<modDirectory>>\{archiveFileName}" },
					Overwrite = true
				};
				extractInstruction.SetParentComponent( component );

				if (!InstructionAlreadyExists( component, extractInstruction ))
				{
					component.Instructions.Add( extractInstruction );
					Logger.LogVerbose( $"[AutoInstructionGenerator] Added Extract instruction for '{archiveFileName}'" );
				}
				else
				{
					Logger.LogVerbose( $"[AutoInstructionGenerator] Extract instruction for '{archiveFileName}' already exists, skipping" );
				}
			}
			else
			{
				return false;
			}

			if (analysis.HasTslPatchData)
			{
				if (analysis.HasNamespacesIni)
					AddNamespacesChooseInstructions( component, archivePath, analysis, extractedPath );
				else if (analysis.HasChangesIni)
					AddSimplePatcherInstruction( component, analysis, extractedPath );
			}

			if (analysis.HasSimpleOverrideFiles)
			{
				List<string> overrideFolders = analysis.FoldersWithFiles
					.Where( f => !IsTslPatcherFolder( f, analysis ) )
					.ToList();

				if (overrideFolders.Count > 1)
					AddMultiFolderChooseInstructions( component, archive, archivePath, extractedPath, overrideFolders );
				else if (overrideFolders.Count == 1)
					AddSimpleMoveInstruction( component, archive, archivePath, extractedPath, overrideFolders[0] );
				else if (analysis.HasFlatFiles)
					AddSimpleMoveInstruction( component, archive, archivePath, extractedPath, null );
			}

			if (analysis.HasTslPatchData && analysis.HasSimpleOverrideFiles)
				component.InstallationMethod = "Hybrid (TSLPatcher + Loose Files)";
			else if (analysis.HasTslPatchData)
				component.InstallationMethod = "TSLPatcher";
			else if (analysis.HasSimpleOverrideFiles)
				component.InstallationMethod = "Loose-File Mod";

			int consolidatedCount = ConsolidateDuplicateOptions( component );
			if (consolidatedCount > 0)
			{
				Logger.LogVerbose( $"[AutoInstructionGenerator] Consolidated and removed {consolidatedCount} duplicate option(s)" );
			}

			return component.Instructions.Count > 0;
		}

		public static async Task<bool> GenerateInstructionsFromUrlsAsync(
			[NotNull] ModComponent component,
			[NotNull] DownloadCacheService downloadCache,
			CancellationToken cancellationToken = default )
		{
			if (component is null)
				throw new ArgumentNullException( nameof( component ) );
			if (downloadCache is null)
				throw new ArgumentNullException( nameof( downloadCache ) );

			if (component.ModLinkFilenames.Count == 0)
			{
				await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Component '{component.Name}' has no URLs to process" ).ConfigureAwait( false );
				return false;
			}

			if (IsRemoveDuplicateTgaTpcMod( component ))
			{
				await Logger.LogVerboseAsync( "[AutoInstructionGenerator] Detected Remove Duplicate TGA/TPC mod, generating DelDuplicate instruction only" ).ConfigureAwait( false );
				return GenerateDelDuplicateInstruction( component );
			}

			try
			{
				await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Pre-resolving URLs for component: {component.Name}" ).ConfigureAwait( false );

				Dictionary<string, List<string>> resolvedUrls = await downloadCache.PreResolveUrlsAsync( component, null, sequential: true, cancellationToken ).ConfigureAwait( false );

				if (resolvedUrls.Count == 0)
				{
					await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] No URLs resolved for component: {component.Name}" ).ConfigureAwait( false );
					return false;
				}

				await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Resolved {resolvedUrls.Count} URLs" ).ConfigureAwait( false );

				if (MainConfig.SourcePath == null || !MainConfig.SourcePath.Exists)
				{
					await Logger.LogVerboseAsync( "[AutoInstructionGenerator] No source directory configured, creating placeholder instructions" ).ConfigureAwait( false );

					foreach (KeyValuePair<string, List<string>> kvp in resolvedUrls)
					{
						List<string> filenames = kvp.Value;
						if (filenames.Count == 0)
							continue;

						// Process ALL files from this URL, not just the first one
						foreach (string fileName in filenames)
						{
							Instruction potentialInstruction = CreatePlaceholderInstructionObject( component, fileName );

							if (potentialInstruction == null)
								continue;

							if (!InstructionAlreadyExists( component, potentialInstruction ))
							{
								component.Instructions.Add( potentialInstruction );
								await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Added placeholder instruction for '{fileName}'" ).ConfigureAwait( false );
							}
							else
							{
								await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Placeholder instruction for '{fileName}' already exists, skipping" ).ConfigureAwait( false );
							}
						}
					}

					return component.Instructions.Count > 0;
				}

				// Track files that are missing
				List<string> missingFiles = new List<string>();

				foreach (KeyValuePair<string, List<string>> kvp in resolvedUrls)
				{
					List<string> filenames = kvp.Value;
					if (filenames.Count == 0)
						continue;

					// Process ALL files from this URL, not just the first one
					foreach (string fileName in filenames)
					{
						// Skip empty or null filenames
						if (string.IsNullOrWhiteSpace( fileName ))
						{
							await Logger.LogWarningAsync( $"[AutoInstructionGenerator] Skipping empty filename from URL: {kvp.Key}" ).ConfigureAwait( false );
							continue;
						}

						string filePath = Path.Combine( MainConfig.SourcePath.FullName, fileName );

						if (File.Exists( filePath ))
						{
							await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Found '{fileName}' on disk, performing comprehensive analysis" ).ConfigureAwait( false );

							bool isArchive = ArchiveHelper.IsArchive( fileName );
							if (isArchive)
							{
								bool generated = GenerateInstructions( component, filePath );
								if (!generated)
								{
									bool fileStillExists = File.Exists( filePath );

									if (!fileStillExists)
									{
										await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Corrupted file '{fileName}' has been deleted, creating placeholder instruction" ).ConfigureAwait( false );
									}
									else
									{
										await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Comprehensive analysis failed for '{fileName}', creating placeholder Extract instruction" ).ConfigureAwait( false );
									}

									Instruction potentialInstruction = CreatePlaceholderInstructionObject( component, fileName );
									if (potentialInstruction != null)
									{
										if (!InstructionAlreadyExists( component, potentialInstruction ))
										{
											component.Instructions.Add( potentialInstruction );
											await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Added placeholder Extract instruction for '{fileName}'" ).ConfigureAwait( false );
										}
										else
										{
											await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Placeholder instruction for '{fileName}' already exists, skipping" ).ConfigureAwait( false );
										}
									}
								}
							}
							else
							{
								await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] '{fileName}' is not an archive, checking if it's a game file" ).ConfigureAwait( false );

								Instruction potentialInstruction = CreatePlaceholderInstructionObject( component, fileName );
								if (potentialInstruction != null)
								{
									if (!InstructionAlreadyExists( component, potentialInstruction ))
									{
										component.Instructions.Add( potentialInstruction );
										await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Added Move instruction for '{fileName}'" ).ConfigureAwait( false );
									}
									else
									{
										await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Move instruction for '{fileName}' already exists, skipping" ).ConfigureAwait( false );
									}
								}
							}
						}
						else
						{
							await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] '{fileName}' not found on disk, creating placeholder instruction" ).ConfigureAwait( false );
							missingFiles.Add( fileName );

							Instruction potentialInstruction = CreatePlaceholderInstructionObject( component, fileName );
							if (potentialInstruction != null)
							{
								if (!InstructionAlreadyExists( component, potentialInstruction ))
								{
									component.Instructions.Add( potentialInstruction );
									await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Added placeholder instruction for '{fileName}'" ).ConfigureAwait( false );
								}
								else
								{
									await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Placeholder instruction for '{fileName}' already exists, skipping" ).ConfigureAwait( false );
								}
							}
						}
					}
				}

				// Warn if files are missing (CLI context - user should have used --download flag)
				if (missingFiles.Count > 0)
				{
					await Logger.LogWarningAsync( $"[AutoInstructionGenerator] Component '{component.Name}' has {missingFiles.Count} file(s) not found on disk:" ).ConfigureAwait( false );
					foreach (string fileName in missingFiles.Take( 5 ))
					{
						await Logger.LogWarningAsync( $"  • {fileName}" ).ConfigureAwait( false );
					}
					if (missingFiles.Count > 5)
					{
						await Logger.LogWarningAsync( $"  ... and {missingFiles.Count - 5} more" ).ConfigureAwait( false );
					}
					await Logger.LogWarningAsync( "[AutoInstructionGenerator] To download files automatically, use the --download flag" ).ConfigureAwait( false );
					await Logger.LogWarningAsync( "[AutoInstructionGenerator] Example: dotnet run --project KOTORModSync.Core -- convert --input file.toml --auto --download --source-path ./mods" ).ConfigureAwait( false );
				}

				int consolidatedCount = ConsolidateDuplicateOptions( component );
				if (consolidatedCount > 0)
				{
					await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Consolidated and removed {consolidatedCount} duplicate option(s)" ).ConfigureAwait( false );
				}

				await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Generated {component.Instructions.Count} instructions for component: {component.Name}" ).ConfigureAwait( false );
				return component.Instructions.Count > 0;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync( ex, $"Failed to generate instructions from URLs for component: {component.Name}" ).ConfigureAwait( false );
				return false;
			}
		}

		/// <summary>
		/// Result of analyzing component files for auto-generation
		/// </summary>
		public class FileAnalysisResult
		{
			public List<string> ExistingArchives { get; set; } = new List<string>();
			public List<string> ExistingNonArchiveFiles { get; set; } = new List<string>();
			public List<string> MissingUrls { get; set; } = new List<string>();
			public List<string> InvalidLinks { get; set; } = new List<string>();
			public Dictionary<string, List<string>> ResolvedUrls { get; set; } = new Dictionary<string, List<string>>( StringComparer.Ordinal );
		}

		/// <summary>
		/// Analyzes a component's mod links to determine what files exist and what needs downloading.
		/// This is cache-first and doesn't download anything.
		/// </summary>
		public static async Task<FileAnalysisResult> AnalyzeComponentFilesAsync(
			[NotNull] ModComponent component,
			[NotNull] DownloadCacheService downloadCache,
			[NotNull] string modDirectory,
			CancellationToken cancellationToken = default )
		{
			if (component == null)
				throw new ArgumentNullException( nameof( component ) );
			if (downloadCache == null)
				throw new ArgumentNullException( nameof( downloadCache ) );
			if (string.IsNullOrEmpty( modDirectory ))
				throw new ArgumentException( "Mod directory cannot be null or empty", nameof( modDirectory ) );

			FileAnalysisResult result = new FileAnalysisResult();

			await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Analyzing files for component: {component.Name}" ).ConfigureAwait( false );

			// Pre-resolve URLs to filenames (uses cache, doesn't download)
			result.ResolvedUrls = await downloadCache.PreResolveUrlsAsync(
				component,
				downloadCache.DownloadManager,
				sequential: false,
				cancellationToken ).ConfigureAwait( false );

			await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Resolved {result.ResolvedUrls.Count} URL(s)" ).ConfigureAwait( false );

			// Check which files exist on disk
			List<string> existingFiles = new List<string>();

			foreach (string modLink in component.ModLinkFilenames.Keys)
			{
				if (string.IsNullOrWhiteSpace( modLink ))
					continue;

				if (IsValidUrl( modLink ))
				{
					// Always check resolved filenames first to get all files for the URL
					if (result.ResolvedUrls.TryGetValue( modLink, out List<string> filenames ) && filenames.Count > 0)
					{
						bool anyFileExists = false;
						foreach (string filename in filenames)
						{
							string filePath = Path.Combine( modDirectory, filename );
							if (File.Exists( filePath ))
							{
								existingFiles.Add( filePath );
								anyFileExists = true;
								await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] File exists: {filename}" ).ConfigureAwait( false );
							}
							else
							{
								await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] File missing: {filename}" ).ConfigureAwait( false );
							}
						}

						if (!anyFileExists)
						{
							result.MissingUrls.Add( modLink );
							await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Files missing for URL: {modLink}" ).ConfigureAwait( false );
						}
					}
					else
					{
						// Fallback to cached filename if no resolved filenames
						string cachedFilename = DownloadCacheService.GetFileName( modLink );
						if (!string.IsNullOrEmpty( cachedFilename ))
						{
							string filePath = Path.Combine( modDirectory, cachedFilename );
							if (File.Exists( filePath ))
							{
								existingFiles.Add( filePath );
								await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] File exists on disk (cached): {cachedFilename}" ).ConfigureAwait( false );
							}
							else
							{
								result.MissingUrls.Add( modLink );
								await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] File missing (cached): {cachedFilename}" ).ConfigureAwait( false );
							}
						}
						else
						{
							result.MissingUrls.Add( modLink );
							await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] URL not resolved: {modLink}" ).ConfigureAwait( false );
						}
					}
				}
				else
				{
					// This is a local file path, not a URL
					string fullPath = Path.IsPathRooted( modLink ) ? modLink : Path.Combine( modDirectory, modLink );

					if (File.Exists( fullPath ))
					{
						existingFiles.Add( fullPath );
					}
					else
					{
						result.InvalidLinks.Add( modLink );
					}
				}
			}

			// Categorize existing files
			foreach (string filePath in existingFiles)
			{
				if (ArchiveHelper.IsArchive( filePath ))
					result.ExistingArchives.Add( filePath );
				else
					result.ExistingNonArchiveFiles.Add( filePath );
			}

			await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Analysis complete: {result.ExistingArchives.Count} archives, {result.ExistingNonArchiveFiles.Count} non-archives, {result.MissingUrls.Count} missing URLs" ).ConfigureAwait( false );

			return result;
		}

		/// <summary>
		/// Generates instructions from analyzed files (archives and non-archive files).
		/// Call after AnalyzeComponentFilesAsync() to generate from existing files.
		/// </summary>
		public static async Task<int> GenerateInstructionsFromAnalyzedFilesAsync(
			[NotNull] ModComponent component,
			[NotNull] FileAnalysisResult analysis,
			[NotNull] string modDirectory )
		{
			if (component == null)
				throw new ArgumentNullException( nameof( component ) );
			if (analysis == null)
				throw new ArgumentNullException( nameof( analysis ) );
			if (string.IsNullOrEmpty( modDirectory ))
				throw new ArgumentException( "Mod directory cannot be null or empty", nameof( modDirectory ) );

			int totalInstructionsGenerated = 0;

			// Generate instructions from archives
			foreach (string archivePath in analysis.ExistingArchives)
			{
				await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Generating instructions for archive: {archivePath}" ).ConfigureAwait( false );
				bool success = GenerateInstructions( component, archivePath );
				if (success)
				{
					int newInstructions = component.Instructions.Count - totalInstructionsGenerated;
					totalInstructionsGenerated = component.Instructions.Count;
					await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Generated {newInstructions} instruction(s) for: {archivePath}" ).ConfigureAwait( false );
				}
			}

			// Generate instructions for non-archive files
			foreach (string filePath in analysis.ExistingNonArchiveFiles)
			{
				string fileName = Path.GetFileName( filePath );
				string relativePath = GetRelativePathToModDirectory( modDirectory, filePath );

				Instruction moveInstruction = new Instruction
				{
					Guid = Guid.NewGuid(),
					Action = Instruction.ActionType.Move,
					Source = new List<string> { $@"<<modDirectory>>\{relativePath}" },
					Destination = @"<<kotorDirectory>>\Override",
					Overwrite = true
				};
				moveInstruction.SetParentComponent( component );

				if (!InstructionAlreadyExists( component, moveInstruction ))
				{
					component.Instructions.Add( moveInstruction );
					totalInstructionsGenerated++;
					await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Added Move instruction for: {fileName}" ).ConfigureAwait( false );
				}
			}

			await Logger.LogVerboseAsync( $"[AutoInstructionGenerator] Total instructions generated: {totalInstructionsGenerated}" ).ConfigureAwait( false );
			return totalInstructionsGenerated;
		}

		private static string GetRelativePathToModDirectory( string modDirectory, string targetPath )
		{
			if (string.IsNullOrEmpty( modDirectory ) || string.IsNullOrEmpty( targetPath ))
				return Path.GetFileName( targetPath );

			string modDirFull = Path.GetFullPath( modDirectory );
			string targetFull = Path.GetFullPath( targetPath );

			if (!targetFull.StartsWith( modDirFull, StringComparison.OrdinalIgnoreCase ))
				return Path.GetFileName( targetPath );

			string relativePath = targetFull.Substring( modDirFull.Length );
			if (relativePath.StartsWith( Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal ))
				relativePath = relativePath.Substring( 1 );

			return relativePath;
		}

		private static bool IsValidUrl( string url )
		{
			if (string.IsNullOrWhiteSpace( url ))
				return false;

			if (!Uri.TryCreate( url, UriKind.Absolute, out Uri uri ))
				return false;

			return string.Equals( uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal ) || string.Equals( uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal );
		}

		[CanBeNull]
		private static Instruction CreatePlaceholderInstructionObject(
			[NotNull] ModComponent component,
			[NotNull] string fileName
		)
		{
			if (string.IsNullOrWhiteSpace( fileName ))
			{
				Logger.LogWarning( $"Cannot create placeholder instruction for empty filename in component '{component.Name}'" );
				return null;
			}

			bool isArchive = ArchiveHelper.IsArchive( fileName );

			if (!isArchive)
			{
				string extension = Path.GetExtension( fileName );
				if (!IsGameFile( extension ))
				{
					Logger.LogVerbose( $"[AutoInstructionGenerator] Skipping non-game file '{fileName}' (extension: {extension})" );
					return null;
				}
			}

			Instruction instruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = isArchive ? Instruction.ActionType.Extract : Instruction.ActionType.Move,
				Source = new List<string> { $@"<<modDirectory>>\{fileName}" },
				Destination = isArchive ? string.Empty : @"<<kotorDirectory>>\Override",
				Overwrite = true
			};
			instruction.SetParentComponent( component );

			return instruction;
		}

		private static readonly char[] s_pathSeparators = new[] { '/', '\\' };

		private static bool IsTslPatcherFolder( string folderName, ArchiveAnalysis analysis )
		{
			if (string.IsNullOrEmpty( folderName ))
				return false;

			if (folderName.Equals( "tslpatchdata", StringComparison.OrdinalIgnoreCase ))
				return true;

			if (string.IsNullOrEmpty( analysis.TslPatcherPath ))
				return false;
			string[] pathParts = analysis.TslPatcherPath.Split( s_pathSeparators, StringSplitOptions.RemoveEmptyEntries );
			if (pathParts.Length > 0 && pathParts[0].Equals( folderName, StringComparison.OrdinalIgnoreCase ))
				return true;

			return false;
		}

		private static void AddNamespacesChooseInstructions(
			ModComponent component,
			string archivePath,
			ArchiveAnalysis analysis,
			string extractedPath
		)
		{
			Dictionary<string, Dictionary<string, string>> namespaces =
				IniHelper.ReadNamespacesIniFromArchive( archivePath );

			if (namespaces == null ||
				 !namespaces.TryGetValue( "Namespaces", out Dictionary<string, string> value ))
			{
				return;
			}

			List<string> optionGuidsToAdd = new List<string>();

			foreach (string ns in value.Values)
			{
				if (!namespaces.TryGetValue( ns, out Dictionary<string, string> namespaceData ))
					continue;

				Option potentialOption = new Option
				{
					Guid = Guid.NewGuid(),
					Name = namespaceData.TryGetValue( "Name", out string value2 ) ? value2 : ns,
					Description = namespaceData.TryGetValue( "Description", out string value3 ) ? value3 : string.Empty,
					IsSelected = false
				};

				string iniName = namespaceData.TryGetValue( "IniName", out string value4 ) ? value4 : "changes.ini";
				string patcherPath = string.IsNullOrEmpty( analysis.TslPatcherPath )
					? extractedPath
					: analysis.TslPatcherPath;

				// Use the namespace name as the executable name instead of generic TSLPatcher.exe
				// This ensures each namespace has its own unique executable path
				string executableName = $"{ns}.exe";

				Instruction patcherInstruction = new Instruction
				{
					Guid = Guid.NewGuid(),
					Action = Instruction.ActionType.Patcher,
					Source = new List<string> { $@"<<modDirectory>>\{patcherPath}\{ns}\{executableName}" },
					Destination = "<<kotorDirectory>>",
					Arguments = iniName,
					Overwrite = true
				};
				patcherInstruction.SetParentComponent( potentialOption );
				potentialOption.Instructions.Add( patcherInstruction );

				Option existingOption = FindEquivalentOption( component, potentialOption );

				if (existingOption != null)
				{
					int addedCount = AddMissingInstructionsToOption( existingOption, potentialOption );
					if (addedCount > 0)
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] Added {addedCount} missing instruction(s) to existing option '{existingOption.Name}'" );
					}
					else
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] Option equivalent to '{potentialOption.Name}' already exists as '{existingOption.Name}' with all instructions present" );
					}

					optionGuidsToAdd.Add( existingOption.Guid.ToString() );
				}
				else
				{
					component.Options.Add( potentialOption );
					optionGuidsToAdd.Add( potentialOption.Guid.ToString() );
					Logger.LogVerbose( $"[AutoInstructionGenerator] Added new option '{potentialOption.Name}' for namespace" );
				}
			}

			if (optionGuidsToAdd.Count > 0)
			{
				Instruction existingChoose = FindCompatibleChooseInstruction( component );

				if (existingChoose != null)
				{
					int addedGuidCount = 0;
					foreach (string optionGuid in optionGuidsToAdd)
					{
						if (AddOptionToChooseInstruction( existingChoose, optionGuid ))
							addedGuidCount++;
					}

					if (addedGuidCount > 0)
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] Added {addedGuidCount} option GUID(s) to existing Choose instruction" );
					}
					else
					{
						Logger.LogVerbose( "[AutoInstructionGenerator] All namespace option GUIDs already present in existing Choose instruction" );
					}
				}
				else
				{
					Instruction chooseInstruction = new Instruction
					{
						Guid = Guid.NewGuid(),
						Action = Instruction.ActionType.Choose,
						Source = optionGuidsToAdd,
						Overwrite = true
					};
					chooseInstruction.SetParentComponent( component );
					component.Instructions.Add( chooseInstruction );
					Logger.LogVerbose( $"[AutoInstructionGenerator] Created new Choose instruction with {optionGuidsToAdd.Count} namespace option(s)" );
				}
			}

			int consolidatedCount = ConsolidateDuplicateOptions( component );
			if (consolidatedCount > 0)
			{
				Logger.LogVerbose( $"[AutoInstructionGenerator] Consolidated {consolidatedCount} duplicate namespace option(s)" );
			}
		}

		private static void AddSimplePatcherInstruction(
			ModComponent component,
			ArchiveAnalysis analysis,
			string extractedPath )
		{
			string patcherPath = string.IsNullOrEmpty( analysis.TslPatcherPath )
				? extractedPath
				: analysis.TslPatcherPath;

			string executableName = string.IsNullOrEmpty( analysis.PatcherExecutable )
				? "TSLPatcher.exe"
				: Path.GetFileName( analysis.PatcherExecutable );

			Instruction patcherInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.Patcher,
				Source = new List<string> { $@"<<modDirectory>>\{patcherPath}\{executableName}" },
				Destination = "<<kotorDirectory>>",
				Overwrite = true
			};
			patcherInstruction.SetParentComponent( component );

			if (!InstructionAlreadyExists( component, patcherInstruction ))
			{
				component.Instructions.Add( patcherInstruction );
				Logger.LogVerbose( $"[AutoInstructionGenerator] Added Patcher instruction for '{patcherPath}'" );
			}
			else
			{
				Logger.LogVerbose( $"[AutoInstructionGenerator] Patcher instruction for '{patcherPath}' already exists, skipping" );
			}
		}

		private static void AddMultiFolderChooseInstructions(
			ModComponent component,
			IArchive archive,
			string archivePath,
			string extractedPath,
			List<string> folders
		)
		{
			List<string> optionGuidsToAdd = new List<string>();

			foreach (string folder in folders)
			{
				if (!FolderContainsGameFiles( archive, archivePath, folder ))
				{
					Logger.LogVerbose( $"[AutoInstructionGenerator] Skipping folder '{folder}' - no game files found" );
					continue;
				}

				string potentialSourcePath = $@"<<modDirectory>>\{extractedPath}\{folder}\*";
				if (IsFolderAlreadyCoveredByInstructions( component, potentialSourcePath ))
				{
					Logger.LogVerbose( $"[AutoInstructionGenerator] Skipping folder '{folder}' - already covered by existing instructions" );
					continue;
				}

				Option potentialOption = new Option
				{
					Guid = Guid.NewGuid(),
					Name = folder,
					Description = $"Install files from {folder} folder",
					IsSelected = false
				};

				Instruction moveInstruction = new Instruction
				{
					Guid = Guid.NewGuid(),
					Action = Instruction.ActionType.Move,
					Source = new List<string> { potentialSourcePath },
					Destination = @"<<kotorDirectory>>\Override",
					Overwrite = true
				};
				moveInstruction.SetParentComponent( potentialOption );
				potentialOption.Instructions.Add( moveInstruction );

				Option existingOption = FindEquivalentOption( component, potentialOption );

				if (existingOption != null)
				{
					int addedCount = AddMissingInstructionsToOption( existingOption, potentialOption );
					if (addedCount > 0)
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] Added {addedCount} missing instruction(s) to existing option '{existingOption.Name}'" );
					}
					else
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] Option equivalent to '{potentialOption.Name}' already exists as '{existingOption.Name}' with all instructions present" );
					}

					optionGuidsToAdd.Add( existingOption.Guid.ToString() );
				}
				else
				{
					component.Options.Add( potentialOption );
					optionGuidsToAdd.Add( potentialOption.Guid.ToString() );
					Logger.LogVerbose( $"[AutoInstructionGenerator] Added new option '{potentialOption.Name}' for folder" );
				}
			}

			if (optionGuidsToAdd.Count > 0)
			{
				Instruction existingChoose = FindCompatibleChooseInstruction( component );

				if (existingChoose != null)
				{
					int addedGuidCount = 0;
					foreach (string optionGuid in optionGuidsToAdd)
					{
						if (AddOptionToChooseInstruction( existingChoose, optionGuid ))
							addedGuidCount++;
					}

					if (addedGuidCount > 0)
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] Added {addedGuidCount} option GUID(s) to existing Choose instruction" );
					}
					else
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] All folder option GUIDs already present in existing Choose instruction" );
					}
				}
				else
				{
					Instruction chooseInstruction = new Instruction
					{
						Guid = Guid.NewGuid(),
						Action = Instruction.ActionType.Choose,
						Source = optionGuidsToAdd,
						Overwrite = true
					};
					chooseInstruction.SetParentComponent( component );
					component.Instructions.Add( chooseInstruction );
					Logger.LogVerbose( $"[AutoInstructionGenerator] Created new Choose instruction with {optionGuidsToAdd.Count} folder option(s)" );
				}
			}

			int consolidatedCount = ConsolidateDuplicateOptions( component );
			if (consolidatedCount > 0)
			{
				Logger.LogVerbose( $"[AutoInstructionGenerator] Consolidated {consolidatedCount} duplicate folder option(s)" );
			}
		}


		private static void AddSimpleMoveInstruction(
			ModComponent component,
			IArchive archive,
			string archivePath,
			string extractedPath,
			string folderName
		)
		{
			string folderPathInArchive = string.IsNullOrEmpty( folderName ) ? null : folderName;

			if (!FolderContainsGameFiles( archive, archivePath, folderPathInArchive ))
			{
				string location = string.IsNullOrEmpty( folderName ) ? "root" : $"folder '{folderName}'";
				Logger.LogVerbose( $"[AutoInstructionGenerator] Skipping Move instruction for {location} - no game files found" );
				return;
			}

			string sourcePath = string.IsNullOrEmpty( folderName )
				? $@"<<modDirectory>>\{extractedPath}\*"
				: $@"<<modDirectory>>\{extractedPath}\{folderName}\*";

			if (IsFolderAlreadyCoveredByInstructions( component, sourcePath ))
			{
				string location = string.IsNullOrEmpty( folderName ) ? "root" : $"folder '{folderName}'";
				Logger.LogVerbose( $"[AutoInstructionGenerator] Skipping Move instruction for {location} - already covered by existing instructions" );
				return;
			}

			Instruction moveInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.Move,
				Source = new List<string> { sourcePath },
				Destination = @"<<kotorDirectory>>\Override",
				Overwrite = true
			};
			moveInstruction.SetParentComponent( component );

			if (!InstructionAlreadyExists( component, moveInstruction ))
			{
				component.Instructions.Add( moveInstruction );
				Logger.LogVerbose( $"[AutoInstructionGenerator] Added Move instruction for '{sourcePath}'" );
			}
			else
			{
				Logger.LogVerbose( $"[AutoInstructionGenerator] Move instruction for '{sourcePath}' already exists, skipping" );
			}
		}

		private static ArchiveAnalysis AnalyzeArchive(
			[NotNull] IArchive archive,
			[NotNull] string archivePath
		)
		{
			ArchiveAnalysis analysis = new ArchiveAnalysis();

			try
			{
				foreach (IArchiveEntry entry in archive.Entries)
				{
					if (entry.IsDirectory)
						continue;

					string path = entry.Key.Replace( '\\', '/' );
					string[] pathParts = path.Split( '/' );

					if (pathParts.Any( p => p.Equals( "tslpatchdata", StringComparison.OrdinalIgnoreCase ) ))
					{
						analysis.HasTslPatchData = true;

						string fileName = Path.GetFileName( path );
						if (fileName.Equals( "namespaces.ini", StringComparison.OrdinalIgnoreCase ))
						{
							analysis.HasNamespacesIni = true;
							analysis.TslPatcherPath = GetTslPatcherPath( path );
						}
						else if (fileName.Equals( "changes.ini", StringComparison.OrdinalIgnoreCase ))
						{
							analysis.HasChangesIni = true;
							if (string.IsNullOrEmpty( analysis.TslPatcherPath ))
								analysis.TslPatcherPath = GetTslPatcherPath( path );
						}
						else if (fileName.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ))
						{
							if (string.IsNullOrEmpty( analysis.PatcherExecutable ))
								analysis.PatcherExecutable = path;
						}
					}
					else
					{

						string extension = Path.GetExtension( path ).ToLowerInvariant();
						if (!IsGameFile( extension ))
							continue;
						analysis.HasSimpleOverrideFiles = true;

						if (pathParts.Length == 1)
						{

							analysis.HasFlatFiles = true;
						}
						else if (pathParts.Length >= 2)
						{

							string topLevelFolder = pathParts[0];
							if (!analysis.FoldersWithFiles.Contains( topLevelFolder, StringComparer.Ordinal ))
								analysis.FoldersWithFiles.Add( topLevelFolder );
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning( $"[AutoInstructionGenerator] SharpCompress failed to read archive entries: {ex.Message}" );
				Logger.LogVerbose( $"[AutoInstructionGenerator] Attempting to use 7zip CLI as fallback to list archive contents..." );

				try
				{
					Task<List<string>> fileListTask = ArchiveHelper.TryListArchiveWithSevenZipCliAsync( archivePath );
					fileListTask.Wait();
					List<string> fileList = fileListTask.Result;

					if (fileList != null && fileList.Count > 0)
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] Successfully listed {fileList.Count} files using 7zip CLI" );
						analysis = AnalyzeArchiveFromFileList( fileList );
					}
					else
					{
						Logger.LogError( $"[AutoInstructionGenerator] 7zip CLI could not list archive contents (returned empty or null): {archivePath}" );
						Logger.LogError( $"[AutoInstructionGenerator] Original SharpCompress error: {ex.Message}" );
						throw new InvalidOperationException(
							$"Unable to read archive contents with either SharpCompress or 7zip CLI. " +
							$"Archive may be corrupted or in an unsupported format: {archivePath}. " +
							$"SharpCompress error: {ex.Message}"
						);
					}
				}
				catch (Exception fallbackEx)
				{
					Logger.LogError( $"[AutoInstructionGenerator] 7zip CLI threw exception while reading archive: {archivePath}" );
					Logger.LogError( $"[AutoInstructionGenerator] SharpCompress error: {ex.Message}" );
					Logger.LogException( fallbackEx, "[AutoInstructionGenerator] 7zip CLI error" );
					throw new InvalidOperationException(
						$"Unable to read archive contents with either SharpCompress or 7zip CLI. " +
						$"Archive may be corrupted or in an unsupported format: {archivePath}. " +
						$"SharpCompress error: {ex.Message}. " +
						$"7zip CLI error: {fallbackEx.Message}", fallbackEx
					);
				}
			}

			return analysis;
		}

		private static string GetTslPatcherPath( string iniPath )
		{

			string[] parts = iniPath.Split( s_pathSeparators, StringSplitOptions.RemoveEmptyEntries );
			for (int i = 0; i < parts.Length - 1; i++)
			{
				if (parts[i].Equals( "tslpatchdata", StringComparison.OrdinalIgnoreCase ))
				{

					return string.Join( "/", parts.Take( i ) );
				}
			}
			return string.Empty;
		}

		private static bool IsGameFile( string extension )
		{

			HashSet<string> gameExtensions = new HashSet<string>( StringComparer.OrdinalIgnoreCase )
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

			return gameExtensions.Contains( extension );
		}

		private static bool FolderContainsGameFiles(
			[NotNull] IArchive archive,
			[NotNull] string archivePath,
			[CanBeNull] string folderPath
		)
		{
			try
			{
				foreach (IArchiveEntry entry in archive.Entries)
				{
					if (entry.IsDirectory)
						continue;

					string entryPath = entry.Key.Replace( '\\', '/' );
					string extension = Path.GetExtension( entryPath );

					if (!IsGameFile( extension ))
						continue;

					if (string.IsNullOrEmpty( folderPath ))
					{
						if (!entryPath.Contains( '/' ) && !entryPath.Contains( '\\' ))
							return true;
					}
					else
					{
						string normalizedFolderPath = folderPath.Replace( '\\', '/' );
						if (!normalizedFolderPath.EndsWith( "/", StringComparison.Ordinal ))
							normalizedFolderPath += "/";

						if (entryPath.StartsWith( normalizedFolderPath, StringComparison.OrdinalIgnoreCase ))
							return true;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning( $"[AutoInstructionGenerator] SharpCompress failed to enumerate archive entries: {ex.Message}" );
				Logger.LogVerbose( "[AutoInstructionGenerator] Attempting to use 7zip CLI to check folder contents..." );

				try
				{
					Task<List<string>> fileListTask = ArchiveHelper.TryListArchiveWithSevenZipCliAsync( archivePath );
					fileListTask.Wait();
					List<string> fileList = fileListTask.Result;

					if (fileList != null && fileList.Count > 0)
					{
						Logger.LogVerbose( $"[AutoInstructionGenerator] Successfully listed {fileList.Count} files using 7zip CLI for folder check" );

						foreach (string entryPath in fileList)
						{
							string normalizedPath = entryPath.Replace( '\\', '/' );
							string extension = Path.GetExtension( normalizedPath );

							if (!IsGameFile( extension ))
								continue;

							if (string.IsNullOrEmpty( folderPath ))
							{
								if (!normalizedPath.Contains( '/' ))
									return true;
							}
							else
							{
								string normalizedFolderPath = folderPath.Replace( '\\', '/' );
								if (!normalizedFolderPath.EndsWith( "/", StringComparison.Ordinal ))
									normalizedFolderPath += "/";

								if (normalizedPath.StartsWith( normalizedFolderPath, StringComparison.OrdinalIgnoreCase ))
									return true;
							}
						}
						return false;
					}
					else
					{
						Logger.LogWarning( "[AutoInstructionGenerator] 7zip CLI failed to list archive. Assuming folder contains game files." );
						return true;
					}
				}
				catch (Exception fallbackEx)
				{
					Logger.LogWarning( $"[AutoInstructionGenerator] 7zip CLI fallback failed: {fallbackEx.Message}" );
					Logger.LogVerbose( "[AutoInstructionGenerator] Assuming folder contains game files since archive cannot be analyzed." );
					return true;
				}
			}

			return false;
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

	/// <summary>
	/// Represents the result of attempting to generate instructions for a component
	/// </summary>
	public class GenerationResult
	{
		public Guid ComponentGuid { get; set; }
		public string ComponentName { get; set; }
		public bool Success { get; set; }
		public int InstructionsGenerated { get; set; }
		public string SkipReason { get; set; }
	}
}