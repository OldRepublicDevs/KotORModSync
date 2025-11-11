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
using KOTORModSync.Core.Services.Checkpoints;

using Newtonsoft.Json;

namespace KOTORModSync.Core.Services.ImmutableCheckpoint
{
    public class CheckpointService
    {
        private const int ANCHOR_FREQUENCY = 10;
        private readonly string _checkpointDirectory;
        private readonly string _gameDirectory;
        private readonly ContentAddressableStore _casStore;
        private readonly BinaryDiffService _diffService;

        private CheckpointSession _currentSession;
        private Dictionary<string, FileState> _baselineFiles;
        private Dictionary<string, FileState> _currentFiles;
        private int _checkpointCounter;

        public event EventHandler<CheckpointEventArgs> CheckpointCreated;
        public event EventHandler<CheckpointEventArgs> CheckpointRestored;
        public event EventHandler<CheckpointProgressEventArgs> Progress;

        public CheckpointService(string gameDirectory)
        {
            if (string.IsNullOrWhiteSpace(gameDirectory))
            {
                throw new ArgumentNullException(nameof(gameDirectory));
            }

            _gameDirectory = gameDirectory;
            _checkpointDirectory = CheckpointPaths.GetCheckpointsRoot(gameDirectory);

            Directory.CreateDirectory(_checkpointDirectory);

            _casStore = new ContentAddressableStore(_checkpointDirectory);
            _diffService = new BinaryDiffService(_casStore);
        }

        public async Task<string> StartInstallationSessionAsync(CancellationToken cancellationToken = default)

        {
            await Logger.LogAsync("[Checkpoint] Starting installation session...").ConfigureAwait(false);

            string sessionId =


Guid.NewGuid().ToString();
            string sessionName =

$"Installation_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";

            _currentSession = new CheckpointSession
            {
                Id = sessionId,
                Name = sessionName,
                GamePath = _gameDirectory,
                StartTime = DateTime.UtcNow,
                IsComplete = false,
            };

            _checkpointCounter = 0;

            ReportProgress("Scanning baseline directory...", 0, 1);
            _baselineFiles = await ScanDirectoryAsync(_gameDirectory, cancellationToken).ConfigureAwait(false);
            _currentFiles = new Dictionary<string, FileState>(_baselineFiles, StringComparer.OrdinalIgnoreCase);

            await Logger.LogAsync($"[Checkpoint] Baseline captured: {_baselineFiles.Count} files, " +
                $"{_baselineFiles.Sum(f => f.Value.Size):N0} bytes").ConfigureAwait(false);

            await SaveSessionAsync().ConfigureAwait(false);

            string baselinePath = GetBaselinePath(sessionId);
            await SaveBaselineAsync(baselinePath, _baselineFiles).ConfigureAwait(false);

            await Logger.LogAsync($"[Checkpoint] Session started: {sessionName} (ID: {sessionId})").ConfigureAwait(false);

            return sessionId;
        }

        public async Task<string> CreateCheckpointAsync(
            string componentName,
            string componentGuid,
            CancellationToken cancellationToken = default)
        {
            if (_currentSession is null)
            {
                throw new InvalidOperationException("No active session. Call StartInstallationSessionAsync first.");
            }

            _checkpointCounter++;
            bool isAnchor = (_checkpointCounter % ANCHOR_FREQUENCY) == 0;

            await Logger.LogAsync($"[Checkpoint] Creating checkpoint #{_checkpointCounter} for '{componentName}' " +

                $"(Anchor: {isAnchor})...")
.ConfigureAwait(false);

            ReportProgress($"Creating checkpoint for {componentName}...", _checkpointCounter - 1, _currentSession.TotalComponents);

            Dictionary<string, FileState> newFiles = await ScanDirectoryAsync(_gameDirectory, cancellationToken).ConfigureAwait(false);

            FileChanges changes = CheckpointService.ComputeFileChanges(_currentFiles, newFiles);

            await Logger.LogAsync($"[Checkpoint] Detected changes: +{changes.Added.Count} ~{changes.Modified.Count} -{changes.Deleted.Count}")
.ConfigureAwait(false);

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
                PreviousAnchorId = previousAnchorId,
            };

            foreach (string addedPath in changes.Added)
            {
                string fullPath = Path.Combine(_gameDirectory, addedPath);
                string casHash = await _casStore.StoreFileAsync(fullPath).ConfigureAwait(false);

                FileState fileState = newFiles[addedPath];
                fileState.CASHash = casHash;

                checkpoint.Added.Add(addedPath);
                checkpoint.Files[addedPath] = fileState;
            }

