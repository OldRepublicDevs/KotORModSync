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
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
	        if ( !(value is string url) )
			    return false;

	        // Always show validation errors in the Summary tab
	        // EditorMode validation is handled separately in ModListItem and DownloadLinksControl
            return !IsValidUrl(url);
        }

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();

		private static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return true; // Empty URLs are considered valid (no error shown)

            // Basic URL validation
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                return false;

            // Check if it's HTTP or HTTPS
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            // Check if it has a valid host
            if (string.IsNullOrWhiteSpace(uri.Host))
                return false;

            return true;
        }
    }
}
