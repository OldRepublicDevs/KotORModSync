// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{

	public static class SearchUtilities
	{


		public static bool MatchesSearch([NotNull] string itemName, [NotNull] string searchText)
		{
			if ( itemName == null )
				throw new ArgumentNullException(nameof(itemName));
			if ( searchText == null )
				throw new ArgumentNullException(nameof(searchText));

			return itemName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
		}



		public static bool ShouldBeVisible([NotNull] string itemName, [NotNull] string searchText)
		{
			if ( string.IsNullOrWhiteSpace(searchText) )
				return true;

			return MatchesSearch(itemName, searchText);
		}
	}
}
