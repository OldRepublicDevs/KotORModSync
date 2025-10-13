



using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using JetBrains.Annotations;

namespace KOTORModSync.Dialogs
{
	public partial class InformationDialog : Window
	{
		public static readonly AvaloniaProperty InfoTextProperty =
			AvaloniaProperty.Register<InformationDialog, string>("InfoText");
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		public InformationDialog()
		{
			InitializeComponent();
			
			ThemeManager.ApplyCurrentToWindow(this);

			
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		[CanBeNull]
		public string InfoText
		{
			get => GetValue(InfoTextProperty) as string;
			set => SetValue(InfoTextProperty, value);
		}

		public static async Task ShowInformationDialog(
			[NotNull] Window parentWindow,
			[CanBeNull] string message,
			[CanBeNull] string title = "Information"
		)
		{
			var dialog = new InformationDialog
			{
				Title = title,
				InfoText = message,
				Topmost = true,
			};
			_ = await dialog.ShowDialog<bool?>(parentWindow);
		}

		protected override void OnOpened([NotNull] EventArgs e)
		{
			base.OnOpened(e);
			UpdateInfoText();
		}       
		private void OKButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => Close();
		private void UpdateInfoText() => Dispatcher.UIThread.InvokeAsync(() => InfoTextBlock.Text = InfoText);

		private void InputElement_OnPointerMoved([NotNull] object sender, [NotNull] PointerEventArgs e)
		{
			if ( !_mouseDownForWindowMoving )
				return;

			PointerPoint currentPoint = e.GetCurrentPoint(this);
			Position = new PixelPoint(
				Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
				Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
			);
		}

		private void InputElement_OnPointerPressed([NotNull] object sender, [NotNull] PointerEventArgs e)
		{
			if ( WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen )
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint(this);
		}

		private void InputElement_OnPointerReleased([NotNull] object sender, [NotNull] PointerEventArgs e) =>
			_mouseDownForWindowMoving = false;

		private void CloseButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e) =>
			Close();
	}
}
