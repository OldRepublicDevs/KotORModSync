// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;
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
		private readonly DownloadProgress _downloadProgress;
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;
		private readonly Action<DownloadProgress> _retryAction;

		public ModDownloadDetailsDialog() => InitializeComponent();

		public ModDownloadDetailsDialog(DownloadProgress progress, Action<DownloadProgress> retryAction = null) : this()
		{
			_downloadProgress = progress;
			_retryAction = retryAction;
			LoadDetails();
			WireUpEvents();

			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
		}

		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private void LoadDetails()
		{
			if (_downloadProgress is null)
				return;


			TextBlock statusIconText = this.FindControl<TextBlock>("StatusIconText");
			if (statusIconText != null)
			{
				statusIconText.Text = _downloadProgress.StatusIcon;
			}

			TextBlock modNameText = this.FindControl<TextBlock>("ModNameText");
			if (modNameText != null)
			{
				modNameText.Text = _downloadProgress.ModName;
			}

			TextBlock statusText = this.FindControl<TextBlock>("StatusText");
			if (statusText != null)
			{
				statusText.Text = $"Status: {_downloadProgress.Status}";
			}


			TextBlock urlText = this.FindControl<TextBlock>("UrlText");
			if (urlText != null)
			{
				urlText.Text = _downloadProgress.Url;
			}

			TextBlock filePathText = this.FindControl<TextBlock>("FilePathText");
			if (filePathText != null)
			{
				filePathText.Text = string.IsNullOrEmpty(_downloadProgress.FilePath) ? "N/A" : _downloadProgress.FilePath;
			}

			TextBlock fileSizeText = this.FindControl<TextBlock>("FileSizeText");
			if (fileSizeText != null)
			{
				fileSizeText.Text = _downloadProgress.TotalBytes > 0
					? $"{_downloadProgress.DownloadedSize} / {_downloadProgress.TotalSize} ({_downloadProgress.ProgressPercentage:F1}%)"
					: "Unknown";
			}

			TextBlock downloadSpeedText = this.FindControl<TextBlock>("DownloadSpeedText");
			if (downloadSpeedText != null)
			{
				downloadSpeedText.Text = _downloadProgress.DownloadSpeed;
			}

			TextBlock durationText = this.FindControl<TextBlock>("DurationText");
			if (durationText != null)
			{
				durationText.Text = FormatDuration(_downloadProgress.Duration);
			}


			Border statusMessageSection = this.FindControl<Border>("StatusMessageSection");
			TextBlock statusMessageText = this.FindControl<TextBlock>("StatusMessageText");
			if (statusMessageSection != null && statusMessageText != null)
			{
				if (!string.IsNullOrWhiteSpace(_downloadProgress.StatusMessage))
				{
					statusMessageText.Text = _downloadProgress.StatusMessage;
					statusMessageSection.IsVisible = true;
				}
				else
				{
					statusMessageSection.IsVisible = false;
				}
			}


			Border errorSection = this.FindControl<Border>("ErrorSection");
			TextBlock errorMessageText = this.FindControl<TextBlock>("ErrorMessageText");
			TextBlock exceptionHeader = this.FindControl<TextBlock>("ExceptionHeader");
			TextBox exceptionDetailsText = this.FindControl<TextBox>("ExceptionDetailsText");
			Button copyErrorButton = this.FindControl<Button>("CopyErrorButton");

			if (errorSection != null && errorMessageText != null)
			{
				if (_downloadProgress.IsFailed && !string.IsNullOrWhiteSpace(_downloadProgress.ErrorMessage))
				{
					errorMessageText.Text = _downloadProgress.ErrorMessage;
					errorSection.IsVisible = true;

					if (_downloadProgress.Exception != null)
					{
						if (exceptionHeader != null)
							exceptionHeader.IsVisible = true;

						if (exceptionDetailsText != null)
						{
							exceptionDetailsText.Text = _downloadProgress.Exception.ToString();
							exceptionDetailsText.IsVisible = true;
						}
					}

					if (copyErrorButton != null)
						copyErrorButton.IsVisible = true;
				}
				else
				{
					errorSection.IsVisible = false;
				}
			}


			TextBlock startTimeText = this.FindControl<TextBlock>("StartTimeText");
			if (startTimeText != null)
			{
				startTimeText.Text = _downloadProgress.StartTime != default
					? _downloadProgress.StartTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)
					: "N/A";
			}

			TextBlock endTimeText = this.FindControl<TextBlock>("EndTimeText");
			if (endTimeText != null)
			{
				endTimeText.Text = _downloadProgress.EndTime.HasValue
					? _downloadProgress.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture)
					: "N/A";
			}


			TextBox logsTextBox = this.FindControl<TextBox>("LogsTextBox");
			if (logsTextBox != null)
			{
				logsTextBox.Text = string.Join(Environment.NewLine, _downloadProgress.GetLogs());
			}


			Button openFolderButton = this.FindControl<Button>("OpenFolderButton");
			if (openFolderButton != null)
				openFolderButton.IsEnabled = !string.IsNullOrEmpty(_downloadProgress.FilePath) && File.Exists(_downloadProgress.FilePath);

			Button retryButton = this.FindControl<Button>("RetryButton");
			if (retryButton != null)
				retryButton.IsVisible = _downloadProgress.IsFailed;
		}

		private void WireUpEvents()
		{
			Button openFolderButton = this.FindControl<Button>("OpenFolderButton");
			if (openFolderButton != null)
			{
				openFolderButton.Click += OpenFolderButton_Click;
			}

			Button copyErrorButton = this.FindControl<Button>("CopyErrorButton");
			if (copyErrorButton != null)
				copyErrorButton.Click += CopyErrorButton_Click;

			Button retryButton = this.FindControl<Button>("RetryButton");
			if (retryButton != null)
				retryButton.Click += RetryButton_Click;

			Button closeButton = this.FindControl<Button>("CloseButton");
			if (closeButton != null)
			{
				closeButton.Click += CloseButton_Click;
			}
		}

		private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(_downloadProgress?.FilePath) || !File.Exists(_downloadProgress.FilePath))
				return;

			try
			{
				string directory = Path.GetDirectoryName(_downloadProgress.FilePath);
				if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
					return;

				if (UtilityHelper.GetOperatingSystem() == OSPlatform.Windows)
					_ = System.Diagnostics.Process.Start("explorer.exe", directory);
				else if (UtilityHelper.GetOperatingSystem() == OSPlatform.OSX)
					_ = System.Diagnostics.Process.Start("open", directory);
				else
					_ = System.Diagnostics.Process.Start("xdg-open", directory);
			}
			catch (Exception ex)
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

				if (_downloadProgress.Exception != null)
					errorDetails += $"\nException:\n{_downloadProgress.Exception}";


				errorDetails += "\n\nDownload Logs:\n";
				errorDetails += string.Join("\n", _downloadProgress.GetLogs());

				if (Clipboard is null)
				{
					await Logger.LogErrorAsync("Clipboard is null").ConfigureAwait(false);
					return;
				}

				await Clipboard.SetTextAsync(errorDetails).ConfigureAwait(true);

				if (!(sender is Button button))
					return;
				button.Content = "Copied!";


				// Use ConfigureAwait(true) to ensure continuation remains on UI thread
				await System.Threading.Tasks.Task.Delay(2000).ConfigureAwait(true);
				button.Content = "Copy Error Details";
			}
			catch (Exception ex)


			{
				await Logger.LogErrorAsync($"Failed to copy error details: {ex.Message}").ConfigureAwait(false);
			}
		}

		private async void RetryButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (_retryAction is null)


				{
					await Logger.LogErrorAsync("Retry action not provided to dialog").ConfigureAwait(false);
					await InformationDialog.ShowInformationDialogAsync(
						this,
						message: "Retry action not provided to dialog"
					).ConfigureAwait(true);
					return;
				}

				if (_downloadProgress is null)


				{
					await Logger.LogErrorAsync("Download progress is null").ConfigureAwait(false);
					return;


				}

				await Logger.LogAsync($"Retrying download for: {_downloadProgress.ModName} ({_downloadProgress.Url})").ConfigureAwait(false);


				_downloadProgress.Status = DownloadStatus.Pending;
				_downloadProgress.StatusMessage = "Retrying download...";
				_downloadProgress.ErrorMessage = string.Empty;
				_downloadProgress.Exception = null;
				_downloadProgress.ProgressPercentage = 0;
				_downloadProgress.BytesDownloaded = 0;
				_downloadProgress.AddLog("Retry requested by user");


				_retryAction(_downloadProgress);


				Close();
			}
			catch (Exception ex)


			{
				await Logger.LogErrorAsync($"Failed to retry download: {ex.Message}").ConfigureAwait(false);
				await InformationDialog.ShowInformationDialogAsync(this, $"Failed to retry download: {ex.Message}").ConfigureAwait(true);
			}
		}

		[UsedImplicitly]
		private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

		[UsedImplicitly]
		private void ToggleMaximizeButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
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
		private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

		private static string FormatDuration(TimeSpan duration)
		{
			if (duration.TotalSeconds < 1)
				return "< 1 second";
			if (duration.TotalMinutes < 1)
				return $"{(int)duration.TotalSeconds} seconds";
			if (duration.TotalHours < 1)
				return $"{(int)duration.TotalMinutes} minutes, {duration.Seconds} seconds";
			return $"{(int)duration.TotalHours} hours, {duration.Minutes} minutes";
		}

		private void InputElement_OnPointerMoved(object sender, PointerEventArgs e)
		{
			if (!_mouseDownForWindowMoving)
				return;

			PointerPoint currentPoint = e.GetCurrentPoint(this);
			Position = new PixelPoint(
				Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
				Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
			);
		}

		private void InputElement_OnPointerPressed(object sender, PointerPressedEventArgs e)
		{
			if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen)
				return;


			if (ShouldIgnorePointerForWindowDrag(e))
				return;

			_mouseDownForWindowMoving = true;
			_originalPoint = e.GetCurrentPoint(this);
		}

		private void InputElement_OnPointerReleased(object sender, PointerEventArgs e) =>
			_mouseDownForWindowMoving = false;

		private bool ShouldIgnorePointerForWindowDrag(PointerEventArgs e)
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