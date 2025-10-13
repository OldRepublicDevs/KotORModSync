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
	/// Converts a list of categories to a comma-separated string for display.
	/// </summary>
	public partial class CategoryListDisplayConverter : IValueConverter
	{
		/// <inheritdoc />
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ( value is string category )
			{
				// Backwards compatibility: if it's a string, just return it
				return category;
			}

			if ( value is List<string> categories && categories.Count > 0 )
			{
				return string.Join(", ", categories);
			}

			return string.Empty;
		}

		/// <inheritdoc />
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ( value is string text && !string.IsNullOrWhiteSpace(text) )
			{
				// Only split on comma and semicolon, preserve other characters like & in category names
				return text.Split(
					new[] { ",", ";" },
					StringSplitOptions.RemoveEmptyEntries
				).Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();
			}

			return new List<string>();
		}
	}
}

