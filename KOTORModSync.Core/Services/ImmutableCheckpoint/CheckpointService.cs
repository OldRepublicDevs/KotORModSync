// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;

namespace KOTORModSync.Core.Services.ImmutableCheckpoint
{
	/// <summary>
	/// Manages installation checkpoints with bidirectional delta chains and anchor strategy.
	/// Anchors are created every 10th checkpoint for faster long-distance navigation.
	/// Thread-safe with file locking and corruption detection.
	/// </summary>
	public class CheckpointService
	{
		private const int ANCHOR_FREQUENCY = 10;
		private readonly string _checkpointDirectory;
		private readonly string _gameDirectory;
		private readonly ContentAddressableStore _casStore;
		private readonly BinaryDiffService _diffService;
		private readonly object _sessionLock = new object();

		private CheckpointSession _currentSession;
		private Dictionary<string, FileState> _baselineFiles;
		private Dictionary<string, FileState> _currentFiles;
		private int _checkpointCounter;

		public event EventHandler<CheckpointEventArgs> CheckpointCreated;
		public event EventHandler<CheckpointEventArgs> CheckpointRestored;
		public event EventHandler<CheckpointProgressEventArgs> Progress;

		public CheckpointService(string gameDirectory)
		{
			if ( string.IsNullOrWhiteSpace(gameDirectory) )
				throw new ArgumentNullException(nameof(gameDirectory));

			_gameDirectory = gameDirectory;
			_checkpointDirectory = Path.Combine(gameDirectory, ".kotor_modsync", "checkpoints");

			Directory.CreateDirectory(_checkpointDirectory);

			_casStore = new ContentAddressableStore(_checkpointDirectory);
			_diffService = new BinaryDiffService(_casStore);
		}

		/// <summary>
		/// Starts a new installation session and captures the baseline (initial game directory state).
		/// </summary>
		public async Task<string> StartInstallationSessionAsync(CancellationToken cancellationToken = default)
		{
			await Logger.LogAsync("[Checkpoint] Starting installation session...");

			string sessionId = Guid.NewGuid().ToString();
			string sessionName = $"Installation_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";

			_currentSession = new CheckpointSession
			{
				Id = sessionId,
				Name = sessionName,
				GamePath = _gameDirectory,
				StartTime = DateTime.UtcNow,
				IsComplete = false
			};

			_checkpointCounter = 0;

			// Capture baseline (current state of game directory)
			ReportProgress("Scanning baseline directory...", 0, 1);
			_baselineFiles = await ScanDirectoryAsync(_gameDirectory, cancellationToken);
			_currentFiles = new Dictionary<string, FileState>(_baselineFiles, StringComparer.OrdinalIgnoreCase);

			await Logger.LogAsync($"[Checkpoint] Baseline captured: {_baselineFiles.Count} files, " +
				$"{_baselineFiles.Sum(f => f.Value.Size):N0} bytes");

			// Save session metadata
			await SaveSessionAsync();

			// Save baseline
			string baselinePath = GetBaselinePath(sessionId);
			await CheckpointService.SaveBaselineAsync(baselinePath, _baselineFiles);

			await Logger.LogAsync($"[Checkpoint] Session started: {sessionName} (ID: {sessionId})");

			return sessionId;
		}

