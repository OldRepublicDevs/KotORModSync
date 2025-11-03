// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Avalonia.Data.Converters;

namespace KOTORModSync.Converters
{
    /// <summary>
    /// Converter that creates a tooltip showing both resolved and unresolved paths.
    /// </summary>
    public class UnresolvedPathTooltipConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
            {
                return string.Empty;
            }

            // First value is the Source (unresolved paths), second is the resolved paths
            object sourceValue = values[0];
            object resolvedValue = values[1];

            var unresolvedPaths = new List<string>();
            if (sourceValue is IEnumerable<string> sourceList)
            {
                unresolvedPaths.AddRange(sourceList.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
            else if (sourceValue is string sourceStr && !string.IsNullOrWhiteSpace(sourceStr))
            {
                unresolvedPaths.Add(sourceStr);
            }

            var resolvedPaths = new List<string>();
            if (resolvedValue is string resolvedStr && !string.IsNullOrWhiteSpace(resolvedStr))
            {
                // Split by newlines in case there are multiple resolved paths
                string[] lines = resolvedStr.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                resolvedPaths.AddRange(lines.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
            else if (resolvedValue is IEnumerable<string> resolvedList)
            {
                resolvedPaths.AddRange(resolvedList.Where(p => !string.IsNullOrWhiteSpace(p)));
            }

            if (unresolvedPaths.Count == 0)
            {
                return string.Empty;
            }

            var tooltipParts = new List<string>();

            // Add unresolved paths
            tooltipParts.Add("Original path:");
            foreach (string path in unresolvedPaths)
            {
                tooltipParts.Add($"  {path}");
            }

            // Add resolved paths if different
            if (resolvedPaths.Count > 0 && !ArePathsEqual(unresolvedPaths, resolvedPaths))
            {
                tooltipParts.Add(string.Empty);
                tooltipParts.Add("Resolved path:");
                foreach (string path in resolvedPaths)
                {
                    tooltipParts.Add($"  {path}");
                }
            }

            return string.Join(Environment.NewLine, tooltipParts);
        }

        private static bool ArePathsEqual(List<string> list1, List<string> list2)
        {
            if (list1.Count != list2.Count)
            {
                return false;
            }

            for (int i = 0; i < list1.Count; i++)
            {
                if (!string.Equals(list1[i], list2[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

