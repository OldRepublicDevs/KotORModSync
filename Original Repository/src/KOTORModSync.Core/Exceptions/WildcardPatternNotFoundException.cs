// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

namespace KOTORModSync.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when wildcard pattern matching fails to find any matching files.
    /// This typically occurs when instruction patterns don't match the actual file structure,
    /// often due to nested archive folders or incorrect path patterns.
    /// </summary>
    public class WildcardPatternNotFoundException : Exception
    {
        /// <summary>
        /// The wildcard patterns that failed to match any files.
        /// </summary>
        [NotNull]
        public IReadOnlyList<string> Patterns { get; }

        /// <summary>
        /// The component name where the pattern matching failed.
        /// </summary>
        [CanBeNull]
        public string ComponentName { get; }

        public WildcardPatternNotFoundException(
            [NotNull] IEnumerable<string> patterns,
            [CanBeNull] string componentName = null)
            : base(BuildMessage(patterns, componentName))
        {
            Patterns = patterns?.ToList() ?? new List<string>();
            ComponentName = componentName;
        }

        public WildcardPatternNotFoundException(
            [NotNull] string pattern,
            [CanBeNull] string componentName = null)
            : this(new[] { pattern }, componentName)
        {
        }

        public WildcardPatternNotFoundException(
            [NotNull] IEnumerable<string> patterns,
            [CanBeNull] string componentName,
            [CanBeNull] Exception innerException)
            : base(BuildMessage(patterns, componentName), innerException)
        {
            Patterns = patterns?.ToList() ?? new List<string>();
            ComponentName = componentName;
        }

        private static string BuildMessage([NotNull] IEnumerable<string> patterns, [CanBeNull] string componentName)
        {
            List<string> patternList = patterns?.ToList() ?? new List<string>();
            string patternsStr = string.Join(", ", patternList);

            string message = $"Could not find any files matching the pattern in the 'Source' path on disk! Got [{patternsStr}]";

            if (!string.IsNullOrWhiteSpace(componentName))
            {
                message = $"[{componentName}] {message}";
            }

            return message;
        }
    }
}