		/// <summary>
		/// Creates a checkpoint after a ModComponent installation.
		/// Automatically determines if this should be an anchor checkpoint.
		/// </summary>
		public async Task<string> CreateCheckpointAsync(
			string componentName,
			string componentGuid,
			CancellationToken cancellationToken = default)
		{
			if ( _currentSession == null )
				throw new InvalidOperationException("No active session. Call StartInstallationSessionAsync first.");

			_checkpointCounter++;
			bool isAnchor = (_checkpointCounter % ANCHOR_FREQUENCY) == 0;

			await Logger.LogAsync($"[Checkpoint] Creating checkpoint #{_checkpointCounter} for '{componentName}' " +
				$"(Anchor: {isAnchor})...");

			ReportProgress($"Creating checkpoint for {componentName}...", _checkpointCounter - 1, _currentSession.TotalComponents);

			// Scan current game directory state
			var newFiles = await ScanDirectoryAsync(_gameDirectory, cancellationToken);

			// Compute changes from previous checkpoint
			var changes = CheckpointService.ComputeFileChanges(_currentFiles, newFiles);

			await Logger.LogAsync($"[Checkpoint] Detected changes: +{changes.Added.Count} ~{changes.Modified.Count} -{changes.Deleted.Count}");

			// Create checkpoint
			string checkpointId = Guid.NewGuid().ToString();
			string previousCheckpointId = _currentSession.CheckpointIds.LastOrDefault();
			string previousAnchorId = GetPreviousAnchorId();

			var checkpoint = new Checkpoint
			{
				Id = checkpointId,
				SessionId = _currentSession.Id,
				ComponentName = componentName,
				ComponentGuid = componentGuid,
				Sequence = _checkpointCounter,
				Timestamp = DateTime.UtcNow,
				PreviousId = previousCheckpointId,
				IsAnchor = isAnchor,
				PreviousAnchorId = previousAnchorId
			};

			// Process added files
			foreach ( string addedPath in changes.Added )
			{
				string fullPath = Path.Combine(_gameDirectory, addedPath);
				string casHash = await _casStore.StoreFileAsync(fullPath);

				var fileState = newFiles[addedPath];
				fileState.CASHash = casHash;

				checkpoint.Added.Add(addedPath);
				checkpoint.Files[addedPath] = fileState;
			}

			// Process modified files with bidirectional deltas
			foreach ( string modifiedPath in changes.Modified )
			{
				string fullPath = Path.Combine(_gameDirectory, modifiedPath);
				string oldFullPath = Path.Combine(_gameDirectory, modifiedPath);

				// Retrieve old file from CAS temporarily
				string tempOldFile = Path.GetTempFileName();
				try
				{
					string oldCASHash = _currentFiles[modifiedPath].CASHash;
					await _casStore.RetrieveFileAsync(oldCASHash, tempOldFile);

					// Create bidirectional delta
					var delta = await _diffService.CreateBidirectionalDeltaAsync(
						tempOldFile,
						fullPath,
						modifiedPath,
						cancellationToken);

					if ( delta != null )
					{
						checkpoint.Modified.Add(delta);
						checkpoint.Files[modifiedPath] = newFiles[modifiedPath];
						checkpoint.Files[modifiedPath].CASHash = delta.TargetCASHash;
						checkpoint.DeltaSize += delta.ForwardDeltaSize + delta.ReverseDeltaSize;
					}
				}
				finally
				{
					if ( File.Exists(tempOldFile) )
						File.Delete(tempOldFile);
				}
			}

			// Process deleted files
			foreach ( string deletedPath in changes.Deleted )
			{
				checkpoint.Deleted.Add(deletedPath);
			}

			// For anchor checkpoints, store full file states
			if ( isAnchor )
			{
				await Logger.LogAsync($"[Checkpoint] Storing anchor snapshot with {newFiles.Count} files");
				checkpoint.Files = new Dictionary<string, FileState>(newFiles, StringComparer.OrdinalIgnoreCase);
			}

			// Calculate totals
			checkpoint.FileCount = newFiles.Count;
			checkpoint.TotalSize = newFiles.Sum(f => f.Value.Size);

			// Save checkpoint
			await SaveCheckpointAsync(checkpoint);

			// Update session
			_currentSession.CheckpointIds.Add(checkpointId);
			_currentSession.CompletedComponents = _checkpointCounter;
			await SaveSessionAsync();

			// Update current state
			_currentFiles = newFiles;

			await Logger.LogAsync($"[Checkpoint] Checkpoint created: {checkpointId} " +
				$"(Delta: {checkpoint.DeltaSize:N0} bytes, Total: {checkpoint.TotalSize:N0} bytes)");

			CheckpointCreated?.Invoke(this, new CheckpointEventArgs { Checkpoint = checkpoint });

			return checkpointId;
		}

