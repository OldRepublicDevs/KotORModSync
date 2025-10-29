// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;

using Avalonia.Data.Converters;

using KOTORModSync.Core;

namespace KOTORModSync.Converters
{

	public partial class ChooseActionDisplayConverter : IValueConverter
	{
		public object Convert( object value, Type targetType, object parameter, CultureInfo culture )
		{
			if (value is Instruction instruction)
			{
				if (instruction.Action == Instruction.ActionType.Choose)
				{

					int sourceCount = instruction.Source.Count;
					if (sourceCount == 0)
						return "Choose (no options)";
					else if (sourceCount == 1)
						return "Choose (1 option)";
					else
						return $"Choose ({sourceCount} options)";
				}

				return instruction.Action.ToString();
			}

			return string.Empty;
		}

		public object ConvertBack( object value, Type targetType, object parameter, CultureInfo culture )
		{
			throw new NotImplementedException();
		}
	}

}