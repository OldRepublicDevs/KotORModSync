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
		private readonly List<string> _logs = new List<string>();
		private readonly object _logLock = new object();

		public List<string> GetLogs()
		{
			lock ( _logLock )
			{
				return new List<string>(_logs);
			}
		}

		// Properties for grouped downloads
		private readonly List<DownloadProgress> _childDownloads = new List<DownloadProgress>();
		private bool _isGrouped;

		public string ModName
		{
			get => _modName;
			set
			{
				if ( _modName == value )
					return;
				_modName = value;
				OnPropertyChanged();
			}
		}

		public string Url
		{
			get => _url;
			set
			{
				if ( _url == value )
					return;
				_url = value;
				OnPropertyChanged();
			}
		}

		public DownloadStatus Status
		{
			get => _status;
			set
			{
				if ( _status == value )
					return;

				// Log status changes to help debug flickering issues
				if ( _status != DownloadStatus.Pending || value != DownloadStatus.Pending )
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
				// Ensure UI bound to grouped message reflects status changes even for single items
				OnPropertyChanged(nameof(GroupStatusMessage));
			}
		}

		public double ProgressPercentage
		{
			get => _progressPercentage;
			set
			{
				if ( Math.Abs(_progressPercentage - value) < 0.01 )
					return;
				_progressPercentage = value;
				OnPropertyChanged();
				// Also notify grouped proxy for single downloads
				OnPropertyChanged(nameof(GroupProgressPercentage));
			}
		}

		public long BytesDownloaded
		{
			get => _bytesDownloaded;
			set
			{
				if ( _bytesDownloaded == value )
					return;
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
				if ( _totalBytes == value )
					return;
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
				if ( _statusMessage == value )
					return;
				_statusMessage = value;
				OnPropertyChanged();
				// Keep grouped message in sync for non-grouped items
				OnPropertyChanged(nameof(GroupStatusMessage));
			}
		}

		public string ErrorMessage
		{
			get => _errorMessage;
			set
			{
				if ( _errorMessage == value )
					return;
				_errorMessage = value;
				OnPropertyChanged();
			}
		}

		public string FilePath
		{
			get => _filePath;
			set
			{
				if ( _filePath == value )
					return;
				_filePath = value;
				OnPropertyChanged();
			}
		}

		public DateTime StartTime
		{
			get => _startTime;
			set
			{
				if ( _startTime == value )
					return;
				_startTime = value;
				OnPropertyChanged();
			}
		}

		public DateTime? EndTime
		{
			get => _endTime;
			set
			{
				if ( _endTime == value )
					return;
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
				if ( _exception == value )
					return;
				_exception = value;
				OnPropertyChanged();
			}
		}

		// Grouped download properties
		public bool IsGrouped
		{
			get => _isGrouped;
			set
			{
				if ( _isGrouped == value )
					return;
				_isGrouped = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(GroupStatusMessage));
				OnPropertyChanged(nameof(GroupProgressPercentage));
			}
		}

		public List<DownloadProgress> ChildDownloads => _childDownloads;

		public string GroupStatusMessage
		{
			get
			{
				if ( !IsGrouped || _childDownloads.Count == 0 )
					return StatusMessage;

				int completed = _childDownloads.Count(c => c.Status == DownloadStatus.Completed);
				int failed = _childDownloads.Count(c => c.Status == DownloadStatus.Failed);
				int skipped = _childDownloads.Count(c => c.Status == DownloadStatus.Skipped);
				int inProgress = _childDownloads.Count(c => c.Status == DownloadStatus.InProgress);
				int pending = _childDownloads.Count(c => c.Status == DownloadStatus.Pending);

				if ( completed == _childDownloads.Count )
					return $"All {_childDownloads.Count} files downloaded successfully";
				if ( failed == _childDownloads.Count )
					return $"All {_childDownloads.Count} files failed to download";
				if ( completed + skipped == _childDownloads.Count )
					return $"All {_childDownloads.Count} files completed ({completed} downloaded, {skipped} skipped)";
				if ( inProgress > 0 )
					return $"Downloading {inProgress} of {_childDownloads.Count} files...";
				if ( pending > 0 )
					return $"Waiting to start {pending} of {_childDownloads.Count} files...";

				// Show total including failures
				int totalFinished = completed + skipped + failed;
				var parts = new List<string>();
				if ( completed > 0 ) parts.Add($"{completed} downloaded");
				if ( skipped > 0 ) parts.Add($"{skipped} skipped");
				if ( failed > 0 ) parts.Add($"{failed} failed");

				return $"{totalFinished}/{_childDownloads.Count} files completed ({string.Join(", ", parts)})";
			}
		}

		public double GroupProgressPercentage
		{
			get
			{
				if ( !IsGrouped || _childDownloads.Count == 0 )
					return ProgressPercentage;

				return _childDownloads.Average(c => c.ProgressPercentage);
			}
		}

		// Computed properties
		public bool IsCompleted => IsGrouped ? _childDownloads.All(c => c.Status == DownloadStatus.Completed || c.Status == DownloadStatus.Skipped) : (Status == DownloadStatus.Completed || Status == DownloadStatus.Skipped);
		public bool IsFailed => IsGrouped ? _childDownloads.All(c => c.Status == DownloadStatus.Failed) : Status == DownloadStatus.Failed;
		public bool IsInProgress => IsGrouped ? _childDownloads.Any(c => c.Status == DownloadStatus.InProgress) : Status == DownloadStatus.InProgress;

		public string DownloadedSize => FormatBytes(BytesDownloaded);
		public string TotalSize => FormatBytes(TotalBytes);

		public TimeSpan Duration => StartTime == default ? TimeSpan.Zero : (EndTime ?? DateTime.Now) - StartTime;

		public string DownloadSpeed
		{
			get
			{
				if ( BytesDownloaded == 0 || Duration.TotalSeconds < 0.1 )
					return "0 B/s";

				double bytesPerSecond = BytesDownloaded / Duration.TotalSeconds;
				return $"{FormatBytes((long)bytesPerSecond)}/s";
			}
		}

		public string StatusIcon
		{
			get
			{
				if ( IsGrouped )
				{
					if ( IsCompleted )
						return "✅";
					if ( IsFailed )
						return "❌";
					if ( IsInProgress )
						return "⬇️";

					// Check for mixed results (some succeeded, some failed)
					if ( _childDownloads.Count > 0 )
					{
						int completed = _childDownloads.Count(c => c.Status == DownloadStatus.Completed || c.Status == DownloadStatus.Skipped);
						int failed = _childDownloads.Count(c => c.Status == DownloadStatus.Failed);

						// If we have both successes and failures, show error icon
						if ( completed > 0 && failed > 0 )
							return "❌";
					}

					return "⏳";
				}

				switch ( Status )
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
				// Use the status directly (works for both grouped and single downloads)
				switch ( Status )
				{
					case DownloadStatus.Pending:
						return "▶"; // Play icon (simple arrow)
					case DownloadStatus.InProgress:
						return "⏸"; // Pause icon
					case DownloadStatus.Completed:
					case DownloadStatus.Skipped:
					case DownloadStatus.Failed:
						return "↻"; // Retry/refresh icon (circular arrow)
					default:
						return "▶";
				}
			}
		}

		public string ControlButtonTooltip
		{
			get
			{
				// Provide appropriate tooltips based on current status
				switch ( Status )
				{
					case DownloadStatus.Pending:
						return IsGrouped ? "Start all downloads now" : "Start download now";
					case DownloadStatus.InProgress:
						return IsGrouped ? "Pause all downloads" : "Pause download";
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

		private static string FormatBytes(long bytes)
		{
			string[] sizes = { "B", "KB", "MB", "GB", "TB" };
			double len = bytes;
			int order = 0;
			while ( len >= 1024 && order < sizes.Length - 1 )
			{
				order++;
				len /= 1024;
			}
			return $"{len:0.##} {sizes[order]}";
		}

		/// <summary>
		/// Adds a log entry for this download.
		/// </summary>
		public void AddLog(string logMessage)
		{
			if ( string.IsNullOrEmpty(logMessage) )
				return;

			lock ( _logLock )
			{
				string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
				_logs.Add($"[{timestamp}] {logMessage}");
			}
		}

		/// <summary>
		/// Creates a grouped download progress item for a mod with multiple URLs.
		/// </summary>
		public static DownloadProgress CreateGrouped(string modName, IEnumerable<string> urls)
		{
			var groupedProgress = new DownloadProgress
			{
				ModName = modName,
				IsGrouped = true,
				Status = DownloadStatus.Pending,
				StatusMessage = "Preparing downloads...",
				ProgressPercentage = 0
			};

			// Materialize urls to a list to avoid multiple enumeration
			var urlList = urls.ToList();
			// Add initial log entry
			groupedProgress.AddLog($"Preparing to download {urlList.Count} files for mod: {modName}");

			foreach ( string url in urlList )
			{
				var childProgress = new DownloadProgress
				{
					ModName = modName,
					Url = url,
					Status = DownloadStatus.Pending,
					StatusMessage = "Waiting to start...",
					ProgressPercentage = 0
				};

				// Add initial log entry for child download
				childProgress.AddLog($"Queued for download: {url}");

				// Subscribe to child property changes to update group status
				childProgress.PropertyChanged += (sender, e) =>
				{
					groupedProgress.OnPropertyChanged(nameof(GroupStatusMessage));
					groupedProgress.OnPropertyChanged(nameof(GroupProgressPercentage));

					// DON'T notify about IsCompleted/IsFailed/IsInProgress/StatusIcon here!
					// They will be notified when we update the parent Status below.
					// Notifying them here causes the UI to read STALE values before the status updates!

					// Update the parent status based on child statuses AND log to parent
					if ( e.PropertyName == nameof(Status) )
					{
						// Log child status changes to parent for visibility
						if ( sender is DownloadProgress child )
						{
							string fileName = "Unknown";
							if ( !string.IsNullOrEmpty(child.Url) )
							{
								try
								{
									fileName = System.IO.Path.GetFileName(new Uri(child.Url).AbsolutePath);
								}
								catch ( UriFormatException )
								{
									// If URL is invalid, use the URL itself or a default name
									fileName = System.IO.Path.GetFileName(child.Url);
								}
							}
							groupedProgress.AddLog($"[File {groupedProgress._childDownloads.IndexOf(child) + 1}/{groupedProgress._childDownloads.Count}] {fileName}: {child.Status}");

							if ( !string.IsNullOrEmpty(child.StatusMessage) )
								groupedProgress.AddLog($"  → {child.StatusMessage}");

							if ( !string.IsNullOrEmpty(child.ErrorMessage) )
								groupedProgress.AddLog($"  ✗ ERROR: {child.ErrorMessage}");
						}

						// Check if ANY child is still pending or in progress
						bool anyPending = groupedProgress._childDownloads.Any(c => c.Status == DownloadStatus.Pending);
						bool anyInProgress = groupedProgress._childDownloads.Any(c => c.Status == DownloadStatus.InProgress);

						// If any child is still downloading or waiting, parent stays in progress
						if ( anyInProgress )
						{
							groupedProgress.Status = DownloadStatus.InProgress;
							groupedProgress.StatusMessage = "Downloading files...";
						}
						// If nothing is pending or in progress, the group is done
						else if ( !anyPending )
						{
							// Count the results
							int completed = groupedProgress._childDownloads.Count(c => c.Status == DownloadStatus.Completed);
							int skipped = groupedProgress._childDownloads.Count(c => c.Status == DownloadStatus.Skipped);
							int failed = groupedProgress._childDownloads.Count(c => c.Status == DownloadStatus.Failed);

							// Determine parent status based on results
							if ( failed > 0 && completed == 0 && skipped == 0 )
							{
								// All failed
								groupedProgress.Status = DownloadStatus.Failed;
								groupedProgress.StatusMessage = "All files failed";
								groupedProgress.ErrorMessage = "All download attempts failed. Check individual file details for specific error information.";
							}
							else if ( failed > 0 )
							{
								// Mixed results with failures - mark as failed but note what succeeded
								groupedProgress.Status = DownloadStatus.Failed;
								groupedProgress.StatusMessage = $"Partially completed ({completed} downloaded, {skipped} skipped, {failed} failed)";
								groupedProgress.ProgressPercentage = 100;

								// Set error message for partial failures
								var failedChildren = groupedProgress._childDownloads.Where(c => c.Status == DownloadStatus.Failed).ToList();
								if ( failedChildren.Any() )
								{
									var errorMessages = failedChildren
										.Where(c => !string.IsNullOrEmpty(c.ErrorMessage))
										.Select(c => {
											string fileName = "Unknown";
											if ( !string.IsNullOrEmpty(c.Url) )
											{
												try
												{
													fileName = System.IO.Path.GetFileName(new Uri(c.Url).AbsolutePath);
												}
												catch ( UriFormatException )
												{
													fileName = System.IO.Path.GetFileName(c.Url);
												}
											}
											return $"• {fileName}: {c.ErrorMessage}";
										})
										.ToList();

									if ( errorMessages.Any() )
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
								// All succeeded (completed or skipped)
								groupedProgress.Status = DownloadStatus.Completed;
								groupedProgress.StatusMessage = $"All files completed ({completed} downloaded, {skipped} skipped)";
								groupedProgress.ProgressPercentage = 100;
								groupedProgress.ErrorMessage = string.Empty; // Clear any previous error message
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

	public enum DownloadStatus
	{
		Pending,
		InProgress,
		Completed,
		Failed,
		Skipped
	}
}

