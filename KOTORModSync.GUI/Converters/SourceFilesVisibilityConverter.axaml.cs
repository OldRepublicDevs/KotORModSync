// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	public partial class SourceFilesVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!(value is Instruction.ActionType action))
				return true;

			// Hide Source File(s) section for DelDuplicate and Choose actions
			// Choose actions use Source for GUIDs (displayed in Components/Options section)
			// DelDuplicate uses Arguments for file extensions
			return action != Instruction.ActionType.DelDuplicate && action != Instruction.ActionType.Choose;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotImplementedException();
	}
}
