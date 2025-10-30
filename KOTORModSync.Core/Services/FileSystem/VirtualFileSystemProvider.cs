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
			if (!Directory.Exists(rootPath))
				return;

			try
			{
				lock (_lockObject)
				{
					var files = new DirectoryInfo(rootPath).GetFilesSafely("*.*", SearchOption.AllDirectories).ToList();
					foreach (string filePath in files.Select(file => file.FullName))
					{
						_virtualFiles.Add(filePath);

						if (IsArchiveFile(filePath))
							ScanArchiveContents(filePath);
					}

					foreach (DirectoryInfo dir in new DirectoryInfo(rootPath).GetDirectoriesSafely("*", SearchOption.AllDirectories))
					{
						_ = _virtualDirectories.Add(dir.FullName);
					}
				}
			}
			catch (Exception ex)
			{
				AddIssue(ValidationSeverity.Warning, "FileSystemInitialization",
					$"Could not fully initialize virtual file system from real file system: {ex.Message}", null);
			}
		}

		private void ScanArchiveContents([NotNull] string archivePath)
		{
			if (_archiveContents.ContainsKey(archivePath))
				return;

			HashSet<string> contents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			try
			{
				using (FileStream stream = File.OpenRead(archivePath))
				{
					IArchive archive = GetArchiveFromPath(archivePath, stream);
					if (archive is null)
					{
						AddIssue(ValidationSeverity.Warning, "ArchiveValidation",
							$"Could not determine archive type: {Path.GetFileName(archivePath)} - may not be a valid archive", archivePath);
						_archiveContents[archivePath] = contents;
						return;
					}

					using (archive)
					{
						foreach (IArchiveEntry entry in archive.Entries.Where(e => !e.IsDirectory))
						{
							_ = contents.Add(entry.Key);
						}
					}
				}
			}
			catch (Exception ex)
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
			if (!Directory.Exists(rootPath))
				return;

			try
			{
				foreach (FileInfo file in new DirectoryInfo(rootPath).GetFilesSafely("*.*", SearchOption.AllDirectories))
				{
					lock (_lockObject)
					{
						_virtualFiles.Add(file.FullName);
					}

					if (IsArchiveFile(file.FullName))
						await ScanArchiveContentsAsync(file.FullName).ConfigureAwait(false);
				}

				foreach (DirectoryInfo dir in new DirectoryInfo(rootPath).GetDirectoriesSafely("*", SearchOption.AllDirectories))
				{
					lock (_lockObject)
					{
						_ = _virtualDirectories.Add(dir.FullName);
					}
				}
			}
			catch (Exception ex)
			{
				AddIssue(ValidationSeverity.Warning, "FileSystemInitialization",
					$"Could not fully initialize virtual file system from real file system: {ex.Message}", null);
			}
		}

		/// <summary>
		/// Initializes VFS only with files relevant to the specific component(s), not the entire directory.
		/// This is much faster than loading all of the potentially thousands of files in the mod directory.
		/// </summary>
		public async Task InitializeFromRealFileSystemForComponentAsync(string rootPath, ModComponent component)
		{
			if (component != null)
			{
				await InitializeFromRealFileSystemForComponentsAsync(rootPath, new List<ModComponent> { component }).ConfigureAwait(false);
			}
		}

		/// <summary>
		/// Initializes VFS only with files relevant to the specified components, not the entire directory.
		/// This is much faster than loading all of the potentially thousands of files in the mod directory.
		/// </summary>
		public async Task InitializeFromRealFileSystemForComponentsAsync(string rootPath, List<ModComponent> components)
		{
			if (!Directory.Exists(rootPath) || components is null || components.Count == 0)
				return;

			try
			{
				// Only load files that are referenced by the components' instructions
				HashSet<string> relevantFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (ModComponent component in components)
				{
					if (component is null)
						continue;

					// Get all source paths from component instructions
					foreach (Instruction instruction in component.Instructions)
					{
						if (instruction.Source != null)
						{
							foreach (string sourcePath in instruction.Source)
							{
								if (string.IsNullOrWhiteSpace(sourcePath))
									continue;

								string resolvedPath = ResolvePath(sourcePath);
								if (File.Exists(resolvedPath))
								{
									relevantFiles.Add(resolvedPath);
								}
							}
						}
					}

					// Get all source paths from option instructions
					foreach (Option option in component.Options)
					{
						foreach (Instruction instruction in option.Instructions)
						{
							if (instruction.Source != null)
							{
								foreach (string sourcePath in instruction.Source)
								{
									if (string.IsNullOrWhiteSpace(sourcePath))
										continue;

									string resolvedPath = ResolvePath(sourcePath);
									if (File.Exists(resolvedPath))
									{
										relevantFiles.Add(resolvedPath);
									}
								}
							}
						}
					}
				}

				// Load only the relevant files into VFS
				foreach (string filePath in relevantFiles)
				{
					lock (_lockObject)
					{
						_virtualFiles.Add(filePath);
					}

					// If it's an archive, scan its contents
					if (IsArchiveFile(filePath))
					{
						await ScanArchiveContentsAsync(filePath).ConfigureAwait(false);
					}
				}

				// Add directories for the relevant files
				foreach (string filePath in relevantFiles)
				{
					string directory = Path.GetDirectoryName(filePath);
					if (!string.IsNullOrEmpty(directory))
					{
						lock (_lockObject)
						{
							_ = _virtualDirectories.Add(directory);
						}
					}
				}
			}
			catch (Exception ex)
			{
				AddIssue(ValidationSeverity.Warning, "FileSystemInitialization",
					$"Could not fully initialize virtual file system for components: {ex.Message}", null);
			}
		}

		private static string ResolvePath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return path ?? string.Empty;

			if (path.Contains("<<modDirectory>>"))
			{
				string modDir = MainConfig.SourcePath?.FullName ?? "";
				path = path.Replace("<<modDirectory>>", modDir);
			}

			if (path.Contains("<<kotorDirectory>>"))
			{
				string kotorDir = MainConfig.DestinationPath?.FullName ?? "";
				path = path.Replace("<<kotorDirectory>>", kotorDir);
			}

			return path;
		}

		private async Task ScanArchiveContentsAsync([NotNull] string archivePath)
		{
			if (_archiveContents.ContainsKey(archivePath))
				return;

			HashSet<string> contents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			try
			{
				using (FileStream stream = File.OpenRead(archivePath))
				{
					IArchive archive = GetArchiveFromPath(archivePath, stream);
					if (archive is null)
					{
						AddIssue(ValidationSeverity.Warning, "ArchiveValidation",
							$"Could not determine archive type: {Path.GetFileName(archivePath)} - may not be a valid archive", archivePath);
						_archiveContents[archivePath] = contents;
						await Task.CompletedTask.ConfigureAwait(false);
						return;
					}

					using (archive)
					{
						foreach (IArchiveEntry entry in archive.Entries.Where(e => !e.IsDirectory))
						{
							_ = contents.Add(entry.Key);
						}
					}
				}
			}
			catch (Exception ex)
			{
				AddIssue(ValidationSeverity.Warning, "ArchiveValidation",
					$"Unable to scan archive '{Path.GetFileName(archivePath)}' - may be corrupted or not an archive: {ex.Message}", archivePath);
				_archiveContents[archivePath] = contents;
				await Task.CompletedTask.ConfigureAwait(false);
				return;
			}

			_archiveContents[archivePath] = contents;
			await Task.CompletedTask.ConfigureAwait(false);
		}

		private static IArchive GetArchiveFromPath([NotNull] string path, [NotNull] Stream stream)
		{
			string extension = Path.GetExtension(path).ToLowerInvariant();

			try
			{

				if (


string.Equals(extension, ".zip", StringComparison.Ordinal))
					return ZipArchive.Open(stream);

				if (string.Equals(extension, ".rar", StringComparison.Ordinal))
					return RarArchive.Open(stream);
				if (string.Equals(extension, ".7z", StringComparison.Ordinal))
					return SevenZipArchive.Open(stream);
				if (string.Equals(extension, ".exe", StringComparison.Ordinal))
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

			return string.Equals(extension, ".zip"
, StringComparison.Ordinal) || string.Equals(extension, ".rar"
, StringComparison.Ordinal) || string.Equals(extension, ".7z"
, StringComparison.Ordinal) || string.Equals(extension, ".exe", StringComparison.Ordinal);
		}

		public bool FileExists(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return false;

			if (_removedFiles.Contains(path))
			{

				return false;
			}

			bool inVirtual = _virtualFiles.Contains(path);
			bool onDisk = File.Exists(path);
			bool result = inVirtual || onDisk;

			if (!result && _virtualFiles.Count > 0)
			{
				AddIssue(ValidationSeverity.Warning, "FileExists",
					$"File not found: {path}", affectedPath: path);
				// Only log in debug mode to avoid performance issues
				if (MainConfig.DebugLogging)
					Logger.LogVerbose($"[VFS] FileExists: File not found: {path}");
				return false;
			}

			return result;
		}

		public bool DirectoryExists(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return false;

			lock (_lockObject)
			{
				return _virtualDirectories.Contains(path) || Directory.Exists(path);
			}
		}

		public Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite)
		{
			if (!FileExists(sourcePath))
			{
				AddIssue(ValidationSeverity.Error, "CopyFile",
					$"Source file does not exist: {sourcePath}", sourcePath);
				return Task.CompletedTask;
			}

			if (FileExists(destinationPath) && !overwrite)
			{
				AddIssue(ValidationSeverity.Warning, "CopyFile",
					$"Destination file already exists and overwrite is false: {destinationPath}", destinationPath);
				return Task.CompletedTask;
			}

			lock (_lockObject)
			{
				_ = _virtualFiles.Add(destinationPath);
				_ = _removedFiles.Remove(destinationPath);

				if (IsArchiveFile(sourcePath) && _archiveContents.TryGetValue(sourcePath, out HashSet<string> archiveContents))
					_archiveContents[destinationPath] = new HashSet<string>(archiveContents, StringComparer.OrdinalIgnoreCase);

				string parentDir = Path.GetDirectoryName(destinationPath);
				if (!string.IsNullOrEmpty(parentDir) && !DirectoryExists(parentDir))
					_ = _virtualDirectories.Add(parentDir);
			}

			return Task.CompletedTask;
		}

		public Task MoveFileAsync(string sourcePath, string destinationPath, bool overwrite)
		{
			// Only log VFS operations in debug mode to avoid performance issues
			if (MainConfig.DebugLogging)
			{
				Logger.LogVerbose($"[VFS] MoveFileAsync: source={sourcePath}");
				Logger.LogVerbose($"[VFS] MoveFileAsync: dest={destinationPath}");
				Logger.LogVerbose($"[VFS] MoveFileAsync: overwrite={overwrite}");
			}

			bool sourceExistsInVirtualFiles = false;
			bool sourceExistsOnDisk = false;
			bool sourceInRemovedFiles = false;
			lock (_lockObject)
			{
				sourceExistsInVirtualFiles = _virtualFiles.Contains(sourcePath);
				sourceInRemovedFiles = _removedFiles.Contains(sourcePath);
			}
			sourceExistsOnDisk = File.Exists(sourcePath);

			if (MainConfig.DebugLogging)
			{
				Logger.LogVerbose($"[VFS] MoveFileAsync: sourceExistsInVirtualFiles={sourceExistsInVirtualFiles}");
				Logger.LogVerbose($"[VFS] MoveFileAsync: sourceExistsOnDisk={sourceExistsOnDisk}");
				Logger.LogVerbose($"[VFS] MoveFileAsync: sourceInRemovedFiles={sourceInRemovedFiles}");
			}

			if (!FileExists(sourcePath))
			{
				AddIssue(ValidationSeverity.Error, "MoveFile",
					$"Source file does not exist: {sourcePath}", sourcePath);
				if (MainConfig.DebugLogging)
				{
					Logger.LogVerbose($"[VFS] MoveFileAsync: ERROR - source does not exist!");
					Logger.LogVerbose($"[VFS] MoveFileAsync: _virtualFiles count={_virtualFiles.Count}");
					Logger.LogVerbose($"[VFS] MoveFileAsync: _removedFiles count={_removedFiles.Count}");
				}
				return Task.CompletedTask;
			}

			if (FileExists(destinationPath) && !overwrite)
			{
				AddIssue(ValidationSeverity.Warning, "MoveFile",
					$"Destination file already exists and overwrite is false: {destinationPath}", destinationPath);
				return Task.CompletedTask;
			}

			lock (_lockObject)
			{
				bool removed = _virtualFiles.Remove(sourcePath);
				_ = _virtualFiles.Add(destinationPath);
				_ = _removedFiles.Add(sourcePath);
				_ = _removedFiles.Remove(destinationPath);

				if (MainConfig.DebugLogging)
				{
					Logger.LogVerbose($"[VFS] MoveFileAsync: Removed={removed}, total files now={_virtualFiles.Count}");
					Logger.LogVerbose($"[VFS] MoveFileAsync: Added destination to _virtualFiles, removed source from _virtualFiles");
					Logger.LogVerbose($"[VFS] MoveFileAsync: Added source to _removedFiles, removed destination from _removedFiles");
				}

				if (IsArchiveFile(sourcePath) && _archiveContents.TryGetValue(sourcePath, out HashSet<string> archiveContents))
				{
					_ = _archiveContents.Remove(sourcePath);
					_archiveContents[destinationPath] = archiveContents;
					if (MainConfig.DebugLogging)
						Logger.LogVerbose($"[VFS] MoveFileAsync: Moved archive contents mapping from source to destination");
				}

				string parentDir = Path.GetDirectoryName(destinationPath);
				if (!string.IsNullOrEmpty(parentDir) && !DirectoryExists(parentDir))
				{
					_ = _virtualDirectories.Add(parentDir);
					if (MainConfig.DebugLogging)
						Logger.LogVerbose($"[VFS] MoveFileAsync: Added parent directory to _virtualDirectories: {parentDir}");
				}
			}

			if (MainConfig.DebugLogging)
				Logger.LogVerbose($"[VFS] MoveFileAsync: Operation completed successfully");
			return Task.CompletedTask;
		}

		public Task DeleteFileAsync(string path)
		{
			if (!FileExists(path))
			{
				AddIssue(ValidationSeverity.Warning, "DeleteFile",
					$"Attempting to delete non-existent file: {path}", path);
				return Task.CompletedTask;
			}

			lock (_lockObject)
			{
				_ = _virtualFiles.Remove(path);
				_ = _removedFiles.Add(path);

				if (IsArchiveFile(path))
					_ = _archiveContents.Remove(path);
			}

			return Task.CompletedTask;
		}

		public Task RenameFileAsync(string sourcePath, string newFileName, bool overwrite)
		{
			if (!FileExists(sourcePath))
			{
				AddIssue(ValidationSeverity.Error, "RenameFile",
					$"Source file does not exist: {sourcePath}", sourcePath);
				return Task.CompletedTask;
			}

			string directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
			string destinationPath = Path.Combine(directory, newFileName);

			if (FileExists(destinationPath) && !overwrite)
			{
				AddIssue(ValidationSeverity.Warning, "RenameFile",
					$"Destination file already exists and overwrite is false: {destinationPath}", destinationPath);
				return Task.CompletedTask;
			}

			lock (_lockObject)
			{
				_ = _virtualFiles.Remove(sourcePath);
				_virtualFiles.Add(destinationPath);
				_ = _removedFiles.Add(sourcePath);
				_ = _removedFiles.Remove(destinationPath);

				if (IsArchiveFile(sourcePath) && _archiveContents.TryGetValue(sourcePath, out HashSet<string> archiveContents))
				{
					_ = _archiveContents.Remove(sourcePath);
					_archiveContents[destinationPath] = archiveContents;
				}
			}

			return Task.CompletedTask;
		}

		public Task<string> ReadFileAsync(string path)
		{
			if (!FileExists(path))
			{
				AddIssue(ValidationSeverity.Error, "ReadFile",
					$"Cannot read non-existent file: {path}", path);
				return Task.FromResult(string.Empty);
			}

			return Task.FromResult(string.Empty);
		}

		public Task WriteFileAsync(string path, string contents)
		{
			lock (_lockObject)
			{
				_virtualFiles.Add(path);

				string directory = GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directory))
					_ = _virtualDirectories.Add(directory);
			}

			return Task.CompletedTask;
		}

		public Task CreateDirectoryAsync(string path)
		{
			if (!DirectoryExists(path))
			{
				lock (_lockObject)
				{
					_ = _virtualDirectories.Add(path);
				}
			}

			return Task.CompletedTask;
		}

		public async Task<List<string>> ExtractArchiveAsync(string archivePath, string destinationPath)
		{
			List<string> extractedFiles = new List<string>();

			if (!FileExists(archivePath))
			{
				AddIssue(ValidationSeverity.Error, "ExtractArchive",
					$"Archive file does not exist: {archivePath}", archivePath);
				return extractedFiles;
			}

			bool hasContents;
			lock (_lockObject)
			{
				hasContents = _archiveContents.ContainsKey(archivePath);
			}

			if (!hasContents)
				await ScanArchiveContentsAsync(archivePath).ConfigureAwait(false);

			HashSet<string> contents;
			lock (_lockObject)
			{
				if (!_archiveContents.TryGetValue(archivePath, out contents))
				{
					AddIssue(ValidationSeverity.Error, "ExtractArchive",
						$"Could not determine archive contents: {archivePath}", archivePath);
					return extractedFiles;
				}
			}

			string extractFolderName = Path.GetFileNameWithoutExtension(archivePath);

			lock (_lockObject)
			{
				foreach (string entryPath in contents)
				{

					string normalizedEntry = entryPath.Replace('/', Path.DirectorySeparatorChar);
					string fullPath = Path.Combine(destinationPath, extractFolderName, normalizedEntry);

					fullPath = fullPath.Replace('/', Path.DirectorySeparatorChar).Replace("\\\\", "\\");

					_ = _virtualFiles.Add(fullPath);
					_ = _removedFiles.Remove(fullPath);
					extractedFiles.Add(fullPath);

					string parentDir = Path.GetDirectoryName(fullPath);
					if (!string.IsNullOrEmpty(parentDir))
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
			if (!DirectoryExists(directoryPath))
				return new List<string>();

			string normalizedDir = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			// Take a snapshot of _virtualFiles and _removedFiles inside a lock to avoid enumeration issues
			List<string> virtualFilesSnapshot;
			HashSet<string> removedFilesSnapshot;
			lock (_lockObject)
			{
				virtualFilesSnapshot = new List<string>(_virtualFiles);
				removedFilesSnapshot = new HashSet<string>(_removedFiles, StringComparer.OrdinalIgnoreCase);
			}

			// Now enumerate the snapshot outside the lock
			foreach (string f in virtualFilesSnapshot)
			{
				string fileDir = Path.GetDirectoryName(f);
				if (string.IsNullOrEmpty(fileDir))
					continue;

				bool matches = searchOption == SearchOption.TopDirectoryOnly
					? string.Equals(fileDir, normalizedDir, StringComparison.OrdinalIgnoreCase)
					: string.Equals(fileDir, normalizedDir, StringComparison.OrdinalIgnoreCase) ||
					  fileDir.StartsWith(normalizedDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

				if (matches)
					_ = files.Add(f);
			}

			if (Directory.Exists(directoryPath))
			{
				try
				{
					foreach (string f in Directory.GetFiles(directoryPath, "*", searchOption))
					{
						if (!removedFilesSnapshot.Contains(f))
							files.Add(f);
					}
				}
				catch (UnauthorizedAccessException)
				{
					AddIssue(ValidationSeverity.Warning, "GetFilesInDirectory",
						$"Unauthorized access to directory: {directoryPath}", directoryPath);
				}
				catch (DirectoryNotFoundException)
				{
					AddIssue(ValidationSeverity.Warning, "GetFilesInDirectory",
						$"Directory not found: {directoryPath}", directoryPath);
				}
			}

			return files.ToList();
		}

		public List<string> GetDirectoriesInDirectory(string directoryPath)
		{
			if (!DirectoryExists(directoryPath))
				return new List<string>();

			lock (_lockObject)
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

			if (FileExists(programPath))
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
			[CanBeNull] string affectedPath
											)
		{
			lock (_lockObject)
			{
				_issues.Add(new ValidationIssue
				{
					Severity = severity,
					Category = category,
					Message = message,
					AffectedPath = affectedPath,
					Timestamp = DateTimeOffset.UtcNow,
				});
			}
		}

		[NotNull]
		public List<string> GetTrackedFiles()
		{
			lock (_lockObject)
			{
				return new List<string>(_virtualFiles);
			}
		}

		[NotNull]
		public List<ValidationIssue> GetValidationIssues()
		{
			lock (_lockObject)
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
		Critical,
	}
}