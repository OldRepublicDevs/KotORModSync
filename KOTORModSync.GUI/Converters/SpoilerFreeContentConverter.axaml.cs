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
	public partial class SpoilerFreeContentConverter : IMultiValueConverter
	{
		public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			if ( values == null || values.Count < 2 )
				return string.Empty;

			// First value should be the ModComponent
			// Second value should be the SpoilerFreeMode boolean
			// Parameter should be the property name (e.g., "Description", "Directions", etc.)

			if ( !(values[0] is ModComponent component) )
				return string.Empty;

			if ( !(values[1] is bool spoilerFreeMode) )
				return string.Empty;

			string propertyName = parameter?.ToString() ?? string.Empty;

			// Return the appropriate content based on spoiler-free mode
			switch ( propertyName.ToLowerInvariant() )
			{
				case "downloadinstructions":
					return spoilerFreeMode ? component.DownloadInstructionsSpoilerFree : component.DownloadInstructions;
				case "usagewarning":
					return spoilerFreeMode ? component.UsageWarningSpoilerFree : component.UsageWarning;
				case "screenshots":
					return spoilerFreeMode ? component.ScreenshotsSpoilerFree : component.Screenshots;
				case "description":
					return spoilerFreeMode ? component.DescriptionSpoilerFree : component.Description;
				case "directions":
					return spoilerFreeMode ? component.DirectionsSpoilerFree : component.Directions;
				default:
					return string.Empty;
			}
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
