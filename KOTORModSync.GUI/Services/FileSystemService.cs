



using System;
using System.IO;
using System.Threading;
using Avalonia.Threading;
using KOTORModSync.Converters;
using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemUtils;

namespace KOTORModSync.Services
{
	
	
	
	public class FileSystemService : IDisposable
	{
		private CrossPlatformFileWatcher _modDirectoryWatcher;
		private Action _onDirectoryChanged;
		private bool _disposed;
		private Timer _debounceTimer;
		private readonly object _timerLock = new object();
		private const int DebounceDelayMs = 2000; 

		
		
		
		public void SetupModDirectoryWatcher(string path, Action onDirectoryChanged)
		{
			try
			{
				_onDirectoryChanged = onDirectoryChanged;

				
				_modDirectoryWatcher?.Dispose();

				if ( string.IsNullOrEmpty(path) || !Directory.Exists(path) )
				{
					Logger.LogVerbose($"Cannot setup file watcher: path is invalid or doesn't exist: {path}");
					return;
				}

				
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

				
				_modDirectoryWatcher.StartWatching();

				Logger.LogVerbose($"Cross-platform file system watcher initialized for: {path}");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to setup mod directory watcher");
			}
		}

		
		
		
		public void StopWatcher()
		{
			try
			{
				
				lock ( _timerLock )
				{
					_debounceTimer?.Dispose();
					_debounceTimer = null;
				}

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
		
		
		lock ( _timerLock )
		{
			
			_debounceTimer?.Dispose();

			
			_debounceTimer = new Timer(_ =>
			{
				
				Dispatcher.UIThread.Post(() =>
				{
					try
					{
						Logger.Log($"[File Watcher] Detected changes in mod directory ({e.ChangeType}: {Path.GetFileName(e.FullPath)}), running validation...");

						
						NamespacesIniOptionConverter.InvalidateCache();

						_onDirectoryChanged?.Invoke();
					}
					catch ( Exception ex )
					{
						Logger.LogException(ex, "Error processing mod directory change");
					}
				}, DispatcherPriority.Background);
			}, null, DebounceDelayMs, Timeout.Infinite);
		}
	}

		private void OnModDirectoryWatcherError(object sender, ErrorEventArgs e)
		{
			Logger.LogException(e.GetException(), "File watcher error occurred");

			
			Dispatcher.UIThread.Post(() =>
			{
				try
				{
					Logger.LogVerbose("Attempting to restart file watcher after error");
					
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
				
				lock ( _timerLock )
				{
					_debounceTimer?.Dispose();
					_debounceTimer = null;
				}

				_modDirectoryWatcher?.Dispose();
				_modDirectoryWatcher = null;
			}

			_disposed = true;
		}
	}
}

