// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using JetBrains.Annotations;

namespace KOTORModSync.Services
{

	public class ScrollNavigationService
	{
		#region Public Methods



		public static async Task ScrollToControlAsync([NotNull] ScrollViewer scrollViewer, [NotNull] Control targetControl, double offsetFromTop = 100)
		{
			if ( scrollViewer == null ) throw new ArgumentNullException(nameof(scrollViewer));
			if ( targetControl == null ) throw new ArgumentNullException(nameof(targetControl));

			try
			{
				double targetPosition = CalculateControlScrollPosition(scrollViewer, targetControl, offsetFromTop);
				await ScrollToPositionSmoothAsync(scrollViewer, targetPosition);
			}
			catch ( Exception ex )
			{
				throw new InvalidOperationException($"Failed to scroll to control: {ex.Message}", ex);
			}
		}



		public static async Task ScrollToPositionSmoothAsync([NotNull] ScrollViewer scrollViewer, double targetOffset, int animationSteps = 20, int stepDelayMs = 16)
		{
			if ( scrollViewer == null ) throw new ArgumentNullException(nameof(scrollViewer));

			double maxOffset = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
			double clampedOffset = Math.Max(0, Math.Min(targetOffset, maxOffset));

			double currentOffset = scrollViewer.Offset.Y;
			double distance = clampedOffset - currentOffset;
			double stepSize = distance / animationSteps;

			for ( int i = 0; i < animationSteps; i++ )
			{
				currentOffset += stepSize;
				scrollViewer.Offset = new Vector(scrollViewer.Offset.X, currentOffset);
				await Task.Delay(stepDelayMs);
			}

			scrollViewer.Offset = new Vector(scrollViewer.Offset.X, clampedOffset);
		}



		public static T FindControlRecursive<T>([CanBeNull] Control parent, [NotNull] Func<T, bool> predicate) where T : Control
		{
			if ( parent == null )
				return null;

			if ( parent is T targetControl && predicate(targetControl) )
			{
				return targetControl;
			}

			IEnumerable<Control> children = parent.GetVisualChildren().OfType<Control>();
			foreach ( Control child in children )
			{
				T result = FindControlRecursive(child, predicate);
				if ( result != null ) return result;
			}
			return null;
		}



		public static ScrollViewer FindScrollViewer([CanBeNull] Control parent)
		{
			return FindControlRecursive<ScrollViewer>(parent, _ => true);
		}


		public static async Task ExpandAndWaitAsync([CanBeNull] Expander expander, int waitTimeMs = 200)
		{
			if ( expander != null && !expander.IsExpanded )
			{
				expander.IsExpanded = true;
				await Task.Delay(waitTimeMs);
			}
		}


		public static async Task NavigateToTabAsync([CanBeNull] TabItem tabItem, int waitTimeMs = 100)
		{
			if ( tabItem != null )
			{
				tabItem.IsSelected = true;
				await Task.Delay(waitTimeMs);
			}
		}

		public static async Task NavigateToControlAsync(
			[CanBeNull] TabItem tabItem = null,
			[CanBeNull] Expander expander = null,
			[CanBeNull] ScrollViewer scrollViewer = null,
			[CanBeNull] Control targetControl = null,
			double? targetPosition = null,
			int expandWaitMs = 200,
			int navigationWaitMs = 100)
		{
			try
			{

				if ( tabItem != null )
				{
					await NavigateToTabAsync(tabItem, navigationWaitMs);
				}

				if ( expander != null )
				{
					await ExpandAndWaitAsync(expander, expandWaitMs);
				}

				if ( scrollViewer != null )
				{
					if ( targetControl != null )
					{
						await ScrollToControlAsync(scrollViewer, targetControl);
					}
					else if ( targetPosition.HasValue )
					{
						await ScrollToPositionSmoothAsync(scrollViewer, targetPosition.Value);
					}
				}
			}
			catch ( Exception ex )
			{
				throw new InvalidOperationException($"Navigation failed: {ex.Message}", ex);
			}
		}

		#endregion

