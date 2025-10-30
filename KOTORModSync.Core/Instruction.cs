// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Data;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.TSLPatcher;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;
namespace KOTORModSync.Core
{
	public sealed class Instruction : INotifyPropertyChanged
	{
		[CanBeNull]
		private Services.FileSystem.IFileSystemProvider _fileSystemProvider;
		internal void SetFileSystemProvider([NotNull] Services.FileSystem.IFileSystemProvider provider) => _fileSystemProvider = provider ?? throw new ArgumentNullException(nameof(provider));
		public enum ActionExitCode
		{
			UnauthorizedAccessException = -1,
			Success,
			InvalidSelfExtractingExecutable,
			InvalidArchive,
			ArchiveParseError,
			FileNotFoundPost,
			IOException,
			RenameTargetAlreadyExists,
			PatcherError,
			ChildProcessError,
			UnknownError,
			UnknownInnerError,
			TSLPatcherError,
			UnknownInstruction,
			TSLPatcherLogNotFound,
			FallbackArchiveExtractionFailed,
			OptionalInstallFailed,
		}
		public enum ActionType
		{
			Unset,
			Extract,
			Execute,
			Patcher,
			Move,
			Copy,
			Rename,
			Delete,
			DelDuplicate,
			Choose,
			Run,
		}
		private ActionType _action;
		private Guid _guid;
		[NotNull] private string _arguments = string.Empty;
		[NotNull] private List<Guid> _dependencies = new List<Guid>();
		[NotNull] private string _destination = string.Empty;
		private bool _overwrite = true;
		[NotNull] private List<Guid> _restrictions = new List<Guid>();
		[NotNull][ItemNotNull] private List<string> _source = new List<string>();
		public Guid Guid
		{
			get
			{
				if (_guid == Guid.Empty)
					_guid = Guid.NewGuid();
				return _guid;
			}
			set
			{
				if (_guid == value)
					return;
				_guid = value;
				OnPropertyChanged();
			}
		}
		public static IEnumerable<string> ActionTypes => Enum.GetValues(typeof(ActionType)).Cast<ActionType>()
			.Select(actionType => actionType.ToString());
		[JsonIgnore]
		public ActionType Action
		{
			get => _action;
			set
			{
				if (_action == value) return;
				_action = value;
				OnPropertyChanged();
			}
		}
		[JsonProperty(nameof(Action))]
		public string ActionString
		{
			get => Action.ToString();
			set => Action = (ActionType)Enum.Parse(typeof(ActionType), value);
		}
		[NotNull]
		[ItemNotNull]
		public IReadOnlyList<string> Source
		{
			get => _source;
			set
			{
				// CRITICAL: Check for infinite recursion with empty GUID strings
				if (value != null && value.Count == 1 && string.Equals(value[0], "00000000-0000-0000-0000-000000000000", StringComparison.Ordinal))
				{
					Logger.LogError($"[Instruction.set_Source] INFINITE RECURSION DETECTED! Attempting to set empty GUID string. Current source: [{string.Join(", ", _source ?? new List<string>())}]. Breaking the loop.");
					return; // Break the infinite loop
				}

				// Break change-notify loops when the contents are identical
				if (ReferenceEquals(_source, value))
				{
					return;
				}
				if (_source != null && value != null && _source.Count == value.Count)
				{
					bool same = true;
					for (int i = 0; i < _source.Count; i++)
					{
						if (!string.Equals(_source[i], value[i], StringComparison.Ordinal))
						{
							same = false;
							break;
						}
					}
					if (same)
					{
						return;
					}
				}

				Logger.LogVerbose($"[Instruction.set_Source] Setting new source and calling OnPropertyChanged");
				_source = value?.ToList() ?? new List<string>();
				OnPropertyChanged();
				Logger.LogVerbose($"[Instruction.set_Source] OnPropertyChanged completed");
			}
		}
		[NotNull]
		public string Destination
		{
			get => _destination;
			set
			{
				if (string.Equals(_destination, value, StringComparison.Ordinal)) return;
				_destination = value;
				OnPropertyChanged();
			}
		}
		public bool Overwrite
		{
			get => _overwrite;
			set
			{
				if (_overwrite == value) return;
				_overwrite = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public string Arguments
		{
			get => _arguments;
			set
			{
				if (string.Equals(_arguments, value, StringComparison.Ordinal)) return;
				_arguments = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public List<Guid> Dependencies
		{
			get => _dependencies;
			set
			{
				if (_dependencies == value) return;
				_dependencies = value;
				OnPropertyChanged();
			}
		}
		[NotNull]
		public List<Guid> Restrictions
		{
			get => _restrictions;
			set
			{
				if (_restrictions == value) return;
				_restrictions = value;
				OnPropertyChanged();
			}
		}

		public bool ShouldSerializeOverwrite()
		{
			return Action == ActionType.Move || Action == ActionType.Copy || Action == ActionType.Rename;
		}
		public bool ShouldSerializeDestination()
		{
			return Action == ActionType.Move || Action == ActionType.Copy || Action == ActionType.Rename
				|| Action == ActionType.Patcher || Action == ActionType.Delete;
		}
		public bool ShouldSerializeArguments()
		{
			return Action == ActionType.DelDuplicate || Action == ActionType.Execute || Action == ActionType.Patcher;
		}
		[NotNull][ItemNotNull] private List<string> RealSourcePaths { get; set; } = new List<string>();
		[CanBeNull] private DirectoryInfo RealDestinationPath { get; set; }
		private ModComponent _parentComponent { get; set; }
		public Dictionary<FileInfo, SHA1> ExpectedChecksums { get; set; }
		public Dictionary<FileInfo, SHA1> OriginalChecksums { get; internal set; }
		public ModComponent GetParentComponent() => _parentComponent;
		public void SetParentComponent(ModComponent thisComponent) => _parentComponent = thisComponent;

		internal void SetRealPaths(bool sourceIsNotFilePath = false, bool skipExistenceCheck = false)
		{
			if (_fileSystemProvider is null)
				throw new InvalidOperationException("File system provider must be set before calling SetRealPaths. Call SetFileSystemProvider() first.");
			if (Source is null)
				throw new InvalidOperationException($"Source is null for instruction `{Guid}`");
			Logger.LogVerbose($"[Instruction.SetRealPaths] Action={Action}, Source count={Source.Count}, sourceIsNotFilePath={sourceIsNotFilePath}, skipExistenceCheck={skipExistenceCheck}");
			Logger.LogVerbose($"[Instruction.SetRealPaths] Raw Source paths: [{string.Join(", ", Source)}]");
			Logger.LogVerbose($"[Instruction.SetRealPaths] MainConfig.SourcePath: {MainConfig.SourcePath?.FullName ?? "NULL"}");
			Logger.LogVerbose($"[Instruction.SetRealPaths] MainConfig.DestinationPath: {MainConfig.DestinationPath?.FullName ?? "NULL"}");
			List<string> newSourcePaths;
			if (!sourceIsNotFilePath)
			{
				Logger.LogVerbose($"[Instruction.SetRealPaths] Calling ReplaceCustomVariables on source paths...");
				List<string> processedSource = Source.Select(UtilityHelper.ReplaceCustomVariables).ToList();
				Logger.LogVerbose($"[Instruction.SetRealPaths] After ReplaceCustomVariables on source: [{string.Join(", ", processedSource)}]");
				Logger.LogVerbose($"[Instruction.SetRealPaths] Calling EnumerateFilesWithWildcards with processed paths...");
				newSourcePaths = PathHelper.EnumerateFilesWithWildcards(processedSource, _fileSystemProvider);
				Logger.LogVerbose($"[Instruction.SetRealPaths] After EnumerateFilesWithWildcards: Found {newSourcePaths?.Count ?? 0} files");
				if (!skipExistenceCheck)
				{
					if (newSourcePaths.IsNullOrEmptyOrAllNull())
					{
						Logger.LogVerbose($"[Instruction.SetRealPaths] ERROR: newSourcePaths is null/empty after wildcard expansion");
						throw new Exceptions.WildcardPatternNotFoundException(
							Source,
							_parentComponent?.Name
						);
					}
					List<string> missingFiles = newSourcePaths.Where(f => !_fileSystemProvider.FileExists(f)).ToList();
					if (missingFiles.Count > 0)
					{
						Logger.LogVerbose($"[Instruction.SetRealPaths] ERROR: {missingFiles.Count} files do not exist: [{string.Join(", ", missingFiles)}]");
						throw new FileNotFoundException(
							$"Could not find all files in the 'Source' path on disk! Got [{string.Join(separator: ", ", Source)}]"
						);
					}
				}
				RealSourcePaths = (
					MainConfig.CaseInsensitivePathing
						? newSourcePaths.Distinct(StringComparer.OrdinalIgnoreCase)
						: newSourcePaths.Distinct(StringComparer.Ordinal)
				).ToList();
			}
			else
			{
				Logger.LogVerbose($"[Instruction.SetRealPaths] sourceIsNotFilePath=true, calling ReplaceCustomVariables on original paths...");
				newSourcePaths = Source.Select(UtilityHelper.ReplaceCustomVariables).ToList();
				Logger.LogVerbose($"[Instruction.SetRealPaths] After ReplaceCustomVariables: [{string.Join(", ", newSourcePaths)}]");
			}
			string destinationPath = UtilityHelper.ReplaceCustomVariables(Destination);
			Logger.LogVerbose($"[Instruction.SetRealPaths] Raw Destination: {Destination ?? "NULL"}");
			Logger.LogVerbose($"[Instruction.SetRealPaths] After ReplaceCustomVariables on Destination: {destinationPath ?? "NULL"}");
			DirectoryInfo thisDestination = PathHelper.TryGetValidDirectoryInfo(destinationPath);
			Logger.LogVerbose($"[Instruction.SetRealPaths] TryGetValidDirectoryInfo result: {thisDestination?.FullName ?? "NULL"}");
			if (sourceIsNotFilePath)
			{
				RealDestinationPath = thisDestination;
				return;
			}
			bool skipDestinationValidation = Action == ActionType.Copy
											|| Action == ActionType.Move
											|| Action == ActionType.Rename
											|| Action == ActionType.Extract
											|| Action == ActionType.DelDuplicate;
			if (skipDestinationValidation && thisDestination is null && !string.IsNullOrWhiteSpace(destinationPath))
			{
				thisDestination = new DirectoryInfo(destinationPath);
			}
			if (
				!skipExistenceCheck
				&& !skipDestinationValidation
				&& thisDestination != null
				&& !_fileSystemProvider.DirectoryExists(thisDestination.FullName)
				&& Action != ActionType.DelDuplicate
			)
			{
				if (MainConfig.CaseInsensitivePathing)
				{
					thisDestination = PathHelper.GetCaseSensitivePath(thisDestination);
				}
				if (thisDestination != null && !_fileSystemProvider.DirectoryExists(thisDestination.FullName))
					throw new DirectoryNotFoundException("Could not find the 'Destination' path on disk!");
			}
			RealDestinationPath = thisDestination;
		}
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		public async Task<ActionExitCode> ExtractFileAsync(
			DirectoryInfo argDestinationPath = null,
			[NotNull][ItemNotNull] IReadOnlyList<string> argSourcePaths = null
		)
		{
			if (_fileSystemProvider is null)
				throw new InvalidOperationException("File system provider must be set before calling ExtractFileAsync. Call SetFileSystemProvider() first.");

			System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
			try
			{
				if (argSourcePaths.IsNullOrEmptyCollection())
					argSourcePaths = RealSourcePaths;
				if (argSourcePaths.IsNullOrEmptyCollection())
					throw new ArgumentNullException(nameof(argSourcePaths));
				RealSourcePaths = argSourcePaths.ToList();
				foreach (string sourcePath in RealSourcePaths)
				{
					string destinationPath = argDestinationPath?.FullName ?? Path.GetDirectoryName(sourcePath);
					if (string.IsNullOrEmpty(destinationPath))
					{
						await Logger.LogErrorAsync($"Could not determine destination path for archive: {sourcePath}").ConfigureAwait(false);
						return ActionExitCode.InvalidArchive;
					}
					try
					{
						List<string> extractedFiles = await _fileSystemProvider.ExtractArchiveAsync(sourcePath, destinationPath).ConfigureAwait(false);
						await Logger.LogAsync($"Extracted {extractedFiles.Count} file(s) from '{Path.GetFileName(sourcePath)}' to '{destinationPath}'").ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
						sw.Stop();
						Services.TelemetryService.Instance.RecordFileOperation(
							operationType: "extract",
							success: false,
							fileCount: RealSourcePaths.Count,
							durationMs: sw.Elapsed.TotalMilliseconds,
							errorMessage: ex.Message
						);
						return ActionExitCode.InvalidArchive;
					}
				}

				sw.Stop();
				Services.TelemetryService.Instance.RecordFileOperation(
					operationType: "extract",
					success: true,
					fileCount: RealSourcePaths.Count,
					durationMs: sw.Elapsed.TotalMilliseconds
				);
				return ActionExitCode.Success;
			}
			catch (ArgumentNullException ex)
			{
				await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
				sw.Stop();
				Services.TelemetryService.Instance.RecordFileOperation(
					operationType: "extract",
					success: false,
					fileCount: RealSourcePaths?.Count ?? 0,
					durationMs: sw.Elapsed.TotalMilliseconds,
					errorMessage: ex.Message
				);
				return ActionExitCode.InvalidArchive;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
				sw.Stop();
				Services.TelemetryService.Instance.RecordFileOperation(
					operationType: "extract",
					success: false,
					fileCount: RealSourcePaths?.Count ?? 0,
					durationMs: sw.Elapsed.TotalMilliseconds,
					errorMessage: ex.Message
				);
				return ActionExitCode.UnknownError;
			}
		}
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		public void DeleteDuplicateFile(
				DirectoryInfo directoryPath = null,
				string fileExtension = null,
				bool caseInsensitive = true,
				IReadOnlyList<string> compatibleExtensions = null
			)
		{
			if (_fileSystemProvider is null)
				throw new InvalidOperationException("File system provider must be set before calling DeleteDuplicateFile. Call SetFileSystemProvider() first.");
			if (directoryPath is null)
				directoryPath = RealDestinationPath;
			if (!(directoryPath is null) && !_fileSystemProvider.DirectoryExists(directoryPath.FullName) && MainConfig.CaseInsensitivePathing)
				directoryPath = PathHelper.GetCaseSensitivePath(directoryPath);
			if (directoryPath is null || !_fileSystemProvider.DirectoryExists(directoryPath.FullName))
				throw new ArgumentException(message: "Invalid directory path.", nameof(directoryPath));
			List<string> sourceExtensions = Source.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
			compatibleExtensions = (compatibleExtensions ?? (!sourceExtensions.IsNullOrEmptyOrAllNull() ? sourceExtensions : null)).ToList();
			if (compatibleExtensions is null)
				compatibleExtensions = Game.TextureOverridePriorityList;
			if (string.IsNullOrEmpty(fileExtension))
				fileExtension = Arguments;
			List<string> filesList = _fileSystemProvider.GetFilesInDirectory(directoryPath.FullName);
			Dictionary<string, int> fileNameCounts = caseInsensitive
				? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
				: new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (string fileNameWithoutExtension in from filePath in filesList
														select _fileSystemProvider.GetFileName(filePath) into fileName
														let fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName)
														let thisExtension = Path.GetExtension(fileName)
														let compatibleExtensionFound = caseInsensitive
						? compatibleExtensions.Any(ext => ext.Equals(thisExtension, StringComparison.OrdinalIgnoreCase))
						: compatibleExtensions.Contains(thisExtension, StringComparer.Ordinal)
														where compatibleExtensionFound
														select fileNameWithoutExtension)
			{
				_ = fileNameCounts.TryGetValue(fileNameWithoutExtension, out int count);
				fileNameCounts[fileNameWithoutExtension] = count + 1;
			}
			foreach (string filePath in filesList)
			{
				if (!ShouldDeleteFile(filePath))
					continue;
				try
				{
					_ = _fileSystemProvider.DeleteFileAsync(filePath);
					string fileName = _fileSystemProvider.GetFileName(filePath);
					_ = Logger.LogAsync($"Deleted file: '{fileName}'");
					string baseName = Path.GetFileNameWithoutExtension(fileName);
					int count = fileNameCounts[baseName] - 1;
					_ = Logger.LogVerboseAsync(
						$"Leaving alone '{count.ToString(System.Globalization.CultureInfo.InvariantCulture)}' file(s) with the same name of '{baseName}'."
					);
				}
				catch (Exception ex)
				{
					Logger.LogException(ex);
				}
			}
			bool ShouldDeleteFile(string filePath)
			{
				string fileName = _fileSystemProvider?.GetFileName(filePath);
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
				string fileExtensionFromFile = Path.GetExtension(fileName);
				if (string.IsNullOrEmpty(fileNameWithoutExtension))
				{
					_ = Logger.LogWarningAsync(
						$"Skipping '{fileName}' Reason: fileNameWithoutExtension is null/empty somehow?"
					);
				}
				else if (!fileNameCounts.TryGetValue(fileNameWithoutExtension, out int value))
				{
					_ = Logger.LogVerboseAsync(
						$"Skipping '{fileName}' Reason: Not present in dictionary, ergo does not have a desired extension."
					);
				}
				else if (value <= 1)
				{
					_ = Logger.LogVerboseAsync(
						$"Skipping '{fileName}' Reason: '{fileNameWithoutExtension}' is the only file with this name."
					);
				}
				else if (!string.Equals(fileExtensionFromFile, fileExtension, StringComparison.OrdinalIgnoreCase))
				{
					string caseInsensitivity = caseInsensitive
						? " (case-insensitive)"
						: string.Empty;
					string message =
						$"Skipping '{fileName}' Reason: '{fileExtensionFromFile}' is not the desired extension '{fileExtension}'{caseInsensitivity}";
					_ = Logger.LogVerboseAsync(message);
				}
				else
				{
					return true;
				}
				return false;
			}
		}
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		public ActionExitCode DeleteFile(
			[ItemNotNull][NotNull] IReadOnlyList<string> sourcePaths = null
		)
		{
			if (_fileSystemProvider is null)
				throw new InvalidOperationException("File system provider must be set before calling DeleteFile. Call SetFileSystemProvider() first.");
			if (sourcePaths is null)
				sourcePaths = RealSourcePaths;
			if (sourcePaths is null)
				throw new ArgumentNullException(nameof(sourcePaths));

			System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
			ActionExitCode exitCode = ActionExitCode.Success;
			try
			{
				foreach (string thisFilePath in sourcePaths)
				{
					string realFilePath = thisFilePath;
					if (MainConfig.CaseInsensitivePathing && !_fileSystemProvider.FileExists(realFilePath))
						realFilePath = PathHelper.GetCaseSensitivePath(realFilePath).Item1;
					string sourceRelDirPath = MainConfig.SourcePath is null
						? thisFilePath
						: PathHelper.GetRelativePath(
							MainConfig.SourcePath.FullName,
							thisFilePath
						);
					if (!Path.IsPathRooted(realFilePath) || !_fileSystemProvider.FileExists(realFilePath))
					{
						// Overwrite=false (default): lenient mode, just log and continue
						// Overwrite=true: strict mode, treat as error
						if (Overwrite)
						{
							Logger.LogWarning($"Invalid wildcards or file does not exist: '{sourceRelDirPath}'");
							exitCode = ActionExitCode.FileNotFoundPost;
						}
						else
						{
							Logger.LogVerbose($"File does not exist (skipping): '{sourceRelDirPath}'");
						}
						continue;
					}
					try
					{
						_ = _fileSystemProvider.DeleteFileAsync(realFilePath);
						_ = Logger.LogAsync($"Deleting '{sourceRelDirPath}'...");
					}
					catch (Exception ex)
					{
						Logger.LogException(ex);
						if (exitCode == ActionExitCode.Success)
						{
							exitCode = ActionExitCode.UnknownInnerError;
						}
					}
				}
				if (sourcePaths.Count == 0)
				{
					Logger.Log("No files to delete, skipping this instruction.");
				}
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
				if (exitCode == ActionExitCode.Success)
				{
					exitCode = ActionExitCode.UnknownInnerError;
				}
			}
			finally
			{
				sw.Stop();
				Services.TelemetryService.Instance.RecordFileOperation(
					operationType: "delete",
					success: exitCode == ActionExitCode.Success,
					fileCount: sourcePaths?.Count ?? 0,
					durationMs: sw.Elapsed.TotalMilliseconds,
					errorMessage: exitCode != ActionExitCode.Success ? exitCode.ToString() : null
				);
			}
			return exitCode;
		}
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		public ActionExitCode RenameFile(
			[ItemNotNull][NotNull] IReadOnlyList<string> sourcePaths = null
		)
		{
			if (_fileSystemProvider is null)
				throw new InvalidOperationException("File system provider must be set before calling RenameFile. Call SetFileSystemProvider() first.");
			if (sourcePaths.IsNullOrEmptyCollection())
				sourcePaths = RealSourcePaths;
			if (sourcePaths.IsNullOrEmptyCollection())
				throw new ArgumentNullException(nameof(sourcePaths));
			ActionExitCode exitCode = ActionExitCode.Success;
			try
			{
				foreach (string sourcePath in sourcePaths)
				{
					string fileName = Path.GetFileName(sourcePath);
					string sourceRelDirPath = MainConfig.SourcePath is null
						? sourcePath
						: PathHelper.GetRelativePath(
							MainConfig.SourcePath.FullName,
							sourcePath
						);
					if (!_fileSystemProvider.FileExists(sourcePath))
					{
						Logger.LogError($"'{sourceRelDirPath}' does not exist!");
						if (exitCode == ActionExitCode.Success)
						{
							exitCode = ActionExitCode.FileNotFoundPost;
						}
						continue;
					}
					string destinationFilePath = Path.Combine(
						Path.GetDirectoryName(sourcePath) ?? string.Empty,
						Destination
					);
					string destinationRelDirPath = MainConfig.DestinationPath is null
						? destinationFilePath
						: PathHelper.GetRelativePath(
							MainConfig.DestinationPath.FullName,
							destinationFilePath
						);
					if (_fileSystemProvider.FileExists(destinationFilePath))
					{
						if (!Overwrite)
						{
							exitCode = ActionExitCode.RenameTargetAlreadyExists;
							_ = Logger.LogAsync(
								$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
								+ " skipping file. Reason: Overwrite set to False )"
							);
							continue;
						}
						_ = Logger.LogAsync(
							$"Removing pre-existing file '{destinationRelDirPath}' Reason: Overwrite set to True"
						);
						_ = _fileSystemProvider.DeleteFileAsync(destinationFilePath);
					}
					try
					{
						_ = Logger.LogAsync($"Rename '{sourceRelDirPath}' to '{destinationRelDirPath}'");
						_ = _fileSystemProvider.RenameFileAsync(sourcePath, Destination, Overwrite);
					}
					catch (IOException ex)
					{
						if (exitCode == ActionExitCode.Success)
						{
							exitCode = ActionExitCode.IOException;
						}
						Logger.LogException(ex);
					}
				}
				return exitCode;
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
				if (exitCode == ActionExitCode.Success)
				{
					exitCode = ActionExitCode.UnknownError;
				}
			}
			return exitCode;
		}
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		public async Task<ActionExitCode> CopyFileAsync(
			[ItemNotNull][NotNull] IReadOnlyList<string> sourcePaths = null,
			[NotNull] DirectoryInfo destinationPath = null
		)
		{
			if (_fileSystemProvider is null)
				throw new InvalidOperationException("File system provider must be set before calling CopyFileAsync. Call SetFileSystemProvider() first.");
			if (sourcePaths.IsNullOrEmptyCollection())
				sourcePaths = RealSourcePaths;
			if (sourcePaths.IsNullOrEmptyCollection())
				throw new ArgumentNullException(nameof(sourcePaths));
			if (destinationPath is null)
				destinationPath = RealDestinationPath;
			if (destinationPath is null)
				throw new ArgumentNullException(nameof(destinationPath));

			System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
			int maxCount = MainConfig.UseMultiThreadedIO
				? 16
				: 1;
			using (SemaphoreSlim semaphore = new SemaphoreSlim(initialCount: 1, maxCount))
			{
				SemaphoreSlim localSemaphore = semaphore;
				async Task CopyIndividualFileAsync(string sourcePath)
				{
					await localSemaphore.WaitAsync().ConfigureAwait(false);
					try
					{
						string sourceRelDirPath = MainConfig.SourcePath is null
							? sourcePath
							: PathHelper.GetRelativePath(
								MainConfig.SourcePath.FullName,
								sourcePath
							);
						string fileName = Path.GetFileName(sourcePath);
						string destinationFilePath = MainConfig.CaseInsensitivePathing
							? PathHelper.GetCaseSensitivePath(
								Path.Combine(destinationPath.FullName, fileName),
								isFile: true
							).Item1
							: Path.Combine(destinationPath.FullName, fileName);
						string destinationRelDirPath = MainConfig.DestinationPath is null
							? destinationFilePath
							: PathHelper.GetRelativePath(
								MainConfig.DestinationPath.FullName,
								destinationFilePath
							);
						if (_fileSystemProvider.FileExists(destinationFilePath))
						{
							if (!Overwrite)
							{
								await Logger.LogWarningAsync(
									$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
									+ " skipping file. Reason: Overwrite set to False )"
								).ConfigureAwait(false);
								return;
							}
							await Logger.LogAsync(
								$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
								+ $" deleting pre-existing file '{destinationRelDirPath}' Reason: Overwrite set to True"
							).ConfigureAwait(false);
							await _fileSystemProvider.DeleteFileAsync(destinationFilePath).ConfigureAwait(false);
						}
						await Logger.LogAsync($"Copy '{sourceRelDirPath}' to '{destinationRelDirPath}'").ConfigureAwait(false);
						await _fileSystemProvider.CopyFileAsync(sourcePath, destinationFilePath, Overwrite).ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
						throw;
					}
					finally
					{
						_ = localSemaphore.Release();
					}
				}
				if (sourcePaths is null)
					throw new InvalidOperationException($"Source paths are null for instruction `{Guid}` in CopyFileAsync");
				List<Task> tasks = sourcePaths.Select(CopyIndividualFileAsync).ToList();
				try
				{
					await Task.WhenAll(tasks).ConfigureAwait(false);
					sw.Stop();
					Services.TelemetryService.Instance.RecordFileOperation(
						operationType: "copy",
						success: true,
						fileCount: sourcePaths.Count,
						durationMs: sw.Elapsed.TotalMilliseconds
					);
					return ActionExitCode.Success;
				}
				catch (Exception ex)
				{
					sw.Stop();
					Services.TelemetryService.Instance.RecordFileOperation(
						operationType: "copy",
						success: false,
						fileCount: sourcePaths.Count,
						durationMs: sw.Elapsed.TotalMilliseconds,
						errorMessage: ex.Message
					);
					return ActionExitCode.UnknownError;
				}
			}
		}
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		public async Task<ActionExitCode> MoveFileAsync(
			[ItemNotNull][NotNull] IReadOnlyList<string> sourcePaths = null,
			[NotNull] DirectoryInfo destinationPath = null
		)
		{
			if (_fileSystemProvider is null)
				throw new InvalidOperationException("File system provider must be set before calling MoveFileAsync. Call SetFileSystemProvider() first.");
			if (sourcePaths.IsNullOrEmptyCollection())
				sourcePaths = RealSourcePaths;
			if (sourcePaths.IsNullOrEmptyCollection())
				throw new ArgumentNullException(nameof(sourcePaths));
			if (destinationPath is null)
				destinationPath = RealDestinationPath;
			if (destinationPath is null)
				throw new ArgumentNullException(nameof(destinationPath));
			await Logger.LogVerboseAsync($"[Instruction.MoveFileAsync] Starting move operation with {sourcePaths.Count} files").ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[Instruction.MoveFileAsync] Destination: {destinationPath.FullName}").ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[Instruction.MoveFileAsync] MainConfig.SourcePath: {MainConfig.SourcePath?.FullName ?? "NULL"}").ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[Instruction.MoveFileAsync] MainConfig.DestinationPath: {MainConfig.DestinationPath?.FullName ?? "NULL"}").ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[Instruction.MoveFileAsync] IsDryRun: {_fileSystemProvider.IsDryRun}").ConfigureAwait(false);

			System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
			int maxCount = MainConfig.UseMultiThreadedIO
				? 16
				: 1;
			using (SemaphoreSlim semaphore = new SemaphoreSlim(initialCount: 1, maxCount))
			{
				SemaphoreSlim localSemaphore = semaphore;
				async Task MoveIndividualFileAsync(string sourcePath)
				{
					await localSemaphore.WaitAsync().ConfigureAwait(false);
					try
					{
						await Logger.LogVerboseAsync($"[Instruction.MoveIndividualFileAsync] Processing: {sourcePath}").ConfigureAwait(false);
						string sourceRelDirPath = MainConfig.SourcePath is null
							? sourcePath
							: PathHelper.GetRelativePath(
								MainConfig.SourcePath.FullName,
								sourcePath
							);
						await Logger.LogVerboseAsync($"[Instruction.MoveIndividualFileAsync] sourceRelDirPath: {sourceRelDirPath}").ConfigureAwait(false);
						string fileName = Path.GetFileName(sourcePath);
						await Logger.LogVerboseAsync($"[Instruction.MoveIndividualFileAsync] fileName: {fileName}").ConfigureAwait(false);
						string destinationFilePath = MainConfig.CaseInsensitivePathing
							? PathHelper.GetCaseSensitivePath(
								Path.Combine(destinationPath.FullName, fileName),
								isFile: true
							).Item1
							: Path.Combine(destinationPath.FullName, fileName);
						await Logger.LogVerboseAsync($"[Instruction.MoveIndividualFileAsync] destinationFilePath: {destinationFilePath}").ConfigureAwait(false);
						string destinationRelDirPath = MainConfig.DestinationPath is null
							? destinationFilePath
							: PathHelper.GetRelativePath(
								MainConfig.DestinationPath.FullName,
								destinationFilePath
							);
						await Logger.LogVerboseAsync($"[Instruction.MoveIndividualFileAsync] destinationRelDirPath: {destinationRelDirPath}").ConfigureAwait(false);
						if (_fileSystemProvider.FileExists(destinationFilePath))
						{
							await Logger.LogVerboseAsync($"[Instruction.MoveIndividualFileAsync] Destination file exists, Overwrite={Overwrite}").ConfigureAwait(false);
							if (!Overwrite)
							{
								await Logger.LogWarningAsync(
									$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
									+ " skipping file. Reason: Overwrite set to False )"
								).ConfigureAwait(false);
								return;
							}
							await Logger.LogAsync(
								$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
								+ $" deleting pre-existing file '{destinationRelDirPath}' Reason: Overwrite set to True"
							).ConfigureAwait(false);
							await _fileSystemProvider.DeleteFileAsync(destinationFilePath).ConfigureAwait(false);
						}
						await Logger.LogAsync($"Move '{sourceRelDirPath}' to '{destinationRelDirPath}'").ConfigureAwait(false);
						await Logger.LogVerboseAsync($"[Instruction.MoveIndividualFileAsync] Calling _fileSystemProvider.MoveFileAsync('{sourcePath}', '{destinationFilePath}', {Overwrite})").ConfigureAwait(false);
						await _fileSystemProvider.MoveFileAsync(sourcePath, destinationFilePath, Overwrite).ConfigureAwait(false);
						await Logger.LogVerboseAsync($"[Instruction.MoveIndividualFileAsync] Move completed successfully").ConfigureAwait(false);
					}
					catch (Exception ex)
					{
						await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
						throw;
					}
					finally
					{
						_ = localSemaphore.Release();
					}
				}
				List<Task> tasks = sourcePaths.Select(MoveIndividualFileAsync).ToList();
				try
				{
					await Task.WhenAll(tasks).ConfigureAwait(false);
					sw.Stop();
					Services.TelemetryService.Instance.RecordFileOperation(
						operationType: "move",
						success: true,
						fileCount: sourcePaths.Count,
						durationMs: sw.Elapsed.TotalMilliseconds
					);
					return ActionExitCode.Success;
				}
				catch (Exception ex)
				{
					sw.Stop();
					Services.TelemetryService.Instance.RecordFileOperation(
						operationType: "move",
						success: false,
						fileCount: sourcePaths.Count,
						durationMs: sw.Elapsed.TotalMilliseconds,
						errorMessage: ex.Message
					);
					return ActionExitCode.UnknownError;
				}
			}
		}
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		public async Task<ActionExitCode> ExecuteTSLPatcherAsync()
		{
			if (_fileSystemProvider is null)
				throw new InvalidOperationException("File system provider must be set before calling ExecuteTSLPatcherAsync. Call SetFileSystemProvider() first.");
			try
			{
				foreach (string t in RealSourcePaths)
				{
					DirectoryInfo tslPatcherDirectory = _fileSystemProvider.FileExists(t)
						? PathHelper.TryGetValidDirectoryInfo(_fileSystemProvider.GetDirectoryName(t))
						: new DirectoryInfo(t);
					if (tslPatcherDirectory is null || !_fileSystemProvider.DirectoryExists(tslPatcherDirectory.FullName))
						throw new DirectoryNotFoundException($"The directory '{t}' could not be located on the disk.");

					if (_fileSystemProvider is Services.FileSystem.VirtualFileSystemProvider)
					{
						await Logger.LogAsync($"[Simulation] Skipping TSLPatcher execution for simulation mode").ConfigureAwait(false);
						continue;
					}

					string fullInstallLogFile = Path.Combine(tslPatcherDirectory.FullName, path2: "installlog.rtf");
					if (_fileSystemProvider.FileExists(fullInstallLogFile))
						await _fileSystemProvider.DeleteFileAsync(fullInstallLogFile).ConfigureAwait(false);
					fullInstallLogFile = Path.Combine(tslPatcherDirectory.FullName, path2: "installlog.txt");
					if (_fileSystemProvider.FileExists(fullInstallLogFile))
						await _fileSystemProvider.DeleteFileAsync(fullInstallLogFile).ConfigureAwait(false);
					IniHelper.ReplaceIniPattern(tslPatcherDirectory, pattern: @"^\s*PlaintextLog\s*=\s*0\s*$", replacement: "PlaintextLog=1");
					IniHelper.ReplaceIniPattern(tslPatcherDirectory, pattern: @"^\s*LookupGameFolder\s*=\s*1\s*$", replacement: "LookupGameFolder=0");
					IniHelper.ReplaceIniPattern(tslPatcherDirectory, pattern: @"^\s*ConfirmMessage\s*=\s*.*$", replacement: "ConfirmMessage=N/A");
					List<string> argList = new List<string>
					{
						"--install",
						$"--game-dir=\"{MainConfig.DestinationPath}\"",
						$"--tslpatchdata=\"{tslPatcherDirectory}\"",
					};
					if (!string.IsNullOrEmpty(Arguments)) argList.Add($"--namespace-option-index={Arguments}");
					string args = string.Join(separator: " ", argList);
					string baseDir = UtilityHelper.GetBaseDirectory();
					string resourcesDir = UtilityHelper.GetResourcesDirectory(baseDir);

					// Use helper method to find holopatcher
					(string holopatcherPath, bool usePythonVersion, bool found) = await Services.InstallationService.FindHolopatcherAsync(resourcesDir, baseDir).ConfigureAwait(false);

					if (!found)
						throw new FileNotFoundException($"Could not load HoloPatcher from the '{resourcesDir}' directory!");
					if (int.TryParse(Arguments.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int namespaceId))
					{
						string message = $"If asked to pick an option, select the {Serializer.ToOrdinal(namespaceId + 1)} from the top.";
						_ = CallbackObjects.InformationCallback.ShowInformationDialog(message);
						await Logger.LogWarningAsync(message).ConfigureAwait(false);
					}

					await Logger.LogAsync($"Using CLI to run command: '{holopatcherPath}' {args}").ConfigureAwait(false);

					int exitCode;
					string output;
					string error;
					if (usePythonVersion)
					{
						// Use Python.NET to run holopatcher
						(exitCode, output, error) = await Services.InstallationService.RunHolopatcherPyAsync(
								holopatcherPath,
								args
							).ConfigureAwait(false);
					}
					else
					{
						// Use platform-specific executable
						(exitCode, output, error) = await _fileSystemProvider.ExecuteProcessAsync(
							holopatcherPath,
							args
						).ConfigureAwait(false);
					}

					await Logger.LogAsync($"'holopatcher' exited with exit code {exitCode}").ConfigureAwait(false);
					if (exitCode != 0)
						return ActionExitCode.PatcherError;
					try
					{
						List<string> installErrors = await VerifyInstall().ConfigureAwait(false);
						if (installErrors.Count <= 0)
							continue;
						await Logger.LogAsync(string.Join(Environment.NewLine, installErrors)).ConfigureAwait(false);
						return ActionExitCode.TSLPatcherError;
					}
					catch (Exception ex)
					{
						await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
						return ActionExitCode.TSLPatcherLogNotFound;
					}
				}
				return ActionExitCode.Success;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
				throw;
			}
		}
		public async Task<ActionExitCode> ExecuteProgramAsync(
			[ItemNotNull] IReadOnlyList<string> sourcePaths = null
		)
		{
			if (_fileSystemProvider is null)
				throw new InvalidOperationException("File system provider must be set before calling ExecuteProgramAsync. Call SetFileSystemProvider() first.");
			try
			{
				if (sourcePaths.IsNullOrEmptyCollection())
					sourcePaths = RealSourcePaths;
				if (sourcePaths.IsNullOrEmptyCollection())
					throw new ArgumentNullException(nameof(sourcePaths));
				ActionExitCode exitCode = ActionExitCode.Success;
				foreach (string sourcePath in sourcePaths)
				{
					try
					{
						(int childExitCode, string output, string error) =
							await _fileSystemProvider.ExecuteProcessAsync(
								sourcePath,
								UtilityHelper.ReplaceCustomVariables(Arguments)
							).ConfigureAwait(false);
						_ = Logger.LogAsync(output + Environment.NewLine + error);
						if (childExitCode == 0)
							continue;
						exitCode = ActionExitCode.ChildProcessError;
						break;
					}
					catch (FileNotFoundException ex)
					{
						await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
						return ActionExitCode.FileNotFoundPost;
					}
					catch (Exception ex)
					{
						await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
						return ActionExitCode.UnknownInnerError;
					}
				}
				return exitCode;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
				return ActionExitCode.UnknownError;
			}
		}
		[NotNull]
		private async Task<List<string>> VerifyInstall([ItemNotNull] IReadOnlyList<string> sourcePaths = null)
		{
			if (_fileSystemProvider is null)
				throw new InvalidOperationException("File system provider must be set before calling VerifyInstall. Call SetFileSystemProvider() first.");
			if (sourcePaths.IsNullOrEmptyCollection())
				sourcePaths = RealSourcePaths;
			if (sourcePaths.IsNullOrEmptyCollection())
				throw new ArgumentNullException(nameof(sourcePaths));
			if (_fileSystemProvider.IsDryRun)
			{
				await Logger.LogVerboseAsync("Skipping install log verification for dry-run").ConfigureAwait(false);
				return new List<string>();
			}
			List<string> allErrorLines = new List<string>();
			foreach (string sourcePath in sourcePaths)
			{
				string tslPatcherDirPath = _fileSystemProvider.GetDirectoryName(sourcePath)
					?? throw new DirectoryNotFoundException($"Could not retrieve parent directory of '{sourcePath}'.");
				string fullInstallLogFile = Path.Combine(tslPatcherDirPath, path2: "installlog.rtf");
				if (!_fileSystemProvider.FileExists(fullInstallLogFile))
				{
					fullInstallLogFile = Path.Combine(tslPatcherDirPath, path2: "installlog.txt");
					if (!_fileSystemProvider.FileExists(fullInstallLogFile))
						throw new FileNotFoundException(message: "Install log file not found.", fullInstallLogFile);
				}
				string installLogContent = await _fileSystemProvider.ReadFileAsync(fullInstallLogFile).ConfigureAwait(false);
				foreach (string line in installLogContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
				{
					if ((line.Contains("Error: ") || line.Contains("[Error]")) && !string.IsNullOrWhiteSpace(line))
					{
						allErrorLines.Add(line);
					}
				}
			}
			await Logger.LogVerboseAsync("No errors found in TSLPatcher installation log file").ConfigureAwait(false);
			return allErrorLines;
		}
		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged([CallerMemberName][CanBeNull] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		[NotNull]
		[ItemNotNull]
		public IReadOnlyList<Option> GetChosenOptions() => _parentComponent?.Options.Where(
				x => x != null && x.IsSelected && Source.Contains(x.Guid.ToString(), StringComparer.OrdinalIgnoreCase)
			).ToArray() ?? Array.Empty<Option>();
	}
}