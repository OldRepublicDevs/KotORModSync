// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Avalonia.Data.Converters;

using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
    public partial class SpoilerFreeContentConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is null || values.Count < 4)
            {
                return string.Empty;
            }

            // First value should be the regular property name (e.g., "Description")
            // Second value should be the ModComponent
            // Third value should be the SpoilerFreeMode boolean
            // Fourth value should be the spoiler-free property name (e.g., "DescriptionSpoilerFree")

            string regularPropertyName = values[0]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(regularPropertyName))
            {
                return string.Empty;
            }

            if (!(values[1] is ModComponent component))
            {
                return string.Empty;
            }

            if (!(values[2] is bool spoilerFreeMode))
            {
                return string.Empty;
            }

            string spoilerFreePropertyName = values[3]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(spoilerFreePropertyName))
            {
                return string.Empty;
            }

            // Use reflection to dynamically get the property value
            return GetPropertyValue(component, regularPropertyName, spoilerFreePropertyName, spoilerFreeMode);
        }

        /// <summary>
        /// Dynamically retrieves the appropriate property value based on spoiler-free mode.
        /// </summary>
        private static string GetPropertyValue(ModComponent component, string regularPropertyName, string spoilerFreePropertyName, bool spoilerFreeMode)
        {
            try
            {
                Type componentType = typeof(ModComponent);

                // If in spoiler-free mode, try to get the spoiler-free version first
                if (spoilerFreeMode)
                {
                    PropertyInfo spoilerFreeProperty = componentType.GetProperty(spoilerFreePropertyName);

                    if (spoilerFreeProperty != null)
                    {
                        object spoilerFreeValue = spoilerFreeProperty.GetValue(component);
                        if (spoilerFreeValue is string spoilerFreeString)
                        {
                            return spoilerFreeString ?? string.Empty;
                        }
                    }
                }

                // Get the regular property value
                PropertyInfo regularProperty = componentType.GetProperty(regularPropertyName);
                if (regularProperty != null)
                {
                    object regularValue = regularProperty.GetValue(component);
                    if (regularValue is string regularString)
                    {
                        return regularString ?? string.Empty;
                    }
                }

                // If property doesn't exist or is not a string, return empty
                return string.Empty;
            }
            catch (Exception)
            {
                // If anything goes wrong, return empty string
                return string.Empty;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
