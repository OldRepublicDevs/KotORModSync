// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Service for managing file paths and directory operations.
	/// </summary>
	public static class PathManagementService
	{
		/// <summary>
		/// Loads recent mod directories from a file.
		/// </summary>
		/// <param name="filePath">Path to the file containing recent directories.</param>
		/// <returns>List of recent mod directory paths.</returns>
		/// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
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

		/// <summary>
		/// Saves recent mod directories to a file.
		/// </summary>
		/// <param name="directories">List of directory paths to save.</param>
		/// <param name="filePath">Path where to save the directories.</param>
		/// <exception cref="ArgumentNullException">Thrown when directories or filePath is null.</exception>
		public static async Task SaveRecentModDirectoriesAsync([NotNull][ItemNotNull] List<string> directories, [NotNull] string filePath)
		{
			if ( directories == null )
				throw new ArgumentNullException(nameof(directories));
			if ( filePath == null )
				throw new ArgumentNullException(nameof(filePath));

			try
			{
				// Ensure directory exists
				string directory = Path.GetDirectoryName(filePath);
				if ( !string.IsNullOrEmpty(directory) && !Directory.Exists(directory) )
				{
					_ = Directory.CreateDirectory(directory);
				}

				using ( var writer = new StreamWriter(filePath) )
				{
					foreach ( string dir in directories.Take(10) ) // Limit to 10 recent directories
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

		/// <summary>
		/// Adds a directory to the recent directories list.
		/// </summary>
		/// <param name="directory">Directory to add.</param>
		/// <param name="recentDirectories">Current list of recent directories.</param>
		/// <param name="maxCount">Maximum number of recent directories to keep.</param>
		/// <exception cref="ArgumentNullException">Thrown when directory or recentDirectories is null.</exception>
		public static void AddToRecentDirectories([NotNull] string directory, [NotNull][ItemNotNull] List<string> recentDirectories, int maxCount = 10)
		{
			if ( directory == null )
				throw new ArgumentNullException(nameof(directory));
			if ( recentDirectories == null )
				throw new ArgumentNullException(nameof(recentDirectories));

			if ( string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory) )
				return;

			// Remove if already exists
			_ = recentDirectories.RemoveAll(d => string.Equals(d, directory, StringComparison.OrdinalIgnoreCase));

			// Add to beginning
			recentDirectories.Insert(0, directory);

			// Trim to max count
			if ( recentDirectories.Count > maxCount )
			{
				recentDirectories.RemoveRange(maxCount, recentDirectories.Count - maxCount);
			}
		}

		/// <summary>
		/// Gets path suggestions based on input text and existing directories.
		/// </summary>
		/// <param name="inputText">The input text to search for.</param>
		/// <param name="baseDirectories">Base directories to search in.</param>
		/// <param name="maxSuggestions">Maximum number of suggestions to return.</param>
		/// <returns>List of suggested paths.</returns>
		/// <exception cref="ArgumentNullException">Thrown when inputText or baseDirectories is null.</exception>
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
					// Ignore exceptions when searching directories
					continue;
				}
			}

			return suggestions.Take(maxSuggestions).ToList();
		}

		/// <summary>
		/// Validates if a path can be used as a source path.
		/// </summary>
		/// <param name="path">Path to validate.</param>
		/// <returns>True if the path is valid for use as a source path.</returns>
		/// <exception cref="ArgumentNullException">Thrown when path is null.</exception>
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

		/// <summary>
		/// Validates if a path can be used as an install path.
		/// </summary>
		/// <param name="path">Path to validate.</param>
		/// <returns>True if the path is valid for use as an install path.</returns>
		/// <exception cref="ArgumentNullException">Thrown when path is null.</exception>
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
