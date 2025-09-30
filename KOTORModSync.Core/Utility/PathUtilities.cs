// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
	/// <summary>
	/// Utility methods for working with file system paths.
	/// </summary>
	public static class PathUtilities
	{
		/// <summary>
		/// Gets default paths for mod directories based on the operating system.
		/// </summary>
		/// <returns>A collection of default mod directory paths.</returns>
		[NotNull]
		public static IEnumerable<string> GetDefaultPathsForMods()
		{
			OSPlatform os = Utility.GetOperatingSystem();
			var list = new List<string>();
			if ( os == OSPlatform.Windows )
			{
				list.AddRange(new[]
				{
					Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
				});
			}
			else if ( os == OSPlatform.Linux || os == OSPlatform.OSX )
			{
				list.AddRange(new[]
				{
					Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
				});
			}
			return list.Where(Directory.Exists).Distinct().ToList();
		}

		/// <summary>
		/// Gets default paths for KOTOR game installations based on the operating system.
		/// </summary>
		/// <returns>A collection of default game installation paths.</returns>
		[NotNull]
		public static IEnumerable<string> GetDefaultPathsForGame()
		{
			OSPlatform os = Utility.GetOperatingSystem();
			var results = new List<string>();
			if ( os == OSPlatform.Windows )
			{
				results.AddRange(new[]
				{
					@"C:\Program Files\Steam\steamapps\common\swkotor",
					@"C:\Program Files (x86)\Steam\steamapps\common\swkotor",
					@"C:\Program Files\LucasArts\SWKotOR",
					@"C:\Program Files (x86)\LucasArts\SWKotOR",
					@"C:\GOG Games\Star Wars - KotOR",
					@"C:\Program Files\Steam\steamapps\common\Knights of the Old Republic II",
					@"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II",
					@"C:\Program Files\LucasArts\SWKotOR2",
					@"C:\Program Files (x86)\LucasArts\SWKotOR2",
					@"C:\GOG Games\Star Wars - KotOR2",
				});
			}
			else if ( os == OSPlatform.OSX )
			{
				results.AddRange(new[]
				{
					"~/Library/Application Support/Steam/steamapps/common/swkotor/Knights of the Old Republic.app/Contents/Assets",
					"~/Library/Application Support/Steam/steamapps/common/Knights of the Old Republic II/Knights of the Old Republic II.app/Contents/Assets",
					"~/Library/Application Support/Steam/steamapps/common/Knights of the Old Republic II/KOTOR2.app/Contents/GameData/",
				});
			}
			else if ( os == OSPlatform.Linux )
			{
				results.AddRange(new[]
				{
					"~/.steam/steam/steamapps/common/swkotor",
					"~/.steam/steam/steamapps/common/Knights of the Old Republic II",
					"~/.local/share/Steam/steamapps/common/swkotor",
					"~/.local/share/Steam/steamapps/common/Knights of the Old Republic II",
				});
			}

			return results.Select(ExpandPath).Where(Directory.Exists).Distinct().ToList();
		}

		/// <summary>
		/// Expands environment variables in a path string.
		/// </summary>
		/// <param name="path">The path to expand.</param>
		/// <returns>The expanded path, or empty string if input is null or whitespace.</returns>
		[NotNull]
		public static string ExpandPath([CanBeNull] string path)
		{
			if ( string.IsNullOrWhiteSpace(path) ) return string.Empty;
			string p = Environment.ExpandEnvironmentVariables(path);
			return Path.GetFullPath(p);
		}
	}
}
