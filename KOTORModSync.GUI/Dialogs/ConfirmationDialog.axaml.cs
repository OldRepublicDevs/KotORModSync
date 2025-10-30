// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Services;

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

		private static readonly AvaloniaProperty s_yesButtonTooltipProperty =
			AvaloniaProperty.Register<ConfirmationDialog, string>(nameof(YesButtonTooltip));

		private static readonly AvaloniaProperty s_noButtonTooltipProperty =
			AvaloniaProperty.Register<ConfirmationDialog, string>(nameof(NoButtonTooltip));

		private static readonly AvaloniaProperty s_closeButtonTooltipProperty =
			AvaloniaProperty.Register<ConfirmationDialog, string>(nameof(CloseButtonTooltip));

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
		private readonly MarkdownRenderingService _markdownService;

		public ConfirmationDialog()
		{
			InitializeComponent();
			DataContext = this;

			ThemeManager.ApplyCurrentToWindow(this);
			_markdownService = new MarkdownRenderingService();

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

		[CanBeNull]
		public string YesButtonTooltip
		{
			get => GetValue(s_yesButtonTooltipProperty) as string;
			set => SetValue(s_yesButtonTooltipProperty, value);
		}

		[CanBeNull]
		public string NoButtonTooltip
		{
			get => GetValue(s_noButtonTooltipProperty) as string;
			set => SetValue(s_noButtonTooltipProperty, value);
		}

		[CanBeNull]
		public string CloseButtonTooltip
		{
			get => GetValue(s_closeButtonTooltipProperty) as string;
			set => SetValue(s_closeButtonTooltipProperty, value);
		}

		public enum ConfirmationResult
		{
			Save,
			Discard,
			Cancel,
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		public static async Task<bool?> ShowConfirmationDialogAsync(
			[CanBeNull] Window parentWindow,
			[CanBeNull] string confirmText,
			[CanBeNull] string yesButtonText = null,
			[CanBeNull] string noButtonText = null,
			[CanBeNull] string yesButtonTooltip = null,
			[CanBeNull] string noButtonTooltip = null,
			[CanBeNull] string closeButtonTooltip = null
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
							YesButtonTooltip = yesButtonTooltip,
							NoButtonTooltip = noButtonTooltip,
							CloseButtonTooltip = closeButtonTooltip,
							Topmost = true,
						};

						confirmationDialog.YesButtonClicked += YesClickedHandler;
						confirmationDialog.NoButtonClicked += NoClickedHandler;
						confirmationDialog.Closed += ClosedHandler;
						confirmationDialog.Opened += confirmationDialog.OnOpened;

						if (parentWindow != null)
						{
							_ = confirmationDialog.ShowDialog(parentWindow);
						}
						else
						{
							confirmationDialog.Show();
						}

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
					catch (Exception e)
					{
						Logger.LogException(e);
					}
				}
			);

			return await tcs.Task.ConfigureAwait(true);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		public static async Task<ConfirmationResult> ShowConfirmationDialogWithDiscard(
			[CanBeNull] Window parentWindow,
			[CanBeNull] string confirmText,
			[CanBeNull] string yesButtonText = null,
			[CanBeNull] string noButtonText = null,
			[CanBeNull] string yesButtonTooltip = null,
			[CanBeNull] string noButtonTooltip = null,
			[CanBeNull] string closeButtonTooltip = null
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
							YesButtonTooltip = yesButtonTooltip,
							NoButtonTooltip = noButtonTooltip,
							CloseButtonTooltip = closeButtonTooltip,
							Topmost = true,
						};

						confirmationDialog.YesButtonClicked += YesClickedHandler;
						confirmationDialog.NoButtonClicked += NoClickedHandler;
						confirmationDialog.Closed += ClosedHandler;
						confirmationDialog.Opened += confirmationDialog.OnOpened;

						if (parentWindow != null)
						{
							_ = confirmationDialog.ShowDialog(parentWindow);
						}
						else
						{
							confirmationDialog.Show();
						}

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
					catch (Exception e)
					{
						Logger.LogException(e);
					}
				}
			);

			return await tcs.Task.ConfigureAwait(true);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		public static async Task<bool?> ShowTwoOptionDialogAsync(
			[CanBeNull] Window parentWindow,
			[CanBeNull] string confirmText,
			[CanBeNull] string firstButtonText,
			[CanBeNull] string secondButtonText,
			[CanBeNull] string firstButtonTooltip = null,
			[CanBeNull] string secondButtonTooltip = null,
			[CanBeNull] string closeButtonTooltip = null
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
							YesButtonText = firstButtonText ?? "First Option",
							NoButtonText = secondButtonText ?? "Second Option",
							YesButtonTooltip = firstButtonTooltip,
							NoButtonTooltip = secondButtonTooltip,
							CloseButtonTooltip = closeButtonTooltip,
							Topmost = true,
						};

						confirmationDialog.YesButtonClicked += YesClickedHandler;
						confirmationDialog.NoButtonClicked += NoClickedHandler;
						confirmationDialog.Closed += ClosedHandler;
						confirmationDialog.Opened += confirmationDialog.OnOpened;

						if (parentWindow != null)
						{
							_ = confirmationDialog.ShowDialog(parentWindow);
						}
						else
						{
							confirmationDialog.Show();
						}

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
					catch (Exception e)
					{
						Logger.LogException(e);
					}
				}
			);

			return await tcs.Task.ConfigureAwait(true);
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

		private void OnOpened([CanBeNull] object sender, [CanBeNull] EventArgs e)
		{
			// Use a small delay to ensure XAML is fully loaded
			Dispatcher.UIThread.Post(() => UpdateConfirmationText(), DispatcherPriority.Loaded);
		}

		private void UpdateConfirmationText()
		{
			try
			{
				// Debug logging
				Logger.LogInfo($"ConfirmationDialog UpdateConfirmationText - ConfirmTextBlock is null: {ConfirmTextBlock == null}");
				Logger.LogInfo($"ConfirmationDialog UpdateConfirmationText - ConfirmText: '{ConfirmText}'");

				if (ConfirmTextBlock == null)
				{
					Logger.LogError("ConfirmTextBlock is null in UpdateConfirmationText method");
					return;
				}

				if (string.IsNullOrWhiteSpace(ConfirmText))
				{
					Logger.LogWarning("ConfirmText is null or empty in UpdateConfirmationText method");
					return;
				}

				// Try markdown rendering first
				try
				{
					_markdownService.RenderMarkdownToTextBlock(ConfirmTextBlock, ConfirmText);
					Logger.LogInfo("Markdown rendering completed successfully");
				}
				catch (Exception markdownEx)
				{
					Logger.LogException(markdownEx, "Markdown rendering failed, falling back to plain text");
					// Fallback to plain text if markdown rendering fails
					ConfirmTextBlock.Text = ConfirmText;
				}

				// Ensure the ConfirmTextBlock has the proper foreground color for the current theme
				string currentTheme = ThemeManager.GetCurrentStylePath();
				Logger.LogInfo($"Current theme: {currentTheme}");

				if (currentTheme.Contains("FluentLightStyle"))
					ConfirmTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)); // #212121
				else if (currentTheme.Contains("Kotor2Style"))
					ConfirmTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x18, 0xae, 0x88)); // #18ae88
				else if (currentTheme.Contains("KotorStyle"))
					ConfirmTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0xAA, 0xFF)); // #3AAAFF

				Logger.LogInfo($"Set foreground color for ConfirmTextBlock");
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Failed to update confirmation text");
			}
		}

		private void YesButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e) =>
			RaiseEvent(new RoutedEventArgs(s_yesButtonClickedEvent));

		private void NoButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e) =>
			RaiseEvent(new RoutedEventArgs(s_noButtonClickedEvent));

		private void CloseButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e) =>
			Close();

		private void InputElement_OnPointerMoved([NotNull] object sender, [NotNull] PointerEventArgs e)
		{
			if (!_mouseDownForWindowMoving)
				return;

			PointerPoint currentPoint = e.GetCurrentPoint(this);
			Position = new PixelPoint(
				Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
				Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
			);
		}

		private void InputElement_OnPointerPressed([NotNull] object sender, [NotNull] PointerEventArgs e)
		{
			if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen)
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint(this);
		}

		private void InputElement_OnPointerReleased([NotNull] object sender, [NotNull] PointerEventArgs e) =>
			_mouseDownForWindowMoving = false;
	}
}