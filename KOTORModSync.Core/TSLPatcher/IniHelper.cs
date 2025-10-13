// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Utility;
using SharpCompress.Archives;

namespace KOTORModSync.Core.TSLPatcher
{
	public static class IniHelper
	{
		public static void ReplaceIniPattern([NotNull] DirectoryInfo directory, string pattern, string replacement)
		{
			if ( directory == null )
				throw new ArgumentNullException(nameof(directory));

			FileInfo[] iniFiles = directory.GetFilesSafely(searchPattern: "*.ini", SearchOption.AllDirectories);
			if ( iniFiles.Length == 0 )
				throw new InvalidOperationException("No .ini files found!");

			foreach ( FileInfo file in iniFiles )
			{
				string filePath = file.FullName;
				string fileContents = File.ReadAllText(filePath);

				fileContents = Regex.Replace(fileContents, pattern, replacement, RegexOptions.IgnoreCase | RegexOptions.Multiline);

				// Write the modified file contents back to the file
				File.WriteAllText(filePath, fileContents);
			}
		}

		public static Dictionary<string, Dictionary<string, string>> ReadNamespacesIniFromArchive(
			[NotNull] string archivePath
		)
		{
			if ( string.IsNullOrWhiteSpace(archivePath) )
				throw new ArgumentException(message: "Value cannot be null or whitespace.", nameof(archivePath));

			(IArchive archive, FileStream thisStream) = ArchiveHelper.OpenArchive(archivePath);
			using ( thisStream )
			{
				if ( !(archive is null) && !(thisStream is null) )
					return TraverseDirectories(archive.Entries);
			}

			return null; // Folder 'tslpatchdata' or 'namespaces.ini' not found in the archive.
		}

		public static Dictionary<string, Dictionary<string, string>> ReadNamespacesIniFromArchive(
			[NotNull] Stream archiveStream
		)
		{
			if ( archiveStream is null )
				throw new ArgumentNullException(nameof(archiveStream));

			try
			{
				using ( IArchive archive = ArchiveFactory.Open(archiveStream) )
				{
					return TraverseDirectories(archive.Entries);
				}
			}
			catch ( InvalidOperationException )
			{
				// Archive type cannot be determined
				return null;
			}
			catch ( Exception )
			{
				// Any other archive-related exception
				return null;
			}
		}
		private static readonly char[] s_separator = new[] { '/', '\\' };

		private static Dictionary<string, Dictionary<string, string>> TraverseDirectories(
			IEnumerable<IArchiveEntry> entries
		)
		{
			IEnumerable<IArchiveEntry> archiveEntries = entries as IArchiveEntry[]
														?? entries?.ToArray() ?? throw new NullReferenceException(nameof(entries));
			foreach ( IArchiveEntry entry in archiveEntries )
			{
				if ( entry != null && entry.IsDirectory )
				{
					// Recurse into subdirectories
					IEnumerable<IArchiveEntry> subDirectoryEntries = archiveEntries.Where(
						e => e != null && (e.Key.StartsWith(entry.Key + "/") || e.Key.StartsWith(entry.Key + "\\"))
					);
					Dictionary<string, Dictionary<string, string>> result = TraverseDirectories(
						subDirectoryEntries
					);
					if ( result != null )
						return result;
				}
				else
				{
					string directoryName = Path.GetDirectoryName(entry?.Key.Replace(oldChar: '\\', newChar: '/'));
					string fileName = Path.GetFileName(entry?.Key);

					// Check if this is namespaces.ini in a tslpatchdata folder
					bool isTslPatchDataFolder = directoryName?.Split(s_separator, StringSplitOptions.RemoveEmptyEntries)
						.Any(dir => dir.Equals("tslpatchdata", StringComparison.OrdinalIgnoreCase)) ?? false;

					if ( !string.Equals(fileName, "namespaces.ini", StringComparison.OrdinalIgnoreCase) ||
						 !isTslPatchDataFolder )
					{
						continue;
					}

					using ( var reader = new StreamReader(entry.OpenEntryStream()) )
						return ParseNamespacesIni(reader);
				}
			}

			return null; // No matching 'tslpatchdata/namespaces.ini' found in this directory or its subdirectories
		}

		public static Dictionary<string, Dictionary<string, string>> ParseNamespacesIni(StreamReader reader)
		{
			if ( reader is null )
				throw new ArgumentNullException(nameof(reader));

			var sections = new Dictionary<string, Dictionary<string, string>>();
			Dictionary<string, string> currentSection = null;

			string line;
			while ( (line = reader.ReadLine()) != null )
			{
				line = line.Trim();

				// Checks if the line is a section header
				if ( line.StartsWith("[") && line.EndsWith("]") )
				{
					string sectionName = line.Substring(startIndex: 1, line.Length - 2);
					currentSection = new Dictionary<string, string>();
					sections[sectionName] = currentSection;
				}
				// Checks if the line is a key-value pair
				else if ( currentSection != null && line.Contains("=") )
				{
					string[] keyValue = line.Split('=');
					if ( keyValue.Length != 2 )
						continue;

					string key = keyValue[0].Trim();
					string value = keyValue[1].Trim();
					currentSection[key] = value;
				}
			}

			return sections;
		}
	}
}
