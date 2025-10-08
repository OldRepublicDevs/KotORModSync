// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using JetBrains.Annotations;
using KOTORModSync.Core;
using static KOTORModSync.Dialogs.ComponentMergeConflictViewModel;
using ModComponent = KOTORModSync.Core.ModComponent;

namespace KOTORModSync.Dialogs
{
	public partial class ComponentMergeConflictDialog : Window
	{
		public ComponentMergeConflictViewModel ViewModel { get; private set; }
		public bool UserConfirmed { get; private set; }
		public List<ModComponent> MergedComponents { get; private set; }
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		public ComponentMergeConflictDialog()
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif

			// Attach window move event handlers
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		public ComponentMergeConflictDialog(
			[NotNull] List<ModComponent> existingComponents,
			[NotNull] List<ModComponent> incomingComponents,
			[NotNull] string existingSource,
			[NotNull] string incomingSource,
			[NotNull] Func<ModComponent, ModComponent, bool> matchFunc)
		{
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
#endif

			ViewModel = new ComponentMergeConflictViewModel(
				existingComponents,
				incomingComponents,
				existingSource,
				incomingSource,
				matchFunc);

			DataContext = ViewModel;

			// Wire up events
			ViewModel.JumpToRawViewRequested += OnJumpToRawViewRequested;
			ViewModel.SyncSelectionRequested += OnSyncSelectionRequested;

			// Attach window move event handlers
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

		private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

		private void MaximizeButton_Click(object sender, RoutedEventArgs e)
		{
			if ( !(sender is Button maximizeButton) )
				return;

			if ( WindowState == WindowState.Maximized )
			{
				WindowState = WindowState.Normal;
				maximizeButton.Content = "▢";
			}
			else
			{
				WindowState = WindowState.Maximized;
				maximizeButton.Content = "▣";
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			UserConfirmed = false;
			Close();
		}

		private void Continue_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				MergedComponents = ViewModel.GetMergedComponents();
				UserConfirmed = true;
				Close();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error merging components");
			}
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			UserConfirmed = false;
			Close();
		}

		private void OnItemClicked(object sender, PointerPressedEventArgs e)
		{
			if ( !(sender is Border border) || !(border.DataContext is ComponentConflictItem item) )
				return;
			// Single click: highlight and show details
			if ( item.IsFromExisting )
				ViewModel.SelectedExistingItem = item;
			else
				ViewModel.SelectedIncomingItem = item;

			e.Handled = true;
		}

		private void OnItemContextRequested(object sender, RoutedEventArgs e)
		{
			// Select the item when context menu is requested
			if ( !(sender is Border border) || !(border.DataContext is ComponentConflictItem item) )
				return;
			if ( item.IsFromExisting )
				ViewModel.SelectedExistingItem = item;
			else
				ViewModel.SelectedIncomingItem = item;
		}

