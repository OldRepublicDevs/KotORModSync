



using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{
	
	
	
	public static class PathManagementService
	{
		
		
		
		
		
		
		public static async Task<List<string>> LoadRecentModDirectoriesAsync([NotNull] string filePath)
		{
			if ( filePath == null )
				throw new ArgumentNullException(nameof(filePath));

			try
			{
				if ( !File.Exists(filePath) )
					return new List<string>();

				var directories = new List<string>();
				using ( var reader = new StreamReader(filePath) )
				{
					string line;
					while ( (line = await reader.ReadLineAsync()) != null )
					{
						if ( !string.IsNullOrWhiteSpace(line) && Directory.Exists(line) )
						{
							directories.Add(line);
						}
					}
				}

				return directories;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return new List<string>();
			}
		}

		
		
		
		
		
		
		public static async Task SaveRecentModDirectoriesAsync([NotNull][ItemNotNull] List<string> directories, [NotNull] string filePath)
		{
			if ( directories == null )
				throw new ArgumentNullException(nameof(directories));
			if ( filePath == null )
				throw new ArgumentNullException(nameof(filePath));

			try
			{
				
				string directory = Path.GetDirectoryName(filePath);
				if ( !string.IsNullOrEmpty(directory) && !Directory.Exists(directory) )
				{
					_ = Directory.CreateDirectory(directory);
				}

				using ( var writer = new StreamWriter(filePath) )
				{
					foreach ( string dir in directories.Take(10) ) 
					{
						if ( !string.IsNullOrWhiteSpace(dir) )
						{
							await writer.WriteLineAsync(dir);
						}
					}
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
			}
		}

		
		
		
		
		
		
		
		public static void AddToRecentDirectories([NotNull] string directory, [NotNull][ItemNotNull] List<string> recentDirectories, int maxCount = 10)
		{
			if ( directory == null )
				throw new ArgumentNullException(nameof(directory));
			if ( recentDirectories == null )
				throw new ArgumentNullException(nameof(recentDirectories));

			if ( string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory) )
				return;

			
			_ = recentDirectories.RemoveAll(d => string.Equals(d, directory, StringComparison.OrdinalIgnoreCase));

			
			recentDirectories.Insert(0, directory);

			
			if ( recentDirectories.Count > maxCount )
			{
				recentDirectories.RemoveRange(maxCount, recentDirectories.Count - maxCount);
			}
		}

		
		
		
		
		
		
		
		
		public static List<string> GetPathSuggestions([NotNull] string inputText, [NotNull][ItemNotNull] List<string> baseDirectories, int maxSuggestions = 10)
		{
			if ( inputText == null )
				throw new ArgumentNullException(nameof(inputText));
			if ( baseDirectories == null )
				throw new ArgumentNullException(nameof(baseDirectories));

			if ( string.IsNullOrWhiteSpace(inputText) )
				return new List<string>();

			var suggestions = new List<string>();

			foreach ( string baseDir in baseDirectories )
			{
				if ( !Directory.Exists(baseDir) )
					continue;

				try
				{
					var directories = Directory.GetDirectories(baseDir, "*" + inputText + "*", SearchOption.TopDirectoryOnly)
						.Where(d => Directory.Exists(d))
						.Take(maxSuggestions - suggestions.Count);

					suggestions.AddRange(directories);

					if ( suggestions.Count >= maxSuggestions )
						break;
				}
				catch ( Exception )
				{
					
					continue;
				}
			}

			return suggestions.Take(maxSuggestions).ToList();
		}

		
		
		
		
		
		
		public static bool IsValidSourcePath([NotNull] string path)
		{
			if ( path == null )
				throw new ArgumentNullException(nameof(path));

			if ( string.IsNullOrWhiteSpace(path) )
				return false;

			try
			{
				var directory = new DirectoryInfo(path);
				return directory.Exists;
			}
			catch
			{
				return false;
			}
		}

		
		
		
		
		
		
		public static bool IsValidInstallPath([NotNull] string path)
		{
			if ( path == null )
				throw new ArgumentNullException(nameof(path));

			if ( string.IsNullOrWhiteSpace(path) )
				return false;

			try
			{
				var directory = new DirectoryInfo(path);
				return directory.Exists;
			}
			catch
			{
				return false;
			}
		}
	}
}
