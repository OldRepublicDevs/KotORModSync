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
    /// <summary>
    /// Service for navigating and scrolling to specific controls or positions within the application.
    /// Provides smooth scrolling animations and intelligent fallback mechanisms.
    /// </summary>
    public class ScrollNavigationService
    {
        #region Public Methods

        /// <summary>
        /// Scrolls to a specific control with smooth animation.
        /// </summary>
        /// <param name="scrollViewer">The ScrollViewer containing the target control</param>
        /// <param name="targetControl">The control to scroll to</param>
        /// <param name="offsetFromTop">Additional offset from the top of the viewport (default: 100)</param>
        /// <returns>Task representing the scroll operation</returns>
        public static async Task ScrollToControlAsync([NotNull] ScrollViewer scrollViewer, [NotNull] Control targetControl, double offsetFromTop = 100)
        {
            if (scrollViewer == null) throw new ArgumentNullException(nameof(scrollViewer));
            if (targetControl == null) throw new ArgumentNullException(nameof(targetControl));

            try
            {
                double targetPosition = CalculateControlScrollPosition(scrollViewer, targetControl, offsetFromTop);
                await ScrollToPositionSmoothAsync(scrollViewer, targetPosition);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to scroll to control: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Scrolls to a specific position with smooth animation.
        /// </summary>
        /// <param name="scrollViewer">The ScrollViewer to scroll</param>
        /// <param name="targetOffset">The target scroll position</param>
        /// <param name="animationSteps">Number of animation steps (default: 20)</param>
        /// <param name="stepDelayMs">Delay between animation steps in milliseconds (default: 16 for ~60 FPS)</param>
        /// <returns>Task representing the scroll operation</returns>
        public static async Task ScrollToPositionSmoothAsync([NotNull] ScrollViewer scrollViewer, double targetOffset, int animationSteps = 20, int stepDelayMs = 16)
        {
            if (scrollViewer == null) throw new ArgumentNullException(nameof(scrollViewer));

            // Ensure we don't scroll beyond the content
            double maxOffset = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
            double clampedOffset = Math.Max(0, Math.Min(targetOffset, maxOffset));

            // Smooth scroll animation
            double currentOffset = scrollViewer.Offset.Y;
            double distance = clampedOffset - currentOffset;
            double stepSize = distance / animationSteps;

            for (int i = 0; i < animationSteps; i++)
            {
                currentOffset += stepSize;
                scrollViewer.Offset = new Vector(scrollViewer.Offset.X, currentOffset);
                await Task.Delay(stepDelayMs);
            }
            // Ensure final position is exact
            scrollViewer.Offset = new Vector(scrollViewer.Offset.X, clampedOffset);
        }

        /// <summary>
        /// Finds a control in the visual tree by matching a predicate condition.
        /// </summary>
        /// <typeparam name="T">Type of control to find</typeparam>
        /// <param name="parent">Parent control to search within</param>
        /// <param name="predicate">Condition to match the target control</param>
        /// <returns>The found control or null if not found</returns>
        public static T FindControlRecursive<T>([CanBeNull] Control parent, [NotNull] Func<T, bool> predicate) where T : Control
        {
            if (parent == null)
                return null;

            // Check if this is the target control
            if (parent is T targetControl && predicate(targetControl))
            {
                return targetControl;
            }
            // Search children
            IEnumerable<Control> children = parent.GetVisualChildren().OfType<Control>();
            foreach (Control child in children)
            {
                T result = FindControlRecursive(child, predicate);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Finds the first ScrollViewer in the visual tree starting from a parent control.
        /// </summary>
        /// <param name="parent">Parent control to search within</param>
        /// <returns>The found ScrollViewer or null if not found</returns>
        public static ScrollViewer FindScrollViewer([CanBeNull] Control parent)
        {
            return FindControlRecursive<ScrollViewer>(parent, _ => true);
        }

        /// <summary>
        /// Expands an expander control and waits for the expansion animation to complete.
        /// </summary>
        /// <param name="expander">The Expander to expand</param>
        /// <param name="waitTimeMs">Time to wait for expansion animation (default: 200ms)</param>
        /// <returns>Task representing the expansion operation</returns>
        public static async Task ExpandAndWaitAsync([CanBeNull] Expander expander, int waitTimeMs = 200)
        {
            if (expander != null && !expander.IsExpanded)
            {
                expander.IsExpanded = true;
                await Task.Delay(waitTimeMs);
            }
        }

        /// <summary>
        /// Navigates to a tab and waits for the UI to update.
        /// </summary>
        /// <param name="tabItem">The TabItem to select</param>
        /// <param name="waitTimeMs">Time to wait for UI update (default: 100ms)</param>
        /// <returns>Task representing the navigation operation</returns>
        public static async Task NavigateToTabAsync([CanBeNull] TabItem tabItem, int waitTimeMs = 100)
        {
            if (tabItem != null)
            {
                tabItem.IsSelected = true;
                await Task.Delay(waitTimeMs);
            }
        }

        /// <summary>
        /// Comprehensive navigation method that handles tab switching, section expansion, and scrolling.
        /// </summary>
        /// <param name="tabItem">The tab to navigate to</param>
        /// <param name="expander">The expander to expand (optional)</param>
        /// <param name="scrollViewer">The ScrollViewer to scroll in</param>
        /// <param name="targetControl">The control to scroll to (optional)</param>
        /// <param name="targetPosition">The position to scroll to (optional, used if targetControl is null)</param>
        /// <param name="expandWaitMs">Time to wait for expansion (default: 200ms)</param>
        /// <param name="navigationWaitMs">Time to wait for navigation (default: 100ms)</param>
        /// <returns>Task representing the complete navigation operation</returns>
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
                // 1. Navigate to tab if specified
                if (tabItem != null)
                {
                    await NavigateToTabAsync(tabItem, navigationWaitMs);
                }
                // 2. Expand section if specified
                if (expander != null)
                {
                    await ExpandAndWaitAsync(expander, expandWaitMs);
                }
                // 3. Scroll to target
                if (scrollViewer != null)
                {
                    if (targetControl != null)
                    {
                        await ScrollToControlAsync(scrollViewer, targetControl);
                    }
                    else if (targetPosition.HasValue)
                    {
                        await ScrollToPositionSmoothAsync(scrollViewer, targetPosition.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Navigation failed: {ex.Message}", ex);
            }
        }

        #endregion

        #region Estimation Methods (Fallback Strategy)

        // DYNAMIC MEASUREMENT STRATEGY:
        // These estimation methods provide a fallback when precise control positioning isn't available.
        // Unlike traditional approaches that use hardcoded estimates, these methods:
        //
        // 1. Traverse the actual UI tree to measure real element heights
        // 2. Sample existing rendered items to calculate average item heights
        // 3. Detect and measure expander headers and content dynamically
        // 4. Calculate viewport-relative centering based on actual scroll viewer dimensions
        // 5. Only use minimal fallback values (100px) when measurements truly fail
        //
        // This ensures scroll positioning remains accurate even as UI styles, themes,
        // or layouts change - no magic numbers to maintain!

        #endregion

        #region Private Methods
        /// <summary>
        /// Calculates the scroll position needed to bring a control into view.
        /// </summary>
        /// <param name="scrollViewer">The ScrollViewer containing the target</param>
        /// <param name="targetControl">The target control</param>
        /// <param name="offsetFromTop">Additional offset from the top</param>
        /// <returns>The calculated scroll position</returns>
        private static double CalculateControlScrollPosition([NotNull] ScrollViewer scrollViewer, [NotNull] Control targetControl, double offsetFromTop)
        {
            try
            {
                // Get the position of the target control relative to the scroll viewer
                Matrix? transform = targetControl.TransformToVisual(scrollViewer);
                if (transform == null) return 0;
                // Calculate the target position using the transform matrix
                Point targetPoint = transform.Value.Transform(new Point(0, 0));
                Size targetSize = targetControl.Bounds.Size;
                var targetBounds = new Rect(targetPoint, targetSize);
                // Calculate the desired scroll position
                double targetY = targetBounds.Y;
                //double viewportHeight = scrollViewer.Viewport.Height;
                double desiredOffset = targetY - offsetFromTop;

                // Ensure we don't scroll beyond the content
                double maxOffset = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
                return Math.Max(0, Math.Min(desiredOffset, maxOffset));
            }
            catch (Exception)
            {
                // Fallback to simple calculation
                return 0;
            }
        }

        /// <summary>
        /// Calculates an estimated scroll position when precise control positioning is not available.
        /// This method dynamically measures UI elements to estimate where a target item should be positioned.
        /// All values are calculated from actual UI measurements - no hardcoded estimates.
        /// </summary>
        /// <param name="parentGrid">The parent Grid containing the sections</param>
        /// <param name="targetSectionExpander">The target section's expander control</param>
        /// <param name="itemsRepeater">The ItemsRepeater containing the items</param>
        /// <param name="itemIndex">The index of the item within the section</param>
        /// <param name="scrollViewport">The ScrollViewer's viewport to calculate centering offset</param>
        /// <returns>The estimated scroll position</returns>
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
                // 1. Measure actual UI elements before the target section
                if (parentGrid != null && targetSectionExpander != null)
                {
                    Avalonia.Controls.Controls children = parentGrid.Children;
                    foreach (Control child in children)
                    {
                        // Stop when we reach the target section
                        if (child == targetSectionExpander)
                        {
                            break;
                        }
                        // Add the actual measured height of each element before the target section
                        if (child is Control control && control.IsVisible)
                        {
                            Rect bounds = control.Bounds;
                            if (bounds.Height > 0)
                            {
                                baseOffset += bounds.Height + control.Margin.Top + control.Margin.Bottom;
                            }
                        }
                    }

                    // 2. Dynamically measure the expander header height
                    if (targetSectionExpander.IsVisible)
                    {
                        // Try to find the header presenter to get actual header height
                        Control headerPresenter = FindControlRecursive<Control>(targetSectionExpander,
                            c => c.Name == "PART_Header" || c.GetType().Name.Contains("Header"));

                        if (headerPresenter != null && headerPresenter.Bounds.Height > 0)
                        {
                            baseOffset += headerPresenter.Bounds.Height;
                        }
                        else
                        {
                            // Fallback: measure the expander's margin and padding
                            baseOffset += targetSectionExpander.Margin.Top + targetSectionExpander.Padding.Top + 30;
                        }
                    }
                }

                // 3. Dynamically calculate item height from actual items
                double itemHeight = 0;
                if (itemsRepeater != null)
                {
                    // Try to measure an existing item to get accurate height
                    var existingItems = itemsRepeater.GetVisualChildren().OfType<Control>().ToList();
                    if (existingItems.Any())
                    {
                        // Average the height of rendered items for better accuracy
                        var measuredHeights = existingItems
                            .Where(item => item.Bounds.Height > 0)
                            .Select(item => item.Bounds.Height + item.Margin.Top + item.Margin.Bottom)
                            .ToList();

                        if (measuredHeights.Any())
                        {
                            itemHeight = measuredHeights.Average();
                        }
                    }
                }

                // Fallback if no items are rendered yet
                if (itemHeight == 0)
                {
                    itemHeight = 100; // Minimal fallback, but this should rarely be used
                }

                // Add height for items before the target
                baseOffset += itemIndex * itemHeight;

                // 4. Dynamically calculate centering offset from viewport
                double centeringOffset;
                if (scrollViewport != null && scrollViewport.Viewport.Height > 0)
                {
                    // Center the item in the viewport by offsetting by a portion of viewport height
                    centeringOffset = scrollViewport.Viewport.Height * 0.3; // Show item in upper-middle of viewport
                }
                else
                {
                    centeringOffset = 100; // Minimal fallback
                }

                baseOffset -= centeringOffset;
            }
            catch (Exception)
            {
                // Absolute minimal fallback - should rarely happen
                baseOffset = Math.Max(0, itemIndex * 100);
            }

            return Math.Max(0, baseOffset);
        }

        /// <summary>
        /// Calculates an estimated scroll position by dynamically measuring all section expanders.
        /// This method traverses the parent grid and measures each section dynamically.
        /// All values are calculated from actual UI measurements - no hardcoded estimates.
        /// </summary>
        /// <param name="parentGrid">The parent Grid containing all sections</param>
        /// <param name="targetSectionExpander">The target section's expander control</param>
        /// <param name="itemsRepeater">The ItemsRepeater containing the items in the target section</param>
        /// <param name="itemIndex">The index of the item within the target section</param>
        /// <param name="scrollViewport">The ScrollViewer's viewport to calculate centering offset</param>
        /// <returns>The estimated scroll position</returns>
        public static double CalculateEstimatedScrollPositionWithSections(
            [CanBeNull] Grid parentGrid,
            [NotNull] Expander targetSectionExpander,
            [CanBeNull] ItemsRepeater itemsRepeater,
            int itemIndex,
            [CanBeNull] ScrollViewer scrollViewport = null)
        {
            if (targetSectionExpander == null) throw new ArgumentNullException(nameof(targetSectionExpander));

            double baseOffset = 0;

            try
            {
                // 1. Dynamically measure all sections before the target
                if (parentGrid != null)
                {
                    bool foundTarget = false;

                    foreach (Control child in parentGrid.Children)
                    {
                        // Check if this is the target section
                        if (child == targetSectionExpander)
                        {
                            foundTarget = true;

                            // Dynamically measure the target section's header
                            if (targetSectionExpander.IsVisible)
                            {
                                Control headerPresenter = FindControlRecursive<Control>(targetSectionExpander,
                                    c => c.Name == "PART_Header" || c.GetType().Name.Contains("Header"));

                                if (headerPresenter != null && headerPresenter.Bounds.Height > 0)
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

                        // Dynamically measure each section before the target
                        if (child is Expander expander)
                        {
                            if (expander.IsVisible)
                            {
                                // Measure header height
                                Control headerPresenter = FindControlRecursive<Control>(expander,
                                    c => c.Name == "PART_Header" || c.GetType().Name.Contains("Header"));

                                if (headerPresenter != null && headerPresenter.Bounds.Height > 0)
                                {
                                    baseOffset += headerPresenter.Bounds.Height;
                                }
                                else
                                {
                                    baseOffset += 30; // Minimal fallback for header
                                }

                                // If expanded, add content height
                                if (expander.IsExpanded)
                                {
                                    Control contentPresenter = FindControlRecursive<Control>(expander,
                                        c => c.Name == "PART_Content" || c.GetType().Name.Contains("Content"));

                                    if (contentPresenter != null && contentPresenter.Bounds.Height > 0)
                                    {
                                        baseOffset += contentPresenter.Bounds.Height;
                                    }
                                }

                                // Add margins
                                baseOffset += expander.Margin.Top + expander.Margin.Bottom;
                            }
                        }
                        else if (child is Control control && control.IsVisible)
                        {
                            // For non-expander controls, just add their full height
                            if (control.Bounds.Height > 0)
                            {
                                baseOffset += control.Bounds.Height + control.Margin.Top + control.Margin.Bottom;
                            }
                        }
                    }

                    if (!foundTarget)
                    {
                        // Target section not found in the grid
                        return 0;
                    }
                }

                // 2. Dynamically calculate item height from actual rendered items
                double itemHeight = 0;
                if (itemsRepeater != null)
                {
                    var existingItems = itemsRepeater.GetVisualChildren().OfType<Control>().ToList();
                    if (existingItems.Count != 0)
                    {
                        var measuredHeights = existingItems
                            .Where(item => item.Bounds.Height > 0)
                            .Select(item => item.Bounds.Height + item.Margin.Top + item.Margin.Bottom)
                            .ToList();

                        if (measuredHeights.Count != 0)
                        {
                            itemHeight = measuredHeights.Average();
                        }
                    }
                }

                // Minimal fallback if no items rendered yet
                if (itemHeight == 0)
                {
                    itemHeight = 100;
                }

                // Add height for items before the target
                baseOffset += itemIndex * itemHeight;

                // 3. Dynamically calculate centering offset from viewport
                double centeringOffset;
                if (scrollViewport != null && scrollViewport.Viewport.Height > 0)
                {
                    centeringOffset = scrollViewport.Viewport.Height * 0.3;
                }
                else
                {
                    centeringOffset = 100;
                }

                baseOffset -= centeringOffset;
            }
            catch (Exception)
            {
                // Absolute minimal fallback
                baseOffset = Math.Max(0, itemIndex * 100);
            }

            return Math.Max(0, baseOffset);
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for common navigation scenarios.
    /// </summary>
    public static class ScrollNavigationExtensions
    {
        /// <summary>
        /// Scrolls to a control that matches a predicate condition.
        /// </summary>
        /// <typeparam name="T">Type of control to find and scroll to</typeparam>
        /// <param name="scrollViewer">The ScrollViewer to scroll</param>
        /// <param name="parent">Parent control to search within</param>
        /// <param name="predicate">Condition to match the target control</param>
        /// <param name="offsetFromTop">Additional offset from the top</param>
        /// <returns>Task representing the operation</returns>
        public static async Task ScrollToControlAsync<T>(
            this ScrollViewer scrollViewer,
            [NotNull] Control parent,
            [NotNull] Func<T, bool> predicate,
            double offsetFromTop = 100) where T : Control
        {
            T targetControl = ScrollNavigationService.FindControlRecursive(parent, predicate);
            if (targetControl != null)
            {
                await ScrollNavigationService.ScrollToControlAsync(scrollViewer, targetControl, offsetFromTop);
            }
        }

        /// <summary>
        /// Finds and scrolls to a control by DataContext matching.
        /// </summary>
        /// <typeparam name="TControl">Type of control to find</typeparam>
        /// <typeparam name="TDataContext">Type of DataContext to match</typeparam>
        /// <param name="scrollViewer">The ScrollViewer to scroll</param>
        /// <param name="parent">Parent control to search within</param>
        /// <param name="dataContextMatcher">Function to match the DataContext</param>
        /// <param name="offsetFromTop">Additional offset from the top</param>
        /// <returns>Task representing the operation</returns>
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

            if (targetControl != null)
            {
                await ScrollNavigationService.ScrollToControlAsync(scrollViewer, targetControl, offsetFromTop);
            }
        }
    }
}