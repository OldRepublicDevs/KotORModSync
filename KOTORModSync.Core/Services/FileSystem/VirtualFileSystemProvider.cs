// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemUtils;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;

namespace KOTORModSync.Core.Services.FileSystem
{
	public class VirtualFileSystemProvider : IFileSystemProvider
	{
		private readonly HashSet<string> _virtualFiles;
		private readonly HashSet<string> _virtualDirectories;
		private readonly HashSet<string> _removedFiles;
		private readonly List<ValidationIssue> _issues;
		private readonly Dictionary<string, HashSet<string>> _archiveContents;
		private readonly object _lockObject = new object();
		public bool IsDryRun => true;

		[NotNull]
		[ItemNotNull]
		public IReadOnlyList<ValidationIssue> ValidationIssues => _issues.AsReadOnly();

		public VirtualFileSystemProvider()
		{
			_virtualFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			_virtualDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			_removedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			_issues = new List<ValidationIssue>();
			_archiveContents = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
		}

		public void InitializeFromRealFileSystem(string rootPath)
		{
			if ( !Directory.Exists(rootPath) )
				return;

			try
			{
				lock ( _lockObject )
				{
					foreach ( FileInfo file in new DirectoryInfo(rootPath).GetFilesSafely("*.*", SearchOption.AllDirectories) )
					{
						_virtualFiles.Add(file.FullName);

						if ( IsArchiveFile(file.FullName) )
							ScanArchiveContents(file.FullName);
					}

					foreach ( DirectoryInfo dir in new DirectoryInfo(rootPath).GetDirectoriesSafely("*", SearchOption.AllDirectories) )
					{
						_ = _virtualDirectories.Add(dir.FullName);
					}
				}
			}
			catch ( Exception ex )
			{
				AddIssue(ValidationSeverity.Warning, "FileSystemInitialization",
					$"Could not fully initialize virtual file system from real file system: {ex.Message}", null);
			}
		}

		private void ScanArchiveContents([NotNull] string archivePath)
		{
			if ( _archiveContents.ContainsKey(archivePath) )
				return;

			var contents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			try
			{
				using ( FileStream stream = File.OpenRead(archivePath) )
				{
					IArchive archive = GetArchiveFromPath(archivePath, stream);
					if ( archive == null )
					{
						AddIssue(ValidationSeverity.Warning, "ArchiveValidation",
							$"Could not determine archive type: {Path.GetFileName(archivePath)} - may not be a valid archive", archivePath);
						_archiveContents[archivePath] = contents;
						return;
					}

					using ( archive )
					{
						foreach ( IArchiveEntry entry in archive.Entries.Where(e => !e.IsDirectory) )
						{
							_ = contents.Add(entry.Key);
						}
					}
				}
			}
			catch ( Exception ex )
			{
				AddIssue(ValidationSeverity.Warning, "ArchiveValidation",
					$"Unable to scan archive '{Path.GetFileName(archivePath)}' - may be corrupted or not an archive: {ex.Message}", archivePath);
				_archiveContents[archivePath] = contents;
				return;
			}

			_archiveContents[archivePath] = contents;
		}

		public async Task InitializeFromRealFileSystemAsync(string rootPath)
		{
			if ( !Directory.Exists(rootPath) )
				return;

			try
			{
				foreach ( FileInfo file in new DirectoryInfo(rootPath).GetFilesSafely("*.*", SearchOption.AllDirectories) )
				{
					lock ( _lockObject )
					{
						_virtualFiles.Add(file.FullName);
					}

					if ( IsArchiveFile(file.FullName) )
						await ScanArchiveContentsAsync(file.FullName);
				}

				foreach ( DirectoryInfo dir in new DirectoryInfo(rootPath).GetDirectoriesSafely("*", SearchOption.AllDirectories) )
				{
					lock ( _lockObject )
					{
						_ = _virtualDirectories.Add(dir.FullName);
					}
				}
			}
			catch ( Exception ex )
			{
				AddIssue(ValidationSeverity.Warning, "FileSystemInitialization",
					$"Could not fully initialize virtual file system from real file system: {ex.Message}", null);
			}
		}

		private async Task ScanArchiveContentsAsync([NotNull] string archivePath)
		{
			if ( _archiveContents.ContainsKey(archivePath) )
				return;

			var contents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			try
			{
				using ( FileStream stream = File.OpenRead(archivePath) )
				{
					IArchive archive = GetArchiveFromPath(archivePath, stream);
					if ( archive == null )
					{
						AddIssue(ValidationSeverity.Warning, "ArchiveValidation",
							$"Could not determine archive type: {Path.GetFileName(archivePath)} - may not be a valid archive", archivePath);
						_archiveContents[archivePath] = contents;
						await Task.CompletedTask;
						return;
					}

					using ( archive )
					{
						foreach ( IArchiveEntry entry in archive.Entries.Where(e => !e.IsDirectory) )
						{
							_ = contents.Add(entry.Key);
						}
					}
				}
			}
			catch ( Exception ex )
			{
				AddIssue(ValidationSeverity.Warning, "ArchiveValidation",
					$"Unable to scan archive '{Path.GetFileName(archivePath)}' - may be corrupted or not an archive: {ex.Message}", archivePath);
				_archiveContents[archivePath] = contents;
				await Task.CompletedTask;
				return;
			}

			_archiveContents[archivePath] = contents;
			await Task.CompletedTask;
		}

