// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Data.Converters;
using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
	public partial class StringGuidListConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// CRITICAL: This converter should NOT be used on file paths or Source properties
			// It's designed for GUID lists, not file path lists
			if (value is List<string> stringList)
			{
				// Check if this looks like file paths (contains backslashes, file extensions, etc.)
				bool looksLikeFilePaths = stringList.Any(s =>
					s.Contains("\\") || s.Contains("/") || s.Contains(".") ||
					s.Contains("<<modDirectory>>") || s.Contains("<<kotorDirectory>>"));

				if (looksLikeFilePaths)
				{
					Logger.LogError($"[StringGuidListConverter.Convert] MISUSE DETECTED! This converter is being used on file paths: [{string.Join(", ", stringList)}]. This will cause infinite recursion. Returning value as-is.");
					return value; // Return as-is to prevent infinite recursion
				}
			}

			// Prevent infinite recursion by checking if value is already a Guid collection
			// or if it's not a string collection (which would indicate it's already been processed)
			if (value is null || value is IEnumerable<Guid> || !(value is IEnumerable<string>))
			{
				return value; // Already converted or invalid, return as-is
			}

			return value is List<string> stringList2
				? stringList2.Select(s => Guid.Parse(Serializer.FixGuidString(s))).ToList()
				: AvaloniaProperty.UnsetValue;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// CRITICAL: This converter should NOT be used on file paths or Source properties
			// It's designed for GUID lists, not file path lists
			if (value is List<Guid> guidList)
			{
				// Check if any GUIDs are empty (which might indicate misuse)
				bool hasEmptyGuids = guidList.Any(g => g == Guid.Empty);

				if (hasEmptyGuids)
				{
					Logger.LogError($"[StringGuidListConverter.ConvertBack] POTENTIAL MISUSE DETECTED! Converting empty GUIDs: [{string.Join(", ", guidList)}]. This might be from file path conversion. Returning value as-is.");
					return value; // Return as-is to prevent infinite recursion
				}
			}

			// Prevent infinite recursion by checking if value is already a string collection
			// or if it's not a Guid collection (which would indicate it's already been processed)
			if (value is null || value is IEnumerable<string> || !(value is IEnumerable<Guid>))
			{
				return value; // Already converted or invalid, return as-is
			}

			return value is List<Guid> guidList2
				? guidList2.Select(guid => guid.ToString()).ToList()
				: AvaloniaProperty.UnsetValue;
		}
	}
}
