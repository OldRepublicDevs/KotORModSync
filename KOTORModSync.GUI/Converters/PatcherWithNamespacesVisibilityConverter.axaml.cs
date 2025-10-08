// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	public partial class PatcherWithNamespacesVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!(value is Instruction instruction))
				return false;

			// Only show for Patcher actions
			if (instruction.Action != Instruction.ActionType.Patcher)
				return false;

			// Check if the mod has namespaces.ini by using the existing converter logic
			var options = NamespacesIniOptionConverter.GetAllArchivesFromInstructions(instruction.GetParentComponent());
			foreach (string archivePath in options)
			{
				if (string.IsNullOrEmpty(archivePath))
					continue;

				var result = Core.TSLPatcher.IniHelper.ReadNamespacesIniFromArchive(archivePath);
				if (result != null && result.Any())
				{
					// Check if there are actual namespace options (not just the [Namespaces] section)
					var optionNames = result.Where(section =>
						section.Key != "Namespaces" &&
						section.Value != null &&
						section.Value.ContainsKey("Name")).ToList();

					if (optionNames.Any())
						return true;
				}
			}

			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotImplementedException();
	}
}
