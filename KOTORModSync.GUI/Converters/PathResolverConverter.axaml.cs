// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

		public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
		{
			lock (_lockObject)
			{
				_convertCallCount++;
			}

			// Safety check: if we're getting too many calls, something is wrong
			if (_convertCallCount > 100)
			{
				Logger.LogError( $"[PathResolverConverter.Convert] INFINITE LOOP DETECTED! Call count: {_convertCallCount}, returning value as-is to break the loop" );
				return value?.ToString() ?? string.Empty;
			}

			Logger.LogVerbose( $"[PathResolverConverter.Convert] Call #{_convertCallCount} - Value type: {value?.GetType().Name ?? "null"}, TargetType: {targetType?.Name ?? "null"}" );
			Logger.LogVerbose( $"[PathResolverConverter.Convert] Call #{_convertCallCount} - Value: '{value}'" );

			if (value is null)
			{
				Logger.LogVerbose( $"[PathResolverConverter.Convert] Call #{_convertCallCount} - Value is null, returning empty string" );
				return string.Empty;
			}

			if (value is string singlePath)
			{
				Logger.LogVerbose( $"[PathResolverConverter.Convert] Call #{_convertCallCount} - Processing single path: '{singlePath}' (length: {singlePath.Length})" );
				var result = ResolvePath( singlePath );
				Logger.LogVerbose( $"[PathResolverConverter.Convert] Call #{_convertCallCount} - Single path result: '{result}' (length: {result.Length})" );
				return result;
			}

			if (value is IEnumerable<string> pathList)
			{
				var pathArray = pathList.ToArray();
				Logger.LogVerbose( $"[PathResolverConverter.Convert] Call #{_convertCallCount} - Processing path list with {pathArray.Length} items" );
				for (int i = 0; i < pathArray.Length; i++)
				{
					Logger.LogVerbose( $"[PathResolverConverter.Convert] Call #{_convertCallCount} - Path[{i}]: '{pathArray[i]}' (length: {pathArray[i]?.Length ?? 0})" );
				}

				Logger.LogVerbose( $"[PathResolverConverter.Convert] Call #{_convertCallCount} - About to call ResolvePath on each path in the list" );
				var resolvedPaths = pathArray.Select( ResolvePath ).ToList();
				var result = string.Join( Environment.NewLine, resolvedPaths );
				Logger.LogVerbose( $"[PathResolverConverter.Convert] Call #{_convertCallCount} - Path list result: '{result}' (length: {result.Length})" );
				return result;
			}

			Logger.LogVerbose( $"[PathResolverConverter.Convert] Call #{_convertCallCount} - Unknown value type, calling ToString(): '{value}'" );
			return value.ToString();
		}

		public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
		{
			throw new NotImplementedException();
		}

		[NotNull]
		private static string ResolvePath( [CanBeNull] string path )
		{
			lock (_lockObject)
			{
				_resolvePathCallCount++;
			}

			// Safety check: if we're getting too many calls, something is wrong
			if (_resolvePathCallCount > 100)
			{
				Logger.LogError( $"[PathResolverConverter.ResolvePath] INFINITE LOOP DETECTED! Call count: {_resolvePathCallCount}, returning path as-is to break the loop" );
				return path ?? string.Empty;
			}

			Logger.LogVerbose( $"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Input path: '{path}' (length: {path?.Length ?? 0})" );

			if (string.IsNullOrEmpty( path ))
			{
				Logger.LogVerbose( $"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Path is null/empty, returning empty string" );
				return string.Empty;
			}

			// Safety check: prevent processing extremely long paths that could cause stack overflow
			if (path.Length > 10000)
			{
				Logger.LogVerbose( $"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Path too long ({path.Length} chars), returning as-is" );
				return path; // Path too long, return as-is to prevent issues
			}

			// Check if path is already resolved (contains actual paths, not placeholders)
			// This prevents infinite recursion when the converter is used in two-way bindings
			try
			{
				bool hasModDir = path.Contains( "<<modDirectory>>" );
				bool hasKotorDir = path.Contains( "<<kotorDirectory>>" );
				Logger.LogVerbose( $"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Contains modDirectory: {hasModDir}, Contains kotorDirectory: {hasKotorDir}" );

				if (!hasModDir && !hasKotorDir)
				{
					Logger.LogVerbose( $"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Path already resolved, returning as-is" );
					return path; // Already resolved, don't process again
				}
			}
			catch (Exception ex)
			{
				// If Contains() itself causes issues, return the path as-is
				Logger.LogVerbose( $"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Exception in Contains check: {ex.Message}, returning path as-is" );
				return path;
			}

			Logger.LogVerbose( $"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - MainConfig.SourcePath: {MainConfig.SourcePath?.FullName ?? "null"}, MainConfig.DestinationPath: {MainConfig.DestinationPath?.FullName ?? "null"}" );

			if (MainConfig.SourcePath == null && MainConfig.DestinationPath == null)
			{
				Logger.LogVerbose( $"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Both config paths are null, returning path as-is" );
				return path;
			}

			try
			{
				Logger.LogVerbose( $"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Calling Utility.ReplaceCustomVariables" );
				var result = UtilityHelper.ReplaceCustomVariables( path );
				Logger.LogVerbose( $"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - ReplaceCustomVariables result: '{result}' (length: {result.Length})" );
				return result;
			}
			catch (Exception ex)
			{
				Logger.LogVerbose( $"[PathResolverConverter.ResolvePath] Call #{_resolvePathCallCount} - Exception in ReplaceCustomVariables: {ex.Message}, returning path as-is" );
				return path;
			}
		}
	}
}