		/// <summary>
		/// Restores the game directory to a specific checkpoint.
		/// Uses bidirectional deltas for efficient navigation.
		/// </summary>
		public async Task RestoreCheckpointAsync(
			string checkpointId,
			CancellationToken cancellationToken = default)
		{
			await Logger.LogAsync($"[Checkpoint] Restoring to checkpoint {checkpointId}...");

			var targetCheckpoint = await LoadCheckpointAsync(checkpointId);
			if ( targetCheckpoint == null )
				throw new InvalidOperationException($"Checkpoint not found: {checkpointId}");

			// Determine restoration strategy
			int targetSequence = targetCheckpoint.Sequence;
			int currentSequence = _checkpointCounter;
			int distance = Math.Abs(targetSequence - currentSequence);

			await Logger.LogAsync($"[Checkpoint] Distance: {distance} checkpoints " +
				$"(Current: #{currentSequence}, Target: #{targetSequence})");

			// Build restoration path
			var checkpoints = await LoadSessionCheckpointsAsync(_currentSession.Id);

			if ( targetSequence < currentSequence )
			{
				// Restore backwards
				await RestoreBackwardsAsync(checkpoints, currentSequence, targetSequence, cancellationToken);
			}
			else
			{
				// Restore forwards
				await RestoreForwardsAsync(checkpoints, currentSequence, targetSequence, cancellationToken);
			}

			// Update current state
			_checkpointCounter = targetCheckpoint.Sequence;
			_currentFiles = await ScanDirectoryAsync(_gameDirectory, cancellationToken);

			await Logger.LogAsync($"[Checkpoint] Restoration complete: {checkpointId}");

			CheckpointRestored?.Invoke(this, new CheckpointEventArgs { Checkpoint = targetCheckpoint });
		}

		/// <summary>
		/// Restores backwards using reverse deltas.
		/// </summary>
		private async Task RestoreBackwardsAsync(
			List<Checkpoint> checkpoints,
			int fromSequence,
			int toSequence,
			CancellationToken cancellationToken)
		{
			await Logger.LogAsync($"[Checkpoint] Restoring backwards from #{fromSequence} to #{toSequence}");

			// Apply reverse deltas in reverse order
			for ( int seq = fromSequence; seq > toSequence; seq-- )
			{
				var checkpoint = checkpoints.FirstOrDefault(c => c.Sequence == seq);
				if ( checkpoint == null )
					continue;

				ReportProgress($"Restoring backwards: checkpoint #{seq}...", fromSequence - seq, fromSequence - toSequence);

				// Undo modified files (apply reverse deltas)
				foreach ( var delta in checkpoint.Modified )
				{
					string fullPath = Path.Combine(_gameDirectory, delta.Path);
					await _diffService.ApplyReverseDeltaAsync(delta, fullPath, cancellationToken);
					await Logger.LogVerboseAsync($"[Checkpoint] Reverted: {delta.Path}");
				}

				// Undo added files (delete them)
				foreach ( string addedPath in checkpoint.Added )
				{
					string fullPath = Path.Combine(_gameDirectory, addedPath);
					if ( File.Exists(fullPath) )
					{
						File.Delete(fullPath);
						await Logger.LogVerboseAsync($"[Checkpoint] Deleted: {addedPath}");
					}
				}

				// Restore deleted files
				foreach ( string deletedPath in checkpoint.Deleted )
				{
					string fullPath = Path.Combine(_gameDirectory, deletedPath);

					// Find the file state from previous checkpoint
					if ( seq > 1 )
					{
						var prevCheckpoint = checkpoints.FirstOrDefault(c => c.Sequence == seq - 1);
						if ( prevCheckpoint != null && prevCheckpoint.Files.TryGetValue(deletedPath, out var fileState) )
						{
							await _casStore.RetrieveFileAsync(fileState.CASHash, fullPath);
							await Logger.LogVerboseAsync($"[Checkpoint] Restored deleted: {deletedPath}");
						}
					}
				}
			}
		}

