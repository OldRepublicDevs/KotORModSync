// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	public partial class ComponentToGuidConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
			!(value is ModComponent selectedComponent)
				? null
				: (object)selectedComponent.Name;

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			!(value is List<ModComponent>)
				? null
				: new object();
	}
}
