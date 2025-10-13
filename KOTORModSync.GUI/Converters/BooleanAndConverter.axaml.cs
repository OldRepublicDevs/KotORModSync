// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using JetBrains.Annotations;

namespace KOTORModSync.Converters
{

	public partial class BooleanAndConverter : IMultiValueConverter
	{
		public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			if ( values.Count == 0 )
				return false;

			// Check if all values are valid and true
			foreach ( object value in values)
			{
				// Handle unset or null values
				if (value == null || value == AvaloniaProperty.UnsetValue)
					return false;

				// Convert to bool if possible
				if (value is bool boolValue)
				{
					if (!boolValue)
						return false;
				}
				else
				{
					// If it's not a bool, consider it false
					return false;
				}
			}

			return true;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, [CanBeNull] object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

}