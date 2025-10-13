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
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs
{
	public partial class ConfirmationDialog : Window
	{
		private static readonly AvaloniaProperty s_confirmTextProperty =
			AvaloniaProperty.Register<ConfirmationDialog, string>(nameof(ConfirmText));

		private static readonly AvaloniaProperty s_yesButtonTextProperty =
			AvaloniaProperty.Register<ConfirmationDialog, string>(nameof(YesButtonText), "Yes");

		private static readonly AvaloniaProperty s_noButtonTextProperty =
			AvaloniaProperty.Register<ConfirmationDialog, string>(nameof(NoButtonText), "No");

		private static readonly RoutedEvent<RoutedEventArgs> s_yesButtonClickedEvent =
			RoutedEvent.Register<ConfirmationDialog, RoutedEventArgs>(
				nameof(YesButtonClicked),
				RoutingStrategies.Bubble
			);

		private static readonly RoutedEvent<RoutedEventArgs> s_noButtonClickedEvent =
			RoutedEvent.Register<ConfirmationDialog, RoutedEventArgs>(
				nameof(NoButtonClicked),
				RoutingStrategies.Bubble
			);
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		public ConfirmationDialog()
		{
			InitializeComponent();

			ThemeManager.ApplyCurrentToWindow(this);

			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		[CanBeNull]
		public string ConfirmText
		{
			get => GetValue(s_confirmTextProperty) as string;
			set => SetValue(s_confirmTextProperty, value);
		}

		[CanBeNull]
		public string YesButtonText
		{
			get => GetValue(s_yesButtonTextProperty) as string;
			set => SetValue(s_yesButtonTextProperty, value);
		}

		[CanBeNull]
		public string NoButtonText
		{
			get => GetValue(s_noButtonTextProperty) as string;
			set => SetValue(s_noButtonTextProperty, value);
		}

		public enum ConfirmationResult
		{
			Save,
			Discard,
			Cancel
		}

		public static async Task<bool?> ShowConfirmationDialogAsync(
			[CanBeNull] Window parentWindow,
			[CanBeNull] string confirmText,
			[CanBeNull] string yesButtonText = null,
			[CanBeNull] string noButtonText = null
		)
		{
			var tcs = new TaskCompletionSource<bool?>();

			await Dispatcher.UIThread.InvokeAsync(
				() =>
				{
					try
					{
						var confirmationDialog = new ConfirmationDialog
						{
							ConfirmText = confirmText,
							YesButtonText = yesButtonText ?? "Yes",
							NoButtonText = noButtonText ?? "No",
							Topmost = true,
						};

						confirmationDialog.YesButtonClicked += YesClickedHandler;
						confirmationDialog.NoButtonClicked += NoClickedHandler;
						confirmationDialog.Closed += ClosedHandler;
						confirmationDialog.Opened += confirmationDialog.OnOpened;

						_ = confirmationDialog.ShowDialog(parentWindow);
						return;

						void CleanupHandlers()
						{
							confirmationDialog.YesButtonClicked -= YesClickedHandler;
							confirmationDialog.NoButtonClicked -= NoClickedHandler;
							confirmationDialog.Closed -= ClosedHandler;
						}

						void YesClickedHandler(object sender, RoutedEventArgs e)
						{
							CleanupHandlers();
							confirmationDialog.Close();
							tcs.SetResult(true);
						}

						void NoClickedHandler(object sender, RoutedEventArgs e)
						{
							CleanupHandlers();
							confirmationDialog.Close();
							tcs.SetResult(false);
						}

						void ClosedHandler(object sender, EventArgs e)
						{
							CleanupHandlers();
							tcs.SetResult(null);
						}
					}
					catch ( Exception e )
					{
						Logger.LogException(e);
					}
				}
			);

			return await tcs.Task;
		}

		public static async Task<ConfirmationResult> ShowConfirmationDialogWithDiscard(
			[CanBeNull] Window parentWindow,
			[CanBeNull] string confirmText,
			[CanBeNull] string yesButtonText = null,
			[CanBeNull] string noButtonText = null
		)
		{
			var tcs = new TaskCompletionSource<ConfirmationResult>();

			await Dispatcher.UIThread.InvokeAsync(
				() =>
				{
					try
					{
						var confirmationDialog = new ConfirmationDialog
						{
							ConfirmText = confirmText,
							YesButtonText = yesButtonText ?? "Yes",
							NoButtonText = noButtonText ?? "No",
							Topmost = true,
						};

						confirmationDialog.YesButtonClicked += YesClickedHandler;
						confirmationDialog.NoButtonClicked += NoClickedHandler;
						confirmationDialog.Closed += ClosedHandler;
						confirmationDialog.Opened += confirmationDialog.OnOpened;

						_ = confirmationDialog.ShowDialog(parentWindow);
						return;

						void CleanupHandlers()
						{
							confirmationDialog.YesButtonClicked -= YesClickedHandler;
							confirmationDialog.NoButtonClicked -= NoClickedHandler;
							confirmationDialog.Closed -= ClosedHandler;
						}

						void YesClickedHandler(object sender, RoutedEventArgs e)
						{
							CleanupHandlers();
							confirmationDialog.Close();
							tcs.SetResult(ConfirmationResult.Save);
						}

						void NoClickedHandler(object sender, RoutedEventArgs e)
						{
							CleanupHandlers();
							confirmationDialog.Close();
							tcs.SetResult(ConfirmationResult.Discard);
						}

						void ClosedHandler(object sender, EventArgs e)
						{
							CleanupHandlers();
							tcs.SetResult(ConfirmationResult.Cancel);
						}
					}
					catch ( Exception e )
					{
						Logger.LogException(e);
					}
				}
			);

			return await tcs.Task;
		}

		public event EventHandler<RoutedEventArgs> YesButtonClicked
		{
			add => AddHandler(s_yesButtonClickedEvent, value);
			remove => RemoveHandler(s_yesButtonClickedEvent, value);
		}

		public event EventHandler<RoutedEventArgs> NoButtonClicked
		{
			add => AddHandler(s_noButtonClickedEvent, value);
			remove => RemoveHandler(s_noButtonClickedEvent, value);
		}

		private void OnOpened([CanBeNull] object sender, [CanBeNull] EventArgs e) =>
			ConfirmTextBlock.Text = ConfirmText;

		private void YesButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e) =>
			RaiseEvent(new RoutedEventArgs(s_yesButtonClickedEvent));

		private void NoButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e) =>
			RaiseEvent(new RoutedEventArgs(s_noButtonClickedEvent));

		private void CloseButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e) =>
			Close();

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
	}
}
