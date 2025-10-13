// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	public partial class ArgumentsDescriptionConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!(value is Instruction.ActionType action))
				return "• Execute: Command-line arguments\n• DelDuplicate: File extension (e.g., .mdl)\n• Patcher: Use options above";

			switch (action)
			{
				case Instruction.ActionType.DelDuplicate:
					return "File extensions to delete when filenames duplicate (e.g., .mdl, .tpc, .wav)";
				case Instruction.ActionType.Execute:
					return "Command-line arguments to pass to the executable";
				case Instruction.ActionType.Patcher:
					return "Select install option from namespaces.ini (if available)";
				default:
					return "• Execute: Command-line arguments\n• DelDuplicate: File extension (e.g., .mdl)\n• Patcher: Use options above";
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotImplementedException();
	}
}
