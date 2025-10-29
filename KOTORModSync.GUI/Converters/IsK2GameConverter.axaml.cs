// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;

using Avalonia.Data.Converters;

using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	public partial class IsK2GameConverter : IValueConverter
	{
		public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
		{
			if (value is string targetGame)
			{
				return !string.IsNullOrWhiteSpace( targetGame ) &&
					   (targetGame.Equals( MainConfig.ValidTargetGames.TSL, StringComparison.OrdinalIgnoreCase ) ||
						targetGame.Equals( MainConfig.ValidTargetGames.KOTOR2, StringComparison.OrdinalIgnoreCase ));
			}
			return false;
		}

		public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture ) => throw new NotImplementedException();
	}
}