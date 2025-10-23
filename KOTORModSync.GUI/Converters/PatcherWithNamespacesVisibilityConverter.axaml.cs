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
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
	public partial class PatcherWithNamespacesVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ( !(value is Instruction instruction) )
				return false;

			if ( instruction.Action != Instruction.ActionType.Patcher )
				return false;

			var allArchives = NamespacesIniOptionConverter.GetAllArchivesFromInstructions(instruction.GetParentComponent());

			var relevantArchives = GetArchivesForSpecificInstruction(instruction, allArchives);

			foreach ( string archivePath in relevantArchives )
			{
				if ( string.IsNullOrEmpty(archivePath) )
					continue;

				var result = Core.TSLPatcher.IniHelper.ReadNamespacesIniFromArchive(archivePath);
				if ( result != null && result.Any() )
				{

					var optionNames = result.Where(section =>
						section.Key != "Namespaces" &&
						section.Value != null &&
						section.Value.ContainsKey("Name")).ToList();

					if ( optionNames.Any() )
						return true;
				}
			}

			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotImplementedException();



		[NotNull]
		private static List<string> GetArchivesForSpecificInstruction([NotNull] Instruction instruction, [NotNull] List<string> allArchives)
		{
			if ( instruction is null )
				throw new ArgumentNullException(nameof(instruction));
			if ( allArchives is null )
				throw new ArgumentNullException(nameof(allArchives));

			var relevantArchives = new List<string>();

			foreach ( string archivePath in allArchives )
			{
				if ( string.IsNullOrEmpty(archivePath) )
					continue;

				foreach ( string sourcePath in instruction.Source )
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
	}
}
