// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using JetBrains.Annotations;

using KOTORModSync.Core;

namespace KOTORModSync.Core.Services.Validation
{
    /// <summary>
    /// Caches path validation results so they can be displayed without re-running expensive VFS validation.
    /// Results are populated when Validate button is pressed, and can be manually refreshed.
    /// </summary>
    public static class PathValidationCache
    {
        private static readonly ConcurrentDictionary<string, PathValidationResult> s_cache = new ConcurrentDictionary<string, PathValidationResult>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets a cache key for a path/instruction combination
        /// </summary>
        private static string GetCacheKey(
            [CanBeNull] string path,
            [CanBeNull] Instruction instruction,
            [CanBeNull] ModComponent component)
        {
            if (string.IsNullOrEmpty(path) || instruction is null || component is null)
            {
                return string.Empty;
            }

            return $"{component.Guid}|{component.Instructions.IndexOf(instruction)}|{path}";
        }

        /// <summary>
        /// Gets a cached validation result, or null if not cached
        /// </summary>
        [CanBeNull]
        public static PathValidationResult GetCachedResult([CanBeNull] string path, [CanBeNull] Instruction instruction, [CanBeNull] ModComponent component)
        {
            string key = GetCacheKey(path, instruction, component);
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return s_cache.TryGetValue(key, out PathValidationResult result) ? result : null;
        }

        /// <summary>
        /// Stores a validation result in the cache
        /// </summary>
        public static void CacheResult([CanBeNull] string path, [CanBeNull] Instruction instruction, [CanBeNull] ModComponent component, [CanBeNull] PathValidationResult result)
        {
            string key = GetCacheKey(path, instruction, component);
            if (string.IsNullOrEmpty(key) || result is null)
            {
                return;
            }

            s_cache[key] = result;
        }

        /// <summary>
        /// Validates a path and caches the result. This uses VFS simulation.
        /// </summary>
        public static async Task<PathValidationResult> ValidateAndCacheAsync([CanBeNull] string path, [CanBeNull] Instruction instruction, [CanBeNull] ModComponent component)
        {
            if (component is null)
            {
                return new PathValidationResult
                {
                    StatusMessage = "⚠️ Context missing",
                    IsValid = false,
                };
            }

            PathValidationResult result = await DryRunValidator.ValidateInstructionPathDetailedAsync(path, instruction, component).ConfigureAwait(false);
            CacheResult(path, instruction, component, result);
            return result;
        }

        /// <summary>
        /// Clears all cached validation results
        /// </summary>
        public static void ClearCache()
        {
            s_cache.Clear();
        }

        /// <summary>
        /// Clears cached results for a specific component
        /// </summary>
        public static void ClearCacheForComponent([CanBeNull] ModComponent component)
        {
            if (component is null)
            {
                return;
            }

            string prefix = $"{component.Guid}|";
            var keysToRemove = new System.Collections.Generic.List<string>();
            foreach (string key in s_cache.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (string key in keysToRemove)
            {
                s_cache.TryRemove(key, out _);
            }
        }
    }
}