		private void LinkSelectedMenuItem_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( ViewModel?.LinkSelectedCommand?.CanExecute(null) == true )
					ViewModel.LinkSelectedCommand.Execute(null);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error linking selected components");
			}
		}

		private void UnlinkMenuItem_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( ViewModel?.UnlinkSelectedCommand?.CanExecute(null) == true )
					ViewModel.UnlinkSelectedCommand.Execute(null);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error unlinking component");
			}
		}

		private void UseThisGuid_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( !(sender is MenuItem menuItem) || !(menuItem.Parent is ContextMenu contextMenu) || !(contextMenu.Parent is Border border) )
					return;

				if ( !(border.DataContext is ComponentConflictItem item) )
					return;

				ViewModel.ChooseGuidForItem(item);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error choosing GUID");
			}
		}

		private void ExistingTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			try
			{
				if ( !(sender is TabControl tabControl) )
					return;

				// If Raw TOML tab is selected (index 1), generate the TOML
				if ( tabControl.SelectedIndex == 1 )
					ViewModel?.UpdateExistingTomlView();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error updating existing TOML view");
			}
		}

		private void IncomingTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			try
			{
				if ( !(sender is TabControl tabControl) )
					return;

				// If Raw TOML tab is selected (index 1), generate the TOML
				if ( tabControl.SelectedIndex == 1 )
					ViewModel?.UpdateIncomingTomlView();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error updating incoming TOML view");
			}
		}

		private void MergedTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			try
			{
				if ( !(sender is TabControl tabControl) )
					return;

				// If Merged TOML tab is selected (index 1), generate the TOML
				if ( tabControl.SelectedIndex == 1 )
					ViewModel?.UpdateMergedTomlView();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error updating merged TOML view");
			}
		}

		private void OnJumpToRawViewRequested(object sender, JumpToRawViewEventArgs e)
		{
			try
			{
				if ( e.Item == null ) return;

				// Switch to raw TOML tab and scroll to the component
				if ( e.Item.IsFromExisting )
				{
					// Find the existing tab control and switch to Raw TOML tab (index 1)
					TabControl existingTabControl = this.FindControl<TabControl>("ExistingTabControl");
					if ( existingTabControl == null )
						return;
					existingTabControl.SelectedIndex = 1;
					// Generate TOML if not already generated
					ViewModel?.UpdateExistingTomlView();

					// Scroll to the component's line
					if ( ViewModel == null )
						return;
					int lineNumber = ViewModel.GetComponentLineNumber(e.Item);
					if ( lineNumber > 0 )
					{
						// Use dispatcher to ensure the view is rendered before scrolling
						Avalonia.Threading.Dispatcher.UIThread.Post(() => ScrollToLineInTomlView(existingTabControl, lineNumber), Avalonia.Threading.DispatcherPriority.Loaded);
					}
				}
				else
				{
					// Find the incoming tab control and switch to Raw TOML tab (index 1)
					TabControl incomingTabControl = this.FindControl<TabControl>("IncomingTabControl");
					if ( incomingTabControl == null )
						return;
					incomingTabControl.SelectedIndex = 1;
					// Generate TOML if not already generated
					ViewModel?.UpdateIncomingTomlView();

					// Scroll to the component's line
					if ( ViewModel == null )
						return;
					int lineNumber = ViewModel.GetComponentLineNumber(e.Item);
					if ( lineNumber > 0 )
					{
						// Use dispatcher to ensure the view is rendered before scrolling
						Avalonia.Threading.Dispatcher.UIThread.Post(() => ScrollToLineInTomlView(incomingTabControl, lineNumber), Avalonia.Threading.DispatcherPriority.Loaded);
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error jumping to raw view");
			}
		}

		private void ScrollToLineInTomlView(TabControl tabControl, int lineNumber)
		{
			try
			{
				// Find the ScrollViewer in the Raw TOML tab (index 1)
				if ( tabControl.SelectedIndex != 1 ) return;

				ScrollViewer scrollViewer = ComponentMergeConflictDialog.FindScrollViewerInTab(tabControl);
				if ( scrollViewer == null ) return;

				// Find the ListBox containing the TOML lines
				ListBox listBox = FindDescendant<ListBox>(scrollViewer);
				if ( listBox == null || listBox.ItemCount == 0 ) return;

				// Ensure we're scrolled within bounds
				Avalonia.Threading.Dispatcher.UIThread.Post(() =>
				{
					try
					{
						// Clamp line number to valid range
						int targetIndex = Math.Max(0, Math.Min(lineNumber - 1, listBox.ItemCount - 1));

						// Measure actual item height from first item
						if ( listBox.ContainerFromIndex(0) is Control firstItem && firstItem.Bounds.Height > 0 )
						{
							double itemHeight = firstItem.Bounds.Height;
							double targetOffset = Math.Max(0, (targetIndex - 2) * itemHeight); // Subtract 2 for some context
							scrollViewer.Offset = new Vector(0, targetOffset);
						}
						else
						{
							// Fallback: use BringIntoView if we can't measure
							if ( listBox.ContainerFromIndex(targetIndex) is Control targetItem )
								targetItem.BringIntoView();
						}
					}
					catch ( Exception innerEx )
					{
						Logger.LogException(innerEx, "Error in scroll dispatcher action");
					}
				}, Avalonia.Threading.DispatcherPriority.Background);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error scrolling to line");
			}
		}

		private static ScrollViewer FindScrollViewerInTab(TabControl tabControl)
		{
			try
			{
				return !(tabControl.SelectedItem is TabItem selectedTab)
					   ? null
					   : FindDescendant<ScrollViewer>(selectedTab);
			}
			catch
			{
				return null;
			}
		}

		private static T FindDescendant<T>(Control control) where T : Control
		{
			if ( control == null ) return null;

			if ( control is T result )
				return result;

			IEnumerable<Visual> visualChildren = control.GetVisualChildren();
			foreach ( Visual child in visualChildren )
			{
				if ( !(child is Control childControl) )
					continue;
				T descendant = FindDescendant<T>(childControl);
				if ( descendant != null )
					return descendant;
			}

			return null;
		}

		private void OnSyncSelectionRequested(object sender, SyncSelectionEventArgs e)
		{
			try
			{
				if ( e.MatchedItem == null ) return;

				// Scroll to the matched item in the appropriate list
				// Note: The ViewModel already handles highlighting, we just need to scroll
				Avalonia.Threading.Dispatcher.UIThread.Post(() =>
				{
					// Scroll in the matched list (existing or incoming)
					string scrollViewerName = e.MatchedItem.IsFromExisting ? "ExistingListScrollViewer" : "IncomingListScrollViewer";
					ScrollToItemInList(scrollViewerName, e.MatchedItem);

					// Also scroll to the component in the preview list if it exists
					ScrollToMatchedItemInPreview(e.SelectedItem);
				}, Avalonia.Threading.DispatcherPriority.Loaded);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error syncing selection");
			}
		}

		private void ScrollToMatchedItemInPreview(ComponentConflictItem selectedItem)
		{
			try
			{
				if ( selectedItem == null || ViewModel == null ) return;

				// Find the preview item that corresponds to this component
				PreviewItem previewItem = ViewModel.PreviewComponents.FirstOrDefault(p =>
					p.ModComponent == selectedItem.ModComponent ||
					p.Name == selectedItem.Name);

				if ( previewItem == null ) return;

				// Find the preview scroll viewer
				ScrollViewer previewScrollViewer = this.FindControl<ScrollViewer>("PreviewScrollViewer");
				if ( previewScrollViewer == null ) return;

				// Find the index of the preview item
				int index = ViewModel.PreviewComponents.IndexOf(previewItem);
				if ( index < 0 ) return;

				Avalonia.Threading.Dispatcher.UIThread.Post(() =>
				{
					try
					{
						// Find the ItemsControl in the preview area
						ItemsControl itemsControl = FindDescendant<ItemsControl>(previewScrollViewer);
						if ( itemsControl == null || itemsControl.ItemCount == 0 ) return;

						// Measure actual item height from first rendered item
						if ( itemsControl.ContainerFromIndex(0) is Control firstItem && firstItem.Bounds.Height > 0 )
						{
							double itemHeight = firstItem.Bounds.Height;
							double targetOffset = Math.Max(0, (index - 1) * itemHeight); // Subtract 1 for some context
							previewScrollViewer.Offset = new Vector(0, targetOffset);
						}
						else if ( itemsControl.ContainerFromIndex(index) is Control targetItem )
						{
							// Fallback: use BringIntoView
							targetItem.BringIntoView();
						}
					}
					catch ( Exception innerEx )
					{
						Logger.LogException(innerEx, "Error in preview scroll action");
					}
				}, Avalonia.Threading.DispatcherPriority.Background);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error scrolling preview");
			}
		}

		private void ScrollToItemInList(string scrollViewerName, ComponentConflictItem item)
		{
			try
			{
				ScrollViewer scrollViewer = this.FindControl<ScrollViewer>(scrollViewerName);
				if ( scrollViewer == null ) return;

				// Find the ItemsControl that contains our items
				ItemsControl itemsControl = FindDescendant<ItemsControl>(scrollViewer);

				// Get the visual collection
				if ( !(itemsControl?.Items is System.Collections.IEnumerable source) )
					return;

				// Find the index of the item
				int index = 0;
				foreach ( object listItem in source )
				{
					if ( listItem == item )
					{
						Avalonia.Threading.Dispatcher.UIThread.Post(() =>
						{
							try
							{
								// Measure actual item height from first rendered item
								if ( itemsControl.ContainerFromIndex(0) is Control firstItem && firstItem.Bounds.Height > 0 )
								{
									double itemHeight = firstItem.Bounds.Height;

									// Account for margins, padding, borders by measuring actual total height
									double targetOffset = Math.Max(0, index * itemHeight);
									scrollViewer.Offset = new Vector(0, targetOffset);
								}
								else if ( itemsControl.ContainerFromIndex(index) is Control targetItem )
								{
									// Fallback: use BringIntoView
									targetItem.BringIntoView();
								}
							}
							catch ( Exception innerEx )
							{
								Logger.LogException(innerEx, $"Error in scroll action for {scrollViewerName}");
							}
						}, Avalonia.Threading.DispatcherPriority.Background);
						break;
					}
					index++;
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error scrolling to item in {scrollViewerName}");
			}
		}

		private void InputElement_OnPointerMoved(object sender, PointerEventArgs e)
		{
			if ( !_mouseDownForWindowMoving )
				return;

			PointerPoint currentPoint = e.GetCurrentPoint(this);
			Position = new PixelPoint(
				Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
				Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
			);
		}

		private void InputElement_OnPointerPressed(object sender, PointerPressedEventArgs e)
		{
			if ( WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen )
				return;

			// Don't start window drag if clicking on interactive controls
			if ( ShouldIgnorePointerForWindowDrag(e) )
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint(this);
		}

		private void InputElement_OnPointerReleased(object sender, PointerEventArgs e) =>
			_mouseDownForWindowMoving = false;

		private bool ShouldIgnorePointerForWindowDrag(PointerEventArgs e)
		{
			// Get the element under the pointer
			if ( !(e.Source is Visual source) )
				return false;

			// Walk up the visual tree to check if we're clicking on an interactive element
			Visual current = source;
			while ( current != null && current != this )
			{
				// Check if we're clicking on any interactive control
				if ( current is Button ||
					 current is TextBox ||
					 current is ComboBox ||
					 current is ListBox ||
					 current is MenuItem ||
					 current is Menu ||
					 current is Expander ||
					 current is Slider ||
					 current is TabControl ||
					 current is TabItem ||
					 current is ProgressBar ||
					 current is ScrollViewer )
				{
					return true;
				}

				// Check if the element has context menu or flyout open
				if ( current is Control control )
				{
					if ( control.ContextMenu?.IsOpen == true )
						return true;
					if ( control.ContextFlyout?.IsOpen == true )
						return true;
				}

				current = current.GetVisualParent();
			}

			return false;
		}
	}
}