		private static IArchive GetArchiveFromPath([NotNull] string path, [NotNull] Stream stream)
		{
			string extension = Path.GetExtension(path).ToLowerInvariant();

			try
			{
				if ( extension == ".zip" )
					return ZipArchive.Open(stream);
				if ( extension == ".rar" )
					return RarArchive.Open(stream);
				if ( extension == ".7z" )
					return SevenZipArchive.Open(stream);
				if ( extension == ".exe" )
					return SevenZipArchive.Open(stream);

				return ArchiveFactory.Open(stream);
			}
			catch
			{
				return null;
			}
		}

		private static bool IsArchiveFile([NotNull] string path)
		{
			string extension = Path.GetExtension(path).ToLowerInvariant();
			return extension == ".zip"
								|| extension == ".rar"
								|| extension == ".7z"
								|| extension == ".exe";
		}

		public bool FileExists(string path)
		{
			if ( string.IsNullOrWhiteSpace(path) )
				return false;

			if ( _removedFiles.Contains(path) )
			{

				return false;
			}

			bool inVirtual = _virtualFiles.Contains(path);
			bool onDisk = File.Exists(path);
			bool result = inVirtual || onDisk;

			if ( !result && _virtualFiles.Count > 0 )
			{
				AddIssue(ValidationSeverity.Warning, "FileExists",
					$"File not found: {path}", path, affectedComponent: null, affectedInstruction: null, instructionIndex: -1);
				Logger.LogVerbose($"[VFS] FileExists: File not found: {path}");
				return false;
			}

			return result;
		}

		public bool DirectoryExists(string path)
		{
			if ( string.IsNullOrWhiteSpace(path) )
				return false;

			lock ( _lockObject )
			{
				return _virtualDirectories.Contains(path) || Directory.Exists(path);
			}
		}

		public Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite)
		{
			if ( !FileExists(sourcePath) )
			{
				AddIssue(ValidationSeverity.Error, "CopyFile",
					$"Source file does not exist: {sourcePath}", sourcePath);
				return Task.CompletedTask;
			}

			if ( FileExists(destinationPath) && !overwrite )
			{
				AddIssue(ValidationSeverity.Warning, "CopyFile",
					$"Destination file already exists and overwrite is false: {destinationPath}", destinationPath);
				return Task.CompletedTask;
			}

			lock ( _lockObject )
			{
				_ = _virtualFiles.Add(destinationPath);
				_ = _removedFiles.Remove(destinationPath);

				if ( IsArchiveFile(sourcePath) && _archiveContents.TryGetValue(sourcePath, out HashSet<string> archiveContents) )
					_archiveContents[destinationPath] = new HashSet<string>(archiveContents, StringComparer.OrdinalIgnoreCase);

				string parentDir = Path.GetDirectoryName(destinationPath);
				if ( !string.IsNullOrEmpty(parentDir) && !DirectoryExists(parentDir) )
					_ = _virtualDirectories.Add(parentDir);
			}

			return Task.CompletedTask;
		}

		public Task MoveFileAsync(string sourcePath, string destinationPath, bool overwrite)
		{
			Logger.LogVerbose($"[VFS] MoveFileAsync: source={sourcePath}");
			Logger.LogVerbose($"[VFS] MoveFileAsync: dest={destinationPath}");

			if ( !FileExists(sourcePath) )
			{
				AddIssue(ValidationSeverity.Error, "MoveFile",
					$"Source file does not exist: {sourcePath}", sourcePath);
				Logger.LogVerbose($"[VFS] MoveFileAsync: ERROR - source does not exist!");
				return Task.CompletedTask;
			}

			if ( FileExists(destinationPath) && !overwrite )
			{
				AddIssue(ValidationSeverity.Warning, "MoveFile",
					$"Destination file already exists and overwrite is false: {destinationPath}", destinationPath);
				return Task.CompletedTask;
			}

			lock ( _lockObject )
			{
				bool removed = _virtualFiles.Remove(sourcePath);
				_ = _virtualFiles.Add(destinationPath);
				_ = _removedFiles.Add(sourcePath);
				_ = _removedFiles.Remove(destinationPath);
				Logger.LogVerbose($"[VFS] MoveFileAsync: Removed={removed}, total files now={_virtualFiles.Count}");

				if ( IsArchiveFile(sourcePath) && _archiveContents.TryGetValue(sourcePath, out HashSet<string> archiveContents) )
				{
					_ = _archiveContents.Remove(sourcePath);
					_archiveContents[destinationPath] = archiveContents;
				}

				string parentDir = Path.GetDirectoryName(destinationPath);
				if ( !string.IsNullOrEmpty(parentDir) && !DirectoryExists(parentDir) )
					_ = _virtualDirectories.Add(parentDir);
			}

			return Task.CompletedTask;
		}

