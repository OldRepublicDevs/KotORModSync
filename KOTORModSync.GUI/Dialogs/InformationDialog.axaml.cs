// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using JetBrains.Annotations;

using KOTORModSync.Services;

namespace KOTORModSync.Dialogs
{
	public partial class InformationDialog : Window
	{
		public static readonly AvaloniaProperty InfoTextProperty =
			AvaloniaProperty.Register<InformationDialog, string>( "InfoText" );

		public static readonly AvaloniaProperty OKButtonTooltipProperty =
			AvaloniaProperty.Register<InformationDialog, string>( nameof( OKButtonTooltip ) );

		public static readonly AvaloniaProperty CloseButtonTooltipProperty =
			AvaloniaProperty.Register<InformationDialog, string>( nameof( CloseButtonTooltip ) );

		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;
		private readonly MarkdownRenderingService _markdownService;

		public InformationDialog()
		{
			InitializeComponent();

			ThemeManager.ApplyCurrentToWindow( this );
			_markdownService = new MarkdownRenderingService();

			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		[CanBeNull]
		public string InfoText
		{
			get => GetValue( InfoTextProperty ) as string;
			set => SetValue( InfoTextProperty, value );
		}

		[CanBeNull]
		public string OKButtonTooltip
		{
			get => GetValue( OKButtonTooltipProperty ) as string;
			set => SetValue( OKButtonTooltipProperty, value );
		}

		[CanBeNull]
		public string CloseButtonTooltip
		{
			get => GetValue( CloseButtonTooltipProperty ) as string;
			set => SetValue( CloseButtonTooltipProperty, value );
		}

		public static async Task ShowInformationDialogAsync(
			[NotNull] Window parentWindow,
			[CanBeNull] string message,
			[CanBeNull] string title = "Information",
			[CanBeNull] string okButtonTooltip = null,
			[CanBeNull] string closeButtonTooltip = null
		)
		{
			await Dispatcher.UIThread.InvokeAsync( async () =>
			{
				var dialog = new InformationDialog
				{
					Title = title,
					InfoText = message,
					OKButtonTooltip = okButtonTooltip,
					CloseButtonTooltip = closeButtonTooltip,
					Topmost = true,
				};


				_ = await dialog.ShowDialog<bool?>( parentWindow ).ConfigureAwait( false );
			} ).ConfigureAwait( false );
		}

		protected override void OnOpened( [NotNull] EventArgs e )
		{
			base.OnOpened( e );
			UpdateInfoText();
		}
		private void OKButton_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => Close();
		private void UpdateInfoText() => Dispatcher.UIThread.InvokeAsync( () => _markdownService.RenderMarkdownToTextBlock( InfoTextBlock, InfoText ) );

		private void InputElement_OnPointerMoved( [NotNull] object sender, [NotNull] PointerEventArgs e )
		{
			if (!_mouseDownForWindowMoving)
				return;

			PointerPoint currentPoint = e.GetCurrentPoint( this );
			Position = new PixelPoint(
				Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
				Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
			);
		}

		private void InputElement_OnPointerPressed( [NotNull] object sender, [NotNull] PointerEventArgs e )
		{
			if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen)
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint( this );
		}

		private void InputElement_OnPointerReleased( [NotNull] object sender, [NotNull] PointerEventArgs e ) =>
			_mouseDownForWindowMoving = false;

		private void CloseButton_Click( [CanBeNull] object sender, [CanBeNull] RoutedEventArgs e ) =>
			Close();
	}
}