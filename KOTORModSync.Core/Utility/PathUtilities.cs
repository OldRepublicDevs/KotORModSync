



using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
	
	
	
	public static class PathUtilities
	{
		
		
		
		
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

		
		
		
		
		
		[NotNull]
		public static string ExpandPath([CanBeNull] string path)
		{
			if ( string.IsNullOrWhiteSpace(path) ) return string.Empty;
			string p = Environment.ExpandEnvironmentVariables(path);
			return Path.GetFullPath(p);
		}
	}
}
