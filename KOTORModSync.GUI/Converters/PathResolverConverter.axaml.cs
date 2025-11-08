// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Data.Converters;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
    public partial class PathResolverConverter : IValueConverter
    {
        private static int _convertCallCount = 0;
        private static int _resolvePathCallCount = 0;
        private static readonly object _lockObject = new object();

        // Cache for resolved paths - thread-safe
        private static readonly ConcurrentDictionary<string, string> _resolvedPathCache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        // Track pending resolutions to avoid duplicate work
        private static readonly ConcurrentDictionary<string, Task<string>> _pendingResolutions = new ConcurrentDictionary<string, Task<string>>(StringComparer.Ordinal);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            lock (_lockObject)
            {
                _convertCallCount++;
            }

            // Safety check: if we're getting too many calls, something is wrong
            if (_convertCallCount > 100)
            {
                Logger.LogError($"[PathResolverConverter.Convert] INFINITE LOOP DETECTED! Call count: {_convertCallCount}, returning value as-is to break the loop");
                return value?.ToString() ?? string.Empty;
            }

            Logger.LogVerbose($"[PathResolverConverter.Convert] Call #{_convertCallCount} - Value type: {value?.GetType().Name ?? "null"}, TargetType: {targetType?.Name ?? "null"}");

            // Expand IEnumerable<string> to show actual items instead of type name
            string valueDisplay;
            if (value is IEnumerable<string> enumerablePaths)
            {
                valueDisplay = $"[{string.Join(", ", enumerablePaths.Select(p => $"'{p}'"))}]";
            }
            else if (value is null)
            {
                valueDisplay = "null";
            }
            else
            {
                valueDisplay = value.ToString();
            }
            Logger.LogVerbose($"[PathResolverConverter.Convert] Call #{_convertCallCount} - Value: {valueDisplay}");

            if (value is null)
            {
                Logger.LogVerbose($"[PathResolverConverter.Convert] Call #{_convertCallCount} - Value is null, returning empty string");
                return string.Empty;
            }

            if (value is string singlePath)
            {
                Logger.LogVerbose($"[PathResolverConverter.Convert] Call #{_convertCallCount} - Processing single path: '{singlePath}' (length: {singlePath.Length})");
                string result = ResolvePath(singlePath);
                Logger.LogVerbose($"[PathResolverConverter.Convert] Call #{_convertCallCount} - Single path result: '{result}' (length: {result.Length})");
                return result;
            }

            if (value is IEnumerable<string> pathList)
            {
                string[] pathArray = pathList.ToArray();
                Logger.LogVerbose($"[PathResolverConverter.Convert] Call #{_convertCallCount} - Processing path list with {pathArray.Length} items");
                for (int i = 0; i < pathArray.Length; i++)
                {
                    Logger.LogVerbose($"[PathResolverConverter.Convert] Call #{_convertCallCount} - Path[{i}]: '{pathArray[i]}' (length: {pathArray[i]?.Length ?? 0})");
                }

                Logger.LogVerbose($"[PathResolverConverter.Convert] Call #{_convertCallCount} - About to call ResolvePath on each path in the list");
                var resolvedPaths = pathArray.Select(ResolvePath).ToList();
                string result = string.Join(Environment.NewLine, resolvedPaths);
                Logger.LogVerbose($"[PathResolverConverter.Convert] Call #{_convertCallCount} - Path list result: '{result}' (length: {result.Length})");
                return result;
            }

            Logger.LogVerbose($"[PathResolverConverter.Convert] Call #{_convertCallCount} - Unknown value type, calling ToString(): '{value}'");
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        [NotNull]
        private static string ResolvePath([CanBeNull] string path)
        {
            lock (_lockObject)
            {
                _resolvePathCallCount++;
            }

            // Safety check: if we're getting too many calls, something is wrong
            if (_resolvePathCallCount > 100)
            {
                Logger.LogError($"[PathResolverConverter.ResolvePath] INFINITE LOOP DETECTED! Call count: {_resolvePathCallCount}, returning path as-is to break the loop");
                return path ?? string.Empty;
            }

            Logger.LogVerbose($"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Input path: '{path}' (length: {path?.Length ?? 0})");

            if (string.IsNullOrEmpty(path))
            {
                Logger.LogVerbose($"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Path is null/empty, returning empty string");
                return string.Empty;
            }

            // Safety check: prevent processing extremely long paths that could cause stack overflow
            if (path.Length > 10000)
            {
                Logger.LogVerbose($"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Path too long ({path.Length} chars), returning as-is");
                return path; // Path too long, return as-is to prevent issues
            }

            // Check if path is already resolved (contains actual paths, not placeholders)
            // This prevents infinite recursion when the converter is used in two-way bindings
            try
            {
                bool hasModDir = path.Contains("<<modDirectory>>");
                bool hasKotorDir = path.Contains("<<kotorDirectory>>");
                Logger.LogVerbose($"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Contains modDirectory: {hasModDir}, Contains kotorDirectory: {hasKotorDir}");

                if (!hasModDir && !hasKotorDir)
                {
                    Logger.LogVerbose($"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Path already resolved, returning as-is");
                    return path; // Already resolved, don't process again
                }
            }
            catch (Exception ex)
            {
                // If Contains() itself causes issues, return the path as-is
                Logger.LogVerbose($"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Exception in Contains check: {ex.Message}, returning path as-is");
                return path;
            }

            // Check cache first - return immediately if already resolved
            if (_resolvedPathCache.TryGetValue(path, out string cachedResult))
            {
                Logger.LogVerbose($"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Found cached result: '{cachedResult}'");
                return cachedResult;
            }

            // Check if resolution is already in progress
            if (_pendingResolutions.TryGetValue(path, out Task<string> pendingTask))
            {
                Logger.LogVerbose($"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Resolution already in progress, returning unresolved path for now");
                // Return unresolved path immediately - next evaluation will use cached result
                // Continue the async task in background
                _ = ContinuePendingResolution(path, pendingTask);
                return path;
            }

            // Capture config values on UI thread before starting background task
            // This ensures safe access to MainConfig properties
            string sourcePath = MainConfig.SourcePath?.FullName;
            string destPath = MainConfig.DestinationPath?.FullName;

            // Start async resolution in background, return unresolved path immediately
            Logger.LogVerbose($"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Starting async resolution, returning unresolved path");
            Task<string> resolutionTask = ResolvePathAsync(path, sourcePath, destPath);
            _pendingResolutions.TryAdd(path, resolutionTask);
            _ = ContinuePendingResolution(path, resolutionTask);

            // Return unresolved path immediately so UI doesn't block
            return path;
        }

        /// <summary>
        /// Continues a pending resolution task and updates the cache when complete.
        /// Runs on background thread to avoid blocking.
        /// </summary>
        private static async Task ContinuePendingResolution(string path, Task<string> resolutionTask)
        {
            try
            {
                // Wait for resolution to complete (this is safe - we're already off UI thread)
                string resolved = await resolutionTask;

                // Update cache
                _resolvedPathCache.TryAdd(path, resolved);

                await Logger.LogVerboseAsync($"[PathResolverConverter] Background resolution completed for '{path}' -> '{resolved}'");
            }
            catch (Exception ex)
            {
                await Logger.LogVerboseAsync($"[PathResolverConverter] Error in background resolution for '{path}': {ex.Message}");
                // On error, cache the original path to prevent repeated failures
                _resolvedPathCache.TryAdd(path, path);
            }
            finally
            {
                // Clean up pending task tracking
                _pendingResolutions.TryRemove(path, out _);
            }
        }

        /// <summary>
        /// Resolves a path asynchronously on a background thread.
        /// This performs the actual path resolution work without blocking the UI thread.
        /// </summary>
        /// <param name="path">The path to resolve</param>
        /// <param name="sourcePath">The mod directory path (captured on UI thread)</param>
        /// <param name="destPath">The KOTOR directory path (captured on UI thread)</param>
        private static Task<string> ResolvePathAsync(string path, string sourcePath, string destPath)
        {
            return Task.Run(() =>
            {
                try
                {
                    Logger.LogVerbose($"[PathResolverConverter.ResolvePathAsync] Starting background resolution for: '{path}'");

                    if (string.IsNullOrEmpty(sourcePath) && string.IsNullOrEmpty(destPath))
                    {
                        Logger.LogVerbose($"[PathResolverConverter.ResolvePathAsync] Both config paths are null, returning path as-is");
                        return path;
                    }

                    // Perform the resolution (simple string replacement, safe on background thread)
                    // Values were captured on UI thread before calling this method
                    string result = path;
                    if (!string.IsNullOrEmpty(sourcePath))
                    {
                        result = result.Replace("<<modDirectory>>", sourcePath);
                    }
                    if (!string.IsNullOrEmpty(destPath))
                    {
                        result = result.Replace("<<kotorDirectory>>", destPath);
                    }

                    Logger.LogVerbose($"[PathResolverConverter.ResolvePathAsync] Resolution completed: '{path}' -> '{result}'");
                    return result;
                }
                catch (Exception ex)
                {
                    Logger.LogVerbose($"[PathResolverConverter.ResolvePathAsync] Exception during resolution: {ex.Message}, returning path as-is");
                    return path;
                }
            });
        }
    }
}
