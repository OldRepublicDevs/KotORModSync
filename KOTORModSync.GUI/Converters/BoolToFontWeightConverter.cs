// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KOTORModSync.Converters
{
    /// <summary>
    /// Converts a boolean value to a FontWeight (Bold for true, Normal for false)
    /// </summary>
    public class BoolToFontWeightConverter : IValueConverter
    {
        public static readonly BoolToFontWeightConverter Instance = new BoolToFontWeightConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? FontWeight.Bold : FontWeight.Normal;
            }

            return FontWeight.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

