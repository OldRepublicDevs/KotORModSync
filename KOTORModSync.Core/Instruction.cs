// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

		/// <summary>
		/// Sets the file system provider for this instruction. Call before executing any actions.
		/// </summary>
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
			OptionalInstallFailed
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

		private bool _overwrite;

		[NotNull] private List<Guid> _restrictions = new List<Guid>();

		[NotNull][ItemNotNull] private List<string> _source = new List<string>();

		public Guid Guid
		{
			get
			{
				if ( _guid == Guid.Empty )
					_guid = Guid.NewGuid();
				return _guid;
			}
			set
			{
				if ( _guid == value )
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
		public List<string> Source
		{
			get => _source;
			set
			{
				_source = value;
				OnPropertyChanged();
			}
		}

		[NotNull]
		public string Destination
		{
			get => _destination;
			set
			{
				_destination = value;
				OnPropertyChanged();
			}
		}

		public bool Overwrite
		{
			get => _overwrite;
			set
			{
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
				_restrictions = value;
				OnPropertyChanged();
			}
		}

		// Conditional serialization methods for JSON.NET
		// These control which fields are saved to TOML based on the Action type

		/// <summary>
		/// Overwrite should only be serialized for Move, Copy, and Rename actions.
		/// </summary>
		public bool ShouldSerializeOverwrite()
		{
			return Action == ActionType.Move || Action == ActionType.Copy || Action == ActionType.Rename;
		}

		/// <summary>
		/// Destination should only be serialized for Move, Copy, Rename, Patcher, and Delete actions.
		/// </summary>
		public bool ShouldSerializeDestination()
		{
			return Action == ActionType.Move || Action == ActionType.Copy || Action == ActionType.Rename
				|| Action == ActionType.Patcher || Action == ActionType.Delete;
		}

		/// <summary>
		/// Arguments should only be serialized for DelDuplicate, Execute, and Patcher actions.
		/// </summary>
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

		// This method will replace custom variables such as <<modDirectory>> and <<kotorDirectory>> with their actual paths.
		// This method should not be ran before an instruction is executed.
		// Otherwise we risk deserializing a full path early, which can lead to unsafe config injections. (e.g. malicious config file targeting sys files)
		// ^ perhaps the above is user error though? User should check what they are running in advance perhaps? Either way, we attempt to baby them here.
		// noValidate: Don't validate if the path/file exists on disk
		// noParse: Don't perform any manipulations on Source or Destination, besides the aforementioned ReplaceCustomVariables.
		internal void SetRealPaths(bool noParse = false, bool noValidate = false)
		{
			if ( _fileSystemProvider == null )
				throw new InvalidOperationException("File system provider must be set before calling SetRealPaths. Call SetFileSystemProvider() first.");

			// Get real path then enumerate the files/folders with wildcards and add them to the list
			if ( Source is null )
				throw new NullReferenceException(nameof(Source));


			List<string> newSourcePaths = Source.ConvertAll(Utility.Utility.ReplaceCustomVariables);
			if ( !noParse )
			{
				// Use provider for wildcard expansion
				newSourcePaths = PathHelper.EnumerateFilesWithWildcards(newSourcePaths, _fileSystemProvider);

				if ( !noValidate )
				{
					if ( newSourcePaths.IsNullOrEmptyOrAllNull() )
					{
						throw new FileNotFoundException(
							$"Could not find any files matching the pattern in the 'Source' path on disk! Got [{string.Join(separator: ", ", Source)}]"
						);
					}

					// Use file system provider instead of File.Exists
					if ( newSourcePaths.Any(f => !_fileSystemProvider.FileExists(f)) )
					{
						throw new FileNotFoundException(
							$"Could not find all files in the 'Source' path on disk! Got [{string.Join(separator: ", ", Source)}]"
						);
					}
				}

				// Remove duplicates
				RealSourcePaths = (
					MainConfig.CaseInsensitivePathing
						? newSourcePaths.Distinct(StringComparer.OrdinalIgnoreCase)
						: newSourcePaths.Distinct()
				).ToList();
			}

			string destinationPath = Utility.Utility.ReplaceCustomVariables(Destination);
			DirectoryInfo thisDestination = PathHelper.TryGetValidDirectoryInfo(destinationPath);
			if ( noParse )
			{
				RealDestinationPath = thisDestination;
				return;
			}

			// For Copy, Move, Rename, and Extract, the destination directory might not exist yet.
			// The file system providers are responsible for creating it. So we don't validate, just create the DirectoryInfo
			// For Rename, the destination is a file path, not a directory, so we also skip directory validation
			bool skipDestinationValidation = Action == ActionType.Copy || Action == ActionType.Move || Action == ActionType.Rename || Action == ActionType.Extract;

			if ( skipDestinationValidation && thisDestination == null )
			{
				thisDestination = new DirectoryInfo(destinationPath);
			}

			// Use file system provider instead of Directory.Exists
			if ( !noValidate && !skipDestinationValidation && thisDestination != null && !_fileSystemProvider.DirectoryExists(thisDestination.FullName) )
			{
				if ( MainConfig.CaseInsensitivePathing )
				{
					thisDestination = PathHelper.GetCaseSensitivePath(thisDestination);
				}

				if ( thisDestination != null && !_fileSystemProvider.DirectoryExists(thisDestination.FullName) )
					throw new DirectoryNotFoundException("Could not find the 'Destination' path on disk!");
			}

			RealDestinationPath = thisDestination;
		}

		// ReSharper disable once AssignNullToNotNullAttribute
		public async Task<ActionExitCode> ExtractFileAsync(
			DirectoryInfo argDestinationPath = null,
			[NotNull][ItemNotNull] List<string> argSourcePaths = null
		)
		{
			if ( _fileSystemProvider == null )
				throw new InvalidOperationException("File system provider must be set before calling ExtractFileAsync. Call SetFileSystemProvider() first.");

			try
			{
				RealSourcePaths = argSourcePaths
					?? RealSourcePaths ?? throw new ArgumentNullException(nameof(argSourcePaths));

				// Delegate to file system provider
				foreach ( string sourcePath in RealSourcePaths )
				{
					string destinationPath = argDestinationPath?.FullName ?? Path.GetDirectoryName(sourcePath);
					if ( string.IsNullOrEmpty(destinationPath) )
					{
						await Logger.LogErrorAsync($"Could not determine destination path for archive: {sourcePath}");
						return ActionExitCode.InvalidArchive;
					}

					try
					{
						List<string> extractedFiles = await _fileSystemProvider.ExtractArchiveAsync(sourcePath, destinationPath);
						await Logger.LogAsync($"Extracted {extractedFiles.Count} file(s) from '{Path.GetFileName(sourcePath)}' to '{destinationPath}'");
					}
					catch ( Exception ex )
					{
						await Logger.LogExceptionAsync(ex);
						return ActionExitCode.InvalidArchive;
					}
				}

				return ActionExitCode.Success;
			}
			catch ( ArgumentNullException ex )
			{
				await Logger.LogExceptionAsync(ex);
				return ActionExitCode.InvalidArchive;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return ActionExitCode.UnknownError;
			}
		}

		public void DeleteDuplicateFile(
				DirectoryInfo directoryPath = null,
				string fileExtension = null,
				bool caseInsensitive = true,
				List<string> compatibleExtensions = null
			)
		{
			if ( _fileSystemProvider == null )
				throw new InvalidOperationException("File system provider must be set before calling DeleteDuplicateFile. Call SetFileSystemProvider() first.");

			// internal args
			if ( directoryPath is null )
				directoryPath = RealDestinationPath;
			if ( !(directoryPath is null) && !_fileSystemProvider.DirectoryExists(directoryPath.FullName) && MainConfig.CaseInsensitivePathing )
				directoryPath = PathHelper.GetCaseSensitivePath(directoryPath);
			if ( directoryPath is null || !_fileSystemProvider.DirectoryExists(directoryPath.FullName) )
				throw new ArgumentException(message: "Invalid directory path.", nameof(directoryPath));

			var sourceExtensions = Source.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
			compatibleExtensions = compatibleExtensions
				?? (!sourceExtensions.IsNullOrEmptyOrAllNull()
					? sourceExtensions
					: compatibleExtensions)
				?? Game.TextureOverridePriorityList;

			if ( string.IsNullOrEmpty(fileExtension) )
				fileExtension = Arguments;

			List<string> filesList = _fileSystemProvider.GetFilesInDirectory(directoryPath.FullName);
			Dictionary<string, int> fileNameCounts = caseInsensitive
				? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
				: new Dictionary<string, int>();

			foreach ( string fileNameWithoutExtension in from filePath in filesList
														 select _fileSystemProvider.GetFileName(filePath) into fileName
														 let fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName)
														 let thisExtension = Path.GetExtension(fileName)
														 let compatibleExtensionFound = caseInsensitive
						 ? compatibleExtensions.Any(ext => ext.Equals(thisExtension, StringComparison.OrdinalIgnoreCase))
						 : compatibleExtensions.Contains(thisExtension)
														 where compatibleExtensionFound
														 select fileNameWithoutExtension )
			{
				_ = fileNameCounts.TryGetValue(fileNameWithoutExtension, out int count);
				fileNameCounts[fileNameWithoutExtension] = count + 1;
			}

			foreach ( string filePath in filesList )
			{
				if ( !ShouldDeleteFile(filePath) )
					continue;

				try
				{
					_ = _fileSystemProvider.DeleteFileAsync(filePath);
					string fileName = _fileSystemProvider.GetFileName(filePath);
					_ = Logger.LogAsync($"Deleted file: '{fileName}'");
					_ = Logger.LogVerboseAsync(
						$"Leaving alone '{fileNameCounts[Path.GetFileNameWithoutExtension(fileName)] - 1}' file(s) with the same name of '{Path.GetFileNameWithoutExtension(fileName)}'."
					);
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex);
				}
			}

			return;

			bool ShouldDeleteFile(string filePath)
			{
				string fileName = _fileSystemProvider?.GetFileName(filePath);
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
				string fileExtensionFromFile = Path.GetExtension(fileName);

				if ( string.IsNullOrEmpty(fileNameWithoutExtension) )
				{
					_ = Logger.LogWarningAsync(
						$"Skipping '{fileName}' Reason: fileNameWithoutExtension is null/empty somehow?"
					);
				}
				else if ( !fileNameCounts.TryGetValue(fileNameWithoutExtension, out int value) )
				{
					_ = Logger.LogVerboseAsync(
						$"Skipping '{fileName}' Reason: Not present in dictionary, ergo does not have a desired extension."
					);
				}
				else if ( value <= 1 )
				{
					_ = Logger.LogVerboseAsync(
						$"Skipping '{fileName}' Reason: '{fileNameWithoutExtension}' is the only file with this name."
					);
				}
				else if ( !string.Equals(fileExtensionFromFile, fileExtension, StringComparison.OrdinalIgnoreCase) )
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

		// ReSharper disable once AssignNullToNotNullAttribute
		public ActionExitCode DeleteFile(
			// ReSharper disable once AssignNullToNotNullAttribute
			[ItemNotNull][NotNull] List<string> sourcePaths = null
		)
		{
			if ( _fileSystemProvider == null )
				throw new InvalidOperationException("File system provider must be set before calling DeleteFile. Call SetFileSystemProvider() first.");

			if ( sourcePaths is null )
				sourcePaths = RealSourcePaths;
			if ( sourcePaths is null )
				throw new ArgumentNullException(nameof(sourcePaths));

			ActionExitCode exitCode = ActionExitCode.Success;
			try
			{
				// ReSharper disable once PossibleNullReferenceException
				foreach ( string thisFilePath in sourcePaths )
				{
					string realFilePath = thisFilePath;
					if ( MainConfig.CaseInsensitivePathing && !_fileSystemProvider.FileExists(realFilePath) )
						realFilePath = PathHelper.GetCaseSensitivePath(realFilePath).Item1;
					string sourceRelDirPath = MainConfig.SourcePath is null
						? thisFilePath
						: PathHelper.GetRelativePath(
							MainConfig.SourcePath.FullName,
							thisFilePath
						);

					if ( !Path.IsPathRooted(realFilePath) || !_fileSystemProvider.FileExists(realFilePath) )
					{
						Logger.LogWarning($"Invalid wildcards or file does not exist: '{sourceRelDirPath}'");
						exitCode = ActionExitCode.FileNotFoundPost;
					}

					// Delete the file using provider
					try
					{
						_ = _fileSystemProvider.DeleteFileAsync(realFilePath);
						_ = Logger.LogAsync($"Deleting '{sourceRelDirPath}'...");
					}
					catch ( Exception ex )
					{
						Logger.LogException(ex);
						if ( exitCode == ActionExitCode.Success )
						{
							exitCode = ActionExitCode.UnknownInnerError;
						}
					}
				}

				if ( sourcePaths.Count == 0 )
				{
					Logger.Log("No files to delete, skipping this instruction.");
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
				if ( exitCode == ActionExitCode.Success )
				{
					exitCode = ActionExitCode.UnknownInnerError;
				}
			}

			return exitCode;
		}

		public ActionExitCode RenameFile(
			// ReSharper disable once AssignNullToNotNullAttribute
			[ItemNotNull][NotNull] List<string> sourcePaths = null
		)
		{
			if ( _fileSystemProvider == null )
				throw new InvalidOperationException("File system provider must be set before calling RenameFile. Call SetFileSystemProvider() first.");

			if ( sourcePaths.IsNullOrEmptyCollection() )
				sourcePaths = RealSourcePaths;
			if ( sourcePaths.IsNullOrEmptyCollection() )
				throw new ArgumentNullException(nameof(sourcePaths));

			ActionExitCode exitCode = ActionExitCode.Success;
			try
			{
				// ReSharper disable once PossibleNullReferenceException
				foreach ( string sourcePath in sourcePaths )
				{
					string fileName = Path.GetFileName(sourcePath);
					string sourceRelDirPath = MainConfig.SourcePath is null
						? sourcePath
						: PathHelper.GetRelativePath(
							MainConfig.SourcePath.FullName,
							sourcePath
						);
					// Check if the source file already exists
					if ( !_fileSystemProvider.FileExists(sourcePath) )
					{
						Logger.LogError($"'{sourceRelDirPath}' does not exist!");
						if ( exitCode == ActionExitCode.Success )
						{
							exitCode = ActionExitCode.FileNotFoundPost;
						}

						continue;
					}

					// Check if the destination file already exists
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
					if ( _fileSystemProvider.FileExists(destinationFilePath) )
					{
						if ( !Overwrite )
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

					// Rename the file using provider
					try
					{
						_ = Logger.LogAsync($"Rename '{sourceRelDirPath}' to '{destinationRelDirPath}'");
						_ = _fileSystemProvider.RenameFileAsync(sourcePath, Destination, Overwrite);
					}
					catch ( IOException ex )
					{
						// Handle file move error, such as destination file already exists
						if ( exitCode == ActionExitCode.Success )
						{
							exitCode = ActionExitCode.IOException;
						}

						Logger.LogException(ex);
					}
				}

				return exitCode;
			}
			catch ( Exception ex )
			{
				// Handle any unexpected exceptions
				Logger.LogException(ex);
				if ( exitCode == ActionExitCode.Success )
				{
					exitCode = ActionExitCode.UnknownError;
				}
			}

			return exitCode;
		}

		public async Task<ActionExitCode> CopyFileAsync(
			// ReSharper disable twice AssignNullToNotNullAttribute
			[ItemNotNull][NotNull] List<string> sourcePaths = null,
			[NotNull] DirectoryInfo destinationPath = null
		)
		{
			if ( _fileSystemProvider == null )
				throw new InvalidOperationException("File system provider must be set before calling CopyFileAsync. Call SetFileSystemProvider() first.");

			if ( sourcePaths.IsNullOrEmptyCollection() )
				sourcePaths = RealSourcePaths;
			if ( sourcePaths.IsNullOrEmptyCollection() )
				throw new ArgumentNullException(nameof(sourcePaths));

			if ( destinationPath == null )
				destinationPath = RealDestinationPath;
			if ( destinationPath == null )
				throw new ArgumentNullException(nameof(destinationPath));

			int maxCount = MainConfig.UseMultiThreadedIO
				? 16
				: 1;
			using ( var semaphore = new SemaphoreSlim(initialCount: 1, maxCount) )
			{
				SemaphoreSlim localSemaphore = semaphore;
				async Task CopyIndividualFileAsync(string sourcePath)
				{
					await localSemaphore.WaitAsync(); // Wait for a semaphore slot
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

						// Check if the destination file already exists
						if ( _fileSystemProvider.FileExists(destinationFilePath) )
						{
							if ( !Overwrite )
							{
								await Logger.LogWarningAsync(
									$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
									+ " skipping file. Reason: Overwrite set to False )"
								);

								return;
							}

							await Logger.LogAsync(
								$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
								+ $" deleting pre-existing file '{destinationRelDirPath}' Reason: Overwrite set to True"
							);
							await _fileSystemProvider.DeleteFileAsync(destinationFilePath);
						}

						await Logger.LogAsync($"Copy '{sourceRelDirPath}' to '{destinationRelDirPath}'");
						await _fileSystemProvider.CopyFileAsync(sourcePath, destinationFilePath, Overwrite);
					}
					catch ( Exception ex )
					{
						await Logger.LogExceptionAsync(ex);
						throw;
					}
					finally
					{
						_ = localSemaphore.Release(); // Release the semaphore slot
					}
				}

				if ( sourcePaths is null )
					throw new NullReferenceException(nameof(sourcePaths));

				var tasks = sourcePaths.Select(CopyIndividualFileAsync).ToList();

				try
				{
					await Task.WhenAll(tasks); // Wait for all move tasks to complete
					return ActionExitCode.Success;
				}
				catch
				{
					return ActionExitCode.UnknownError;
				}
			}
		}

		public async Task<ActionExitCode> MoveFileAsync(
			// ReSharper disable twice AssignNullToNotNullAttribute
			[ItemNotNull][NotNull] List<string> sourcePaths = null,
			[NotNull] DirectoryInfo destinationPath = null
		)
		{
			if ( _fileSystemProvider == null )
				throw new InvalidOperationException("File system provider must be set before calling MoveFileAsync. Call SetFileSystemProvider() first.");

			if ( sourcePaths.IsNullOrEmptyCollection() )
				sourcePaths = RealSourcePaths;
			if ( sourcePaths.IsNullOrEmptyCollection() )
				throw new ArgumentNullException(nameof(sourcePaths));

			if ( destinationPath == null )
				destinationPath = RealDestinationPath;
			if ( destinationPath == null )
				throw new ArgumentNullException(nameof(destinationPath));

			int maxCount = MainConfig.UseMultiThreadedIO
				? 16
				: 1;
			using ( var semaphore = new SemaphoreSlim(initialCount: 1, maxCount) )
			{
				SemaphoreSlim localSemaphore = semaphore;
				async Task MoveIndividualFileAsync(string sourcePath)
				{
					await localSemaphore.WaitAsync(); // Wait for a semaphore slot

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

						// Check if the destination file already exists
						if ( _fileSystemProvider.FileExists(destinationFilePath) )
						{
							if ( !Overwrite )
							{
								await Logger.LogWarningAsync(
									$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
									+ " skipping file. Reason: Overwrite set to False )"
								);

								return;
							}

							await Logger.LogAsync(
								$"File '{fileName}' already exists in {Path.GetDirectoryName(destinationRelDirPath)},"
								+ $" deleting pre-existing file '{destinationRelDirPath}' Reason: Overwrite set to True"
							);
							await _fileSystemProvider.DeleteFileAsync(destinationFilePath);
						}

						await Logger.LogAsync($"Move '{sourceRelDirPath}' to '{destinationRelDirPath}'");
						await _fileSystemProvider.MoveFileAsync(sourcePath, destinationFilePath, Overwrite);
					}
					catch ( Exception ex )
					{
						await Logger.LogExceptionAsync(ex);
						throw;
					}
					finally
					{
						_ = localSemaphore.Release(); // Release the semaphore slot
					}
				}

				if ( sourcePaths is null )
					throw new NullReferenceException(nameof(sourcePaths));

				var tasks = sourcePaths.Select(MoveIndividualFileAsync).ToList();
				try
				{
					await Task.WhenAll(tasks); // Wait for all move tasks to complete
					return ActionExitCode.Success;
				}
				catch
				{
					return ActionExitCode.UnknownError;
				}
			}
		}


		// todo: define exit codes here.
		public async Task<ActionExitCode> ExecuteTSLPatcherAsync()
		{
			if ( _fileSystemProvider == null )
				throw new InvalidOperationException("File system provider must be set before calling ExecuteTSLPatcherAsync. Call SetFileSystemProvider() first.");

			try
			{
				foreach ( string t in RealSourcePaths )
				{
					DirectoryInfo tslPatcherDirectory = _fileSystemProvider.FileExists(t)
						? PathHelper.TryGetValidDirectoryInfo(_fileSystemProvider.GetDirectoryName(t)) // It's a file, create DirectoryInfo instance of the parent.
						: new DirectoryInfo(t);                                         // It's a folder, create a DirectoryInfo instance

					if ( tslPatcherDirectory is null || !_fileSystemProvider.DirectoryExists(tslPatcherDirectory.FullName) )
						throw new DirectoryNotFoundException($"The directory '{t}' could not be located on the disk.");

					//PlaintextLog=0
					string fullInstallLogFile = Path.Combine(tslPatcherDirectory.FullName, path2: "installlog.rtf");
					if ( _fileSystemProvider.FileExists(fullInstallLogFile) )
						await _fileSystemProvider.DeleteFileAsync(fullInstallLogFile);

					//PlaintextLog=1
					fullInstallLogFile = Path.Combine(tslPatcherDirectory.FullName, path2: "installlog.txt");
					if ( _fileSystemProvider.FileExists(fullInstallLogFile) )
						await _fileSystemProvider.DeleteFileAsync(fullInstallLogFile);

					IniHelper.ReplaceIniPattern(tslPatcherDirectory, pattern: @"^\s*PlaintextLog\s*=\s*0\s*$", replacement: "PlaintextLog=1");
					IniHelper.ReplaceIniPattern(tslPatcherDirectory, pattern: @"^\s*LookupGameFolder\s*=\s*1\s*$", replacement: "LookupGameFolder=0");
					IniHelper.ReplaceIniPattern(tslPatcherDirectory, pattern: @"^\s*ConfirmMessage\s*=\s*.*$", replacement: "ConfirmMessage=N/A");

					var argList = new List<string>
					{
						"--install",
						$"--game-dir=\"{MainConfig.DestinationPath}\"",
						$"--tslpatchdata=\"{tslPatcherDirectory}\"",
					};

					if ( !string.IsNullOrEmpty(Arguments) ) argList.Add($"--namespace-option-index={Arguments}");

					string args = string.Join(separator: " ", argList);


					FileSystemInfo patcherCliPath = null;

					string baseDir = Utility.Utility.GetBaseDirectory();
					string resourcesDir = Utility.Utility.GetResourcesDirectory(baseDir);
					if ( Utility.Utility.GetOperatingSystem() == OSPlatform.Windows )
					{
						patcherCliPath = new FileInfo(Path.Combine(resourcesDir, "holopatcher.exe"));
					}
					else
					{
						// Handling OSX specific paths
						// FIXME: .app's aren't accepting command-line arguments correctly.
						string[] possibleOsxPaths = {
							Path.Combine(resourcesDir, "HoloPatcher.app", "Contents", "MacOS", "holopatcher"),
							Path.Combine(resourcesDir, "holopatcher"),
							Path.Combine(baseDir, "Resources", "HoloPatcher.app", "Contents", "MacOS", "holopatcher"),
							Path.Combine(baseDir, "Resources", "holopatcher")
						};

						OSPlatform thisOperatingSystem = Utility.Utility.GetOperatingSystem();
						foreach ( string path in possibleOsxPaths )
						{
							patcherCliPath = thisOperatingSystem == OSPlatform.OSX && path.ToLowerInvariant().EndsWith(".app")
								? PathHelper.GetCaseSensitivePath(new DirectoryInfo(path))
								: (FileSystemInfo)PathHelper.GetCaseSensitivePath(new FileInfo(path));
							if ( patcherCliPath.Exists )
							{
								await Logger.LogVerboseAsync($"Found holopatcher at '{patcherCliPath.FullName}'...");
								break;
							}

							await Logger.LogVerboseAsync($"Holopatcher not found at '{patcherCliPath.FullName}'...");
						}
					}

					if ( patcherCliPath is null || !patcherCliPath.Exists )
						throw new FileNotFoundException($"Could not load HoloPatcher from the '{resourcesDir}' directory!");
					patcherCliPath = new FileInfo(t);
					if ( int.TryParse(Arguments.Trim(), out int namespaceId) )
					{
						string message = $"If asked to pick an option, select the {Serializer.ToOrdinal(namespaceId + 1)} from the top.";
						_ = CallbackObjects.InformationCallback.ShowInformationDialog(message);
						await Logger.LogWarningAsync(message);
					}

					await Logger.LogAsync($"Using CLI to run command: '{patcherCliPath} {args}'");

					// ReSharper disable twice UnusedVariable
					(int exitCode, string output, string error) = await _fileSystemProvider.ExecuteProcessAsync(
						patcherCliPath.FullName,
						args
					);
					await Logger.LogVerboseAsync($"'{patcherCliPath.Name}' exited with exit code {exitCode}");
					if ( exitCode != 0 )
						return ActionExitCode.PatcherError;

					try
					{
						List<string> installErrors = await VerifyInstall();
						if ( installErrors.Count <= 0 )
							continue;

						await Logger.LogAsync(string.Join(Environment.NewLine, installErrors));

						return ActionExitCode.TSLPatcherError;
					}
					catch ( Exception )
					{
						return ActionExitCode.TSLPatcherLogNotFound;
					}
				}

				return ActionExitCode.Success;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				throw;
			}
		}

		public async Task<ActionExitCode> ExecuteProgramAsync([ItemNotNull] List<string> sourcePaths = null)
		{
			if ( _fileSystemProvider == null )
				throw new InvalidOperationException("File system provider must be set before calling ExecuteProgramAsync. Call SetFileSystemProvider() first.");

			try
			{
				if ( sourcePaths == null )
					sourcePaths = RealSourcePaths;
				if ( sourcePaths == null )
					throw new ArgumentNullException(nameof(sourcePaths));

				ActionExitCode exitCode = ActionExitCode.Success; // Track the success status
				foreach ( string sourcePath in sourcePaths )
				{
					try
					{
						(int childExitCode, string output, string error) =
							await _fileSystemProvider.ExecuteProcessAsync(
								sourcePath,
								Utility.Utility.ReplaceCustomVariables(Arguments)
							);

						_ = Logger.LogVerboseAsync(output + Environment.NewLine + error);
						if ( childExitCode == 0 )
							continue;

						exitCode = ActionExitCode.ChildProcessError;
						break;
					}
					catch ( FileNotFoundException ex )
					{
						await Logger.LogExceptionAsync(ex);
						return ActionExitCode.FileNotFoundPost;
					}
					catch ( Exception ex )
					{
						await Logger.LogExceptionAsync(ex);
						return ActionExitCode.UnknownInnerError;
					}
				}

				return exitCode;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return ActionExitCode.UnknownError;
			}
		}

		// parse TSLPatcher's installlog.rtf (or installlog.txt) for errors.
		[NotNull]
		private async Task<List<string>> VerifyInstall([ItemNotNull] List<string> sourcePaths = null)
		{
			if ( _fileSystemProvider == null )
				throw new InvalidOperationException("File system provider must be set before calling VerifyInstall. Call SetFileSystemProvider() first.");

			if ( sourcePaths == null )
				sourcePaths = RealSourcePaths;
			if ( sourcePaths == null )
				throw new ArgumentNullException(nameof(sourcePaths));

			// For dry-run (virtual file system), we can't actually verify install logs
			// since the patcher didn't really run. Just return empty list.
			if ( _fileSystemProvider.IsDryRun )
			{
				Logger.LogVerbose("Skipping install log verification for dry-run");
				return new List<string>();
			}

			foreach ( string sourcePath in sourcePaths )
			{
				string tslPatcherDirPath = _fileSystemProvider.GetDirectoryName(sourcePath)
					?? throw new DirectoryNotFoundException($"Could not retrieve parent directory of '{sourcePath}'.");

				//PlaintextLog=0
				string fullInstallLogFile = Path.Combine(tslPatcherDirPath, path2: "installlog.rtf");
				if ( !_fileSystemProvider.FileExists(fullInstallLogFile) )
				{
					//PlaintextLog=1
					fullInstallLogFile = Path.Combine(tslPatcherDirPath, path2: "installlog.txt");
					if ( !_fileSystemProvider.FileExists(fullInstallLogFile) )
						throw new FileNotFoundException(message: "Install log file not found.", fullInstallLogFile);
				}

				// Use file system provider to read the log file
				string installLogContent = await _fileSystemProvider.ReadFileAsync(fullInstallLogFile);

				return installLogContent.Split(Environment.NewLine.ToCharArray()).Where(
					thisLine => thisLine.Contains("Error: ") || thisLine.Contains("[Error]")
				).ToList();
			}

			Logger.LogVerbose("No errors found in TSLPatcher installation log file");
			return new List<string>();
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName][CanBeNull] string propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		[NotNull]
		[ItemNotNull]
		public List<Option> GetChosenOptions() => _parentComponent?.Options.Where(
				x => x != null && x.IsSelected && Source.Contains(x.Guid.ToString(), StringComparer.OrdinalIgnoreCase)
			).ToList()
			?? new List<Option>();
		/*return theseChosenOptions?.Count > 0
		    ? theseChosenOptions
		    : throw new KeyNotFoundException( message: "Could not find chosen option for this instruction" );*/
	}
}
