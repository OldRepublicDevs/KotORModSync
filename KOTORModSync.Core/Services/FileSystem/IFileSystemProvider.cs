// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Services.FileSystem
{



	public interface IFileSystemProvider
	{
		bool IsDryRun { get; }

		bool FileExists([NotNull] string path);

		bool DirectoryExists([NotNull] string path);

		Task CopyFileAsync([NotNull] string sourcePath, [NotNull] string destinationPath, bool overwrite);

		Task MoveFileAsync([NotNull] string sourcePath, [NotNull] string destinationPath, bool overwrite);

		Task DeleteFileAsync([NotNull] string path);

		Task RenameFileAsync([NotNull] string sourcePath, [NotNull] string newFileName, bool overwrite);

		Task<string> ReadFileAsync([NotNull] string path);

		Task WriteFileAsync([NotNull] string path, [NotNull] string contents);

		Task CreateDirectoryAsync([NotNull] string path);


		Task<List<string>> ExtractArchiveAsync([NotNull] string archivePath, [NotNull] string destinationPath);

		[NotNull]
		[ItemNotNull]
		List<string> GetFilesInDirectory([NotNull] string directoryPath, string searchPattern = "*.*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

		[NotNull]
		[ItemNotNull]
		List<string> GetDirectoriesInDirectory([NotNull] string directoryPath);

		[NotNull]
		string GetFileName([NotNull] string path);

		[CanBeNull]
		string GetDirectoryName([NotNull] string path);

		Task<(int exitCode, string output, string error)> ExecuteProcessAsync([NotNull] string programPath, [NotNull] string arguments);

		[NotNull]
		string GetActualPath([NotNull] string path);
	}
}