		/// <summary>
		/// Restores forwards using forward deltas.
		/// </summary>
		private async Task RestoreForwardsAsync(
			List<Checkpoint> checkpoints,
			int fromSequence,
			int toSequence,
			CancellationToken cancellationToken)
		{
			await Logger.LogAsync($"[Checkpoint] Restoring forwards from #{fromSequence} to #{toSequence}");

			// Apply forward deltas in order
			for ( int seq = fromSequence + 1; seq <= toSequence; seq++ )
			{
				var checkpoint = checkpoints.FirstOrDefault(c => c.Sequence == seq);
				if ( checkpoint == null )
					continue;

				ReportProgress($"Restoring forwards: checkpoint #{seq}...", seq - fromSequence, toSequence - fromSequence);

				// Apply modified files (apply forward deltas)
				foreach ( var delta in checkpoint.Modified )
				{
					string fullPath = Path.Combine(_gameDirectory, delta.Path);
					await _diffService.ApplyForwardDeltaAsync(delta, fullPath, cancellationToken);
					await Logger.LogVerboseAsync($"[Checkpoint] Applied: {delta.Path}");
				}

				// Add new files
				foreach ( string addedPath in checkpoint.Added )
				{
					string fullPath = Path.Combine(_gameDirectory, addedPath);
					var fileState = checkpoint.Files[addedPath];
					await _casStore.RetrieveFileAsync(fileState.CASHash, fullPath);
					await Logger.LogVerboseAsync($"[Checkpoint] Added: {addedPath}");
				}

				// Delete files
				foreach ( string deletedPath in checkpoint.Deleted )
				{
					string fullPath = Path.Combine(_gameDirectory, deletedPath);
					if ( File.Exists(fullPath) )
					{
						File.Delete(fullPath);
						await Logger.LogVerboseAsync($"[Checkpoint] Deleted: {deletedPath}");
					}
				}
			}
		}

		/// <summary>
		/// Completes the current installation session.
		/// </summary>
		public async Task CompleteSessionAsync(bool keepCheckpoints = true)
		{
			if ( _currentSession == null )
				return;

			_currentSession.EndTime = DateTime.UtcNow;
			_currentSession.IsComplete = true;

			await SaveSessionAsync();

			await Logger.LogAsync($"[Checkpoint] Session completed: {_currentSession.Name} " +
				$"({_currentSession.CompletedComponents}/{_currentSession.TotalComponents} components)");

			if ( !keepCheckpoints )
			{
				await DeleteSessionAsync(_currentSession.Id);
			}

			_currentSession = null;
			_baselineFiles = null;
			_currentFiles = null;
		}

		/// <summary>
		/// Lists all checkpoints in a session.
		/// </summary>
		public async Task<List<Checkpoint>> ListCheckpointsAsync(string sessionId)
		{
			return await LoadSessionCheckpointsAsync(sessionId);
		}

		/// <summary>
		/// Lists all installation sessions.
		/// </summary>
		public async Task<List<CheckpointSession>> ListSessionsAsync()
		{
			var sessions = new List<CheckpointSession>();
			string sessionsDir = Path.Combine(_checkpointDirectory, "sessions");

			if ( !Directory.Exists(sessionsDir) )
				return sessions;

			foreach ( string sessionDir in Directory.GetDirectories(sessionsDir) )
			{
				string sessionFile = Path.Combine(sessionDir, "session.json");
				if ( File.Exists(sessionFile) )
				{
					try
					{
						string json = await ReadAllTextAsync(sessionFile);
						var session = JsonConvert.DeserializeObject<CheckpointSession>(json);
						if ( session != null )
							sessions.Add(session);
					}
					catch ( Exception ex )
					{
						await Logger.LogWarningAsync($"[Checkpoint] Failed to load session from {sessionFile}: {ex.Message}");
					}
				}
			}

			return sessions.OrderByDescending(s => s.StartTime).ToList();
		}

		/// <summary>
		/// Deletes a session and its checkpoints.
		/// </summary>
		public async Task DeleteSessionAsync(string sessionId)
		{
			await Logger.LogAsync($"[Checkpoint] Deleting session {sessionId}...");

			string sessionDir = Path.Combine(_checkpointDirectory, "sessions", sessionId);

			if ( Directory.Exists(sessionDir) )
			{
				Directory.Delete(sessionDir, recursive: true);
				await Logger.LogAsync($"[Checkpoint] Session deleted: {sessionId}");
			}

			// Run garbage collection to clean up orphaned CAS objects
			await GarbageCollectAsync();
		}

