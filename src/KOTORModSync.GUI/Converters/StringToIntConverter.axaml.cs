// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;

using Avalonia.Data.Converters;

namespace KOTORModSync.Converters
{
    public partial class StringToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue;
            }

            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    return -1; // no selection
                }

                if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    return parsed;
                }

                return -1;
            }

            if (value is null)
            {
                return -1;
            }

            // Fallback: attempt to stringify and parse
            if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedFromObj))
            {
                return parsedFromObj;
            }

            return -1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
            {
                return i < 0 ? string.Empty : i.ToString(CultureInfo.InvariantCulture);
            }

            if (value is string s)
            {
                return s; // already string
            }

            if (value is null)
            {
                return string.Empty;
            }

            // Fallback
            return value.ToString() ?? string.Empty;
        }
    }
}