		#region Estimation Methods (Fallback Strategy)


		#endregion

		#region Private Methods


		private static double CalculateControlScrollPosition([NotNull] ScrollViewer scrollViewer, [NotNull] Control targetControl, double offsetFromTop)
		{
			try
			{

				Matrix? transform = targetControl.TransformToVisual(scrollViewer);
				if ( transform == null ) return 0;

				Point targetPoint = transform.Value.Transform(new Point(0, 0));
				Size targetSize = targetControl.Bounds.Size;
				var targetBounds = new Rect(targetPoint, targetSize);

				double targetY = targetBounds.Y;

				double desiredOffset = targetY - offsetFromTop;

				double maxOffset = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
				return Math.Max(0, Math.Min(desiredOffset, maxOffset));
			}
			catch ( Exception )
			{

				return 0;
			}
		}

		public static double CalculateEstimatedScrollPosition(
			[CanBeNull] Grid parentGrid,
			[CanBeNull] Expander targetSectionExpander,
			[CanBeNull] ItemsRepeater itemsRepeater,
			int itemIndex,
			[CanBeNull] ScrollViewer scrollViewport = null)
		{
			double baseOffset = 0;

			try
			{

				if ( parentGrid != null && targetSectionExpander != null )
				{
					Avalonia.Controls.Controls children = parentGrid.Children;
					foreach ( Control child in children )
					{

						if ( child == targetSectionExpander )
						{
							break;
						}

						if ( child is Control control && control.IsVisible )
						{
							Rect bounds = control.Bounds;
							if ( bounds.Height > 0 )
							{
								baseOffset += bounds.Height + control.Margin.Top + control.Margin.Bottom;
							}
						}
					}

					if ( targetSectionExpander.IsVisible )
					{

						Control headerPresenter = FindControlRecursive<Control>(targetSectionExpander,
							c => c.Name == "PART_Header" || c.GetType().Name.Contains("Header"));

						if ( headerPresenter != null && headerPresenter.Bounds.Height > 0 )
						{
							baseOffset += headerPresenter.Bounds.Height;
						}
						else
						{

							baseOffset += targetSectionExpander.Margin.Top + targetSectionExpander.Padding.Top + 30;
						}
					}
				}

				double itemHeight = 0;
				if ( itemsRepeater != null )
				{

					var existingItems = itemsRepeater.GetVisualChildren().OfType<Control>().ToList();
					if ( existingItems.Any() )
					{

						var measuredHeights = existingItems
							.Where(item => item.Bounds.Height > 0)
							.Select(item => item.Bounds.Height + item.Margin.Top + item.Margin.Bottom)
							.ToList();

						if ( measuredHeights.Any() )
						{
							itemHeight = measuredHeights.Average();
						}
					}
				}

				if ( itemHeight == 0 )
				{
					itemHeight = 100;
				}

				baseOffset += itemIndex * itemHeight;

				double centeringOffset;
				if ( scrollViewport != null && scrollViewport.Viewport.Height > 0 )
				{

					centeringOffset = scrollViewport.Viewport.Height * 0.3;
				}
				else
				{
					centeringOffset = 100;
				}

				baseOffset -= centeringOffset;
			}
			catch ( Exception )
			{

				baseOffset = Math.Max(0, itemIndex * 100);
			}

			return Math.Max(0, baseOffset);
		}

