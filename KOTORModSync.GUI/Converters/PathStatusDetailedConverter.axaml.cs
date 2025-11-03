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
    public partial class PathStatusDetailedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var instruction = parameter as Instruction;

            if (value is string singlePath)
            {
                return ValidateSinglePath(singlePath, instruction);
            }

            if (value is System.Collections.Generic.List<string> pathList)
            {
                if (pathList.Count == 0)
                {
                    Logger.LogVerbose("Path list is empty");
                    return new PathValidationResult { StatusMessage = "❓ Empty", IsValid = false };
                }

                return ValidateSinglePath(pathList.FirstOrDefault(), instruction);
            }
            Logger.LogVerbose("Path list is null or empty");
            return new PathValidationResult { StatusMessage = "❓ Empty", IsValid = false };
        }

        private static PathValidationResult ValidateSinglePath(string path, Instruction instruction)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Logger.LogVerbose("Path is null or empty");
                return new PathValidationResult { StatusMessage = "❓ Empty", IsValid = false };
            }

            ModComponent component = MainConfig.CurrentComponent;

            // First, check if we have a cached validation result from the last Validate button press
            PathValidationResult cachedResult = PathValidationCache.GetCachedResult(path, instruction, component);
            if (cachedResult != null)
            {
                Logger.LogVerbose($"Cached result found: {cachedResult}");
                return cachedResult;
            }

            // No cached result - show basic placeholder check only (no VFS, no file existence checks)
            // Full validation results are shown when Validate button is pressed
            try
            {
                return DryRunValidator.ValidateInstructionPathDetailedAsync(path, instruction, component)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error in detailed path validation converter");
                return new PathValidationResult
                {
                    StatusMessage = "⚠️ Validation error",
                    DetailedMessage = $"Error: {ex.Message}",
                    IsValid = false,
                };
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
