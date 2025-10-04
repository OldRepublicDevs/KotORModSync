using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

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
				_status = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsCompleted));
				OnPropertyChanged(nameof(IsFailed));
				OnPropertyChanged(nameof(IsInProgress));
				OnPropertyChanged(nameof(StatusIcon));
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

		// Computed properties
		public bool IsCompleted => Status == DownloadStatus.Completed;
		public bool IsFailed => Status == DownloadStatus.Failed;
		public bool IsInProgress => Status == DownloadStatus.InProgress;

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
		/// Gets all log entries for this download.
		/// </summary>
		public string GetLogs()
		{
			lock ( _logLock )
			{
				if ( _logs.Count == 0 )
					return "No logs available for this download.";

				var sb = new StringBuilder();
				foreach ( string log in _logs )
				{
					_ = sb.AppendLine(log);
				}
				return sb.ToString();
			}
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

