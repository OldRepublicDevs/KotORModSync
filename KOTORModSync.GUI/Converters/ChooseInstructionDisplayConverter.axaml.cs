// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	public partial class ChooseInstructionDisplayConverter : IMultiValueConverter
	{
		public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			try
			{
				if (!(values[0] is Instruction instruction) || !(values[1] is List<ModComponent> componentsList))
					return string.Empty;

				// For Choose actions, convert GUIDs to component names
				if (instruction.Action == Instruction.ActionType.Choose)
				{
					if (instruction.Source == null || instruction.Source.Count == 0)
						return "Choose (no options)";

					var componentNames = (from guidString in instruction.Source
										let guid = Guid.Parse(guidString)
										let foundComponent = ModComponent.FindComponentFromGuid(guid, componentsList)
										select foundComponent != null ? foundComponent.Name : guidString).ToList();

					return string.Join(" ", componentNames);
				}

				// For all other actions, use the default ListToStringConverter behavior
				if (instruction.Source == null || instruction.Source.Count == 0)
					return string.Empty;

				return string.Join(Environment.NewLine, instruction.Source);
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return string.Empty;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// This converter is read-only for display purposes
			throw new NotImplementedException();
		}
	}
}
