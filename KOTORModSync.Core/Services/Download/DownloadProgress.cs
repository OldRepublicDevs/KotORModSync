// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace KOTORModSync.Core.Services.Download
{
    public sealed class DownloadProgress : INotifyPropertyChanged
    {
        private string _modName = string.Empty;
        private string _url = string.Empty;
        private DownloadStatus _status = DownloadStatus.Pending;
        private double _progressPercentage;
        private long _bytesDownloaded;
        private long _totalBytes;
        private string _statusMessage = string.Empty;
        private string _errorMessage = string.Empty;
        private string _filePath = string.Empty;
        private DateTime _startTime;
        private DateTime? _endTime;
        private Exception _exception;
        private DownloadSource _downloadSource = DownloadSource.Direct;
        private readonly List<string> _logs = new List<string>();
        private readonly object _logLock = new object();
        private bool _completedFromCache;

        public List<string> TargetFilenames { get; set; } = new List<string>();

        public List<string> GetLogs()
        {
            lock (_logLock)
            {
                return new List<string>(_logs);
            }
        }

        private readonly List<DownloadProgress> _childDownloads = new List<DownloadProgress>();
        private bool _isGrouped;
        private Guid? _componentGuid;

        public string ModName
        {
            get => _modName;

            set
            {
                if (





string.Equals(_modName, value, StringComparison.Ordinal))
                {
                    return;
                }

                _modName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public string

Url
        {
            get => _url;
            set
            {
                if (string.Equals(_url, value, StringComparison.Ordinal))
                {
                    return;
                }

                _url = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public DownloadStatus Status
        {
            get => _status;
            set
            {
                if (_status == value)
                {
                    return;
                }

                if (_status != DownloadStatus.Pending || value != DownloadStatus.Pending)
                {
                    AddLog($"Status changed: {_status} → {value}");
                }

                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(IsInProgress));
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(ControlButtonIcon));
                OnPropertyChanged(nameof(ControlButtonTooltip));

                OnPropertyChanged(nameof(GroupStatusMessage));
            }
        }

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                if (Math.Abs(_progressPercentage - value) < 0.01)
                {
                    return;
                }

                _progressPercentage = value;
                OnPropertyChanged();

                OnPropertyChanged(nameof(GroupProgressPercentage));
            }
        }

        public long BytesDownloaded
        {
            get => _bytesDownloaded;
            set
            {
                if (_bytesDownloaded == value)
                {
                    return;
                }

                _bytesDownloaded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DownloadedSize));
                OnPropertyChanged(nameof(DownloadSpeed));
            }
        }

        public long TotalBytes
        {
            get => _totalBytes;
            set
            {
                if (_totalBytes == value)
                {
                    return;
                }

                _totalBytes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalSize));
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (string.Equals(_statusMessage, value, StringComparison.Ordinal))
                {
                    return;
                }

                _statusMessage = value;
                OnPropertyChanged();

                OnPropertyChanged(nameof(GroupStatusMessage));
            }
        }

        public bool CompletedFromCache
        {
            get => _completedFromCache;
            set
            {
                if (_completedFromCache == value)
                {
                    return;
                }

                _completedFromCache = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GroupStatusMessage));
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (string.Equals(_errorMessage, value, StringComparison.Ordinal))
                {
                    return;
                }

                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (string.Equals(_filePath, value, StringComparison.Ordinal))
                {
                    return;
                }

                _filePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                if (_startTime == value)
                {
                    return;
                }

                _startTime = value;
                OnPropertyChanged();
            }
        }

        public DateTime? EndTime
        {
            get => _endTime;
            set
            {
                if (_endTime == value)
                {
                    return;
                }

                _endTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(DownloadSpeed));
            }
        }

        public Exception Exception
        {
            get => _exception;
            set
            {
                if (_exception == value)
                {
                    return;
                }

                _exception = value;
                OnPropertyChanged();
            }
        }

        public DownloadSource DownloadSource
        {
            get => _downloadSource;
            set
            {
                if (_downloadSource == value)
                {
                    return;
                }

                _downloadSource = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SourceIcon));
                OnPropertyChanged(nameof(SourceDisplayName));
            }
        }

        public bool IsGrouped
        {
            get => _isGrouped;
            set
            {
                if (_isGrouped == value)
                {
                    return;
                }

                _isGrouped = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GroupStatusMessage));
                OnPropertyChanged(nameof(GroupProgressPercentage));
            }
        }

        public List<DownloadProgress> ChildDownloads => _childDownloads;

        public Guid? ComponentGuid
        {
            get => _componentGuid;
            set
            {
                if (_componentGuid == value)
                {
                    return;
                }

                _componentGuid = value;
                OnPropertyChanged();
            }
        }

        public string GroupStatusMessage
        {
            get
            {
                if (!IsGrouped || _childDownloads.Count == 0)
                {
                    return StatusMessage;
                }

                int cached = _childDownloads.Count(c => c.Status == DownloadStatus.Completed && c.CompletedFromCache);
                int downloaded = _childDownloads.Count(c => c.Status == DownloadStatus.Completed && !c.CompletedFromCache);
                int completed = cached + downloaded;
                int failed = _childDownloads.Count(c => c.Status == DownloadStatus.Failed);
                int skipped = _childDownloads.Count(c => c.Status == DownloadStatus.Skipped);
                int inProgress = _childDownloads.Count(c => c.Status == DownloadStatus.InProgress);
                int pending = _childDownloads.Count(c => c.Status == DownloadStatus.Pending);

                if (completed == _childDownloads.Count)
                {
                    if (downloaded > 0 && cached > 0)
                    {
                        return $"All {_childDownloads.Count} files ready ({downloaded} downloaded, {cached} cached)";
                    }

                    if (downloaded > 0)
                    {
                        return $"All {_childDownloads.Count} files downloaded successfully";
                    }

                    return $"All {_childDownloads.Count} files already cached";
                }

                if (failed == _childDownloads.Count)
                {
                    return $"All {_childDownloads.Count} files failed to download";
                }

                if (completed + skipped == _childDownloads.Count)
                {
                    var partsAll = new List<string>();
                    if (downloaded > 0)
                    {
                        partsAll.Add($"{downloaded} downloaded");
                    }

                    if (cached > 0)
                    {
                        partsAll.Add($"{cached} cached");
                    }

                    if (skipped > 0)
                    {
                        partsAll.Add($"{skipped} skipped");
                    }

                    return $"All {_childDownloads.Count} files completed ({string.Join(", ", partsAll)})";
                }

                if (inProgress > 0)
                {
                    return $"Downloading {inProgress} of {_childDownloads.Count} files...";
                }

                if (pending > 0)
                {
                    return $"Waiting to start {pending} of {_childDownloads.Count} files...";
                }

                int totalFinished = completed + skipped + failed;
                var parts = new List<string>();
                if (downloaded > 0)
                {
                    parts.Add($"{downloaded} downloaded");
                }

                if (cached > 0)
                {
                    parts.Add($"{cached} cached");
                }

                if (skipped > 0)
                {
                    parts.Add($"{skipped} skipped");
                }

                if (failed > 0)
                {
                    parts.Add($"{failed} failed");
                }

                return $"{totalFinished}/{_childDownloads.Count} files completed ({string.Join(", ", parts)})";
            }
        }

        public double GroupProgressPercentage
        {
            get
            {
                if (!IsGrouped || _childDownloads.Count == 0)
                {
                    return ProgressPercentage;
                }

                return _childDownloads.Average(c => c.ProgressPercentage);
            }
        }

        public bool IsCompleted => IsGrouped ? _childDownloads.TrueForAll(c => c.Status == DownloadStatus.Completed || c.Status == DownloadStatus.Skipped) : (Status == DownloadStatus.Completed || Status == DownloadStatus.Skipped);
        public bool IsFailed => IsGrouped ? _childDownloads.TrueForAll(c => c.Status == DownloadStatus.Failed) : Status == DownloadStatus.Failed;
        public bool IsInProgress => IsGrouped ? _childDownloads.Exists(c => c.Status == DownloadStatus.InProgress) : Status == DownloadStatus.InProgress;

        public string DownloadedSize => FormatBytes(BytesDownloaded);
        public string TotalSize => FormatBytes(TotalBytes);

        public string DisplayName
        {
            get
            {
                string fileName = string.IsNullOrEmpty(FilePath) ? "" : System.IO.Path.GetFileName(FilePath);
                string url = string.IsNullOrEmpty(Url) ? "" : Url;

                if (IsGrouped)
                {
                    return ModName;
                }

                if (string.IsNullOrEmpty(fileName))
                {
                    return $"{ModName} - {url}";
                }

                return $"{ModName} - {url} - {fileName}";
            }
        }

        public TimeSpan Duration => StartTime == default ? TimeSpan.Zero : (EndTime ?? DateTime.Now) - StartTime;

        public string DownloadSpeed
        {
            get
            {
                if (BytesDownloaded == 0 || Duration.TotalSeconds < 0.1)
                {
                    return "0 B/s";
                }

                double bytesPerSecond = BytesDownloaded / Duration.TotalSeconds;
                return $"{FormatBytes((long)bytesPerSecond)}/s";
            }
        }

        public string StatusIcon
        {
            get
            {
                if (IsGrouped)
                {
                    if (IsCompleted)
                    {
                        return "✅";
                    }

                    if (IsFailed)
                    {
                        return "❌";
                    }

                    if (IsInProgress)
                    {
                        return "⬇️";
                    }

                    if (_childDownloads.Count > 0)
                    {
                        int completed = _childDownloads.Count(c => c.Status == DownloadStatus.Completed || c.Status == DownloadStatus.Skipped);
                        int failed = _childDownloads.Count(c => c.Status == DownloadStatus.Failed);

                        if (completed > 0 && failed > 0)
                        {
                            return "❌";
                        }
                    }

                    return "⏳";
                }

                switch (Status)
                {
                    case DownloadStatus.Pending:
                        return "⏳";
                    case DownloadStatus.InProgress:
                        return "⬇️";
                    case DownloadStatus.Completed:
                        return "✓";
                    case DownloadStatus.Failed:
                        return "❌";
                    case DownloadStatus.Skipped:
                        return "⏭️";
                    default:
                        return "❓";
                }
            }
        }

        public string ControlButtonIcon
        {
            get
            {

                switch (Status)
                {
                    case DownloadStatus.Pending:
                        return "▶";
                    case DownloadStatus.InProgress:
                        return "⏹";
                    case DownloadStatus.Completed:
                    case DownloadStatus.Skipped:
                    case DownloadStatus.Failed:
                        return "↻";
                    default:
                        return "▶";
                }
            }
        }

        public string ControlButtonTooltip
        {
            get
            {

                switch (Status)
                {
                    case DownloadStatus.Pending:
                        return IsGrouped ? "Start all downloads now" : "Start download now";
                    case DownloadStatus.InProgress:
                        return IsGrouped ? "Stop all downloads" : "Stop download";
                    case DownloadStatus.Completed:
                    case DownloadStatus.Skipped:
                        return IsGrouped ? "Retry all downloads" : "Retry download";
                    case DownloadStatus.Failed:
                        return IsGrouped ? "Retry all failed downloads" : "Retry failed download";
                    default:
                        return IsGrouped ? "Control all downloads" : "Control download";
                }
            }
        }

        public string SourceIcon
        {
            get
            {
                switch (_downloadSource)
                {
                    case DownloadSource.Direct:
                        return "🌐";
                    case DownloadSource.Optimized:
                        return "⚡";
                    case DownloadSource.Hybrid:
                        return "🔄";
                    default:
                        return "❓";
                }
            }
        }

        public string SourceDisplayName
        {
            get
            {
                switch (_downloadSource)
                {
                    case DownloadSource.Direct:
                        return "Direct Download";
                    case DownloadSource.Optimized:
                        return "Network Cache";
                    case DownloadSource.Hybrid:
                        return "Hybrid Download";
                    default:
                        return "Unknown";
                }
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public void AddLog(string logMessage)
        {
            if (string.IsNullOrEmpty(logMessage))
            {
                return;
            }

            lock (_logLock)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                _logs.Add($"[{timestamp}] {logMessage}");
            }
        }

        public static DownloadProgress CreateGrouped(string modName, IEnumerable<string> urls)
        {
            var groupedProgress = new DownloadProgress
            {
                ModName = modName,
                IsGrouped = true,
                Status = DownloadStatus.Pending,
                StatusMessage = "Preparing downloads...",
                ProgressPercentage = 0,
            };

            var urlList = urls.ToList();

            groupedProgress.AddLog($"Preparing to download {urlList.Count} files for mod: {modName}");

            foreach (string url in urlList)
            {
                var childProgress = new DownloadProgress
                {
                    ModName = modName,
                    Url = url,
                    Status = DownloadStatus.Pending,
                    StatusMessage = "Waiting to start...",
                    ProgressPercentage = 0,
                };

                childProgress.AddLog($"Queued for download: {url}");

                childProgress.PropertyChanged += (sender, e) =>
                {
                    groupedProgress.OnPropertyChanged(nameof(GroupStatusMessage));
                    groupedProgress.OnPropertyChanged(nameof(GroupProgressPercentage));




                    if (string.Equals(e.PropertyName, nameof(Status), StringComparison.Ordinal))
                    {

                        if (sender is DownloadProgress child)
                        {
                            string fileName = "Unknown";
                            if (!string.IsNullOrEmpty(child.Url))
                            {
                                try
                                {
                                    fileName = System.IO.Path.GetFileName(new Uri(child.Url).AbsolutePath);
                                }
                                catch (UriFormatException ex)
                                {
                                    Logger.LogVerbose($"[DownloadProgress] Failed to parse file name from URL '{child.Url}': {ex.Message}");
                                    fileName = System.IO.Path.GetFileName(child.Url);
                                }
                            }
                            groupedProgress.AddLog($"[File {groupedProgress._childDownloads.IndexOf(child) + 1}/{groupedProgress._childDownloads.Count}] {fileName}: {child.Status}");

                            if (!string.IsNullOrEmpty(child.StatusMessage))
                            {
                                groupedProgress.AddLog($"  → {child.StatusMessage}");
                            }

                            if (!string.IsNullOrEmpty(child.ErrorMessage))
                            {
                                groupedProgress.AddLog($"  ✗ ERROR: {child.ErrorMessage}");
                            }
                        }

                        bool anyPending = groupedProgress._childDownloads.Exists(c => c.Status == DownloadStatus.Pending);
                        bool anyInProgress = groupedProgress._childDownloads.Exists(c => c.Status == DownloadStatus.InProgress);

                        if (anyInProgress)
                        {
                            groupedProgress.Status = DownloadStatus.InProgress;
                            groupedProgress.StatusMessage = "Downloading files...";
                        }
                        else if (!anyPending)
                        {

                            int completed = groupedProgress._childDownloads.Count(c => c.Status == DownloadStatus.Completed);
                            int skipped = groupedProgress._childDownloads.Count(c => c.Status == DownloadStatus.Skipped);
                            int failed = groupedProgress._childDownloads.Count(c => c.Status == DownloadStatus.Failed);

                            if (failed > 0 && completed == 0 && skipped == 0)
                            {

                                groupedProgress.Status = DownloadStatus.Failed;
                                groupedProgress.StatusMessage = "All files failed";
                                groupedProgress.ErrorMessage = "All download attempts failed. Check individual file details for specific error information.";
                            }
                            else if (failed > 0)
                            {

                                groupedProgress.Status = DownloadStatus.Failed;
                                groupedProgress.StatusMessage = $"Partially completed ({completed} downloaded, {skipped} skipped, {failed} failed)";
                                groupedProgress.ProgressPercentage = 100;

                                var failedChildren = groupedProgress._childDownloads.Where(c => c.Status == DownloadStatus.Failed).ToList();
                                if (failedChildren.Any())
                                {
                                    var errorMessages = failedChildren
                                        .Where(c => !string.IsNullOrEmpty(c.ErrorMessage))
                                        .Select(c =>
                                        {
                                            string fileName = "Unknown";
                                            if (!string.IsNullOrEmpty(c.Url))
                                            {
                                                try
                                                {
                                                    fileName = System.IO.Path.GetFileName(new Uri(c.Url).AbsolutePath);
                                                }
                                                catch (UriFormatException ex)
                                                {
                                                    Logger.LogVerbose($"[DownloadProgress] Failed to parse file name from URL '{c.Url}': {ex.Message}");
                                                    fileName = System.IO.Path.GetFileName(c.Url);
                                                }
                                            }
                                            return $"• {fileName}: {c.ErrorMessage}";
                                        })
                                        .ToList();

                                    if (errorMessages.Any())
                                    {
                                        groupedProgress.ErrorMessage = $"Some files failed to download:\n{string.Join("\n", errorMessages)}";
                                    }
                                    else
                                    {
                                        groupedProgress.ErrorMessage = $"{failed} file(s) failed to download. Check individual file details for specific error information.";
                                    }
                                }
                            }
                            else
                            {

                                groupedProgress.Status = DownloadStatus.Completed;
                                groupedProgress.StatusMessage = $"All files completed ({completed} downloaded, {skipped} skipped)";
                                groupedProgress.ProgressPercentage = 100;
                                groupedProgress.ErrorMessage = string.Empty;
                            }
                        }
                    }
                };

                groupedProgress._childDownloads.Add(childProgress);
            }

            return groupedProgress;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
    public enum DownloadStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Skipped,
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
    public enum DownloadSource
    {
        Direct,      // Traditional HTTP/HTTPS download
        Optimized,   // Network cache download
        Hybrid,      // Both sources attempted
    }
}
