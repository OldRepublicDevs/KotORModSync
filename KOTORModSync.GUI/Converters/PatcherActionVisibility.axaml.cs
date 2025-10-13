// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	public partial class PatcherActionVisibility : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
			value is Instruction.ActionType action && action.Equals(Instruction.ActionType.Patcher);

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
			throw new NotImplementedException();
	}
}

