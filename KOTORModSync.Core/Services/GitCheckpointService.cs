// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using KOTORModSync.Core.Services.Checkpoints;
using KOTORModSync.Core.Utility;
using LibGit2Sharp;

namespace KOTORModSync.Core.Services
{
    /// <summary>
    /// Git-based checkpoint system for tracking mod installations and enabling rollback.
    /// Each mod installation creates a git commit, allowing full version control of the game directory.
    /// </summary>
    public sealed class GitCheckpointService : IDisposable
    {
        private const string InitialCommitMessage = "KOTORModSync: Initial game state";

        private readonly string _gameDirectory;
        private readonly string _gitDirectory;
        private Repository _repository;
        private bool _isInitialized;

        public event EventHandler<CheckpointCreatedEventArgs> CheckpointCreated;
        public event EventHandler<CheckpointRestoredEventArgs> CheckpointRestored;
        public event EventHandler<CheckpointProgressEventArgs> Progress;

        public GitCheckpointService([NotNull] string gameDirectory)
        {
            _gameDirectory = gameDirectory ?? throw new ArgumentNullException(nameof(gameDirectory));
            _gitDirectory = CheckpointPaths.GetCheckpointsRoot(_gameDirectory);
        }

        /// <summary>
        /// Initializes the git repository and creates the baseline checkpoint.
        /// </summary>
        public async Task<string> InitializeAsync(CancellationToken cancellationToken = default)
        {
            await Logger.LogAsync("Initializing Git-based checkpoint system...").ConfigureAwait(false);

            try
            {
                // Create checkpoint directory if it doesn't exist
                Directory.CreateDirectory(_gitDirectory);

                // Initialize or open repository
                if (!Repository.IsValid(_gitDirectory))
                {
                    await Logger.LogAsync("Creating new git repository for checkpoints...").ConfigureAwait(false);
                    Repository.Init(_gitDirectory, isBare: false);
                }

                _repository = new Repository(_gitDirectory);

                // Configure repository
                ConfigureRepository();

                // Create initial baseline if needed
                string baselineCommitId = await CreateBaselineIfNeededAsync(cancellationToken).ConfigureAwait(false);

                _isInitialized = true;
                await Logger.LogAsync($"Checkpoint system initialized. Baseline: {baselineCommitId}").ConfigureAwait(false);

                return baselineCommitId;
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to initialize checkpoint system").ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Creates a checkpoint after a mod component installation.
        /// </summary>
        public async Task<CheckpointInfo> CreateCheckpointAsync(
            [NotNull] ModComponent component,
            int componentIndex,
            int totalComponents,
            CancellationToken cancellationToken = default)
        {
            if (component is null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (!_isInitialized)
            {
                throw new InvalidOperationException("Checkpoint system not initialized. Call InitializeAsync first.");
            }

            ReportProgress($"Creating checkpoint for '{component.Name}'...", componentIndex, totalComponents);

            try
            {
                // Stage all changes in game directory
                await StageGameDirectoryChangesAsync(cancellationToken).ConfigureAwait(false);

                // Create commit
                string commitMessage = BuildCommitMessage(component, componentIndex, totalComponents);
                Signature signature = new Signature("KOTORModSync", "checkpoint@kotormodsync.local", DateTimeOffset.Now);

                Commit commit = _repository.Commit(commitMessage, signature, signature, new CommitOptions());

                var checkpointInfo = new CheckpointInfo
                {
                    CommitId = commit.Sha,
                    ComponentName = component.Name,
                    ComponentGuid = component.Guid,
                    Timestamp = DateTimeOffset.Now,
                    ComponentIndex = componentIndex,
                    TotalComponents = totalComponents,
                    Message = commitMessage,
                };

                await Logger.LogAsync($"✓ Checkpoint created: {commit.Sha.Substring(0, 8)} - {component.Name}").ConfigureAwait(false);

                CheckpointCreated?.Invoke(this, new CheckpointCreatedEventArgs(checkpointInfo));

                return checkpointInfo;
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, $"Failed to create checkpoint for '{component.Name}'").ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Restores the game directory to a specific checkpoint.
        /// </summary>
        public async Task RestoreCheckpointAsync(
            [NotNull] string commitId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(commitId))
            {
                throw new ArgumentNullException(nameof(commitId));
            }

            if (!_isInitialized)
            {
                throw new InvalidOperationException("Checkpoint system not initialized.");
            }

            await Logger.LogAsync($"Restoring to checkpoint: {commitId}...").ConfigureAwait(false);
            ReportProgress($"Restoring checkpoint {commitId.Substring(0, 8)}...", 0, 1);

            try
            {
                Commit targetCommit = _repository.Lookup<Commit>(commitId);
                if (targetCommit is null)
                {
                    throw new InvalidOperationException($"Checkpoint {commitId} not found.");
                }

                // Checkout the target commit
                CheckoutOptions checkoutOptions = new CheckoutOptions
                {
                    CheckoutModifiers = CheckoutModifiers.Force,
                    CheckoutNotifyFlags = CheckoutNotifyFlags.Updated | CheckoutNotifyFlags.Untracked,
                };

                Commands.Checkout(_repository, targetCommit, checkoutOptions);

                // Copy restored files back to game directory
                await RestoreFilesToGameDirectoryAsync(cancellationToken).ConfigureAwait(false);

                await Logger.LogAsync($"✓ Successfully restored to checkpoint: {commitId}").ConfigureAwait(false);

                CheckpointRestored?.Invoke(this, new CheckpointRestoredEventArgs
                {
                    CommitId = commitId,
                    Timestamp = DateTimeOffset.Now,
                });
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, $"Failed to restore checkpoint {commitId}").ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Lists all available checkpoints in chronological order.
        /// </summary>
        public async Task<List<CheckpointInfo>> ListCheckpointsAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }

            try
            {
                var checkpoints = new List<CheckpointInfo>();

                var commits = (IQueryableCommitLog)_repository.Commits.QueryBy(new CommitFilter
                {
                    SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time,
                });

                foreach (Commit commit in commits)
                {
                    checkpoints.Add(ParseCheckpointFromCommit(commit));
                }

                return checkpoints;
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to list checkpoints").ConfigureAwait(false);
                return new List<CheckpointInfo>();
            }
        }

        /// <summary>
        /// Gets the current checkpoint (HEAD commit).
        /// </summary>
        public async Task<CheckpointInfo> GetCurrentCheckpointAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync().ConfigureAwait(false);
            }

            Commit headCommit = _repository.Head.Tip;
            return headCommit != null ? ParseCheckpointFromCommit(headCommit) : null;
        }

