



using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KOTORModSync.Converters
{
	public partial class IndexConverter : IMultiValueConverter
	{
		public object Convert(
			IList<object> values,
			Type targetType,
			object parameter,
			CultureInfo culture
		) =>
			values[0] is IList list
				? $"{list.IndexOf(values[1]) + 1}:"
				: "-1";
	}
}
