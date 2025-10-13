



using System;
using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KOTORModSync.Converters
{
	public partial class ListToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
			value is IList list && list.Count > 0;

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
	}
}
