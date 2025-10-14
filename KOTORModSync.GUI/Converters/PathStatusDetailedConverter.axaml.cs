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
			// Get the instruction from parameter
			Instruction instruction = parameter as Instruction;

			// For single path validation
			if (value is string singlePath)
			{
				return ValidateSinglePath(singlePath, instruction);
			}

			// For multiple paths (Source can be a list)
			if (value is System.Collections.Generic.List<string> pathList)
			{
				if (pathList == null || pathList.Count == 0)
					return new PathValidationResult { StatusMessage = "❓ Empty", IsValid = false };

				// Validate the first path as a representative
				return ValidateSinglePath(pathList.FirstOrDefault(), instruction);
			}

			return new PathValidationResult { StatusMessage = "❓ Empty", IsValid = false };
		}

		private static PathValidationResult ValidateSinglePath(string path, Instruction instruction)
		{
			if (string.IsNullOrWhiteSpace(path))
				return new PathValidationResult { StatusMessage = "❓ Empty", IsValid = false };

			// Use the DryRunValidator to get detailed validation result
			ModComponent currentComponent = MainConfig.CurrentComponent;

			try
			{
				// Call the async method synchronously (acceptable for validation in UI converters)
				return DryRunValidator.ValidateInstructionPathDetailedAsync(path, instruction, currentComponent)
					.GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				Core.Logger.LogException(ex, "Error in detailed path validation converter");
				return new PathValidationResult
				{
					StatusMessage = "⚠️ Validation error",
					DetailedMessage = $"Error: {ex.Message}",
					IsValid = false
				};
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}

