// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	public partial class GuidListToComponentNames : IMultiValueConverter
	{
		public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			try
			{
				if ( !(values[0] is List<Guid> guids) || !(values[1] is List<ModComponent> componentsList) )
					return null;

				var selectedComponentNames = (from cGuid in guids
											  let foundComponent = ModComponent.FindComponentFromGuid(cGuid, componentsList)
											  select !(foundComponent is null)
												  ? foundComponent.Name
												  : cGuid.ToString()).ToList();

				if ( selectedComponentNames.Count == 0 )
					selectedComponentNames.Add("None");

				return selectedComponentNames;
			}
			catch ( Exception e )
			{
				Logger.LogException(e);
				return null;
			}
		}
	}
}