            foreach (string modifiedPath in changes.Modified)
            {
                string fullPath = Path.Combine(_gameDirectory, modifiedPath);
                string oldFullPath = Path.Combine(_gameDirectory, modifiedPath);

                string tempOldFile = Path.GetTempFileName();
                try
                {
                    string oldCASHash = _currentFiles[modifiedPath].CASHash;
                    await _casStore.RetrieveFileAsync(oldCASHash, tempOldFile).ConfigureAwait(false);

                    FileDelta delta = await _diffService.CreateBidirectionalDeltaAsync(
                        tempOldFile,
                        fullPath,
                        modifiedPath,
                        cancellationToken).ConfigureAwait(false);

                    if (delta != null)
                    {
                        checkpoint.Modified.Add(delta);
                        checkpoint.Files[modifiedPath] = newFiles[modifiedPath];
                        checkpoint.Files[modifiedPath].CASHash = delta.TargetCASHash;
                        checkpoint.DeltaSize += delta.ForwardDeltaSize + delta.ReverseDeltaSize;
                    }
                }
                finally
                {
                    if (File.Exists(tempOldFile))
                    {
                        File.Delete(tempOldFile);
                    }
                }
            }

            foreach (string deletedPath in changes.Deleted)
            {
                checkpoint.Deleted.Add(deletedPath);
            }

            if (isAnchor)

            {
                await Logger.LogAsync($"[Checkpoint] Storing anchor snapshot with {newFiles.Count} files").ConfigureAwait(false);
                checkpoint.Files = new Dictionary<string, FileState>(newFiles, StringComparer.OrdinalIgnoreCase);
            }

            checkpoint.FileCount = newFiles.Count;
            checkpoint.TotalSize = newFiles.Sum(f => f.Value.Size);

            await SaveCheckpointAsync(checkpoint)
.ConfigureAwait(false);

            _currentSession.CheckpointIds.Add(checkpointId);
            _currentSession.CompletedComponents = _checkpointCounter;
            await SaveSessionAsync().ConfigureAwait(false);

            _currentFiles = newFiles;

            await Logger.LogAsync($"[Checkpoint] Checkpoint created: {checkpointId} " +
                $"(Delta: {checkpoint.DeltaSize:N0} bytes, Total: {checkpoint.TotalSize:N0} bytes)").ConfigureAwait(false);

            CheckpointCreated?.Invoke(this, new CheckpointEventArgs { Checkpoint = checkpoint });

            return checkpointId;
        }

        public async Task RestoreCheckpointAsync(
            string checkpointId,
            CancellationToken cancellationToken = default)

        {
            await Logger.LogAsync($"[Checkpoint] Restoring to checkpoint {checkpointId}...")
.ConfigureAwait(false);

            Checkpoint targetCheckpoint = await LoadCheckpointAsync(checkpointId).ConfigureAwait(false);
            if (targetCheckpoint is null)
            {
                throw new InvalidOperationException($"Checkpoint not found: {checkpointId}");
            }

            int targetSequence = targetCheckpoint.Sequence;
            int currentSequence = _checkpointCounter;
            int distance = Math.Abs(targetSequence - currentSequence);

            await Logger.LogAsync($"[Checkpoint] Distance: {distance} checkpoints " +
                $"(Current: #{currentSequence}, Target: #{targetSequence})").ConfigureAwait(false);

            List<Checkpoint> checkpoints = await LoadSessionCheckpointsAsync(_currentSession.Id).ConfigureAwait(false);

            if (targetSequence < currentSequence)

            {
                await RestoreBackwardsAsync(checkpoints, currentSequence, targetSequence, cancellationToken)
.ConfigureAwait(false);
            }
            else
            {
                await RestoreForwardsAsync(checkpoints, currentSequence, targetSequence, cancellationToken)
.ConfigureAwait(false);
            }

            _checkpointCounter = targetCheckpoint.Sequence;
            _currentFiles = await ScanDirectoryAsync(_gameDirectory, cancellationToken).ConfigureAwait(false);

            await Logger.LogAsync($"[Checkpoint] Restoration complete: {checkpointId}").ConfigureAwait(false);

            CheckpointRestored?.Invoke(this, new CheckpointEventArgs { Checkpoint = targetCheckpoint });
        }

