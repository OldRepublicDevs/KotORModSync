



using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KOTORModSync.Converters
{
	public partial class StringToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
			!string.IsNullOrWhiteSpace(value?.ToString());

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
	}
}