        /// <summary>
        /// Deletes all checkpoints and resets the system.
        /// </summary>
        public async Task ClearAllCheckpointsAsync()
        {
            await Logger.LogAsync("Clearing all checkpoints...").ConfigureAwait(false);

            try
            {
                Dispose();

                if (Directory.Exists(_gitDirectory))
                {
                    Directory.Delete(_gitDirectory, recursive: true);
                }

                await Logger.LogAsync("✓ All checkpoints cleared.").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to clear checkpoints").ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// Gets the diff between two checkpoints.
        /// </summary>
        public async Task<List<FileChangeInfo>> GetDiffAsync(string fromCommitId, string toCommitId = null)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Checkpoint system not initialized.");
            }

            try
            {
                Commit fromCommit = _repository.Lookup<Commit>(fromCommitId);
                Tree fromTree = fromCommit?.Tree;

                Tree toTree = null;
                if (!string.IsNullOrEmpty(toCommitId))
                {
                    Commit toCommit = _repository.Lookup<Commit>(toCommitId);
                    toTree = toCommit?.Tree;
                }

                TreeChanges changes = toTree != null
                    ? _repository.Diff.Compare<TreeChanges>(fromTree, toTree)
                    : _repository.Diff.Compare<TreeChanges>(fromTree, DiffTargets.WorkingDirectory);

                var fileChanges = new List<FileChangeInfo>();

                foreach (TreeEntryChanges change in changes)
                {
                    fileChanges.Add(new FileChangeInfo
                    {
                        Path = change.Path,
                        Status = ConvertChangeKind(change.Status),
                        OldPath = change.OldPath,
                    });
                }

                return fileChanges;
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "Failed to get diff").ConfigureAwait(false);
                return new List<FileChangeInfo>();
            }
        }

