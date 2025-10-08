// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	/// <summary>
	/// Converts a Guid (component or option) to a human-readable label: "[ModComponent] Name" or "[Option] Parent > Name".
	/// </summary>
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
				// find parent component for context
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