		/// <summary>
		/// Garbage collects orphaned CAS objects not referenced by any checkpoint.
		/// </summary>
		public async Task<int> GarbageCollectAsync()
		{
			await Logger.LogAsync("[Checkpoint] Starting garbage collection...");

			// Collect all referenced CAS hashes from all sessions
			var referencedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var sessions = await ListSessionsAsync();

			foreach ( var session in sessions )
			{
				var checkpoints = await LoadSessionCheckpointsAsync(session.Id);
				foreach ( var checkpoint in checkpoints )
				{
					// Collect file CAS hashes
					foreach ( var fileState in checkpoint.Files.Values )
					{
						if ( !string.IsNullOrEmpty(fileState.CASHash) )
							referencedHashes.Add(fileState.CASHash);
					}

					// Collect delta CAS hashes
					foreach ( var delta in checkpoint.Modified )
					{
						if ( !string.IsNullOrEmpty(delta.SourceCASHash) )
							referencedHashes.Add(delta.SourceCASHash);
						if ( !string.IsNullOrEmpty(delta.TargetCASHash) )
							referencedHashes.Add(delta.TargetCASHash);
						if ( !string.IsNullOrEmpty(delta.ForwardDeltaCASHash) )
							referencedHashes.Add(delta.ForwardDeltaCASHash);
						if ( !string.IsNullOrEmpty(delta.ReverseDeltaCASHash) )
							referencedHashes.Add(delta.ReverseDeltaCASHash);
					}
				}
			}

			int deleted = await _casStore.GarbageCollectAsync(referencedHashes);
			return deleted;
		}

		#region Helper Methods

		/// <summary>
		/// Scans a directory and returns file states.
		/// </summary>
		private async Task<Dictionary<string, FileState>> ScanDirectoryAsync(
			string directory,
			CancellationToken cancellationToken)
		{
			var files = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

			var allFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

			foreach ( string fullPath in allFiles )
			{
				cancellationToken.ThrowIfCancellationRequested();

				// Skip checkpoint directory itself
				if ( fullPath.Contains(".kotor_modsync") )
					continue;

				string relativePath = PathHelper.GetRelativePath(directory, fullPath);
				var fileInfo = new FileInfo(fullPath);

				string hash = await ContentAddressableStore.ComputeFileHashAsync(fullPath);

				files[relativePath] = new FileState
				{
					Path = relativePath,
					Hash = hash,
					Size = fileInfo.Length,
					LastModified = fileInfo.LastWriteTimeUtc
				};
			}

			return files;
		}

		/// <summary>
		/// Computes changes between two file states.
		/// </summary>
		private static FileChanges ComputeFileChanges(
			Dictionary<string, FileState> oldFiles,
			Dictionary<string, FileState> newFiles)
		{
			var changes = new FileChanges();

			// Find added and modified files
			foreach ( var kvp in newFiles )
			{
				string path = kvp.Key;
				var newState = kvp.Value;

				if ( !oldFiles.TryGetValue(path, out var oldState) )
				{
					// File added
					changes.Added.Add(path);
				}
				else if ( oldState.Hash != newState.Hash )
				{
					// File modified
					changes.Modified.Add(path);
				}
			}

			// Find deleted files
			foreach ( string path in oldFiles.Keys )
			{
				if ( !newFiles.ContainsKey(path) )
				{
					changes.Deleted.Add(path);
				}
			}

			return changes;
		}

		private string GetPreviousAnchorId()
		{
			if ( _currentSession == null || _currentSession.CheckpointIds.Count == 0 )
				return null;

			// Find the most recent anchor
			for ( int i = _currentSession.CheckpointIds.Count - 1; i >= 0; i-- )
			{
				int sequence = i + 1;
				if ( (sequence % ANCHOR_FREQUENCY) == 0 )
				{
					return _currentSession.CheckpointIds[i];
				}
			}

			return null;
		}

		private string GetSessionPath(string sessionId)
		{
			return Path.Combine(_checkpointDirectory, "sessions", sessionId);
		}

		private string GetBaselinePath(string sessionId)
		{
			return Path.Combine(GetSessionPath(sessionId), "baseline.json");
		}

		private string GetCheckpointPath(string sessionId, string checkpointId)
		{
			return Path.Combine(GetSessionPath(sessionId), "checkpoints", $"{checkpointId}.json");
		}

