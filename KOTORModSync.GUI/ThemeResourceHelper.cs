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
			if ( resource is IBrush brush )
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

		// Dependency Unlink Brushes
		public static IBrush DependencyWarningForeground => GetBrush("Dependency.WarningForeground", Brushes.Gold);
		public static IBrush DependencyWarningBackground => GetBrush("Dependency.WarningBackground",
			new SolidColorBrush(Color.FromArgb(30, 255, 193, 7)));
		public static IBrush DependencyWarningBorder => GetBrush("Dependency.WarningBorder", Brushes.Gold);

		// URL Validation Brushes
		public static IBrush UrlValidationValidBrush => GetBrush("UrlValidation.ValidBrush", new SolidColorBrush(Color.FromRgb(76, 175, 80)));
		public static IBrush UrlValidationInvalidBrush => GetBrush("UrlValidation.InvalidBrush", new SolidColorBrush(Color.FromRgb(244, 67, 54)));
		public static IBrush UrlValidationErrorIconBrush => GetBrush("UrlValidation.ErrorIconBrush", new SolidColorBrush(Color.FromRgb(244, 67, 54)));

		// ModListItem Validation Brushes
		public static IBrush ModListItemErrorBrush => GetBrush("ModListItem.ErrorBrush", new SolidColorBrush(Color.FromRgb(255, 107, 107)));
		public static IBrush ModListItemWarningBrush => GetBrush("ModListItem.WarningBrush", new SolidColorBrush(Color.FromRgb(255, 165, 0)));
		public static IBrush ModListItemHoverErrorBrush => GetBrush("ModListItem.HoverErrorBrush", new SolidColorBrush(Color.FromRgb(255, 136, 136)));
		public static IBrush ModListItemHoverWarningBrush => GetBrush("ModListItem.HoverWarningBrush", new SolidColorBrush(Color.FromRgb(255, 184, 77)));
		public static IBrush ModListItemHoverDefaultBrush => GetBrush("ModListItem.HoverDefaultBrush", new SolidColorBrush(Color.FromRgb(168, 179, 72)));
		public static IBrush ModListItemHoverBackgroundBrush => GetBrush("ModListItem.HoverBackgroundBrush", new SolidColorBrush(Color.FromRgb(2, 2, 40)));
		public static IBrush ModListItemDefaultBackgroundBrush => GetBrush("ModListItem.DefaultBackgroundBrush", new SolidColorBrush(Color.FromRgb(1, 1, 22)));

		// Component Dependency/Restriction Brushes
		public static IBrush ComponentDependencyBrush => GetBrush("Component.DependencyBrush", new SolidColorBrush(Color.FromRgb(76, 175, 80)));
		public static IBrush ComponentRestrictionBrush => GetBrush("Component.RestrictionBrush", new SolidColorBrush(Color.FromRgb(244, 67, 54)));

		// Validation Dialog Brushes
		public static IBrush ValidationSolutionBrush => GetBrush("Validation.SolutionBrush", new SolidColorBrush(Color.FromRgb(168, 179, 72)));

		// Log Viewer Brushes
		public static IBrush LogHighlightBorderBrush => GetBrush("Log.HighlightBorderBrush", new SolidColorBrush(Color.FromRgb(168, 179, 72)));
		public static IBrush LogErrorBackgroundBrush => GetBrush("Log.ErrorBackgroundBrush", new SolidColorBrush(Color.FromArgb(26, 255, 68, 68)));
		public static IBrush LogErrorBorderBrush => GetBrush("Log.ErrorBorderBrush", new SolidColorBrush(Color.FromRgb(255, 68, 68)));
		public static IBrush LogErrorBadgeBrush => GetBrush("Log.ErrorBadgeBrush", new SolidColorBrush(Color.FromRgb(255, 68, 68)));
		public static IBrush LogWarningBackgroundBrush => GetBrush("Log.WarningBackgroundBrush", new SolidColorBrush(Color.FromArgb(26, 255, 170, 0)));
		public static IBrush LogWarningBorderBrush => GetBrush("Log.WarningBorderBrush", new SolidColorBrush(Color.FromRgb(255, 170, 0)));
		public static IBrush LogWarningBadgeBrush => GetBrush("Log.WarningBadgeBrush", new SolidColorBrush(Color.FromRgb(255, 170, 0)));
		public static IBrush LogInfoBackgroundBrush => GetBrush("Log.InfoBackgroundBrush", new SolidColorBrush(Color.FromArgb(26, 0, 170, 0)));
		public static IBrush LogInfoBadgeBrush => GetBrush("Log.InfoBadgeBrush", new SolidColorBrush(Color.FromRgb(0, 170, 0)));

		// Expander Brushes
		public static IBrush ExpanderDefaultBackgroundBrush => GetBrush("Expander.DefaultBackgroundBrush", new SolidColorBrush(Color.FromRgb(6, 39, 102)));
		public static IBrush ExpanderDefaultForegroundBrush => GetBrush("Expander.DefaultForegroundBrush", new SolidColorBrush(Color.FromRgb(58, 170, 255)));
		public static IBrush ExpanderHoverBackgroundBrush => GetBrush("Expander.HoverBackgroundBrush", new SolidColorBrush(Color.FromRgb(8, 51, 136)));
		public static IBrush ExpanderHoverForegroundBrush => GetBrush("Expander.HoverForegroundBrush", new SolidColorBrush(Color.FromRgb(168, 179, 72)));
	}
}

