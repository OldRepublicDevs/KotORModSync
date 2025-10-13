



using System;
using System.Globalization;
using Avalonia.Data.Converters;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Converters
{
	
	
	
	public partial class TierTooltipConverter : IValueConverter
	{
		
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ( value is string tier )
			{
				return CategoryTierDefinitions.GetTierDescription(tier);
			}

			return "No tier specified.";
		}

		
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
