// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.TSLPatcher;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
	public partial class NamespacesIniOptionConverter : IValueConverter
	{
		// Static cache for archives per component - automatically invalidated by FileSystemService watcher
		private static readonly Dictionary<Guid, List<string>> _archiveCache = new Dictionary<Guid, List<string>>();
		private static readonly object _cacheLock = new object();

		/// <summary>
		/// Clears the archive cache. Called automatically by FileSystemService when mod directory changes.
		/// </summary>
		public static void InvalidateCache()
		{
			lock ( _cacheLock )
			{
				_archiveCache.Clear();
				Logger.LogVerbose("[NamespacesIniOptionConverter] Archive cache invalidated due to file system changes");
			}
		}

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			if ( !(value is Instruction dataContextInstruction) )
				return null;

			// Only process Patcher instructions
			if ( dataContextInstruction.Action != Instruction.ActionType.Patcher )
				return null;

			ModComponent parentComponent = dataContextInstruction.GetParentComponent();
			if ( parentComponent is null )
				return null;

			// Get ALL archives from the component (cached)
			List<string> allArchives = GetAllArchivesFromInstructions(parentComponent);

			// Filter to only archives that match this specific instruction's source path
			List<string> relevantArchives = GetArchivesForSpecificInstruction(dataContextInstruction, allArchives);

			foreach ( string archivePath in relevantArchives )
			{
				if ( string.IsNullOrEmpty(archivePath) )
					continue;

				Dictionary<string, Dictionary<string, string>> result = IniHelper.ReadNamespacesIniFromArchive(archivePath);
				if ( result == null || result.Count == 0 )
					continue;

				var optionNames = new List<string>();
				foreach ( KeyValuePair<string, Dictionary<string, string>> section in result )
				{
					if ( section.Value != null && section.Value.TryGetValue("Name", out string name) )
						optionNames.Add(name);
				}

				if ( optionNames.Count != 0 )
					return optionNames;
			}
			return null;
		}
		catch ( Exception ex )
		{
			Logger.LogException(ex);
			return null;
		}
	}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

		/// <summary>
		/// Filters archives to only those that match the specific instruction's source path.
		/// For Patcher instructions, this matches the Patcher source to Extract instruction destinations
		/// that would have created the directories containing the Patcher executables.
		/// </summary>
		[NotNull]
		private static List<string> GetArchivesForSpecificInstruction([NotNull] Instruction instruction, [NotNull] List<string> allArchives)
		{
			if ( instruction is null )
				throw new ArgumentNullException(nameof(instruction));
			if ( allArchives is null )
				throw new ArgumentNullException(nameof(allArchives));

			var relevantArchives = new List<string>();

			// Get the instruction's source paths (with variables replaced)
			List<string> instructionSourcePaths = instruction.Source.ConvertAll(Utility.ReplaceCustomVariables);

			foreach ( string archivePath in allArchives )
			{
				if ( string.IsNullOrEmpty(archivePath) )
					continue;

				// For Patcher instructions, check if the Patcher source path would be found
				// in a directory that was created by extracting this archive
				foreach ( string sourcePath in instructionSourcePaths )
				{
					if ( string.IsNullOrEmpty(sourcePath) )
						continue;

					// Check if the Patcher source path is within a directory that would be created by this archive
					if ( IsPatcherSourceInArchiveDestination(sourcePath, archivePath) )
					{
						relevantArchives.Add(archivePath);
						break; // Found a match, no need to check other source paths for this archive
					}
				}
			}

			return relevantArchives;
		}

		/// <summary>
		/// Determines if a Patcher instruction source path would be found in a directory
		/// created by extracting the given archive. This handles the relationship between
		/// Extract instructions (that create directories) and Patcher instructions (that point to executables in those directories).
		/// Uses EnumerateFilesWithWildcards to properly handle path matching like the original logic.
		/// </summary>
		private static bool IsPatcherSourceInArchiveDestination(string patcherSourcePath, string archivePath)
		{
			if ( string.IsNullOrEmpty(patcherSourcePath) || string.IsNullOrEmpty(archivePath) )
				return false;

			try
			{
				// Use EnumerateFilesWithWildcards to check if the Patcher source path would match
				// any files that would be created by extracting this archive
				// This properly handles wildcards and path resolution like the original logic
				List<string> matchingFiles = PathHelper.EnumerateFilesWithWildcards(
					new List<string> { patcherSourcePath },
					new Core.Services.FileSystem.RealFileSystemProvider(),
					includeSubFolders: true
				);

				// Check if any of the matching files are the same as files that would come from this archive
				// We do this by checking if the archive path and patcher source path resolve to the same logical location
				if ( matchingFiles?.Any() == true )
				{
					// Check if the patcher source is within a directory that would be created by the archive
					// This handles the relationship: Archive extracts -> creates directory -> Patcher executable in that directory

					// Get the archive name without extension to determine the extraction directory
					string archiveName = Path.GetFileNameWithoutExtension(archivePath);
					if ( !string.IsNullOrEmpty(archiveName) )
					{
						// Check if the patcher source path contains the archive name (indicating it's in the extracted directory)
						if ( patcherSourcePath.IndexOf(archiveName, StringComparison.OrdinalIgnoreCase) >= 0 )
							return true;
					}
				}

				return false;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error checking if Patcher source '{patcherSourcePath}' matches archive '{archivePath}'");
				return false;
			}
		}

		/// <summary>
		/// Gets all archives from instructions with automatic caching. Cache is automatically invalidated by FileSystemService.
		/// </summary>
		[NotNull]
		public static List<string> GetAllArchivesFromInstructions([NotNull] ModComponent parentComponent)
		{
			if ( parentComponent is null )
				throw new ArgumentNullException(nameof(parentComponent));

			Guid componentGuid = parentComponent.Guid;

			lock ( _cacheLock )
			{
				// Check cache first
				if ( _archiveCache.TryGetValue(componentGuid, out List<string> cachedArchives) )
				{
					Logger.LogVerbose($"[NamespacesIniOptionConverter] Using cached archives for component {componentGuid}");
					return cachedArchives;
				}

				// Cache miss - compute and cache
				Logger.LogVerbose($"[NamespacesIniOptionConverter] Cache miss for component {componentGuid}, computing archives...");
				List<string> allArchives = ComputeAllArchivesFromInstructions(parentComponent);
				_archiveCache[componentGuid] = allArchives;
				return allArchives;
			}
		}

		/// <summary>
		/// Computes all archives from instructions without caching. Used internally by the caching layer.
		/// </summary>
		[NotNull]
		private static List<string> ComputeAllArchivesFromInstructions([NotNull] ModComponent parentComponent)
		{
			if ( parentComponent is null )
				throw new ArgumentNullException(nameof(parentComponent));

			var allArchives = new List<string>();

			var instructions = parentComponent.Instructions.ToList();
			foreach ( Option thisOption in parentComponent.Options )
			{
				if ( thisOption is null )
					continue;

				instructions.AddRange(thisOption.Instructions);
			}

			foreach ( Instruction instruction in instructions )
			{
				if ( instruction.Action != Instruction.ActionType.Extract )
					continue;

				List<string> realPaths = PathHelper.EnumerateFilesWithWildcards(
					instruction.Source.ConvertAll(Utility.ReplaceCustomVariables),
					new Core.Services.FileSystem.RealFileSystemProvider(),
					includeSubFolders: true
				);
				if ( !realPaths?.IsNullOrEmptyCollection() ?? false )
					allArchives.AddRange(realPaths.Where(File.Exists));
			}

			return allArchives;
		}
	}
}
