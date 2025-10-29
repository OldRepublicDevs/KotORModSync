// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

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
using KOTORModSync.Core.Services;

namespace KOTORModSync.Dialogs
{
	public partial class CircularDependencyResolutionDialog : Window
	{
		public CircularDependencyResolutionViewModel ViewModel { get; }
		public bool UserRetried { get; private set; }
		public List<ModComponent> ResolvedComponents => ViewModel?.Components
			.Where( c => c.IsSelected )
			.Select( c => c.ModComponent )
			.ToList();
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		public CircularDependencyResolutionDialog()
		{
			InitializeComponent();

			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		public CircularDependencyResolutionDialog( List<ModComponent> components, CircularDependencyDetector.CircularDependencyResult cycleInfo )
		{
			InitializeComponent();
			ViewModel = new CircularDependencyResolutionViewModel( components, cycleInfo );
			DataContext = ViewModel;

			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load( this );
		}

		private void Retry_Click( object sender, RoutedEventArgs e )
		{
			UserRetried = true;
			Close();
		}

		private void Cancel_Click( object sender, RoutedEventArgs e )
		{
			UserRetried = false;
			Close();
		}

		public static async System.Threading.Tasks.Task<(bool retry, List<ModComponent> components)> ShowResolutionDialog(
			Window owner,
			List<ModComponent> components,
			CircularDependencyDetector.CircularDependencyResult cycleInfo )
		{

			if (!cycleInfo.HasCircularDependencies || cycleInfo.Cycles.Count == 0)
				return (false, components);

			var dialog = new CircularDependencyResolutionDialog( components, cycleInfo );


			await dialog.ShowDialog( owner ).ConfigureAwait( false );
			return (dialog.UserRetried, dialog.ResolvedComponents);
		}

		[UsedImplicitly]
		private void MinimizeButton_Click( object sender, RoutedEventArgs e ) => WindowState = WindowState.Minimized;

		[UsedImplicitly]
		private void ToggleMaximizeButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e )
		{
			if (!(sender is Button maximizeButton))
				return;
			if (WindowState == WindowState.Maximized)
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

		[UsedImplicitly]
		private void CloseButton_Click( object sender, RoutedEventArgs e ) => Close();

		private void InputElement_OnPointerMoved( object sender, PointerEventArgs e )
		{
			if (!_mouseDownForWindowMoving)
				return;

			PointerPoint currentPoint = e.GetCurrentPoint( this );
			Position = new PixelPoint(
				Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
				Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
			);
		}

		private void InputElement_OnPointerPressed( object sender, PointerPressedEventArgs e )
		{
			if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen)
				return;

			if (ShouldIgnorePointerForWindowDrag( e ))
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint( this );
		}

		private void InputElement_OnPointerReleased( object sender, PointerEventArgs e ) =>
			_mouseDownForWindowMoving = false;

		private bool ShouldIgnorePointerForWindowDrag( PointerEventArgs e )
		{

			if (!(e.Source is Visual source))
				return false;

			Visual current = source;
			while (current != null && current != this)
			{
				switch (current)
				{

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