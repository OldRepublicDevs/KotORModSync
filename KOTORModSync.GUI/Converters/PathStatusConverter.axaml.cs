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
			Instruction instruction = parameter as Instruction;

			if ( value is string singlePath )
			{
				return ValidateSinglePath(singlePath, instruction);
			}

			if ( value is System.Collections.Generic.List<string> pathList )
			{
				if ( pathList == null || pathList.Count == 0 )
					return "❓ Empty";

				return ValidateSinglePath(pathList.FirstOrDefault(), instruction);
			}

			return "❓ Empty";
		}

		private static string ValidateSinglePath(string path, Instruction instruction)
		{
			if ( string.IsNullOrWhiteSpace(path) )
				return "❓ Empty";

			ModComponent currentComponent = MainConfig.CurrentComponent;

			try
			{
				return DryRunValidator.ValidateInstructionPathAsync(path, instruction, currentComponent)
					.GetAwaiter().GetResult();
			}
			catch ( Exception ex )
			{
				Core.Logger.LogException(ex, "Error in path validation converter");
				return "⚠️ Validation error";
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