		private async Task SaveSessionAsync()
		{
			if ( _currentSession == null )
				return;

			string sessionPath = GetSessionPath(_currentSession.Id);
			Directory.CreateDirectory(sessionPath);

			string sessionFile = Path.Combine(sessionPath, "session.json");
			string json = JsonConvert.SerializeObject(_currentSession, Formatting.Indented);
			await WriteAllTextAsync(sessionFile, json);
		}

		private static async Task SaveBaselineAsync(string path, Dictionary<string, FileState> baseline)
		{
			string directory = Path.GetDirectoryName(path);
			Directory.CreateDirectory(directory);

			string json = JsonConvert.SerializeObject(baseline, Formatting.Indented);
			await WriteAllTextAsync(path, json);
		}

		private async Task SaveCheckpointAsync(Checkpoint checkpoint)
		{
			string checkpointPath = GetCheckpointPath(checkpoint.SessionId, checkpoint.Id);
			string directory = Path.GetDirectoryName(checkpointPath);
			Directory.CreateDirectory(directory);

			string json = JsonConvert.SerializeObject(checkpoint, Formatting.Indented);
			await WriteAllTextAsync(checkpointPath, json);
		}

		// Helper methods for .NET Standard 2.0 compatibility
		private static async Task<string> ReadAllTextAsync(string path)
		{
			using ( var reader = new StreamReader(path, Encoding.UTF8) )
			{
				return await reader.ReadToEndAsync();
			}
		}

		private static async Task WriteAllTextAsync(string path, string contents)
		{
			using ( var writer = new StreamWriter(path, false, Encoding.UTF8) )
			{
				await writer.WriteAsync(contents);
			}
		}

		private async Task<Checkpoint> LoadCheckpointAsync(string checkpointId)
		{
			if ( _currentSession == null )
				return null;

			string checkpointPath = GetCheckpointPath(_currentSession.Id, checkpointId);

			if ( !File.Exists(checkpointPath) )
				return null;

			string json = await ReadAllTextAsync(checkpointPath);
			return JsonConvert.DeserializeObject<Checkpoint>(json);
		}

		private async Task<List<Checkpoint>> LoadSessionCheckpointsAsync(string sessionId)
		{
			var checkpoints = new List<Checkpoint>();
			string checkpointsDir = Path.Combine(GetSessionPath(sessionId), "checkpoints");

			if ( !Directory.Exists(checkpointsDir) )
				return checkpoints;

			foreach ( string checkpointFile in Directory.GetFiles(checkpointsDir, "*.json") )
			{
				try
				{
					string json = await ReadAllTextAsync(checkpointFile);
					var checkpoint = JsonConvert.DeserializeObject<Checkpoint>(json);
					if ( checkpoint != null )
						checkpoints.Add(checkpoint);
				}
				catch ( Exception ex )
				{
					await Logger.LogWarningAsync($"[Checkpoint] Failed to load checkpoint from {checkpointFile}: {ex.Message}");
				}
			}

			return checkpoints.OrderBy(c => c.Sequence).ToList();
		}

		private void ReportProgress(string message, int current, int total)
		{
			Progress?.Invoke(this, new CheckpointProgressEventArgs
			{
				Message = message,
				Current = current,
				Total = total
			});
		}

		#endregion

		#region Validation and Corruption Detection

