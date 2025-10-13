// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Services.FileSystem
{
	/// <summary>
	/// Abstraction layer for file system operations to enable both real and virtual (dry-run) execution.
	/// </summary>
	public interface IFileSystemProvider
	{
		/// <summary>
		/// Gets a value indicating whether this is a dry-run (virtual) file system.
		/// </summary>
		bool IsDryRun { get; }

		/// <summary>
		/// Checks if a file exists.
		/// </summary>
		bool FileExists([NotNull] string path);

		/// <summary>
		/// Checks if a directory exists.
		/// </summary>
		bool DirectoryExists([NotNull] string path);

		/// <summary>
		/// Copies a file from source to destination.
		/// </summary>
		Task CopyFileAsync([NotNull] string sourcePath, [NotNull] string destinationPath, bool overwrite);

		/// <summary>
		/// Moves a file from source to destination.
		/// </summary>
		Task MoveFileAsync([NotNull] string sourcePath, [NotNull] string destinationPath, bool overwrite);

		/// <summary>
		/// Deletes a file.
		/// </summary>
		Task DeleteFileAsync([NotNull] string path);

		/// <summary>
		/// Renames a file.
		/// </summary>
		Task RenameFileAsync([NotNull] string sourcePath, [NotNull] string newFileName, bool overwrite);

		/// <summary>
		/// Reads all text from a file.
		/// </summary>
		Task<string> ReadFileAsync([NotNull] string path);

		/// <summary>
		/// Writes all text to a file.
		/// </summary>
		Task WriteFileAsync([NotNull] string path, [NotNull] string contents);

		/// <summary>
		/// Creates a directory.
		/// </summary>
		Task CreateDirectoryAsync([NotNull] string path);

		/// <summary>
		/// Extracts an archive to a destination directory.
		/// </summary>
		/// <returns>List of extracted file paths.</returns>
		Task<List<string>> ExtractArchiveAsync([NotNull] string archivePath, [NotNull] string destinationPath);

		/// <summary>
		/// Gets all files in a directory.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		List<string> GetFilesInDirectory([NotNull] string directoryPath, string searchPattern = "*.*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

		/// <summary>
		/// Gets all directories in a directory.
		/// </summary>
		[NotNull]
		[ItemNotNull]
		List<string> GetDirectoriesInDirectory([NotNull] string directoryPath);

		/// <summary>
		/// Gets the file name from a path.
		/// </summary>
		[NotNull]
		string GetFileName([NotNull] string path);

		/// <summary>
		/// Gets the directory name from a path.
		/// </summary>
		[CanBeNull]
		string GetDirectoryName([NotNull] string path);

		/// <summary>
		/// Executes a program (for dry-run, this just validates the program exists).
		/// </summary>
		Task<(int exitCode, string output, string error)> ExecuteProcessAsync([NotNull] string programPath, [NotNull] string arguments);

		/// <summary>
		/// Gets the actual physical path (for real file system) or virtual path (for dry-run).
		/// </summary>
		[NotNull]
		string GetActualPath([NotNull] string path);
	}
}

