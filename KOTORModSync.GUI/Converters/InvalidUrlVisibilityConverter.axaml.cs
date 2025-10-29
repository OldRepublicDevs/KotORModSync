// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;

using Avalonia.Data.Converters;

namespace KOTORModSync.Converters
{
	public class InvalidUrlVisibilityConverter : IValueConverter
	{
		public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
		{
			if (!(value is string url))
				return false;

			return !IsValidUrl( url );
		}

		public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture ) => throw new NotImplementedException();

		private static bool IsValidUrl( string url )
		{
			if (string.IsNullOrWhiteSpace( url ))
				return true;

			if (!Uri.TryCreate( url, UriKind.Absolute, out Uri uri ))
				return false;

			if (!string.Equals( uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal ) && !string.Equals( uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal ))
				return false;

			if (string.IsNullOrWhiteSpace( uri.Host ))
				return false;

			return true;
		}
	}
}