		public Task DeleteFileAsync(string path)
		{
			if ( !FileExists(path) )
			{
				AddIssue(ValidationSeverity.Warning, "DeleteFile",
					$"Attempting to delete non-existent file: {path}", path);
				return Task.CompletedTask;
			}

			lock ( _lockObject )
			{
				_ = _virtualFiles.Remove(path);
				_ = _removedFiles.Add(path);

				if ( IsArchiveFile(path) )
					_ = _archiveContents.Remove(path);
			}

			return Task.CompletedTask;
		}

		public Task RenameFileAsync(string sourcePath, string newFileName, bool overwrite)
		{
			if ( !FileExists(sourcePath) )
			{
				AddIssue(ValidationSeverity.Error, "RenameFile",
					$"Source file does not exist: {sourcePath}", sourcePath);
				return Task.CompletedTask;
			}

			string directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
			string destinationPath = Path.Combine(directory, newFileName);

			if ( FileExists(destinationPath) && !overwrite )
			{
				AddIssue(ValidationSeverity.Warning, "RenameFile",
					$"Destination file already exists and overwrite is false: {destinationPath}", destinationPath);
				return Task.CompletedTask;
			}

			lock ( _lockObject )
			{
				_ = _virtualFiles.Remove(sourcePath);
				_virtualFiles.Add(destinationPath);
				_ = _removedFiles.Add(sourcePath);
				_ = _removedFiles.Remove(destinationPath);

				if ( IsArchiveFile(sourcePath) && _archiveContents.TryGetValue(sourcePath, out HashSet<string> archiveContents) )
				{
					_ = _archiveContents.Remove(sourcePath);
					_archiveContents[destinationPath] = archiveContents;
				}
			}

			return Task.CompletedTask;
		}

		public Task<string> ReadFileAsync(string path)
		{
			if ( !FileExists(path) )
			{
				AddIssue(ValidationSeverity.Error, "ReadFile",
					$"Cannot read non-existent file: {path}", path);
				return Task.FromResult(string.Empty);
			}

			return Task.FromResult(string.Empty);
		}

		public Task WriteFileAsync(string path, string contents)
		{
			lock ( _lockObject )
			{
				_virtualFiles.Add(path);

				string directory = GetDirectoryName(path);
				if ( !string.IsNullOrEmpty(directory) )
					_ = _virtualDirectories.Add(directory);
			}

			return Task.CompletedTask;
		}

		public Task CreateDirectoryAsync(string path)
		{
			if ( !DirectoryExists(path) )
			{
				lock ( _lockObject )
				{
					_ = _virtualDirectories.Add(path);
				}
			}

			return Task.CompletedTask;
		}

		public async Task<List<string>> ExtractArchiveAsync(string archivePath, string destinationPath)
		{
			var extractedFiles = new List<string>();

			if ( !FileExists(archivePath) )
			{
				AddIssue(ValidationSeverity.Error, "ExtractArchive",
					$"Archive file does not exist: {archivePath}", archivePath);
				return extractedFiles;
			}

			bool hasContents;
			lock ( _lockObject )
			{
				hasContents = _archiveContents.ContainsKey(archivePath);
			}

			if ( !hasContents )
				await ScanArchiveContentsAsync(archivePath);

			HashSet<string> contents;
			lock ( _lockObject )
			{
				if ( !_archiveContents.TryGetValue(archivePath, out contents) )
				{
					AddIssue(ValidationSeverity.Error, "ExtractArchive",
						$"Could not determine archive contents: {archivePath}", archivePath);
					return extractedFiles;
				}
			}

			string extractFolderName = Path.GetFileNameWithoutExtension(archivePath);

			lock ( _lockObject )
			{
				foreach ( string entryPath in contents )
				{

					string normalizedEntry = entryPath.Replace('/', Path.DirectorySeparatorChar);
					string fullPath = Path.Combine(destinationPath, extractFolderName, normalizedEntry);

					fullPath = fullPath.Replace('/', Path.DirectorySeparatorChar).Replace("\\\\", "\\");

					_ = _virtualFiles.Add(fullPath);
					_ = _removedFiles.Remove(fullPath);
					extractedFiles.Add(fullPath);

					string parentDir = Path.GetDirectoryName(fullPath);
					if ( !string.IsNullOrEmpty(parentDir) )
						_ = _virtualDirectories.Add(parentDir);
				}
			}

			return extractedFiles;
		}

