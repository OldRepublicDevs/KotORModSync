



using Avalonia;
using Avalonia.Media;

namespace KOTORModSync
{
	
	
	
	
	public static class ThemeResourceHelper
	{
		
		
		
		
		
		
		
		public static IBrush GetBrush(string resourceKey, IBrush fallback = null)
		{
			if ( Application.Current?.Resources.TryGetResource(resourceKey, null, out object resource) != true )
				return fallback ?? Brushes.Transparent;
			if ( resource is IBrush brush )
				return brush;
			return fallback ?? Brushes.Transparent;
		}

		
		public static IBrush MergeStatusNewBrush => GetBrush("MergeStatus.NewBrush", Brushes.LightGreen);
		public static IBrush MergeStatusExistingOnlyBrush => GetBrush("MergeStatus.ExistingOnlyBrush", Brushes.LightGray);
		public static IBrush MergeStatusMatchedBrush => GetBrush("MergeStatus.MatchedBrush", Brushes.Yellow);
		public static IBrush MergeStatusUpdatedBrush => GetBrush("MergeStatus.UpdatedBrush", Brushes.Orange);
		public static IBrush MergeStatusDefaultBrush => GetBrush("MergeStatus.DefaultBrush", Brushes.White);

		
		public static IBrush MergeSelectionBorderBrush => GetBrush("MergeSelection.BorderBrush", Brushes.Cyan);
		public static IBrush MergeSelectionBackgroundBrush => GetBrush("MergeSelection.BackgroundBrush");

		
		public static IBrush MergeSourceIncomingBrush => GetBrush("MergeSource.IncomingBrush", Brushes.Green);
		public static IBrush MergeSourceExistingBrush => GetBrush("MergeSource.ExistingBrush", Brushes.Blue);

		
		public static IBrush MergePositionChangedBrush => GetBrush("MergePosition.ChangedBrush", Brushes.Orange);
		public static IBrush MergePositionNewBrush => GetBrush("MergePosition.NewBrush", Brushes.Yellow);

		
		public static IBrush MergeDiffUnchangedBrush => GetBrush("MergeDiff.UnchangedBrush", Brushes.Black);
		public static IBrush MergeDiffAddedBrush => GetBrush("MergeDiff.AddedBrush", Brushes.DarkGreen);
		public static IBrush MergeDiffRemovedBrush => GetBrush("MergeDiff.RemovedBrush", Brushes.DarkRed);
		public static IBrush MergeDiffModifiedBrush => GetBrush("MergeDiff.ModifiedBrush", Brushes.DarkGoldenrod);

		
		public static IBrush DependencyWarningForeground => GetBrush("Dependency.WarningForeground", Brushes.Gold);
		public static IBrush DependencyWarningBackground => GetBrush("Dependency.WarningBackground");
		public static IBrush DependencyWarningBorder => GetBrush("Dependency.WarningBorder", Brushes.Gold);

		
		public static IBrush UrlValidationValidBrush => GetBrush("UrlValidation.ValidBrush", new SolidColorBrush(Color.FromRgb(76, 175, 80)));
		public static IBrush UrlValidationWarningBrush => GetBrush("UrlValidation.WarningBrush", new SolidColorBrush(Color.FromRgb(255, 165, 0)));
		public static IBrush UrlValidationInvalidBrush => GetBrush("UrlValidation.InvalidBrush", new SolidColorBrush(Color.FromRgb(244, 67, 54)));
		public static IBrush UrlValidationErrorIconBrush => GetBrush("UrlValidation.ErrorIconBrush", new SolidColorBrush(Color.FromRgb(244, 67, 54)));

		
		public static IBrush ModListItemErrorBrush => GetBrush("ModListItem.ErrorBrush", new SolidColorBrush(Color.FromRgb(255, 107, 107)));
		public static IBrush ModListItemWarningBrush => GetBrush("ModListItem.WarningBrush", new SolidColorBrush(Color.FromRgb(255, 165, 0)));
		public static IBrush ModListItemHoverErrorBrush => GetBrush("ModListItem.HoverErrorBrush", new SolidColorBrush(Color.FromRgb(255, 136, 136)));
		public static IBrush ModListItemHoverWarningBrush => GetBrush("ModListItem.HoverWarningBrush", new SolidColorBrush(Color.FromRgb(255, 184, 77)));
		public static IBrush ModListItemHoverDefaultBrush => GetBrush("ModListItem.HoverDefaultBrush", new SolidColorBrush(Color.FromRgb(168, 179, 72)));
		public static IBrush ModListItemHoverBackgroundBrush => GetBrush("ModListItem.HoverBackgroundBrush");
		public static IBrush ModListItemDefaultBackgroundBrush => GetBrush("ModListItem.DefaultBackgroundBrush");

		
		public static IBrush ComponentDependencyBrush => GetBrush("ModComponent.DependencyBrush", new SolidColorBrush(Color.FromRgb(76, 175, 80)));
		public static IBrush ComponentRestrictionBrush => GetBrush("ModComponent.RestrictionBrush", new SolidColorBrush(Color.FromRgb(244, 67, 54)));

		
		public static IBrush ValidationSolutionBrush => GetBrush("Validation.SolutionBrush", new SolidColorBrush(Color.FromRgb(168, 179, 72)));

		
		public static IBrush LogHighlightBorderBrush => GetBrush("Log.HighlightBorderBrush", new SolidColorBrush(Color.FromRgb(168, 179, 72)));
		public static IBrush LogErrorBackgroundBrush => GetBrush("Log.ErrorBackgroundBrush", new SolidColorBrush(Color.FromArgb(26, 255, 68, 68)));
		public static IBrush LogErrorBorderBrush => GetBrush("Log.ErrorBorderBrush", new SolidColorBrush(Color.FromRgb(255, 68, 68)));
		public static IBrush LogErrorBadgeBrush => GetBrush("Log.ErrorBadgeBrush", new SolidColorBrush(Color.FromRgb(255, 68, 68)));
		public static IBrush LogWarningBackgroundBrush => GetBrush("Log.WarningBackgroundBrush");
		public static IBrush LogWarningBorderBrush => GetBrush("Log.WarningBorderBrush", new SolidColorBrush(Color.FromRgb(255, 170, 0)));
		public static IBrush LogWarningBadgeBrush => GetBrush("Log.WarningBadgeBrush", new SolidColorBrush(Color.FromRgb(255, 170, 0)));
		public static IBrush LogInfoBackgroundBrush => GetBrush("Log.InfoBackgroundBrush", new SolidColorBrush(Color.FromArgb(26, 0, 170, 0)));
		public static IBrush LogInfoBadgeBrush => GetBrush("Log.InfoBadgeBrush", new SolidColorBrush(Color.FromRgb(0, 170, 0)));

		
		public static IBrush ExpanderDefaultBackgroundBrush => GetBrush("Expander.DefaultBackgroundBrush");
		public static IBrush ExpanderDefaultForegroundBrush => GetBrush("Expander.DefaultForegroundBrush");
		public static IBrush ExpanderHoverBackgroundBrush => GetBrush("Expander.HoverBackgroundBrush");
		public static IBrush ExpanderHoverForegroundBrush => GetBrush("Expander.HoverForegroundBrush");
	}
}

