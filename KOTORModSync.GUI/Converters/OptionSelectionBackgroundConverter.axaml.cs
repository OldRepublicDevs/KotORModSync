// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KOTORModSync.Converters
{
	public partial class OptionSelectionBackgroundConverter : IMultiValueConverter
	{
		public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			if ( values == null || values.Count < 2 )
				return Brushes.Transparent;

			bool isSelected = values[0] is bool selected && selected;
			IBrush selectionBrush = values[1] as IBrush ?? Brushes.Transparent;

			return isSelected ? selectionBrush : Brushes.Transparent;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