        private async Task RestoreBackwardsAsync(
            List<Checkpoint> checkpoints,
            int fromSequence,
            int toSequence,
            CancellationToken cancellationToken)
        {
            await Logger.LogAsync($"[Checkpoint] Restoring backwards from #{fromSequence} to #{toSequence}").ConfigureAwait(false);

            for (int seq = fromSequence; seq > toSequence; seq--)
            {
                Checkpoint checkpoint = checkpoints.Find(c => c.Sequence == seq);
                if (checkpoint is null)
                {
                    continue;
                }

                ReportProgress($"Restoring backwards: checkpoint #{seq}...", fromSequence - seq, fromSequence - toSequence);

                foreach (FileDelta delta in checkpoint.Modified)
                {
                    string fullPath = Path.Combine(_gameDirectory, delta.Path);

                    await _diffService.ApplyReverseDeltaAsync(delta, fullPath, cancellationToken).ConfigureAwait(false);
                    await Logger.LogVerboseAsync($"[Checkpoint] Reverted: {delta.Path}").ConfigureAwait(false);
                }

                foreach (string addedPath in checkpoint.Added)
                {
                    string fullPath = Path.Combine(_gameDirectory, addedPath);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        await Logger.LogVerboseAsync($"[Checkpoint] Deleted: {addedPath}").ConfigureAwait(false);
                    }
                }

                foreach (string deletedPath in checkpoint.Deleted)
                {
                    string fullPath = Path.Combine(_gameDirectory, deletedPath);

                    if (seq > 1)
                    {
                        Checkpoint prevCheckpoint = checkpoints.Find(c => c.Sequence == seq - 1);
                        if (prevCheckpoint != null && prevCheckpoint.Files.TryGetValue(deletedPath, out FileState fileState))

                        {
                            await _casStore.RetrieveFileAsync(fileState.CASHash, fullPath).ConfigureAwait(false);
                            await Logger.LogVerboseAsync($"[Checkpoint] Restored deleted: {deletedPath}").ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        private async Task RestoreForwardsAsync(
            List<Checkpoint> checkpoints,
            int fromSequence,
            int toSequence,
            CancellationToken cancellationToken)
        {
            await Logger.LogAsync($"[Checkpoint] Restoring forwards from #{fromSequence} to #{toSequence}").ConfigureAwait(false);

            for (int seq = fromSequence + 1; seq <= toSequence; seq++)
            {
                Checkpoint checkpoint = checkpoints.Find(c => c.Sequence == seq);
                if (checkpoint is null)
                {
                    continue;
                }

                ReportProgress($"Restoring forwards: checkpoint #{seq}...", seq - fromSequence, toSequence - fromSequence);

                foreach (FileDelta delta in checkpoint.Modified)
                {
                    string fullPath = Path.Combine(_gameDirectory, delta.Path);

                    await _diffService.ApplyForwardDeltaAsync(delta, fullPath, cancellationToken).ConfigureAwait(false);
                    await Logger.LogVerboseAsync($"[Checkpoint] Applied: {delta.Path}").ConfigureAwait(false);
                }

                foreach (string addedPath in checkpoint.Added)
                {
                    string fullPath = Path.Combine(_gameDirectory, addedPath);
                    FileState fileState = checkpoint.Files[addedPath];

                    await _casStore.RetrieveFileAsync(fileState.CASHash, fullPath).ConfigureAwait(false);
                    await Logger.LogVerboseAsync($"[Checkpoint] Added: {addedPath}").ConfigureAwait(false);
                }

                foreach (string deletedPath in checkpoint.Deleted)
                {
                    string fullPath = Path.Combine(_gameDirectory, deletedPath);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        await Logger.LogVerboseAsync($"[Checkpoint] Deleted: {deletedPath}").ConfigureAwait(false);
                    }
                }
            }
        }

        public async Task CompleteSessionAsync(bool keepCheckpoints = true)
        {
            if (_currentSession is null)
            {
                return;
            }

            _currentSession.EndTime = DateTime.UtcNow;
            _currentSession.IsComplete = true;

            await SaveSessionAsync()
.ConfigureAwait(false);

            await Logger.LogAsync($"[Checkpoint] Session completed: {_currentSession.Name} " +
                $"({_currentSession.CompletedComponents}/{_currentSession.TotalComponents} components)").ConfigureAwait(false);

            if (!keepCheckpoints)

            {
                await DeleteSessionAsync(_currentSession.Id).ConfigureAwait(false);
            }

            _currentSession = null;
            _baselineFiles = null;
            _currentFiles = null;
        }

        public async Task<List<Checkpoint>> ListCheckpointsAsync(string sessionId)
        {
            return await LoadSessionCheckpointsAsync(sessionId).ConfigureAwait(false);
        }

        public async Task<List<CheckpointSession>> ListSessionsAsync()
        {
            var sessions = new List<CheckpointSession>();
            string sessionsDir = Path.Combine(_checkpointDirectory, "sessions");

            if (!Directory.Exists(sessionsDir))
            {
                return sessions;
            }

            foreach (string sessionDir in Directory.GetDirectories(sessionsDir))
            {
                string sessionFile = Path.Combine(sessionDir, "session.json");
                if (File.Exists(sessionFile))

                {
                    try
                    {
                        string json = await ReadAllTextAsync(sessionFile).ConfigureAwait(false);
                        CheckpointSession session = JsonConvert.DeserializeObject<CheckpointSession>(json);
                        if (session != null)
                        {
                            sessions.Add(session);
                        }
                    }
                    catch (Exception ex)

                    {
                        await Logger.LogWarningAsync($"[Checkpoint] Failed to load session from {sessionFile}: {ex.Message}").ConfigureAwait(false);
                    }
                }
            }

            return sessions.OrderByDescending(s => s.StartTime).ToList();
        }

        public async Task DeleteSessionAsync(string sessionId)

        {
            await Logger.LogAsync($"[Checkpoint] Deleting session {sessionId}...").ConfigureAwait(false);

            string sessionDir = Path.Combine(_checkpointDirectory, "sessions", sessionId);

            if (Directory.Exists(sessionDir))
            {
                Directory.Delete(sessionDir, recursive: true);

                await Logger.LogAsync($"[Checkpoint] Session deleted: {sessionId}").ConfigureAwait(false);
            }

            await GarbageCollectAsync().ConfigureAwait(false);
        }

        public async Task<int> GarbageCollectAsync()

        {
            await Logger.LogAsync("[Checkpoint] Starting garbage collection...")
.ConfigureAwait(false);

            var referencedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<CheckpointSession> sessions = await ListSessionsAsync().ConfigureAwait(false);

            foreach (CheckpointSession session in sessions)

            {
                List<Checkpoint> checkpoints = await LoadSessionCheckpointsAsync(session.Id).ConfigureAwait(false);
                foreach (Checkpoint checkpoint in checkpoints)
                {
                    foreach (FileState fileState in checkpoint.Files.Values)
                    {
                        if (!string.IsNullOrEmpty(fileState.CASHash))
                        {
                            referencedHashes.Add(fileState.CASHash);
                        }
                    }

                    foreach (FileDelta delta in checkpoint.Modified)
                    {
                        if (!string.IsNullOrEmpty(delta.SourceCASHash))
                        {
                            referencedHashes.Add(delta.SourceCASHash);
                        }

                        if (!string.IsNullOrEmpty(delta.TargetCASHash))
                        {
                            referencedHashes.Add(delta.TargetCASHash);
                        }

                        if (!string.IsNullOrEmpty(delta.ForwardDeltaCASHash))
                        {
                            referencedHashes.Add(delta.ForwardDeltaCASHash);
                        }

                        if (!string.IsNullOrEmpty(delta.ReverseDeltaCASHash))
                        {
                            referencedHashes.Add(delta.ReverseDeltaCASHash);
                        }
                    }
                }
            }

            int deleted = await _casStore.GarbageCollectAsync(referencedHashes).ConfigureAwait(false);
            return deleted;
        }

        #region Helper Methods

        private static async Task<Dictionary<string, FileState>> ScanDirectoryAsync(
            string directory,
            CancellationToken cancellationToken)
        {
            var files = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);

            string[] allFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

            foreach (string fullPath in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (fullPath.Contains(CheckpointPaths.CheckpointFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePath = PathHelper.GetRelativePath(directory, fullPath);
                var fileInfo = new FileInfo(fullPath);

                string hash = await ContentAddressableStore.ComputeFileHashAsync(fullPath).ConfigureAwait(false);

                files[relativePath] = new FileState
                {
                    Path = relativePath,
                    Hash = hash,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTimeUtc,
                };
            }

            return files;
        }

        private static FileChanges ComputeFileChanges(
            Dictionary<string, FileState> oldFiles,
            Dictionary<string, FileState> newFiles)
        {
            var changes = new FileChanges();

            foreach (KeyValuePair<string, FileState> kvp in newFiles)
            {
                string path = kvp.Key;
                FileState newState = kvp.Value;

                if (!oldFiles.TryGetValue(path, out FileState oldState))
                {
                    changes.Added.Add(path);
                }
                else if (!string.Equals(oldState.Hash, newState.Hash, StringComparison.Ordinal))
                {
                    changes.Modified.Add(path);
                }
            }

            foreach (string path in oldFiles.Keys)
            {
                if (!newFiles.ContainsKey(path))
                {
                    changes.Deleted.Add(path);
                }
            }

            return changes;
        }

        private string GetPreviousAnchorId()
        {
            if (_currentSession is null || _currentSession.CheckpointIds.Count == 0)
            {
                return null;
            }

            for (int i = _currentSession.CheckpointIds.Count - 1; i >= 0; i--)
            {
                int sequence = i + 1;
                if ((sequence % ANCHOR_FREQUENCY) == 0)
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
            if (_currentSession is null)
            {
                return;
            }

            string sessionPath = GetSessionPath(_currentSession.Id);
            Directory.CreateDirectory(sessionPath);

            string sessionFile = Path.Combine(sessionPath, "session.json");
            string json = JsonConvert.SerializeObject(_currentSession, Formatting.Indented);
            await WriteAllTextAsync(sessionFile, json).ConfigureAwait(false);
        }

        private static async Task SaveBaselineAsync(string path, Dictionary<string, FileState> baseline)
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(baseline, Formatting.Indented);
            await WriteAllTextAsync(path, json).ConfigureAwait(false);
        }

        private async Task SaveCheckpointAsync(Checkpoint checkpoint)
        {
            string checkpointPath = GetCheckpointPath(checkpoint.SessionId, checkpoint.Id);
            string directory = Path.GetDirectoryName(checkpointPath);
            Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(checkpoint, Formatting.Indented);
            await WriteAllTextAsync(checkpointPath, json).ConfigureAwait(false);
        }

        private static async Task<string> ReadAllTextAsync(string path)
        {
            using (var reader = new StreamReader(path, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        private static async Task WriteAllTextAsync(string path, string contents)
        {
            using (var writer = new StreamWriter(path, append: false, Encoding.UTF8))
            {
                await writer.WriteAsync(contents).ConfigureAwait(false);
            }
        }

        private async Task<Checkpoint> LoadCheckpointAsync(string checkpointId)
        {
            if (_currentSession is null)
            {
                return null;
            }

            string checkpointPath = GetCheckpointPath(_currentSession.Id, checkpointId);

            if (!File.Exists(checkpointPath))
            {
                return null;
            }

            string json = await ReadAllTextAsync(checkpointPath).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<Checkpoint>(json);
        }

        private async Task<List<Checkpoint>> LoadSessionCheckpointsAsync(string sessionId)
        {
            var checkpoints = new List<Checkpoint>();
            string checkpointsDir = Path.Combine(GetSessionPath(sessionId), "checkpoints");

            if (!Directory.Exists(checkpointsDir))
            {
                return checkpoints;
            }

            foreach (string checkpointFile in Directory.GetFiles(checkpointsDir, "*.json"))

            {
                try
                {
                    string json = await ReadAllTextAsync(checkpointFile).ConfigureAwait(false);
                    Checkpoint checkpoint = JsonConvert.DeserializeObject<Checkpoint>(json);
                    if (checkpoint != null)
                    {
                        checkpoints.Add(checkpoint);
                    }
                }
                catch (Exception ex)

                {
                    await Logger.LogWarningAsync($"[Checkpoint] Failed to load checkpoint from {checkpointFile}: {ex.Message}").ConfigureAwait(false);
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
                Total = total,
            });
        }

        #endregion

        #region Validation and Corruption Detection

        public async Task<(bool isValid, List<string> errors)> ValidateCheckpointAsync(string checkpointId)
        {
            var errors = new List<string>();

            try
            {
                Checkpoint checkpoint = await LoadCheckpointAsync(checkpointId).ConfigureAwait(false);
                if (checkpoint is null)
                {
                    errors.Add($"Checkpoint metadata not found: {checkpointId}");
                    return (false, errors);
                }

                foreach (FileState file in checkpoint.Files.Values)
                {
                    if (!string.IsNullOrEmpty(file.CASHash) && !_casStore.HasObject(file.CASHash))
                    {
                        errors.Add($"Missing CAS object for file '{file.Path}': {file.CASHash}");
                    }
                }

                foreach (FileDelta delta in checkpoint.Modified)
                {
                    if (!string.IsNullOrEmpty(delta.ForwardDeltaCASHash) && !_casStore.HasObject(delta.ForwardDeltaCASHash))
                    {
                        errors.Add($"Missing forward delta CAS object for '{delta.Path}': {delta.ForwardDeltaCASHash}");
                    }

                    if (!string.IsNullOrEmpty(delta.ReverseDeltaCASHash) && !_casStore.HasObject(delta.ReverseDeltaCASHash))
                    {
                        errors.Add($"Missing reverse delta CAS object for '{delta.Path}': {delta.ReverseDeltaCASHash}");
                    }

                    if (!string.IsNullOrEmpty(delta.SourceCASHash) && !_casStore.HasObject(delta.SourceCASHash))
                    {
                        errors.Add($"Missing source CAS object for '{delta.Path}': {delta.SourceCASHash}");
                    }

                    if (!string.IsNullOrEmpty(delta.TargetCASHash) && !_casStore.HasObject(delta.TargetCASHash))
                    {
                        errors.Add($"Missing target CAS object for '{delta.Path}': {delta.TargetCASHash}");
                    }
                }

                return (errors.Count == 0, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"Validation failed: {ex.Message}");
                return (false, errors);
            }
        }

        public async Task<(bool isValid, Dictionary<string, List<string>> errorsByCheckpoint)> ValidateSessionAsync(string sessionId)

        {
            var errorsByCheckpoint = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            List<Checkpoint> checkpoints = await ListCheckpointsAsync(sessionId).ConfigureAwait(false);

            foreach (Checkpoint checkpoint in checkpoints)

            {
                (bool isValid, List<string> errors) = await ValidateCheckpointAsync(checkpoint.Id).ConfigureAwait(false);
                if (!isValid)
                {
                    errorsByCheckpoint[checkpoint.Id] = errors;
                }
            }

            return (errorsByCheckpoint.Count == 0, errorsByCheckpoint);
        }

        public async Task<bool> TryRepairCheckpointAsync(string checkpointId, CancellationToken cancellationToken = default)

        {
            try
            {
                Checkpoint checkpoint = await LoadCheckpointAsync(checkpointId)
.ConfigureAwait(false);
                if (checkpoint is null)
                {
                    return false;
                }

                (bool isValid, List<string> errors) = await ValidateCheckpointAsync(checkpointId).ConfigureAwait(false);
                if (isValid)
                {
                    return true;
                }

                await Logger.LogAsync($"Attempting to repair checkpoint {checkpointId}...").ConfigureAwait(false);

                foreach (FileState file in checkpoint.Files.Values)
                {
                    if (string.IsNullOrEmpty(file.CASHash))
                    {
                        continue;
                    }

                    if (!_casStore.HasObject(file.CASHash))
                    {
                        string gamePath = Path.Combine(_gameDirectory, file.Path);
                        if (File.Exists(gamePath))

                        {
                            string hash = await _casStore.StoreFileAsync(gamePath).ConfigureAwait(false);
                            if (string.Equals(hash, file.CASHash, StringComparison.Ordinal))

                            {
                                await Logger.LogAsync($"Restored CAS object for: {file.Path}")
.ConfigureAwait(false);
                            }
                            else
                            {
                                await Logger.LogWarningAsync($"Hash mismatch while repairing: {file.Path}")
.ConfigureAwait(false);
                            }
                        }
                    }
                }

                (bool isNowValid, List<string> _) = await ValidateCheckpointAsync(checkpointId).ConfigureAwait(false);
                return isNowValid;
            }
            catch (Exception ex)

            {
                await Logger.LogErrorAsync($"Failed to repair checkpoint: {ex.Message}").ConfigureAwait(false);
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
    public class CheckpointEventArgs : EventArgs
    {
        public Checkpoint Checkpoint { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
    public class CheckpointProgressEventArgs : EventArgs
    {
        public string Message { get; set; }
        public int Current { get; set; }
        public int Total { get; set; }
    }

    #endregion
}
