// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.FileSystemUtils
{
	/// <summary>
	/// Cross-platform file system watcher that works on Windows, Linux, and macOS.
	/// Uses platform-specific APIs for optimal performance.
	/// </summary>
	public class CrossPlatformFileWatcher : IDisposable
	{
		private readonly string _path;
		private readonly string _filter;
		private readonly NotifyFilters _notifyFilters;
		private readonly bool _includeSubdirectories;
		private readonly object _lockObject = new object();

		private FileSystemWatcher _windowsWatcher;
		private Task _pollingTask;
		private CancellationTokenSource _cancellationTokenSource;
		private bool _disposed;
		private DateTime _lastPollTime;
		private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(1);

		public event FileSystemEventHandler Created;
		public event FileSystemEventHandler Deleted;
		public event FileSystemEventHandler Changed;
		public event RenamedEventHandler Renamed;
		public event ErrorEventHandler Error;

		public bool EnableRaisingEvents { get; set; }

		public CrossPlatformFileWatcher(
			string path,
			string filter = "*.*",
			NotifyFilters notifyFilters = NotifyFilters.FileName | NotifyFilters.LastWrite,
			bool includeSubdirectories = false
		)
		{
			_path = path ?? throw new ArgumentNullException(nameof(path));
			_filter = filter ?? "*.*";
			_notifyFilters = notifyFilters;
			_includeSubdirectories = includeSubdirectories;
			_lastPollTime = DateTime.Now;
		}

		public void StartWatching()
		{
			lock ( _lockObject )
			{
				if ( _disposed )
					throw new ObjectDisposedException(nameof(CrossPlatformFileWatcher));

				if ( EnableRaisingEvents )
					return;

				EnableRaisingEvents = true;

				// Choose platform-specific implementation
				if ( Utility.Utility.GetOperatingSystem() == OSPlatform.Windows )
					StartWindowsWatcher();
				else
					StartPollingWatcher();
			}
		}

		public void StopWatching()
		{
			lock ( _lockObject )
			{
				if ( _disposed )
					return;

				EnableRaisingEvents = false;

				if ( _windowsWatcher != null )
				{
					_windowsWatcher.EnableRaisingEvents = false;
					_windowsWatcher.Dispose();
					_windowsWatcher = null;
				}

				_cancellationTokenSource?.Cancel();
				_pollingTask?.Wait(TimeSpan.FromSeconds(5));
				_cancellationTokenSource?.Dispose();
				_cancellationTokenSource = null;
				_pollingTask = null;
			}
		}

		private void StartWindowsWatcher()
		{
			try
			{
				_windowsWatcher = new FileSystemWatcher
				{
					Path = _path,
					Filter = _filter,
					NotifyFilter = _notifyFilters,
					IncludeSubdirectories = _includeSubdirectories,
					EnableRaisingEvents = true
				};

				_windowsWatcher.Created += OnWindowsWatcherCreated;
				_windowsWatcher.Deleted += OnWindowsWatcherDeleted;
				_windowsWatcher.Changed += OnWindowsWatcherChanged;
				_windowsWatcher.Renamed += OnWindowsWatcherRenamed;
				_windowsWatcher.Error += OnWindowsWatcherError;

				Logger.LogVerbose($"Cross-platform file watcher started for Windows: {_path}");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to start Windows file watcher, falling back to polling");
				StartPollingWatcher();
			}
		}

		private void StartPollingWatcher()
		{
			try
			{
				_cancellationTokenSource = new CancellationTokenSource();
				_pollingTask = Task.Run(() => PollingLoop(_cancellationTokenSource.Token));

				Logger.LogVerbose($"Cross-platform file watcher started with polling: {_path}");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to start polling file watcher");
				throw;
			}
		}

		private async Task PollingLoop(CancellationToken cancellationToken)
		{
			var previousFiles = new HashSet<string>();
			var currentFiles = new HashSet<string>();

			// Initial scan
			try
			{
				await ScanDirectory(previousFiles, cancellationToken);
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "Error during initial directory scan");
				OnError(new ErrorEventArgs(ex));
				return;
			}

			while ( !cancellationToken.IsCancellationRequested && EnableRaisingEvents )
			{
				try
				{
					await Task.Delay(_pollingInterval, cancellationToken);

					currentFiles.Clear();
					await ScanDirectory(currentFiles, cancellationToken);

					// Check for deleted files
					foreach ( string file in previousFiles )
					{
						if ( !currentFiles.Contains(file) )
							OnDeleted(new FileSystemEventArgs(WatcherChangeTypes.Deleted, Path.GetDirectoryName(file) ?? string.Empty, Path.GetFileName(file)));
					}

					// Check for created files
					foreach ( string file in currentFiles )
					{
						if ( !previousFiles.Contains(file) )
							OnCreated(new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(file) ?? string.Empty, Path.GetFileName(file)));
					}

					// Check for changed files (simplified - just check modification time)
					foreach ( string file in currentFiles )
					{
						if ( !previousFiles.Contains(file) )
							continue;
						try
						{
							var fileInfo = new FileInfo(file);
							if ( fileInfo.LastWriteTime > _lastPollTime )
								OnChanged(new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(file) ?? string.Empty, Path.GetFileName(file)));
						}
						catch ( Exception ex )
						{
							await Logger.LogVerboseAsync($"Error checking file modification time for {file}: {ex.Message}");
						}
					}

					// Update state for next iteration
					previousFiles.Clear();
					foreach ( string file in currentFiles )
					{
						_ = previousFiles.Add(file);
					}

					_lastPollTime = DateTime.Now;
				}
				catch ( OperationCanceledException )
				{
					break;
				}
				catch ( Exception ex )
				{
					await Logger.LogExceptionAsync(ex, "Error in polling loop");
					OnError(new ErrorEventArgs(ex));

					// Wait a bit before retrying to avoid rapid error loops
					try
					{
						await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
					}
					catch ( OperationCanceledException )
					{
						break;
					}
				}
			}
		}

		private async Task ScanDirectory(HashSet<string> fileSet, CancellationToken cancellationToken)
		{
			if ( !Directory.Exists(_path) )
				return;

			try
			{
				SearchOption searchOption = _includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
				string[] files = Directory.GetFiles(_path, _filter, searchOption);

				foreach ( string file in files )
				{
					cancellationToken.ThrowIfCancellationRequested();
					_ = fileSet.Add(file);
				}

				await Task.CompletedTask;
			}
			catch ( Exception ex )
			{
				await Logger.LogVerboseAsync($"Error scanning directory {_path}: {ex.Message}");
				throw;
			}
		}

		#region Windows Watcher Event Handlers

		private void OnWindowsWatcherCreated(object sender, FileSystemEventArgs e) => OnCreated(e);
		private void OnWindowsWatcherDeleted(object sender, FileSystemEventArgs e) => OnDeleted(e);
		private void OnWindowsWatcherChanged(object sender, FileSystemEventArgs e) => OnChanged(e);
		private void OnWindowsWatcherRenamed(object sender, RenamedEventArgs e) => OnRenamed(e);
		private void OnWindowsWatcherError(object sender, ErrorEventArgs e) => OnError(e);

		#endregion

		#region Event Invokers

		private void OnCreated(FileSystemEventArgs e)
		{
			if ( EnableRaisingEvents )
				Created?.Invoke(this, e);
		}

		private void OnDeleted(FileSystemEventArgs e)
		{
			if ( EnableRaisingEvents )
				Deleted?.Invoke(this, e);
		}

		private void OnChanged(FileSystemEventArgs e)
		{
			if ( EnableRaisingEvents )
				Changed?.Invoke(this, e);
		}

		private void OnRenamed(RenamedEventArgs e)
		{
			if ( EnableRaisingEvents )
				Renamed?.Invoke(this, e);
		}

		private void OnError(ErrorEventArgs e) => Error?.Invoke(this, e);

		#endregion

		#region IDisposable Implementation

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			lock ( _lockObject )
			{
				if ( _disposed )
					return;

				_disposed = true;

				if ( !disposing )
					return;
				EnableRaisingEvents = false;
				StopWatching();
			}
		}

		~CrossPlatformFileWatcher() => Dispose(false);

		#endregion
	}
}
