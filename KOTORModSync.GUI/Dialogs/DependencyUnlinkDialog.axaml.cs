



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
		public List<ModComponent> ComponentsToUnlink => ViewModel?.DependentComponents
			.Where(c => c.IsSelected)
			.Select(c => c.ModComponent)
			.ToList();
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		public DependencyUnlinkDialog()
		{
			InitializeComponent();
			
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		public DependencyUnlinkDialog(ModComponent componentToDelete, List<ModComponent> dependentComponents)
		{
			InitializeComponent();
			ViewModel = new DependencyUnlinkViewModel(componentToDelete, dependentComponents);
			DataContext = ViewModel;
			
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

		public static async System.Threading.Tasks.Task<(bool confirmed, List<ModComponent> componentsToUnlink)> ShowUnlinkDialog(
			Window owner,
			ModComponent componentToDelete,
			List<ModComponent> dependentComponents)
		{
			
			if ( dependentComponents == null || !dependentComponents.Any() )
				return (true, new List<ModComponent>());

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

			
			if ( ShouldIgnorePointerForWindowDrag(e) )
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint(this);
		}

		private void InputElement_OnPointerReleased(object sender, PointerEventArgs e) =>
			_mouseDownForWindowMoving = false;

		private bool ShouldIgnorePointerForWindowDrag(PointerEventArgs e)
		{
			
			if ( !(e.Source is Visual source) )
				return false;

			
			Visual current = source;
			while ( current != null && current != this )
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
