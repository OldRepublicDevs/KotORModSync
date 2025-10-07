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
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs
{
	public partial class DependencyUnlinkDialog : Window
	{
		public DependencyUnlinkViewModel ViewModel { get; }
		public bool UserConfirmed { get; private set; }
		public List<Component> ComponentsToUnlink => ViewModel?.DependentComponents
			.Where(c => c.IsSelected)
			.Select(c => c.Component)
			.ToList();
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		public DependencyUnlinkDialog()
		{
			InitializeComponent();
			// Attach window move event handlers
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		public DependencyUnlinkDialog(Component componentToDelete, List<Component> dependentComponents)
		{
			InitializeComponent();
			ViewModel = new DependencyUnlinkViewModel(componentToDelete, dependentComponents);
			DataContext = ViewModel;
			// Attach window move event handlers
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void UnlinkAndDelete_Click(object sender, RoutedEventArgs e)
		{
			UserConfirmed = true;
			Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			UserConfirmed = false;
			Close();
		}

		public static async System.Threading.Tasks.Task<(bool confirmed, List<Component> componentsToUnlink)> ShowUnlinkDialog(
			Window owner,
			Component componentToDelete,
			List<Component> dependentComponents)
		{
			// Don't show dialog if there are no dependent components
			if ( dependentComponents == null || !dependentComponents.Any() )
				return (true, new List<Component>());

			var dialog = new DependencyUnlinkDialog(componentToDelete, dependentComponents);
			await dialog.ShowDialog(owner);
			return (dialog.UserConfirmed, dialog.ComponentsToUnlink);
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

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
				switch (current)
				{
					// Check if we're clicking on any interactive control
					case Button _:
					case TextBox _:
					case ComboBox _:
					case ListBox _:
					case MenuItem _:
					case Menu _:
					case Expander _:
					case Slider _:
					case TabControl _:
					case TabItem _:
					case ProgressBar _:
					case ScrollViewer _:
					// Check if the element has context menu or flyout open
					case Control control when control.ContextMenu?.IsOpen == true:
						return true;
					case Control control when control.ContextFlyout?.IsOpen == true:
						return true;
					default:
						current = current.GetVisualParent();
						break;
				}
			}

			return false;
		}
	}
}
