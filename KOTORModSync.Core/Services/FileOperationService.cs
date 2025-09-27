// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Utility;
using SharpCompress.Archives;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Service for handling file operations and validation
	/// </summary>
	public class FileOperationService
	{
		/// <summary>
		/// Validates a file for loading as an instruction file
		/// </summary>
		/// <param name="filePath">Path to the file to validate</param>
		/// <returns>True if file is valid, false otherwise</returns>
		public bool ValidateInstructionFile([NotNull] string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				return false;

			if (!PathValidator.IsValidPath(filePath))
				return false;

			var fileInfo = new FileInfo(filePath);
			if (!fileInfo.Exists)
				return false;

			// Verify the file size
			const int maxInstructionSize = 524288000; // instruction file larger than 500mb is probably unsupported
			if (fileInfo.Length > maxInstructionSize)
			{
				Logger.Log($"Invalid instruction file selected: '{fileInfo.Name}'");
				return false;
			}

			// Check file extension
			string fileExt = Path.GetExtension(filePath);
			return fileExt.Equals(".toml", StringComparison.OrdinalIgnoreCase) ||
			       fileExt.Equals(".tml", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Loads components from a TOML file
		/// </summary>
		/// <param name="filePath">Path to the TOML file</param>
		/// <returns>List of loaded components</returns>
		public List<Component> LoadComponentsFromFile([NotNull] string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

			if (!ValidateInstructionFile(filePath))
				throw new InvalidOperationException($"Invalid instruction file: {filePath}");

			return Component.ReadComponentsFromFile(filePath);
		}

		/// <summary>
		/// Loads components from markdown content
		/// </summary>
		/// <param name="markdownContent">Markdown content to parse</param>
		/// <returns>List of parsed components</returns>
		public List<Component> LoadComponentsFromMarkdown([NotNull] string markdownContent)
		{
			if (string.IsNullOrEmpty(markdownContent))
				throw new ArgumentException("Markdown content cannot be null or empty", nameof(markdownContent));

			return ModParser.ParseMods(markdownContent) ?? new List<Component>();
		}

		/// <summary>
		/// Saves components to a TOML file
		/// </summary>
		/// <param name="components">Components to save</param>
		/// <param name="filePath">Path where to save the file</param>
		public async Task SaveComponentsToFileAsync([NotNull][ItemNotNull] List<Component> components, [NotNull] string filePath)
		{
			if (components == null)
				throw new ArgumentNullException(nameof(components));
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

			await Logger.LogVerboseAsync($"Saving TOML config to {filePath}");

			using (var writer = new StreamWriter(filePath))
			{
				foreach (Component component in components)
				{
					string tomlContents = component.SerializeComponent();
					await writer.WriteLineAsync(tomlContents);
				}
			}
		}

		/// <summary>
		/// Generates documentation for components
		/// </summary>
		/// <param name="components">Components to document</param>
		/// <returns>Generated documentation string</returns>
		public string GenerateComponentDocumentation([NotNull][ItemNotNull] List<Component> components)
		{
			if (components == null)
				throw new ArgumentNullException(nameof(components));

			return Component.GenerateModDocumentation(components);
		}

		/// <summary>
		/// Saves documentation to a file
		/// </summary>
		/// <param name="filePath">Path where to save the documentation</param>
		/// <param name="documentation">Documentation content</param>
		public async Task SaveDocumentationToFileAsync([NotNull] string filePath, [NotNull] string documentation)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
			if (string.IsNullOrEmpty(documentation))
				throw new ArgumentException("Documentation cannot be null or empty", nameof(documentation));

			try
			{
				using (var writer = new StreamWriter(filePath))
				{
					await writer.WriteAsync(documentation);
					await writer.FlushAsync();
				}
			}
			catch (Exception e)
			{
				await Logger.LogExceptionAsync(e);
				throw;
			}
		}

		/// <summary>
		/// Analyzes an archive file for executable content
		/// </summary>
		/// <param name="filePath">Path to the archive file</param>
		/// <returns>Path to executable found in archive, or null if none found</returns>
		public async Task<string> AnalyzeArchiveForExecutableAsync([NotNull] string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
				throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

			try
			{
				(IArchive archive, FileStream archiveStream) = ArchiveHelper.OpenArchive(filePath);
				if (archive is null || archiveStream is null)
					return null;

				string exePath = ArchiveHelper.AnalyzeArchiveForExe(archiveStream, archive);
				await Logger.LogVerboseAsync(exePath);
				return exePath;
			}
			catch (Exception e)
			{
				await Logger.LogExceptionAsync(e);
				return null;
			}
		}

		/// <summary>
		/// Fixes file and folder permissions for a given path
		/// </summary>
		/// <param name="folderPath">Path to fix permissions for</param>
		public async Task FixPathPermissionsAsync([NotNull] string folderPath)
		{
			if (string.IsNullOrEmpty(folderPath))
				throw new ArgumentException("Folder path cannot be null or empty", nameof(folderPath));

			DirectoryInfo directory = PathHelper.TryGetValidDirectoryInfo(folderPath);
			if (directory is null || !directory.Exists)
			{
				await Logger.LogErrorAsync($"Directory not found: '{folderPath}', skipping...");
				return;
			}

			await FilePermissionHelper.FixPermissionsAsync(directory);
			Logger.Log($"Completed FixPathPermissions at '{directory.FullName}'");
		}

		/// <summary>
		/// Fixes iOS case sensitivity by lowercasing all files and folders
		/// </summary>
		/// <param name="folderPath">Path to fix case sensitivity for</param>
		/// <returns>Number of files/folders renamed</returns>
		public async Task<int> FixIOSCaseSensitivityAsync([NotNull] string folderPath)
		{
			if (string.IsNullOrEmpty(folderPath))
				throw new ArgumentException("Folder path cannot be null or empty", nameof(folderPath));

			var directory = new DirectoryInfo(folderPath);
			if (!directory.Exists)
			{
				await Logger.LogErrorAsync($"Directory not found: '{directory.FullName}', skipping...");
				return 0;
			}

			return await FixIOSCaseSensitivityCoreAsync(directory);
		}

		private async Task<int> FixIOSCaseSensitivityCoreAsync([NotNull] DirectoryInfo gameDirectory)
		{
			try
			{
				int numObjectsRenamed = 0;
				// Process all files in the current directory
				foreach (FileInfo file in gameDirectory.GetFilesSafely())
				{
					string lowercaseName = file.Name.ToLowerInvariant();
					string dirName = file.DirectoryName;
					if (dirName is null)
						continue;

					string lowercasePath = Path.Combine(dirName, lowercaseName);
					if (lowercasePath != file.FullName)
					{
						await Logger.LogAsync($"Rename file '{file.FullName}' -> '{lowercasePath}'");
						File.Move(file.FullName, lowercasePath);
						numObjectsRenamed++;
					}
				}

				// Recursively process all subdirectories
				foreach (DirectoryInfo directory in gameDirectory.GetDirectoriesSafely())
				{
					string lowercaseName = directory.Name.ToLowerInvariant();
					string dirParentPath = directory.Parent?.FullName;
					if (dirParentPath is null)
						continue;

					string lowercasePath = Path.Combine(dirParentPath, lowercaseName);
					if (lowercasePath != directory.FullName)
					{
						await Logger.LogAsync($"Rename folder '{directory.FullName}' -> '{lowercasePath}'");
						Directory.Move(directory.FullName, lowercasePath);
						numObjectsRenamed++;
						// Recurse into the subdirectory
						numObjectsRenamed += await FixIOSCaseSensitivityCoreAsync(new DirectoryInfo(lowercasePath));
					}
					else
					{
						// Directory name is already lowercase, just recurse
						await Logger.LogAsync($"Recursing into folder '{directory.FullName}'...");
						numObjectsRenamed += await FixIOSCaseSensitivityCoreAsync(directory);
					}
				}

				return numObjectsRenamed;
			}
			catch (Exception exception)
			{
				await Logger.LogExceptionAsync(exception);
				return -1;
			}
		}
	}
}
