// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{

	public partial class CategoryTooltipConverter : IValueConverter
	{

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ( value is string category )
			{
				return CategoryTierDefinitions.GetCategoryDescription(category);
			}

			if ( value is List<string> categories && categories.Count > 0 )
			{
				var descriptions = categories
					.Select(cat => $"• {cat}: {CategoryTierDefinitions.GetCategoryDescription(cat)}")
					.ToList();
				return string.Join("\n", descriptions);
			}

			return "No category specified.";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
