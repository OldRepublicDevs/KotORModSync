// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	/// <summary>
	/// Converts path status to show whether paths are resolved or contain placeholders
	/// </summary>
	public partial class PathStatusConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!(value is string path))
				return "❓ Unknown";

			if (string.IsNullOrEmpty(path))
				return "❓ Empty";

			// Check if path contains unresolved placeholders
			if (path.Contains("<<modDirectory>>") || path.Contains("<<kotorDirectory>>"))
			{
				// Check if the directories are configured
				if (MainConfig.SourcePath == null && MainConfig.DestinationPath == null)
				{
					return "⚠️ Paths not configured";
				}
				else if (MainConfig.SourcePath == null)
				{
					return "⚠️ Mod directory not configured";
				}
				else if (MainConfig.DestinationPath == null)
				{
					return "⚠️ KOTOR directory not configured";
				}
				else
				{
					return "⚠️ Contains placeholders";
				}
			}

			// Path appears to be resolved
			return "✅ Resolved";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