		public List<string> GetFilesInDirectory(
			string directoryPath,
			string searchPattern = "*.*",
			SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			if ( !DirectoryExists(directoryPath) )
				return new List<string>();

			string normalizedDir = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			// Take a snapshot of _virtualFiles and _removedFiles inside a lock to avoid enumeration issues
			List<string> virtualFilesSnapshot;
			HashSet<string> removedFilesSnapshot;
			lock ( _lockObject )
			{
				virtualFilesSnapshot = new List<string>(_virtualFiles);
				removedFilesSnapshot = new HashSet<string>(_removedFiles, StringComparer.OrdinalIgnoreCase);
			}

			// Now enumerate the snapshot outside the lock
			foreach ( string f in virtualFilesSnapshot )
			{
				string fileDir = Path.GetDirectoryName(f);
				if ( string.IsNullOrEmpty(fileDir) )
					continue;

				bool matches = searchOption == SearchOption.TopDirectoryOnly
					? string.Equals(fileDir, normalizedDir, StringComparison.OrdinalIgnoreCase)
					: string.Equals(fileDir, normalizedDir, StringComparison.OrdinalIgnoreCase) ||
					  fileDir.StartsWith(normalizedDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

				if ( matches )
					_ = files.Add(f);
			}

			if ( Directory.Exists(directoryPath) )
			{
				try
				{
					foreach ( string f in Directory.GetFiles(directoryPath, "*", searchOption) )
					{
						if ( !removedFilesSnapshot.Contains(f) )
							files.Add(f);
					}
				}
				catch ( UnauthorizedAccessException )
				{
					AddIssue(ValidationSeverity.Warning, "GetFilesInDirectory",
						$"Unauthorized access to directory: {directoryPath}", directoryPath);
				}
				catch ( DirectoryNotFoundException )
				{
					AddIssue(ValidationSeverity.Warning, "GetFilesInDirectory",
						$"Directory not found: {directoryPath}", directoryPath);
				}
			}

			return files.ToList();
		}

		public List<string> GetDirectoriesInDirectory(string directoryPath)
		{
			if ( !DirectoryExists(directoryPath) )
				return new List<string>();

			lock ( _lockObject )
			{
				return _virtualDirectories
					.Where(d =>
					{
						string parentDir = Path.GetDirectoryName(d);
						return string.Equals(parentDir, directoryPath, StringComparison.OrdinalIgnoreCase);
					})
					.ToList();
			}
		}

		public string GetFileName(string path) => Path.GetFileName(path);

		public string GetDirectoryName(string path) => Path.GetDirectoryName(path);

		public Task<(int exitCode, string output, string error)> ExecuteProcessAsync(string programPath, string arguments)
		{

			if ( FileExists(programPath) )
				return Task.FromResult((0, "[Dry-run: Program execution simulated]", string.Empty));
			AddIssue(ValidationSeverity.Error, "ExecuteProcess",
				$"Program file does not exist: {programPath}", programPath);
			return Task.FromResult((1, string.Empty, $"Program not found: {programPath}"));
		}

		public string GetActualPath(string path) => path;

		private void AddIssue(
			ValidationSeverity severity,
			[NotNull] string category,
			[NotNull] string message,
			[CanBeNull] string affectedPath,
			[CanBeNull] ModComponent affectedComponent = null,
			[CanBeNull] Instruction affectedInstruction = null,
			int instructionIndex = -1
		)
		{
			lock ( _lockObject )
			{
				_issues.Add(new ValidationIssue
				{
					Severity = severity,
					Category = category,
					Message = message,
					AffectedPath = affectedPath,
					Timestamp = DateTimeOffset.UtcNow
				});
			}
		}

		[NotNull]
		public List<string> GetTrackedFiles()
		{
			lock ( _lockObject )
			{
				return new List<string>(_virtualFiles);
			}
		}

		[NotNull]
		public List<ValidationIssue> GetValidationIssues()
		{
			lock ( _lockObject )
			{
				return new List<ValidationIssue>(_issues);
			}
		}
	}

	public class ValidationIssue
	{
		public ValidationSeverity Severity { get; set; }
		public string Category { get; set; }
		public string Message { get; set; }
		public string AffectedPath { get; set; }
		public DateTimeOffset Timestamp { get; set; }
		public ModComponent AffectedComponent { get; set; }
		public Instruction AffectedInstruction { get; set; }
		public int InstructionIndex { get; set; }
		public string Icon { get; set; }
		public string IssueType { get; set; }
		public string Solution { get; set; }
		public bool HasSolution => !string.IsNullOrEmpty(Solution);
	}



	public enum ValidationSeverity
	{
		Info,
		Warning,
		Error,
		Critical
	}
}

