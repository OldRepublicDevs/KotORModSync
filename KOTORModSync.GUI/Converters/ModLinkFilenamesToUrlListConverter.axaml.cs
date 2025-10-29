// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Avalonia.Data;
using Avalonia.Data.Converters;

namespace KOTORModSync.Converters
{
	/// <summary>
	/// Converts between Dictionary<string, Dictionary<string, bool?>> (ModLinkFilenames)
	/// and List<string> (URL list for DownloadLinksControl)
	/// </summary>
	public class ModLinkFilenamesToUrlListConverter : IValueConverter
	{
		public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
		{
			if (value is Dictionary<string, Dictionary<string, bool?>> modLinkFilenames)
			{
				return modLinkFilenames.Keys.ToList();
			}

			return new List<string>();
		}

		public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
		{
			if (!(value is List<string> urlList))
			{
				return new BindingNotification(
					new InvalidCastException( "Expected List<string>" ),
					BindingErrorType.Error
				);
			}

			// Create a new dictionary with the URLs as keys and empty filename dicts as values
			var result = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase );
			foreach (string url in urlList)
			{
				if (!string.IsNullOrWhiteSpace( url ))
				{
					result[url] = new Dictionary<string, bool?>( StringComparer.OrdinalIgnoreCase );
				}
			}

			return result;
		}
	}
}