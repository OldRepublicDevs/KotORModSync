



using System;
using System.Globalization;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	
	
	
	public sealed partial class GuidToComponentConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if ( !(value is Guid guid) )
				return string.Empty;

			var found = ModComponent.FindComponentFromGuid(guid, MainConfig.AllComponents);
			if ( found == null )
				return guid.ToString();

			if ( found is Option opt )
			{
				
				foreach ( ModComponent c in MainConfig.AllComponents )
				{
					if ( c.Options.Contains(opt) )
						return $"[Option] {c.Name} > {opt.Name}";
				}
				return $"[Option] {opt.Name}";
			}

			return $"[ModComponent] {found.Name}";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotImplementedException();
	}
}