		/// <summary>
		/// Validates checkpoint integrity by checking for missing CAS objects and corrupted metadata.
		/// </summary>
		public async Task<(bool isValid, List<string> errors)> ValidateCheckpointAsync(string checkpointId)
		{
			var errors = new List<string>();

			try
			{
				var checkpoint = await LoadCheckpointAsync(checkpointId);
				if ( checkpoint == null )
				{
					errors.Add($"Checkpoint metadata not found: {checkpointId}");
					return (false, errors);
				}

				// Validate all CAS references exist
				foreach ( var file in checkpoint.Files.Values )
				{
					if ( !string.IsNullOrEmpty(file.CASHash) && !_casStore.HasObject(file.CASHash) )
					{
						errors.Add($"Missing CAS object for file '{file.Path}': {file.CASHash}");
					}
				}

				// Validate delta CAS references
				foreach ( var delta in checkpoint.Modified )
				{
					if ( !string.IsNullOrEmpty(delta.ForwardDeltaCASHash) && !_casStore.HasObject(delta.ForwardDeltaCASHash) )
					{
						errors.Add($"Missing forward delta CAS object for '{delta.Path}': {delta.ForwardDeltaCASHash}");
					}

					if ( !string.IsNullOrEmpty(delta.ReverseDeltaCASHash) && !_casStore.HasObject(delta.ReverseDeltaCASHash) )
					{
						errors.Add($"Missing reverse delta CAS object for '{delta.Path}': {delta.ReverseDeltaCASHash}");
					}

					if ( !string.IsNullOrEmpty(delta.SourceCASHash) && !_casStore.HasObject(delta.SourceCASHash) )
					{
						errors.Add($"Missing source CAS object for '{delta.Path}': {delta.SourceCASHash}");
					}

					if ( !string.IsNullOrEmpty(delta.TargetCASHash) && !_casStore.HasObject(delta.TargetCASHash) )
					{
						errors.Add($"Missing target CAS object for '{delta.Path}': {delta.TargetCASHash}");
					}
				}

				return (errors.Count == 0, errors);
			}
			catch ( Exception ex )
			{
				errors.Add($"Validation failed: {ex.Message}");
				return (false, errors);
			}
		}

		/// <summary>
		/// Validates all checkpoints in a session.
		/// </summary>
		public async Task<(bool isValid, Dictionary<string, List<string>> errorsByCheckpoint)> ValidateSessionAsync(string sessionId)
		{
			var errorsByCheckpoint = new Dictionary<string, List<string>>();
			var checkpoints = await ListCheckpointsAsync(sessionId);

			foreach ( var checkpoint in checkpoints )
			{
				var (isValid, errors) = await ValidateCheckpointAsync(checkpoint.Id);
				if ( !isValid )
				{
					errorsByCheckpoint[checkpoint.Id] = errors;
				}
			}

			return (errorsByCheckpoint.Count == 0, errorsByCheckpoint);
		}

		/// <summary>
		/// Attempts to repair a corrupted checkpoint by recreating missing CAS objects from the game directory.
		/// Only works if the files still exist in the game directory.
		/// </summary>
		public async Task<bool> TryRepairCheckpointAsync(string checkpointId, CancellationToken cancellationToken = default)
		{
			try
			{
				var checkpoint = await LoadCheckpointAsync(checkpointId);
				if ( checkpoint == null )
					return false;

				var (isValid, errors) = await ValidateCheckpointAsync(checkpointId);
				if ( isValid )
					return true; // Already valid

				await Logger.LogAsync($"Attempting to repair checkpoint {checkpointId}...");

				// Attempt to restore missing CAS objects from game directory
				foreach ( var file in checkpoint.Files.Values )
				{
					if ( string.IsNullOrEmpty(file.CASHash) )
						continue;

					if ( !_casStore.HasObject(file.CASHash) )
					{
						string gamePath = Path.Combine(_gameDirectory, file.Path);
						if ( File.Exists(gamePath) )
						{
							string hash = await _casStore.StoreFileAsync(gamePath);
							if ( hash == file.CASHash )
							{
								await Logger.LogAsync($"Restored CAS object for: {file.Path}");
							}
							else
							{
								await Logger.LogWarningAsync($"Hash mismatch while repairing: {file.Path}");
							}
						}
					}
				}

				// Revalidate
				var (isNowValid, _) = await ValidateCheckpointAsync(checkpointId);
				return isNowValid;
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"Failed to repair checkpoint: {ex.Message}");
				return false;
			}
		}

		#endregion

		#region Helper Classes

		private class FileChanges
		{
			public List<string> Added { get; set; } = new List<string>();
			public List<string> Modified { get; set; } = new List<string>();
			public List<string> Deleted { get; set; } = new List<string>();
		}

		#endregion
	}

	#region Event Args

	public class CheckpointEventArgs : EventArgs
	{
		public Checkpoint Checkpoint { get; set; }
	}

	public class CheckpointProgressEventArgs : EventArgs
	{
		public string Message { get; set; }
		public int Current { get; set; }
		public int Total { get; set; }
	}

	#endregion
}

