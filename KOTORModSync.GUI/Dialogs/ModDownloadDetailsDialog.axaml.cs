// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Dialogs
{
	public partial class ModDownloadDetailsDialog : Window
	{
		private DownloadProgress _downloadProgress;
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;
		private Action<DownloadProgress> _retryAction;

		public ModDownloadDetailsDialog() => InitializeComponent();

		public ModDownloadDetailsDialog(DownloadProgress progress, Action<DownloadProgress> retryAction = null) : this()
		{
			_downloadProgress = progress;
			_retryAction = retryAction;
			LoadDetails();
			WireUpEvents();
			// Attach window move event handlers
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

		private void LoadDetails()
		{
			if ( _downloadProgress == null )
				return;

			// Header
			TextBlock statusIconText = this.FindControl<TextBlock>("StatusIconText");
			if ( statusIconText != null )
			{
				statusIconText.Text = _downloadProgress.StatusIcon;
			}

			TextBlock modNameText = this.FindControl<TextBlock>("ModNameText");
			if ( modNameText != null )
			{
				modNameText.Text = _downloadProgress.ModName;
			}

			TextBlock statusText = this.FindControl<TextBlock>("StatusText");
			if ( statusText != null )
			{
				statusText.Text = $"Status: {_downloadProgress.Status}";
			}

			// Download Information
			TextBlock urlText = this.FindControl<TextBlock>("UrlText");
			if ( urlText != null )
			{
				urlText.Text = _downloadProgress.Url;
			}

			TextBlock filePathText = this.FindControl<TextBlock>("FilePathText");
			if ( filePathText != null )
			{
				filePathText.Text = string.IsNullOrEmpty(_downloadProgress.FilePath) ? "N/A" : _downloadProgress.FilePath;
			}

			TextBlock fileSizeText = this.FindControl<TextBlock>("FileSizeText");
			if ( fileSizeText != null )
			{
				fileSizeText.Text = _downloadProgress.TotalBytes > 0
					? $"{_downloadProgress.DownloadedSize} / {_downloadProgress.TotalSize} ({_downloadProgress.ProgressPercentage:F1}%)"
					: "Unknown";
			}

			TextBlock downloadSpeedText = this.FindControl<TextBlock>("DownloadSpeedText");
			if ( downloadSpeedText != null )
			{
				downloadSpeedText.Text = _downloadProgress.DownloadSpeed;
			}

			TextBlock durationText = this.FindControl<TextBlock>("DurationText");
			if ( durationText != null )
			{
				durationText.Text = FormatDuration(_downloadProgress.Duration);
			}

			// Status Message
			Border statusMessageSection = this.FindControl<Border>("StatusMessageSection");
			TextBlock statusMessageText = this.FindControl<TextBlock>("StatusMessageText");
			if ( statusMessageSection != null && statusMessageText != null )
			{
				if ( !string.IsNullOrWhiteSpace(_downloadProgress.StatusMessage) )
				{
					statusMessageText.Text = _downloadProgress.StatusMessage;
					statusMessageSection.IsVisible = true;
				}
				else
				{
					statusMessageSection.IsVisible = false;
				}
			}

			// Error Details
			Border errorSection = this.FindControl<Border>("ErrorSection");
			TextBlock errorMessageText = this.FindControl<TextBlock>("ErrorMessageText");
			TextBlock exceptionHeader = this.FindControl<TextBlock>("ExceptionHeader");
			TextBox exceptionDetailsText = this.FindControl<TextBox>("ExceptionDetailsText");
			Button copyErrorButton = this.FindControl<Button>("CopyErrorButton");

			if ( errorSection != null && errorMessageText != null )
			{
				if ( _downloadProgress.IsFailed && !string.IsNullOrWhiteSpace(_downloadProgress.ErrorMessage) )
				{
					errorMessageText.Text = _downloadProgress.ErrorMessage;
					errorSection.IsVisible = true;

					if ( _downloadProgress.Exception != null )
					{
						if ( exceptionHeader != null )
							exceptionHeader.IsVisible = true;

						if ( exceptionDetailsText != null )
						{
							exceptionDetailsText.Text = _downloadProgress.Exception.ToString();
							exceptionDetailsText.IsVisible = true;
						}
					}

					if ( copyErrorButton != null )
						copyErrorButton.IsVisible = true;
				}
				else
				{
					errorSection.IsVisible = false;
				}
			}

			// Timing Information
			TextBlock startTimeText = this.FindControl<TextBlock>("StartTimeText");
			if ( startTimeText != null )
			{
				startTimeText.Text = _downloadProgress.StartTime != default
					? _downloadProgress.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
					: "N/A";
			}

			TextBlock endTimeText = this.FindControl<TextBlock>("EndTimeText");
			if ( endTimeText != null )
			{
				endTimeText.Text = _downloadProgress.EndTime.HasValue
					? _downloadProgress.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss")
					: "N/A";
			}

			// Logs
			TextBox logsTextBox = this.FindControl<TextBox>("LogsTextBox");
			if ( logsTextBox != null )
			{
				logsTextBox.Text = string.Join(Environment.NewLine, _downloadProgress.GetLogs());
			}

			// Footer Buttons
			Button openFolderButton = this.FindControl<Button>("OpenFolderButton");
			if ( openFolderButton != null )
				openFolderButton.IsEnabled = !string.IsNullOrEmpty(_downloadProgress.FilePath) && File.Exists(_downloadProgress.FilePath);

			Button retryButton = this.FindControl<Button>("RetryButton");
			if ( retryButton != null )
				retryButton.IsVisible = _downloadProgress.IsFailed;
		}

		private void WireUpEvents()
		{
			Button openFolderButton = this.FindControl<Button>("OpenFolderButton");
			if ( openFolderButton != null )
			{
				openFolderButton.Click += OpenFolderButton_Click;
			}

			Button copyErrorButton = this.FindControl<Button>("CopyErrorButton");
			if ( copyErrorButton != null )
				copyErrorButton.Click += CopyErrorButton_Click;

			Button retryButton = this.FindControl<Button>("RetryButton");
			if ( retryButton != null )
				retryButton.Click += RetryButton_Click;

			Button closeButton = this.FindControl<Button>("CloseButton");
			if ( closeButton != null )
			{
				closeButton.Click += CloseButton_Click;
			}
		}

		private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
		{
			if ( string.IsNullOrEmpty(_downloadProgress?.FilePath) || !File.Exists(_downloadProgress.FilePath) )
				return;

			try
			{
				string directory = Path.GetDirectoryName(_downloadProgress.FilePath);
				if ( string.IsNullOrEmpty(directory) || !Directory.Exists(directory) )
					return;
				// Use the same approach as MainWindow for opening folders
				if ( Utility.GetOperatingSystem() == OSPlatform.Windows )
					_ = System.Diagnostics.Process.Start("explorer.exe", directory);
				else if ( Utility.GetOperatingSystem() == OSPlatform.OSX )
					_ = System.Diagnostics.Process.Start("open", directory);
				else // Linux
					_ = System.Diagnostics.Process.Start("xdg-open", directory);
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Failed to open download folder: {ex.Message}");
			}
		}

		private async void CopyErrorButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string errorDetails = $"Mod: {_downloadProgress.ModName}\n";
				errorDetails += $"URL: {_downloadProgress.Url}\n";
				errorDetails += $"Error: {_downloadProgress.ErrorMessage}\n";

				if ( _downloadProgress.Exception != null )
					errorDetails += $"\nException:\n{_downloadProgress.Exception}";

				// Add download logs
				errorDetails += "\n\nDownload Logs:\n";
				errorDetails += string.Join("\n", _downloadProgress.GetLogs());

				if ( Clipboard is null )
				{
					await Logger.LogErrorAsync("Clipboard is null");
					return;
				}

				await Clipboard.SetTextAsync(errorDetails);

				if ( !(sender is Button button) )
					return;
				button.Content = "Copied!";
				await System.Threading.Tasks.Task.Delay(2000);
				button.Content = "Copy Error Details";
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"Failed to copy error details: {ex.Message}");
			}
		}

		private async void RetryButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( _retryAction == null )
				{
					await Logger.LogErrorAsync("Retry action not provided to dialog");
					await InformationDialog.ShowInformationDialog(this, "Retry functionality is not available.");
					return;
				}

				if ( _downloadProgress == null )
				{
					await Logger.LogErrorAsync("Download progress is null");
					return;
				}

				await Logger.LogAsync($"Retrying download for: {_downloadProgress.ModName} ({_downloadProgress.Url})");

				// Reset the download progress state
				_downloadProgress.Status = DownloadStatus.Pending;
				_downloadProgress.StatusMessage = "Retrying download...";
				_downloadProgress.ErrorMessage = string.Empty;
				_downloadProgress.Exception = null;
				_downloadProgress.ProgressPercentage = 0;
				_downloadProgress.BytesDownloaded = 0;
				_downloadProgress.AddLog("Retry requested by user");

				// Invoke the retry action
				_retryAction(_downloadProgress);

				// Close the dialog
				Close();
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"Failed to retry download: {ex.Message}");
				await InformationDialog.ShowInformationDialog(this, $"Failed to retry download: {ex.Message}");
			}
		}

		[UsedImplicitly]
		private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

		[UsedImplicitly]
		private void ToggleMaximizeButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
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

		[UsedImplicitly]
		private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

		private static string FormatDuration(TimeSpan duration)
		{
			if ( duration.TotalSeconds < 1 )
				return "< 1 second";
			if ( duration.TotalMinutes < 1 )
				return $"{(int)duration.TotalSeconds} seconds";
			if ( duration.TotalHours < 1 )
				return $"{(int)duration.TotalMinutes} minutes, {duration.Seconds} seconds";
			return $"{(int)duration.TotalHours} hours, {duration.Minutes} minutes";
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
				switch ( current )
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

