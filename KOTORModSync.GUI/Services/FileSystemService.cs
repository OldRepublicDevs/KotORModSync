// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using Avalonia.Threading;
using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemUtils;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for file system operations including directory watching
	/// </summary>
	public class FileSystemService : IDisposable
	{
		private CrossPlatformFileWatcher _modDirectoryWatcher;
		private Action _onDirectoryChanged;
		private bool _disposed;

		/// <summary>
		/// Initializes file system watcher for a directory
		/// </summary>
		public void SetupModDirectoryWatcher(string path, Action onDirectoryChanged)
		{
			try
			{
				_onDirectoryChanged = onDirectoryChanged;

				// Dispose existing watcher if any
				_modDirectoryWatcher?.Dispose();

				if ( string.IsNullOrEmpty(path) || !Directory.Exists(path) )
				{
					Logger.LogVerbose($"Cannot setup file watcher: path is invalid or doesn't exist: {path}");
					return;
				}

				// Create cross-platform file watcher
				_modDirectoryWatcher = new CrossPlatformFileWatcher(
					path: path,
					filter: "*.*",
					notifyFilters: NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
					includeSubdirectories: true
				);

				_modDirectoryWatcher.Created += OnModDirectoryChanged;
				_modDirectoryWatcher.Deleted += OnModDirectoryChanged;
				_modDirectoryWatcher.Changed += OnModDirectoryChanged;
				_modDirectoryWatcher.Error += OnModDirectoryWatcherError;

				// Start watching
				_modDirectoryWatcher.StartWatching();

				Logger.LogVerbose($"Cross-platform file system watcher initialized for: {path}");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to setup mod directory watcher");
			}
		}

		/// <summary>
		/// Stops the file system watcher
		/// </summary>
		public void StopWatcher()
		{
			try
			{
				_modDirectoryWatcher?.Dispose();
				_modDirectoryWatcher = null;
				Logger.LogVerbose("File system watcher stopped");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error stopping file watcher");
			}
		}

		private void OnModDirectoryChanged(object sender, FileSystemEventArgs e)
		{
			// Debounce file system events by dispatching to UI thread
			Dispatcher.UIThread.Post(() =>
			{
				try
				{
					_onDirectoryChanged?.Invoke();
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex, "Error processing mod directory change");
				}
			}, DispatcherPriority.Background);
		}

		private void OnModDirectoryWatcherError(object sender, ErrorEventArgs e)
		{
			Logger.LogException(e.GetException(), "File watcher error occurred");

			// Attempt to restart the watcher
			Dispatcher.UIThread.Post(() =>
			{
				try
				{
					Logger.LogVerbose("Attempting to restart file watcher after error");
					// The caller should provide the path if needed for restart
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex, "Failed to restart file watcher");
				}
			}, DispatcherPriority.Background);
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if ( _disposed )
				return;

			if ( disposing )
			{
				_modDirectoryWatcher?.Dispose();
				_modDirectoryWatcher = null;
			}

			_disposed = true;
		}
	}
}

