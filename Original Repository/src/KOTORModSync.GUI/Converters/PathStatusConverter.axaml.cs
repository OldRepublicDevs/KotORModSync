// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using System.Linq;

using Avalonia.Data.Converters;

using KOTORModSync.Core;
using KOTORModSync.Core.Services.Validation;

namespace KOTORModSync.Converters
{

    public partial class PathStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var instruction = parameter as Instruction;
            ModComponent component = MainConfig.CurrentComponent;

            if (value is string singlePath)
            {
                return ValidateSinglePath(singlePath, instruction, component);
            }

            if (value is System.Collections.Generic.List<string> pathList)
            {
                if (pathList.Count == 0)
                {
                    return "❓ Empty";
                }

                return ValidateSinglePath(pathList.FirstOrDefault(), instruction, component);
            }

            return "❓ Empty";
        }

        private static string ValidateSinglePath(string path, Instruction instruction, ModComponent component)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "❓ Empty";
            }

            // First, check if we have a cached validation result from the last Validate button press
            PathValidationResult cachedResult = PathValidationCache.GetCachedResult(path, instruction, component);
            if (cachedResult != null)
            {
                return cachedResult.StatusMessage ?? "⚠️ Unknown";
            }

            // No cached result - show basic placeholder check only (no VFS, no file existence checks)
            // Full validation results are shown when Validate button is pressed
            try
            {
                // Basic placeholder check
                if (!path.StartsWith("<<modDirectory>>", StringComparison.Ordinal) &&
                    !path.StartsWith("<<kotorDirectory>>", StringComparison.Ordinal) &&
                    instruction?.Action != Instruction.ActionType.Choose)
                {
                    return "⚠️ Invalid path (must start with <<modDirectory>> or <<kotorDirectory>>)";
                }

                // No cached result - prompt user to validate
                return "⚠️ Click validate or refresh to check";
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error in path validation converter");
                return "⚠️ Validation error";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
