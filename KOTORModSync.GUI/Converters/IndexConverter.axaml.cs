// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KOTORModSync.Converters
{
	public partial class IndexConverter : IMultiValueConverter
	{
		public object Convert(
			IList<object> values,
			Type targetType,
			object parameter,
			CultureInfo culture
		) =>
			values[0] is IList list
				? $"{list.IndexOf(values[1]) + 1}:"
				: "-1";
	}
}
