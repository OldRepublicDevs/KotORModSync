// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Avalonia.Threading;

using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Services.Validation;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Services
{
    /// <summary>
    /// Watches file system for the current component and updates validation state in real-time.
    /// Only watches specific files from ResourceRegistry, not the entire directory.
    /// </summary>
    public class ComponentValidationWatcherService : IDisposable
    {
        private ModComponent _currentComponent;
        private readonly List<CrossPlatformFileWatcher> _fileWatchers = new List<CrossPlatformFileWatcher>();
        private readonly HashSet<string> _watchedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lockObject = new object();
        private bool _disposed;

        public event EventHandler<ModComponent> ValidationStateChanged;

        public void SetCurrentComponent(ModComponent component)
        {
            lock (_lockObject)
            {
                if (_currentComponent == component)
                {
                    return;
                }

                // Stop watching previous component
                StopWatching();

                _currentComponent = component;

                // Start watching new component
                if (_currentComponent != null && MainConfig.SourcePath != null)
                {
                    StartWatching();
                }
            }
        }

        private void StartWatching()
        {
            if (_currentComponent == null || MainConfig.SourcePath == null || !MainConfig.SourcePath.Exists)
            {
                return;
            }

            try
            {
                // Collect all files from ResourceRegistry
                var filesToWatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (_currentComponent.ResourceRegistry != null)
                {
                    foreach (KeyValuePair<string, ResourceMetadata> resource in _currentComponent.ResourceRegistry)
                    {
                        if (resource.Value?.Files != null)
                        {
                            foreach (string fileName in resource.Value.Files.Keys)
                            {
                                if (!string.IsNullOrWhiteSpace(fileName))
                                {
                                    filesToWatch.Add(fileName);
                                }
                            }
                        }
                    }
                }

                if (filesToWatch.Count == 0)
                {
                    Logger.LogVerbose($"[ComponentValidationWatcher] No files to watch for component: {_currentComponent.Name}");
                    return;
                }

                _watchedFiles.Clear();
                _watchedFiles.UnionWith(filesToWatch);

                string modDirectory = MainConfig.SourcePath.FullName;

                // Create a single watcher for the mod directory, watching only the specific files
                // We'll filter events to only process files in our watch list
                var watcher = new CrossPlatformFileWatcher(
                    path: modDirectory,
                    filter: "*.*",  // We filter by filename in the event handler
                    includeSubdirectories: false  // Only watch the top-level directory
                );

                watcher.Created += OnFileSystemChanged;
                watcher.Deleted += OnFileSystemChanged;
                watcher.Changed += OnFileSystemChanged;
                watcher.Renamed += OnFileSystemRenamed;

                watcher.StartWatching();
                _fileWatchers.Add(watcher);

                Logger.LogVerbose($"[ComponentValidationWatcher] Started watching {filesToWatch.Count} file(s) for component: {_currentComponent.Name}");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[ComponentValidationWatcher] Failed to start file watcher");
            }
        }

        private void StopWatching()
        {
            lock (_lockObject)
            {
                foreach (CrossPlatformFileWatcher watcher in _fileWatchers)
                {
                    try
                    {
                        watcher.Created -= OnFileSystemChanged;
                        watcher.Deleted -= OnFileSystemChanged;
                        watcher.Changed -= OnFileSystemChanged;
                        watcher.Renamed -= OnFileSystemRenamed;

                        watcher.StopWatching();
                        watcher.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, "[ComponentValidationWatcher] Error stopping file watcher");
                    }
                }

                _fileWatchers.Clear();
                _watchedFiles.Clear();

                if (_fileWatchers.Count > 0)
                {
                    Logger.LogVerbose("[ComponentValidationWatcher] Stopped watching");
                }
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            if (_currentComponent == null)
            {
                return;
            }

            // Fast check: only process files we're actually watching
            if (!IsFileRelevantToComponent(e.Name))
            {
                return;
            }

            Logger.LogVerbose($"[ComponentValidationWatcher] File system change detected: {e.ChangeType} - {e.Name}");

            // Revalidate the component
            _ = RevalidateComponentAsync();
        }

        private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
        {
            if (_currentComponent == null)
            {
                return;
            }

            // Fast check: only process files we're actually watching
            bool oldRelevant = IsFileRelevantToComponent(e.OldName);
            bool newRelevant = IsFileRelevantToComponent(e.Name);

            if (!oldRelevant && !newRelevant)
            {
                return;
            }

            Logger.LogVerbose($"[ComponentValidationWatcher] File renamed: {e.OldName} -> {e.Name}");

            // Revalidate the component
            _ = RevalidateComponentAsync();
        }

        private bool IsFileRelevantToComponent(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            // Only check against the watched files list (ResourceRegistry files)
            // This is much faster than checking all instructions
            return _watchedFiles.Contains(fileName) || _watchedFiles.Contains(Path.GetFileName(fileName));
        }

        private async Task RevalidateComponentAsync()
        {
            if (_currentComponent == null)
            {
                return;
            }

            try
            {
                // Validate all instructions in this component
                foreach (Instruction instruction in _currentComponent.Instructions)
                {
                    if (instruction.Source != null)
                    {
                        foreach (string sourcePath in instruction.Source)
                        {
                            if (!string.IsNullOrWhiteSpace(sourcePath))
                            {
                                await PathValidationCache.ValidateAndCacheAsync(
                                    sourcePath, instruction, _currentComponent).ConfigureAwait(false);
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(instruction.Destination))
                    {
                        await PathValidationCache.ValidateAndCacheAsync(
                            instruction.Destination, instruction, _currentComponent).ConfigureAwait(false);
                    }
                }

                // Notify that validation state has changed
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ValidationStateChanged?.Invoke(this, _currentComponent);
                });
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex, "[ComponentValidationWatcher] Error revalidating component");
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (_lockObject)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                if (disposing)
                {
                    StopWatching();
                }
            }
        }
    }
}

