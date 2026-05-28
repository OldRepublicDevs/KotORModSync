// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Avalonia.Data.Converters;
using Avalonia.Media;

using KOTORModSync.Core;
using KOTORModSync.Core.Services.Validation;

namespace KOTORModSync.Converters
{
    /// <summary>
    /// Converter that checks if a path or list of paths is valid using the PathValidationCache.
    /// Returns true if valid, false if invalid (for use with border styling).
    /// </summary>
    public class PathValidationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                // Get the instruction from DataContext (passed as parameter)
                if (!(parameter is Instruction instruction))
                {
                    return true; // Default to valid if we can't determine
                }

                ModComponent component = MainConfig.CurrentComponent;
                if (component == null)
                {
                    return true; // Default to valid if no component
                }

                List<string> pathsToCheck = new List<string>();

                // Handle single string
                if (value is string singlePath && !string.IsNullOrWhiteSpace(singlePath))
                {
                    pathsToCheck.Add(singlePath);
                }
                // Handle list of strings
                else if (value is IEnumerable<string> pathList)
                {
                    pathsToCheck.AddRange(pathList.Where(p => !string.IsNullOrWhiteSpace(p)));
                }

                if (pathsToCheck.Count == 0)
                {
                    return true; // No paths to validate, consider valid
                }

                // Check validation cache for each path
                foreach (string path in pathsToCheck)
                {
                    PathValidationResult result = PathValidationCache.GetCachedResult(path, instruction, component);
                    if (result != null && !result.IsValid)
                    {
                        return false; // At least one path is invalid
                    }
                }

                return true; // All paths are valid or not yet validated
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[PathValidationConverter] Error converting value");
                return true; // Default to valid on error
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter that returns an error message for invalid paths.
    /// Used for tooltips.
    /// </summary>
    public class PathValidationMessageConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values == null || values.Count < 2)
                {
                    return null;
                }

                // First value is the path(s), second is the instruction
                object pathValue = values[0];
                if (!(values[1] is Instruction instruction))
                {
                    return null;
                }

                ModComponent component = MainConfig.CurrentComponent;
                if (component == null)
                {
                    return null;
                }

                List<string> pathsToCheck = new List<string>();

                // Handle single string
                if (pathValue is string singlePath && !string.IsNullOrWhiteSpace(singlePath))
                {
                    pathsToCheck.Add(singlePath);
                }
                // Handle list of strings
                else if (pathValue is IEnumerable<string> pathList)
                {
                    pathsToCheck.AddRange(pathList.Where(p => !string.IsNullOrWhiteSpace(p)));
                }

                if (pathsToCheck.Count == 0)
                {
                    return null;
                }

                // Collect all error messages
                List<string> errorMessages = new List<string>();

                foreach (string path in pathsToCheck)
                {
                    PathValidationResult result = PathValidationCache.GetCachedResult(path, instruction, component);
                    if (result != null && !result.IsValid)
                    {
                        if (!string.IsNullOrWhiteSpace(result.DetailedMessage))
                        {
                            errorMessages.Add($"• {path}: {result.DetailedMessage}");
                        }
                        else if (!string.IsNullOrWhiteSpace(result.StatusMessage))
                        {
                            errorMessages.Add($"• {path}: {result.StatusMessage}");
                        }
                        else
                        {
                            errorMessages.Add($"• {path}: Invalid path");
                        }
                    }
                }

                return errorMessages.Count > 0 ? string.Join("\n", errorMessages) : null;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[PathValidationMessageConverter] Error converting value");
                return null;
            }
        }
    }

    /// <summary>
    /// Converter that returns a brush color based on validation state.
    /// Red for invalid, transparent for valid.
    /// </summary>
    public class PathValidationBorderBrushConverter : IMultiValueConverter
    {
        private static readonly SolidColorBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69));
        private static readonly SolidColorBrush TransparentBrush = new SolidColorBrush(Colors.Transparent);

        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values == null || values.Count < 2)
                {
                    return TransparentBrush;
                }

                // First value is the path(s), second is the instruction
                object pathValue = values[0];
                if (!(values[1] is Instruction instruction))
                {
                    return TransparentBrush;
                }

                ModComponent component = MainConfig.CurrentComponent;
                if (component == null)
                {
                    return TransparentBrush;
                }

                List<string> pathsToCheck = new List<string>();

                // Handle single string
                if (pathValue is string singlePath && !string.IsNullOrWhiteSpace(singlePath))
                {
                    pathsToCheck.Add(singlePath);
                }
                // Handle list of strings
                else if (pathValue is IEnumerable<string> pathList)
                {
                    pathsToCheck.AddRange(pathList.Where(p => !string.IsNullOrWhiteSpace(p)));
                }

                if (pathsToCheck.Count == 0)
                {
                    return TransparentBrush;
                }

                // Check validation cache for each path
                foreach (string path in pathsToCheck)
                {
                    PathValidationResult result = PathValidationCache.GetCachedResult(path, instruction, component);
                    if (result != null && !result.IsValid)
                    {
                        return ErrorBrush; // At least one path is invalid
                    }
                }

                return TransparentBrush; // All paths are valid
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[PathValidationBorderBrushConverter] Error converting value");
                return TransparentBrush;
            }
        }
    }
}

