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
	public partial class ConditionalGuidListConverter : IValueConverter
	{
		public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
		{
			// This converter should only be used with a parameter that indicates the Action type
			// or we need to get the Action from the binding context
			if (value is List<string> stringList)
			{
				// Check if this looks like file paths (contains backslashes, file extensions, etc.)
				bool looksLikeFilePaths = stringList.Any( s =>
					s.Contains( "\\" ) || s.Contains( "/" ) || s.Contains( "." ) ||
					s.Contains( "<<modDirectory>>" ) || s.Contains( "<<kotorDirectory>>" ) );

				if (looksLikeFilePaths)
				{
					// This is file paths, not GUIDs - return UnsetValue to avoid writing back/clearing the source
					Logger.LogVerbose( $"[ConditionalGuidListConverter.Convert] Detected file paths, returning UnsetValue to avoid write-back. Paths: [{string.Join( ", ", stringList )}]" );
					return AvaloniaProperty.UnsetValue;
				}

				// This looks like GUID strings, convert them
				try
				{
					var guidList = stringList.Select( s => Guid.Parse( Serializer.FixGuidString( s ) ) ).ToList();
					Logger.LogVerbose( $"[ConditionalGuidListConverter.Convert] Converted {stringList.Count} strings to GUIDs" );
					return guidList;
				}
				catch (Exception ex)
				{
					Logger.LogError( $"[ConditionalGuidListConverter.Convert] Failed to parse GUIDs: {ex.Message}. Input: [{string.Join( ", ", stringList )}]" );
					// Do not update target/source when parsing fails
					return AvaloniaProperty.UnsetValue;
				}
			}

			// If it's already a Guid collection or null, return as-is
			if (value is null || value is IEnumerable<Guid>)
			{
				return value;
			}

			Logger.LogVerbose( $"[ConditionalGuidListConverter.Convert] Unexpected value type: {value?.GetType().Name ?? "null"}" );
			return AvaloniaProperty.UnsetValue;
		}

		public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
		{
			if (value is List<Guid> guidList)
			{
				var stringList = guidList.Select( guid => guid.ToString() ).ToList();
				Logger.LogVerbose( $"[ConditionalGuidListConverter.ConvertBack] Converted {guidList.Count} GUIDs to strings" );
				return stringList;
			}

			// If it's already a string collection or null, return as-is
			if (value is null)
			{
				return AvaloniaProperty.UnsetValue;
			}

			Logger.LogVerbose( $"[ConditionalGuidListConverter.ConvertBack] Unexpected value type: {value?.GetType().Name ?? "null"}" );
			return AvaloniaProperty.UnsetValue;
		}
	}
}