		public static double CalculateEstimatedScrollPositionWithSections(
			[CanBeNull] Grid parentGrid,
			[NotNull] Expander targetSectionExpander,
			[CanBeNull] ItemsRepeater itemsRepeater,
			int itemIndex,
			[CanBeNull] ScrollViewer scrollViewport = null)
		{
			if ( targetSectionExpander == null ) throw new ArgumentNullException(nameof(targetSectionExpander));

			double baseOffset = 0;

			try
			{

				if ( parentGrid != null )
				{
					bool foundTarget = false;

					foreach ( Control child in parentGrid.Children )
					{

						if ( child == targetSectionExpander )
						{
							foundTarget = true;

							if ( targetSectionExpander.IsVisible )
							{
								Control headerPresenter = FindControlRecursive<Control>(targetSectionExpander,
									c => c.Name == "PART_Header" || c.GetType().Name.Contains("Header"));

								if ( headerPresenter != null && headerPresenter.Bounds.Height > 0 )
								{
									baseOffset += headerPresenter.Bounds.Height;
								}
								else
								{
									baseOffset += targetSectionExpander.Margin.Top + targetSectionExpander.Padding.Top + 30;
								}
							}
							break;
						}

						if ( child is Expander expander )
						{
							if ( expander.IsVisible )
							{

								Control headerPresenter = FindControlRecursive<Control>(expander,
									c => c.Name == "PART_Header" || c.GetType().Name.Contains("Header"));

								if ( headerPresenter != null && headerPresenter.Bounds.Height > 0 )
								{
									baseOffset += headerPresenter.Bounds.Height;
								}
								else
								{
									baseOffset += 30;
								}

								if ( expander.IsExpanded )
								{
									Control contentPresenter = FindControlRecursive<Control>(expander,
										c => c.Name == "PART_Content" || c.GetType().Name.Contains("Content"));

									if ( contentPresenter != null && contentPresenter.Bounds.Height > 0 )
									{
										baseOffset += contentPresenter.Bounds.Height;
									}
								}

								baseOffset += expander.Margin.Top + expander.Margin.Bottom;
							}
						}
						else if ( child is Control control && control.IsVisible )
						{

							if ( control.Bounds.Height > 0 )
							{
								baseOffset += control.Bounds.Height + control.Margin.Top + control.Margin.Bottom;
							}
						}
					}

					if ( !foundTarget )
					{

						return 0;
					}
				}

				double itemHeight = 0;
				if ( itemsRepeater != null )
				{
					var existingItems = itemsRepeater.GetVisualChildren().OfType<Control>().ToList();
					if ( existingItems.Count != 0 )
					{
						var measuredHeights = existingItems
							.Where(item => item.Bounds.Height > 0)
							.Select(item => item.Bounds.Height + item.Margin.Top + item.Margin.Bottom)
							.ToList();

						if ( measuredHeights.Count != 0 )
						{
							itemHeight = measuredHeights.Average();
						}
					}
				}

				if ( itemHeight == 0 )
				{
					itemHeight = 100;
				}

				baseOffset += itemIndex * itemHeight;

				double centeringOffset;
				if ( scrollViewport != null && scrollViewport.Viewport.Height > 0 )
				{
					centeringOffset = scrollViewport.Viewport.Height * 0.3;
				}
				else
				{
					centeringOffset = 100;
				}

				baseOffset -= centeringOffset;
			}
			catch ( Exception )
			{

				baseOffset = Math.Max(0, itemIndex * 100);
			}

			return Math.Max(0, baseOffset);
		}

		#endregion
	}

	public static class ScrollNavigationExtensions
	{



		public static async Task ScrollToControlAsync<T>(
			this ScrollViewer scrollViewer,
			[NotNull] Control parent,
			[NotNull] Func<T, bool> predicate,
			double offsetFromTop = 100) where T : Control
		{
			T targetControl = ScrollNavigationService.FindControlRecursive(parent, predicate);
			if ( targetControl != null )
			{
				await ScrollNavigationService.ScrollToControlAsync(scrollViewer, targetControl, offsetFromTop);
			}
		}



		public static async Task ScrollToControlByDataContextAsync<TControl, TDataContext>(
			this ScrollViewer scrollViewer,
			[NotNull] Control parent,
			[NotNull] Func<TDataContext, bool> dataContextMatcher,
			double offsetFromTop = 100)
			where TControl : Control
			where TDataContext : class
		{
			TControl targetControl = ScrollNavigationService.FindControlRecursive<TControl>(parent, control =>
				control.DataContext is TDataContext dataContext && dataContextMatcher(dataContext));

			if ( targetControl != null )
			{
				await ScrollNavigationService.ScrollToControlAsync(scrollViewer, targetControl, offsetFromTop);
			}
		}
	}
}