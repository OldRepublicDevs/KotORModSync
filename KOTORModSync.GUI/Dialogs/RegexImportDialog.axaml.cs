// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;

namespace KOTORModSync.Dialogs
{
	public partial class RegexImportDialog : Window
	{
		public RegexImportDialogViewModel ViewModel { get; private set; }
		public bool LoadSuccessful { get; private set; }
		private TextBlock _previewTextBlock;
		private Func<Task<bool>> _confirmationCallback;
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		public RegexImportDialog([NotNull] string markdown, [CanBeNull] MarkdownImportProfile initialProfile = null, [CanBeNull] Func<Task<bool>> confirmationCallback = null)
		{
			InitializeComponent();
			ViewModel = new RegexImportDialogViewModel(markdown, initialProfile ?? MarkdownImportProfile.CreateDefault());
			DataContext = ViewModel;
			LoadSuccessful = false;
			_confirmationCallback = confirmationCallback;

			_previewTextBlock = this.FindControl<TextBlock>("PreviewTextBlock");
			if ( _previewTextBlock != null )
			{
				ViewModel.PropertyChanged += (_, e) =>
				{
					if ( e.PropertyName == nameof(RegexImportDialogViewModel.HighlightedPreview) )
					{
						UpdatePreviewInlines();
					}
				};

				UpdatePreviewInlines();
			}

			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		public RegexImportDialog()
		{
			InitializeComponent();
			ViewModel = null;
			LoadSuccessful = false;

			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

		private void OnResetDefaults(object sender, RoutedEventArgs e) => ViewModel?.ResetDefaults();

		private void OnCancel(object sender, RoutedEventArgs e) => Close();

		private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

		private void ToggleMaximizeButton_Click(object sender, RoutedEventArgs e) =>
			WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

		private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

		private async void OnLoad(object sender, RoutedEventArgs e)
		{

			if ( _confirmationCallback != null )
			{
				bool confirmed = await _confirmationCallback();
				if ( !confirmed )
					return;
			}

			LoadSuccessful = true;
			Close();
		}

		private void UpdatePreviewInlines()
		{
			if ( _previewTextBlock == null )
			{
				Logger.LogVerbose("UpdatePreviewInlines: _previewTextBlock is null");
				return;
			}
			ObservableCollection<Inline> inlines = ViewModel?.HighlightedPreview;
			Logger.LogVerbose($"UpdatePreviewInlines: Updating with {inlines?.Count ?? 0} inlines");
			Dispatcher.UIThread.Post(() =>
			{
				_previewTextBlock.Inlines?.Clear();
				if ( inlines != null )
				{
					foreach ( Inline inline in inlines )
					{
						_previewTextBlock.Inlines?.Add(inline);
					}
				}
			});
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
