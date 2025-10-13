// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
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

		private static readonly Dictionary<Guid, List<string>> _archiveCache = new Dictionary<Guid, List<string>>();
		private static readonly object _cacheLock = new object();

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

			if ( dataContextInstruction.Action != Instruction.ActionType.Patcher )
				return null;

			ModComponent parentComponent = dataContextInstruction.GetParentComponent();
			if ( parentComponent is null )
				return null;

			List<string> allArchives = GetAllArchivesFromInstructions(parentComponent);

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



		[NotNull]
		private static List<string> GetArchivesForSpecificInstruction([NotNull] Instruction instruction, [NotNull] List<string> allArchives)
		{
			if ( instruction is null )
				throw new ArgumentNullException(nameof(instruction));
			if ( allArchives is null )
				throw new ArgumentNullException(nameof(allArchives));

			var relevantArchives = new List<string>();

			List<string> instructionSourcePaths = instruction.Source.ConvertAll(Utility.ReplaceCustomVariables);

			foreach ( string archivePath in allArchives )
			{
				if ( string.IsNullOrEmpty(archivePath) )
					continue;

				foreach ( string sourcePath in instructionSourcePaths )
				{
					if ( string.IsNullOrEmpty(sourcePath) )
						continue;

					if ( IsPatcherSourceInArchiveDestination(sourcePath, archivePath) )
					{
						relevantArchives.Add(archivePath);
						break;
					}
				}
			}

			return relevantArchives;
		}


		private static bool IsPatcherSourceInArchiveDestination(string patcherSourcePath, string archivePath)
		{
			if ( string.IsNullOrEmpty(patcherSourcePath) || string.IsNullOrEmpty(archivePath) )
				return false;

			try
			{

				List<string> matchingFiles = PathHelper.EnumerateFilesWithWildcards(
					new List<string> { patcherSourcePath },
					new Core.Services.FileSystem.RealFileSystemProvider(),
					includeSubFolders: true
				);

				if ( matchingFiles?.Any() == true )
				{

					string archiveName = Path.GetFileNameWithoutExtension(archivePath);
					if ( !string.IsNullOrEmpty(archiveName) )
					{

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

		[NotNull]
		public static List<string> GetAllArchivesFromInstructions([NotNull] ModComponent parentComponent)
		{
			if ( parentComponent is null )
				throw new ArgumentNullException(nameof(parentComponent));

			Guid componentGuid = parentComponent.Guid;

			lock ( _cacheLock )
			{

				if ( _archiveCache.TryGetValue(componentGuid, out List<string> cachedArchives) )
				{
					Logger.LogVerbose($"[NamespacesIniOptionConverter] Using cached archives for component {componentGuid}");
					return cachedArchives;
				}

				Logger.LogVerbose($"[NamespacesIniOptionConverter] Cache miss for component {componentGuid}, computing archives...");
				List<string> allArchives = ComputeAllArchivesFromInstructions(parentComponent);
				_archiveCache[componentGuid] = allArchives;
				return allArchives;
			}
		}

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
