// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using Avalonia;
using Avalonia.Media;

namespace KOTORModSync
{
	/// <summary>
	/// Helper class to access theme resources from ViewModels and other non-UI classes.
	/// This allows for theme-aware colors without hardcoding values.
	/// </summary>
	public static class ThemeResourceHelper
	{
		/// <summary>
		/// Attempts to retrieve a brush resource by key from the current application resources.
		/// Falls back to the specified fallback brush if the resource is not found.
		/// </summary>
		/// <param name="resourceKey">The resource key to look up (e.g., "MergeStatus.NewBrush")</param>
		/// <param name="fallback">The fallback brush to use if the resource is not found</param>
		/// <returns>The brush from resources, or the fallback if not found</returns>
		public static IBrush GetBrush(string resourceKey, IBrush fallback = null)
		{
			if ( Application.Current?.Resources.TryGetResource(resourceKey, null, out object resource) != true )
				return fallback ?? Brushes.Transparent;
			if (resource is IBrush brush)
				return brush;
			return fallback ?? Brushes.Transparent;
		}

		// Merge Status Brushes
		public static IBrush MergeStatusNewBrush => GetBrush("MergeStatus.NewBrush", Brushes.LightGreen);
		public static IBrush MergeStatusExistingOnlyBrush => GetBrush("MergeStatus.ExistingOnlyBrush", Brushes.LightGray);
		public static IBrush MergeStatusMatchedBrush => GetBrush("MergeStatus.MatchedBrush", Brushes.Yellow);
		public static IBrush MergeStatusUpdatedBrush => GetBrush("MergeStatus.UpdatedBrush", Brushes.Orange);
		public static IBrush MergeStatusDefaultBrush => GetBrush("MergeStatus.DefaultBrush", Brushes.White);

		// Selection Brushes
		public static IBrush MergeSelectionBorderBrush => GetBrush("MergeSelection.BorderBrush", Brushes.Cyan);
		public static IBrush MergeSelectionBackgroundBrush => GetBrush("MergeSelection.BackgroundBrush",
			new SolidColorBrush(Color.FromArgb(40, 0, 255, 255)));

		// Source Brushes
		public static IBrush MergeSourceIncomingBrush => GetBrush("MergeSource.IncomingBrush", Brushes.Green);
		public static IBrush MergeSourceExistingBrush => GetBrush("MergeSource.ExistingBrush", Brushes.Blue);

		// Position Change Brushes
		public static IBrush MergePositionChangedBrush => GetBrush("MergePosition.ChangedBrush", Brushes.Orange);
		public static IBrush MergePositionNewBrush => GetBrush("MergePosition.NewBrush", Brushes.Yellow);

		// TOML Diff Brushes
		public static IBrush MergeDiffUnchangedBrush => GetBrush("MergeDiff.UnchangedBrush", Brushes.Black);
		public static IBrush MergeDiffAddedBrush => GetBrush("MergeDiff.AddedBrush", Brushes.DarkGreen);
		public static IBrush MergeDiffRemovedBrush => GetBrush("MergeDiff.RemovedBrush", Brushes.DarkRed);
		public static IBrush MergeDiffModifiedBrush => GetBrush("MergeDiff.ModifiedBrush", Brushes.DarkGoldenrod);
	}
}

