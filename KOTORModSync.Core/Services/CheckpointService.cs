// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Core.Services
{
	
	
	
	
	public class CheckpointService
	{
		private readonly string _checkpointRootPath;
		private readonly string _destinationPath;
		private InstallationSession _currentSession;

		public event EventHandler<CheckpointEventArgs> CheckpointCreated;
		public event EventHandler<CheckpointEventArgs> CheckpointRestored;
		public event EventHandler<CheckpointProgressEventArgs> CheckpointProgress;

		public CheckpointService(string destinationPath)
		{
			_destinationPath = destinationPath ?? throw new ArgumentNullException(nameof(destinationPath));
			_checkpointRootPath = Path.Combine(
				Path.GetDirectoryName(destinationPath) ?? destinationPath,
				".kotormodsync_checkpoints"
			);
		}

		
		
		
		public async Task<InstallationSession> StartInstallationSessionAsync(
			string sessionName,
			CancellationToken cancellationToken = default)
		{
			await Logger.LogAsync($"[Checkpoint] Starting installation session: {sessionName}");

			_currentSession = new InstallationSession
			{
				SessionId = Guid.NewGuid().ToString(),
				SessionName = sessionName,
				StartTime = DateTime.UtcNow,
				CheckpointRootPath = _checkpointRootPath,
				Checkpoints = new List<Checkpoint>()
			};

			
			Directory.CreateDirectory(_checkpointRootPath);
			Directory.CreateDirectory(GetSessionPath(_currentSession.SessionId));

			
			await CreateBaselineCheckpointAsync(cancellationToken);

			
			await SaveSessionMetadataAsync();

			return _currentSession;
		}

		
		
		
		public async Task<Checkpoint> CreateCheckpointAsync(
			string componentName,
			Guid componentGuid,
			CancellationToken cancellationToken = default)
		{
			if ( _currentSession == null )
				throw new InvalidOperationException("No active installation session. Call StartInstallationSessionAsync first.");

			await Logger.LogAsync($"[Checkpoint] Creating checkpoint for: {componentName}");

			var checkpoint = new Checkpoint
			{
				CheckpointId = Guid.NewGuid().ToString(),
				ComponentName = componentName,
				ComponentGuid = componentGuid,
				Timestamp = DateTime.UtcNow,
				SequenceNumber = _currentSession.Checkpoints.Count
			};

			try
			{
				
				var currentState = await ScanDirectoryStateAsync(_destinationPath, cancellationToken);

				
				var previousState = _currentSession.Checkpoints.LastOrDefault()?.FileState ?? new Dictionary<string, FileStateInfo>();
				var changes = DetectChanges(previousState, currentState);

				checkpoint.FileState = currentState;
				checkpoint.Changes = changes;

				
				await BackupChangedFilesAsync(checkpoint, changes, cancellationToken);

				
				_currentSession.Checkpoints.Add(checkpoint);

				
				await SaveCheckpointMetadataAsync(checkpoint);
				await SaveSessionMetadataAsync();

				CheckpointCreated?.Invoke(this, new CheckpointEventArgs(checkpoint));

				await Logger.LogAsync($"[Checkpoint] Created checkpoint {checkpoint.CheckpointId}: {changes.AddedFiles.Count} added, {changes.ModifiedFiles.Count} modified, {changes.DeletedFiles.Count} deleted");

				return checkpoint;
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"[Checkpoint] Failed to create checkpoint: {ex.Message}");
				throw;
			}
		}

		
		
		
		public async Task RollbackToCheckpointAsync(
			string checkpointId,
			IProgress<RollbackProgress> progress = null,
			CancellationToken cancellationToken = default)
		{
			if ( _currentSession == null )
				throw new InvalidOperationException("No active installation session.");

			var targetCheckpoint = _currentSession.Checkpoints.FirstOrDefault(c => c.CheckpointId == checkpointId);
			if ( targetCheckpoint == null )
				throw new ArgumentException($"Checkpoint {checkpointId} not found in current session.");

			await Logger.LogAsync($"[Checkpoint] Rolling back to checkpoint: {targetCheckpoint.ComponentName}");

			var checkpointsToUndo = _currentSession.Checkpoints
				.Where(c => c.SequenceNumber > targetCheckpoint.SequenceNumber)
				.OrderByDescending(c => c.SequenceNumber)
				.ToList();

			int totalSteps = checkpointsToUndo.Sum(c => c.Changes.AddedFiles.Count + c.Changes.ModifiedFiles.Count + c.Changes.DeletedFiles.Count);
			int currentStep = 0;

			foreach ( var checkpoint in checkpointsToUndo )
			{
				await Logger.LogVerboseAsync($"[Checkpoint] Undoing checkpoint: {checkpoint.ComponentName}");

				
				await UndoCheckpointChangesAsync(checkpoint, (step, total, message) =>
				{
					currentStep++;
					progress?.Report(new RollbackProgress
					{
						CurrentStep = currentStep,
						TotalSteps = totalSteps,
						CurrentCheckpoint = checkpoint.ComponentName,
						CurrentAction = message
					});
				}, cancellationToken);

				
				_currentSession.Checkpoints.Remove(checkpoint);

				CheckpointRestored?.Invoke(this, new CheckpointEventArgs(checkpoint));
			}

			await SaveSessionMetadataAsync();

			await Logger.LogAsync($"[Checkpoint] Rollback complete. Restored to: {targetCheckpoint.ComponentName}");
		}

		
		
		
		public async Task CompleteSessionAsync(bool keepCheckpoints = true)
		{
			if ( _currentSession == null )
				return;

			_currentSession.EndTime = DateTime.UtcNow;
			_currentSession.IsCompleted = true;

			await SaveSessionMetadataAsync();

			await Logger.LogAsync($"[Checkpoint] Installation session completed: {_currentSession.SessionName}");

			if ( !keepCheckpoints )
			{
				await CleanupSessionAsync(_currentSession.SessionId);
			}

			_currentSession = null;
		}

		
		
		
		public async Task CleanupSessionAsync(string sessionId)
		{
			var sessionPath = GetSessionPath(sessionId);
			if ( Directory.Exists(sessionPath) )
			{
				await Task.Run(() => Directory.Delete(sessionPath, recursive: true));
				await Logger.LogAsync($"[Checkpoint] Cleaned up session: {sessionId}");
			}
		}

		
		
		
		public async Task<List<InstallationSession>> ListSessionsAsync()
		{
			if ( !Directory.Exists(_checkpointRootPath) )
				return new List<InstallationSession>();

			var sessions = new List<InstallationSession>();
			var sessionDirs = Directory.GetDirectories(_checkpointRootPath);

			foreach ( var sessionDir in sessionDirs )
			{
				var metadataPath = Path.Combine(sessionDir, "session.json");
				if ( File.Exists(metadataPath) )
				{
					try
					{
						string json;
						using ( var reader = new StreamReader(metadataPath) )
						{
							json = await reader.ReadToEndAsync();
						}
						var session = JsonSerializer.Deserialize<InstallationSession>(json);

						if ( session != null )
						{
							sessions.Add(session);
						}
					}
					catch ( Exception ex )
					{
						await Logger.LogWarningAsync($"[Checkpoint] Failed to load session metadata from {sessionDir}: {ex.Message}");
					}
				}
			}

			return sessions.OrderByDescending(s => s.StartTime).ToList();
		}

		#region Private Methods

		private async Task CreateBaselineCheckpointAsync(CancellationToken cancellationToken)
		{
			var baseline = new Checkpoint
			{
				CheckpointId = "baseline",
				ComponentName = "Initial State",
				ComponentGuid = Guid.Empty,
				Timestamp = DateTime.UtcNow,
				SequenceNumber = 0,
				Changes = new FileChanges()
			};

			baseline.FileState = await ScanDirectoryStateAsync(_destinationPath, cancellationToken);
			_currentSession.Checkpoints.Add(baseline);

			await SaveCheckpointMetadataAsync(baseline);
		}

		private async Task<Dictionary<string, FileStateInfo>> ScanDirectoryStateAsync(
			string path,
			CancellationToken cancellationToken)
		{
			var state = new Dictionary<string, FileStateInfo>(StringComparer.OrdinalIgnoreCase);

			if ( !Directory.Exists(path) )
				return state;

			var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
			int totalFiles = files.Length;
			int processedFiles = 0;

			foreach ( var file in files )
			{
				if ( cancellationToken.IsCancellationRequested )
					break;

				var relativePath = PathHelper.GetRelativePath(path, file);
				var fileInfo = new FileInfo(file);

				state[relativePath] = new FileStateInfo
				{
					RelativePath = relativePath,
					Size = fileInfo.Length,
					LastModified = fileInfo.LastWriteTimeUtc,
					Hash = await ComputeFileHashAsync(file, cancellationToken)
				};

				processedFiles++;
				if ( processedFiles % 100 == 0 )
				{
					CheckpointProgress?.Invoke(this, new CheckpointProgressEventArgs
					{
						Operation = "Scanning",
						CurrentFile = relativePath,
						Processed = processedFiles,
						Total = totalFiles
					});
				}
			}

			return state;
		}

		private static FileChanges DetectChanges(
			Dictionary<string, FileStateInfo> previous,
			Dictionary<string, FileStateInfo> current)
		{
			var changes = new FileChanges
			{
				AddedFiles = new List<string>(),
				ModifiedFiles = new List<string>(),
				DeletedFiles = new List<string>()
			};

			
			foreach ( var kvp in current )
			{
				if ( !previous.ContainsKey(kvp.Key) )
				{
					changes.AddedFiles.Add(kvp.Key);
				}
				else if ( previous[kvp.Key].Hash != kvp.Value.Hash )
				{
					changes.ModifiedFiles.Add(kvp.Key);
				}
			}

			
			foreach ( var kvp in previous )
			{
				if ( !current.ContainsKey(kvp.Key) )
				{
					changes.DeletedFiles.Add(kvp.Key);
				}
			}

			return changes;
		}

		private async Task BackupChangedFilesAsync(
			Checkpoint checkpoint,
			FileChanges changes,
			CancellationToken cancellationToken)
		{
			var backupPath = GetCheckpointBackupPath(checkpoint.CheckpointId);
			Directory.CreateDirectory(backupPath);

			int totalFiles = changes.AddedFiles.Count + changes.ModifiedFiles.Count;
			int processedFiles = 0;

			
			foreach ( var relativePath in changes.AddedFiles.Concat(changes.ModifiedFiles) )
			{
				if ( cancellationToken.IsCancellationRequested )
					break;

				var sourcePath = Path.Combine(_destinationPath, relativePath);
				var backupFilePath = Path.Combine(backupPath, relativePath);

				if ( File.Exists(sourcePath) )
				{
					var backupDir = Path.GetDirectoryName(backupFilePath);
					if ( !string.IsNullOrEmpty(backupDir) )
						Directory.CreateDirectory(backupDir);

					await Task.Run(() => File.Copy(sourcePath, backupFilePath, overwrite: true), cancellationToken);
				}

				processedFiles++;
				if ( processedFiles % 50 == 0 )
				{
					CheckpointProgress?.Invoke(this, new CheckpointProgressEventArgs
					{
						Operation = "Backing up",
						CurrentFile = relativePath,
						Processed = processedFiles,
						Total = totalFiles
					});
				}
			}

			
			if ( changes.DeletedFiles.Any() )
			{
				var deletedFilesPath = Path.Combine(backupPath, "_deleted_files.json");
				var json = JsonSerializer.Serialize(changes.DeletedFiles, new JsonSerializerOptions { WriteIndented = true });
				using ( var writer = new StreamWriter(deletedFilesPath) )
				{
					await writer.WriteAsync(json);
				}
			}
		}

		private async Task UndoCheckpointChangesAsync(
			Checkpoint checkpoint,
			Action<int, int, string> progressCallback,
			CancellationToken cancellationToken)
		{
			var backupPath = GetCheckpointBackupPath(checkpoint.CheckpointId);
			int totalChanges = checkpoint.Changes.AddedFiles.Count + checkpoint.Changes.ModifiedFiles.Count + checkpoint.Changes.DeletedFiles.Count;
			int currentChange = 0;

			
			foreach ( var relativePath in checkpoint.Changes.AddedFiles )
			{
				if ( cancellationToken.IsCancellationRequested )
					break;

				var filePath = Path.Combine(_destinationPath, relativePath);
				if ( File.Exists(filePath) )
				{
					await Task.Run(() => File.Delete(filePath), cancellationToken);
					progressCallback?.Invoke(++currentChange, totalChanges, $"Deleting {relativePath}");
				}
			}

			
			foreach ( var relativePath in checkpoint.Changes.ModifiedFiles )
			{
				if ( cancellationToken.IsCancellationRequested )
					break;

				var backupFilePath = Path.Combine(backupPath, relativePath);
				var destinationFilePath = Path.Combine(_destinationPath, relativePath);

				if ( File.Exists(backupFilePath) )
				{
					await Task.Run(() => File.Copy(backupFilePath, destinationFilePath, overwrite: true), cancellationToken);
					progressCallback?.Invoke(++currentChange, totalChanges, $"Restoring {relativePath}");
				}
			}

			
			
			currentChange += checkpoint.Changes.DeletedFiles.Count;
		}

		private async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
		{
			try
			{
				using ( var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 8192, useAsync: true) )
				using ( var sha256 = SHA256.Create() )
				{
					var hashBytes = await Task.Run(() => sha256.ComputeHash(stream), cancellationToken);
					return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
				}
			}
			catch
			{
				return string.Empty;
			}
		}

		private async Task SaveCheckpointMetadataAsync(Checkpoint checkpoint)
		{
			var metadataPath = Path.Combine(GetSessionPath(_currentSession.SessionId), $"checkpoint_{checkpoint.CheckpointId}.json");
			var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions { WriteIndented = true });
			using ( var writer = new StreamWriter(metadataPath) )
			{
				await writer.WriteAsync(json);
			}
		}

		private async Task SaveSessionMetadataAsync()
		{
			var metadataPath = Path.Combine(GetSessionPath(_currentSession.SessionId), "session.json");
			var json = JsonSerializer.Serialize(_currentSession, new JsonSerializerOptions { WriteIndented = true });
			using ( var writer = new StreamWriter(metadataPath) )
			{
				await writer.WriteAsync(json);
			}
		}

		private static string GetRelativePath(string relativeTo, string path)
		{
			var relativeToUri = new Uri(relativeTo.EndsWith(Path.DirectorySeparatorChar.ToString()) ? relativeTo : relativeTo + Path.DirectorySeparatorChar);
			var pathUri = new Uri(path);
			return Uri.UnescapeDataString(relativeToUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
		}

		private string GetSessionPath(string sessionId) => Path.Combine(_checkpointRootPath, sessionId);

		private string GetCheckpointBackupPath(string checkpointId) =>
			Path.Combine(GetSessionPath(_currentSession.SessionId), "backups", checkpointId);

		#endregion
	}

	#region Data Models

	public class InstallationSession
	{
		public string SessionId { get; set; }
		public string SessionName { get; set; }
		public DateTime StartTime { get; set; }
		public DateTime? EndTime { get; set; }
		public bool IsCompleted { get; set; }
		public string CheckpointRootPath { get; set; }
		public List<Checkpoint> Checkpoints { get; set; }
	}

	public class Checkpoint
	{
		public string CheckpointId { get; set; }
		public string ComponentName { get; set; }
		public Guid ComponentGuid { get; set; } = Guid.Empty;
		public DateTime Timestamp { get; set; }
		public int SequenceNumber { get; set; }
		public Dictionary<string, FileStateInfo> FileState { get; set; }
		public FileChanges Changes { get; set; }
	}

	public class FileStateInfo
	{
		public string RelativePath { get; set; }
		public long Size { get; set; }
		public DateTime LastModified { get; set; }
		public string Hash { get; set; }
	}

	public class FileChanges
	{
		public List<string> AddedFiles { get; set; } = new List<string>();
		public List<string> ModifiedFiles { get; set; } = new List<string>();
		public List<string> DeletedFiles { get; set; } = new List<string>();
	}

	public class CheckpointEventArgs : EventArgs
	{
		public Checkpoint Checkpoint { get; }

		public CheckpointEventArgs(Checkpoint checkpoint)
		{
			Checkpoint = checkpoint;
		}
	}

	public class CheckpointProgressEventArgs : EventArgs
	{
		public string Operation { get; set; }
		public string CurrentFile { get; set; }
		public int Processed { get; set; }
		public int Total { get; set; }
	}

	public class RollbackProgress
	{
		public int CurrentStep { get; set; }
		public int TotalSteps { get; set; }
		public string CurrentCheckpoint { get; set; }
		public string CurrentAction { get; set; }
	}

	#endregion
}

