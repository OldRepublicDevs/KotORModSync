// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{
	/// <summary>
	/// Utility methods for search and filtering operations.
	/// </summary>
	public static class SearchUtilities
	{
		/// <summary>
		/// Checks if an item name matches the search text (case-insensitive).
		/// </summary>
		/// <param name="itemName">The name of the item to check.</param>
		/// <param name="searchText">The search text to match against.</param>
		/// <returns>True if the item name contains the search text, false otherwise.</returns>
		/// <exception cref="ArgumentNullException">Thrown when itemName or searchText is null.</exception>
		public static bool MatchesSearch([NotNull] string itemName, [NotNull] string searchText)
		{
			if ( itemName == null )
				throw new ArgumentNullException(nameof(itemName));
			if ( searchText == null )
				throw new ArgumentNullException(nameof(searchText));

			return itemName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		/// <summary>
		/// Determines if an item should be visible based on search text.
		/// </summary>
		/// <param name="itemName">The name of the item to check.</param>
		/// <param name="searchText">The search text to match against.</param>
		/// <returns>True if the item should be visible, false otherwise.</returns>
		/// <exception cref="ArgumentNullException">Thrown when itemName or searchText is null.</exception>
		public static bool ShouldBeVisible([NotNull] string itemName, [NotNull] string searchText)
		{
			if ( string.IsNullOrWhiteSpace(searchText) )
				return true;

			return MatchesSearch(itemName, searchText);
		}
	}
}