        private void ConfigureRepository()
        {
            // Set configuration to reduce repo size and improve performance
            _repository.Config.Set("core.compression", 9);
            _repository.Config.Set("core.looseCompression", 9);
            _repository.Config.Set("gc.auto", 256);
            _repository.Config.Set("pack.windowMemory", "100m");

            // Configure user for commits
            _repository.Config.Set("user.name", "KOTORModSync");
            _repository.Config.Set("user.email", "checkpoint@kotormodsync.local");
        }

        private async Task<string> CreateBaselineIfNeededAsync(CancellationToken cancellationToken)
        {
            // Check if repository is empty
            if (_repository.Head.Tip != null)
            {
                await Logger.LogAsync("Existing checkpoint history found.").ConfigureAwait(false);
                return _repository.Head.Tip.Sha;
            }

            await Logger.LogAsync("Creating baseline checkpoint of game directory...").ConfigureAwait(false);

            // Stage all files in game directory
            await StageGameDirectoryChangesAsync(cancellationToken).ConfigureAwait(false);

            // Create initial commit
            Signature signature = new Signature("KOTORModSync", "checkpoint@kotormodsync.local", DateTimeOffset.Now);
            Commit initialCommit = _repository.Commit(InitialCommitMessage, signature, signature, new CommitOptions());

            return initialCommit.Sha;
        }

        private async Task StageGameDirectoryChangesAsync(CancellationToken cancellationToken)
        {
            // Copy current game files to git working directory
            await SyncGameDirectoryToGitAsync(cancellationToken).ConfigureAwait(false);

            // Stage all changes
            Commands.Stage(_repository, "*");
        }

        private async Task SyncGameDirectoryToGitAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                // Get all files in game directory (excluding checkpoint folder)
                string checkpointRoot = CheckpointPaths.GetRoot(_gameDirectory);
                var gameFiles = Directory.GetFiles(_gameDirectory, "*", SearchOption.AllDirectories)
                    .Where(f => !f.StartsWith(checkpointRoot, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (string gameFile in gameFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string relativePath = NetFrameworkCompatibility.GetRelativePath(_gameDirectory, gameFile);
                    string gitPath = Path.Combine(_gitDirectory, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(gitPath));
                    File.Copy(gameFile, gitPath, overwrite: true);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        private async Task RestoreFilesToGameDirectoryAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                // Get all files in git working directory
                var gitFiles = Directory.GetFiles(_gitDirectory, "*", SearchOption.AllDirectories)
                    .Where(f => !f.Contains(".git"))
                    .ToList();

                // First, delete files in game directory that don't exist in git
                string checkpointRoot = CheckpointPaths.GetRoot(_gameDirectory);
                var gameFiles = Directory.GetFiles(_gameDirectory, "*", SearchOption.AllDirectories)
                    .Where(f => !f.StartsWith(checkpointRoot, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (string gameFile in gameFiles)
                {
                    string relativePath = NetFrameworkCompatibility.GetRelativePath(_gameDirectory, gameFile);
                    string gitPath = Path.Combine(_gitDirectory, relativePath);

                    if (!File.Exists(gitPath))
                    {
                        File.Delete(gameFile);
                    }
                }

                // Copy files from git to game directory
                foreach (string gitFile in gitFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string relativePath = NetFrameworkCompatibility.GetRelativePath(_gitDirectory, gitFile);
                    string gamePath = Path.Combine(_gameDirectory, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(gamePath));
                    File.Copy(gitFile, gamePath, overwrite: true);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        private static string BuildCommitMessage(ModComponent component, int index, int total)
        {
            return $"[{index}/{total}] {component.Name}\n\nGUID: {component.Guid}\nAuthor: {component.Author}\nInstalled: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}";
        }

        private static CheckpointInfo ParseCheckpointFromCommit(Commit commit)
        {
            string[] lines = commit.Message.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string firstLine = lines[0];

            // Parse [index/total] Name format
            string componentName = firstLine;
            int componentIndex = 0;
            int totalComponents = 0;

            if (firstLine.StartsWith("[", StringComparison.Ordinal))
            {
                int endBracket = firstLine.IndexOf(']');
                if (endBracket > 0)
                {
                    string indexPart = firstLine.Substring(1, endBracket - 1);
                    string[] parts = indexPart.Split('/');
                    if (parts.Length == 2)
                    {
                        _ = int.TryParse(parts[0], out componentIndex);
                        _ = int.TryParse(parts[1], out totalComponents);
                    }
                    componentName = firstLine.Substring(endBracket + 1).Trim();
                }
            }

            // Try to parse GUID from message
            Guid componentGuid = Guid.Empty;
            foreach (string line in lines)
            {
                if (line.StartsWith("GUID:", StringComparison.Ordinal))
                {
                    string guidStr = line.Substring(5).Trim();
                    _ = Guid.TryParse(guidStr, out componentGuid);
                    break;
                }
            }

            return new CheckpointInfo
            {
                CommitId = commit.Sha,
                ComponentName = componentName,
                ComponentGuid = componentGuid,
                Timestamp = commit.Author.When,
                ComponentIndex = componentIndex,
                TotalComponents = totalComponents,
                Message = commit.Message,
                Author = commit.Author.Name,
            };
        }

        private static FileChangeStatus ConvertChangeKind(ChangeKind kind)
        {
            switch (kind)
            {
                case ChangeKind.Added:
                    return FileChangeStatus.Added;
                case ChangeKind.Deleted:
                    return FileChangeStatus.Deleted;
                case ChangeKind.Modified:
                    return FileChangeStatus.Modified;
                case ChangeKind.Renamed:
                    return FileChangeStatus.Renamed;
                default:
                    return FileChangeStatus.Unmodified;
            }
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

        public void Dispose()
        {
            _repository?.Dispose();
            _isInitialized = false;
        }
    }

    public class CheckpointInfo
    {
        public string CommitId { get; set; }
        public string ComponentName { get; set; }
        public Guid ComponentGuid { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public int ComponentIndex { get; set; }
        public int TotalComponents { get; set; }
        public string Message { get; set; }
        public string Author { get; set; }

        public string ShortCommitId => CommitId?.Substring(0, Math.Min(8, CommitId.Length));
        public string DisplayName => $"{ShortCommitId} - {ComponentName}";
    }

    public class FileChangeInfo
    {
        public string Path { get; set; }
        public FileChangeStatus Status { get; set; }
        public string OldPath { get; set; }
    }

    public enum FileChangeStatus
    {
        Unmodified,
        Added,
        Deleted,
        Modified,
        Renamed,
    }

    public class CheckpointCreatedEventArgs : EventArgs
    {
        public CheckpointInfo Checkpoint { get; }

        public CheckpointCreatedEventArgs(CheckpointInfo checkpoint)
        {
            Checkpoint = checkpoint;
        }
    }

    public class CheckpointRestoredEventArgs : EventArgs
    {
        public string CommitId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

    public class CheckpointProgressEventArgs : EventArgs
    {
        public string Message { get; set; }
        public int Current { get; set; }
        public int Total { get; set; }
    }
}

