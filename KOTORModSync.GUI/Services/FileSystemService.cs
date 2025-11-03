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
        private readonly Action<string> _onDirectoryChanged;
        private bool _disposed;
        private Timer _debounceTimer;
        private readonly object _timerLock = new object();
        private const int DebounceDelayMs = 2000;
        private string _lastChangedFile;

        // TEMPORARY: Set to false to disable file watching
        private const bool _watcherEnabled = false;

        public static void SetupModDirectoryWatcher(string path, Action<string> onDirectoryChanged)
        {
            // TEMPORARY: File watcher is disabled
            if (!_watcherEnabled)
            {
                Logger.LogVerbose("File watcher is disabled");
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
                Logger.LogVerbose("File system watcher stopped");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error stopping file watcher");
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

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
