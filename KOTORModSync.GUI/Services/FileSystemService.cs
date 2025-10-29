// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

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
		private Action<string> _onDirectoryChanged;
		private bool _disposed;
		private Timer _debounceTimer;
		private readonly object _timerLock = new object();
		private const int DebounceDelayMs = 2000;
		private string _lastChangedFile;
		
		// TEMPORARY: Set to false to disable file watching
		private const bool _watcherEnabled = false;

		public void SetupModDirectoryWatcher( string path, Action<string> onDirectoryChanged )
		{
			// TEMPORARY: File watcher is disabled
			if (!_watcherEnabled)
			{
				Logger.LogVerbose( "File watcher is disabled" );
			}
		}

		public void StopWatcher()
		{
			try
			{

				lock (_timerLock)
				{
					_debounceTimer?.Dispose();
					_debounceTimer = null;
				}

				_modDirectoryWatcher?.Dispose();
				_modDirectoryWatcher = null;
				Logger.LogVerbose( "File system watcher stopped" );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error stopping file watcher" );
			}
		}

		private void OnModDirectoryChanged( object sender, FileSystemEventArgs e )
		{
			// Store the changed file path for the debounced callback
			_lastChangedFile = e.FullPath;

			lock (_timerLock)
			{

				_debounceTimer?.Dispose();

				_debounceTimer = new Timer( _ =>
				{
					string changedFile = _lastChangedFile;

					Dispatcher.UIThread.Post( () =>
					{
						try
						{
							Logger.Log( $"[File Watcher] Detected changes in mod directory ({e.ChangeType}: {Path.GetFileName( changedFile )}), running validation..." );

							NamespacesIniOptionConverter.InvalidateCache();

							_onDirectoryChanged?.Invoke( changedFile );
						}
						catch (Exception ex)
						{
							Logger.LogException( ex, "Error processing mod directory change" );
						}
					}, DispatcherPriority.Background );
				}, null, DebounceDelayMs, Timeout.Infinite );
			}
		}

		private void OnModDirectoryWatcherError( object sender, ErrorEventArgs e )
		{
			Logger.LogException( e.GetException(), "File watcher error occurred" );

			Dispatcher.UIThread.Post( () =>
			{
				try
				{
					Logger.LogVerbose( "Attempting to restart file watcher after error" );

				}
				catch (Exception ex)
				{
					Logger.LogException( ex, "Failed to restart file watcher" );
				}
			}, DispatcherPriority.Background );
		}

		public void Dispose()
		{
			Dispose( disposing: true );
			GC.SuppressFinalize( this );
		}

		protected virtual void Dispose( bool disposing )
		{
			if (_disposed)
				return;

			if (disposing)
			{

				lock (_timerLock)
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