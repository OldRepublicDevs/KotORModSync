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
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ( value is null )
				return string.Empty;

			if ( value is string singlePath )
			{
				return ResolvePath(singlePath);
			}

			if ( value is IEnumerable<string> pathList )
			{
				var resolvedPaths = pathList.Select(ResolvePath).ToList();
				return string.Join(Environment.NewLine, resolvedPaths);
			}

			return value.ToString();
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		[NotNull]
		private static string ResolvePath([CanBeNull] string path)
		{
			if ( string.IsNullOrEmpty(path) )
				return string.Empty;

			if ( MainConfig.SourcePath == null && MainConfig.DestinationPath == null )
			{
				return path;
			}

			try
			{
				return Utility.ReplaceCustomVariables(path);
			}
			catch ( Exception )
			{

				return path;
			}
		}
	}
}
