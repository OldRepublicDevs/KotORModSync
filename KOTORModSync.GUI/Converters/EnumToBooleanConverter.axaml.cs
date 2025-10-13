// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KOTORModSync.Converters
{
	public partial class EnumToBooleanConverter : IValueConverter
	{
		public static readonly EnumToBooleanConverter Instance = new EnumToBooleanConverter();

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ( value == null || parameter == null )
				return false;

			string enumValue = value.ToString();
			string targetValue = parameter.ToString();

			return enumValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ( parameter == null )
				return null;

			if ( value is bool boolValue && boolValue )
			{
				return Enum.Parse(targetType, parameter.ToString());
			}

			return null;
		}
	}
}
