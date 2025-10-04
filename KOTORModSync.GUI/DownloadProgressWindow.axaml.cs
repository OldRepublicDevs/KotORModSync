using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KOTORModSync.Core;
using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Utility;

namespace KOTORModSync
{
	public partial class DownloadProgressWindow : Window
	{
		private readonly ObservableCollection<DownloadProgress> _downloadItems;
		private readonly CancellationTokenSource _cancellationTokenSource;
		private bool _isCompleted;
		private int _totalDownloads;

		public DownloadProgressWindow()
		{
			InitializeComponent();
			_downloadItems = new ObservableCollection<DownloadProgress>();
			_cancellationTokenSource = new CancellationTokenSource();

			// Set up the items control
			ItemsControl itemsControl = this.FindControl<ItemsControl>("DownloadItemsControl");
			if ( itemsControl != null )
				itemsControl.ItemsSource = _downloadItems;

			// Wire up button events
			Button closeButton = this.FindControl<Button>("CloseButton");
			if ( closeButton != null )
				closeButton.Click += CloseButton_Click;

			Button cancelButton = this.FindControl<Button>("CancelButton");
			if ( cancelButton != null )
				cancelButton.Click += CancelButton_Click;

		}

		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

		private void DownloadItem_PointerPressed(object sender, PointerPressedEventArgs e)
		{
			// Allow clicking on download items to view details
			if ( !(sender is Border border) || !(border.DataContext is DownloadProgress progress) )
				return;
			// Double-click to view details
			if ( e.ClickCount != 2 )
				return;
			try
			{
				var detailsDialog = new ModDownloadDetailsDialog(progress);
				_ = detailsDialog.ShowDialog(this);
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Failed to show download details: {ex.Message}");
			}
		}

		private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if ( sender is MenuItem menuItem && menuItem.DataContext is DownloadProgress progress )
			{
				if ( string.IsNullOrEmpty(progress.FilePath) || !File.Exists(progress.FilePath) )
					return;

				try
				{
					string directory = Path.GetDirectoryName(progress.FilePath);
					if ( !string.IsNullOrEmpty(directory) && Directory.Exists(directory) )
					{
						if ( Utility.GetOperatingSystem() == OSPlatform.Windows )
							_ = Process.Start("explorer.exe", directory);
						else if ( Utility.GetOperatingSystem() == OSPlatform.OSX )
							_ = Process.Start("open", directory);
						else // Linux
							_ = Process.Start("xdg-open", directory);
					}
				}
				catch ( Exception ex )
				{
					Logger.LogError($"Failed to open download folder: {ex.Message}");
				}
			}
		}

		private async void CopyUrlMenuItem_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if ( sender is MenuItem menuItem && menuItem.DataContext is DownloadProgress progress )
				{
					if ( string.IsNullOrEmpty(progress.Url) )
						return;

					try
					{
						if ( Clipboard != null )
						{
							await Clipboard.SetTextAsync(progress.Url);
							await Logger.LogVerboseAsync($"Copied URL to clipboard: {progress.Url}");
						}
					}
					catch ( Exception ex )
					{
						await Logger.LogErrorAsync($"Failed to copy URL to clipboard: {ex.Message}");
					}
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"Failed to copy URL to clipboard: {ex.Message}");
			}
		}

		private void ViewDetailsMenuItem_Click(object sender, RoutedEventArgs e)
		{
			if ( !(sender is MenuItem menuItem) || !(menuItem.DataContext is DownloadProgress progress) )
				return;
			try
			{
				var detailsDialog = new ModDownloadDetailsDialog(progress);
				_ = detailsDialog.ShowDialog(this);
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Failed to show download details: {ex.Message}");
			}
		}

		public void AddDownload(DownloadProgress progress)
		{
			Dispatcher.UIThread.Post(() =>
			{
				_downloadItems.Add(progress);
				_totalDownloads++;
				UpdateSummary();

				// Subscribe to property changes to update clickable links
				progress.PropertyChanged += DownloadProgress_PropertyChanged;
			});
		}

		public void MarkCompleted()
		{
			Dispatcher.UIThread.Post(() =>
			{
				_isCompleted = true;

				Button closeButton = this.FindControl<Button>("CloseButton");
				if ( closeButton != null )
					closeButton.IsEnabled = true;

				Button cancelButton = this.FindControl<Button>("CancelButton");
				if ( cancelButton != null )
					cancelButton.IsEnabled = false;

				UpdateSummary();
			});
		}

		private void UpdateSummary()
		{
			TextBlock summaryText = this.FindControl<TextBlock>("SummaryText");
			if ( summaryText == null )
				return;

			int completedCount = _downloadItems.Count(x => x.Status == DownloadStatus.Completed);
			int skippedCount = _downloadItems.Count(x => x.Status == DownloadStatus.Skipped);
			int failedCount = _downloadItems.Count(x => x.Status == DownloadStatus.Failed);

			if ( _isCompleted )
			{
				string message = $"Download complete! {completedCount} succeeded";
				if ( skippedCount > 0 )
					message += $", {skippedCount} skipped";
				if ( failedCount > 0 )
					message += $", {failedCount} failed";
				summaryText.Text = message;
			}
			else
			{
				int inProgress = _downloadItems.Count(x => x.Status == DownloadStatus.InProgress);
				int pending = _downloadItems.Count(x => x.Status == DownloadStatus.Pending);

				if ( inProgress > 0 )
					summaryText.Text = $"Downloading {inProgress} mod(s)... {completedCount + skippedCount}/{_totalDownloads} complete";
				else if ( pending > 0 )
					summaryText.Text = $"Preparing downloads... {pending} mod(s) pending";
				else
					summaryText.Text = "Initializing downloads...";
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			_cancellationTokenSource?.Cancel();

			Button cancelButton = this.FindControl<Button>("CancelButton");
			if ( cancelButton == null )
				return;
			cancelButton.IsEnabled = false;
			cancelButton.Content = "Cancelling...";
		}

		public CancellationToken CancellationToken => _cancellationTokenSource.Token;

		protected override void OnClosing(WindowClosingEventArgs e)
		{
			// If downloads are still in progress, ask for confirmation
			if ( !_isCompleted && _downloadItems.Any(x => x.Status == DownloadStatus.InProgress || x.Status == DownloadStatus.Pending) )
			{
				// In a real implementation, you'd show a confirmation dialog here
				// For now, we'll just cancel the downloads
				_cancellationTokenSource?.Cancel();
			}

			base.OnClosing(e);
		}

		private void DownloadProgress_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if ( e.PropertyName == nameof(DownloadProgress.ErrorMessage) && sender is DownloadProgress progress )
			    Dispatcher.UIThread.Post(() => UpdateErrorMessageWithLinks(progress));
		}

		private void UpdateErrorMessageWithLinks(DownloadProgress progress)
		{
			if ( string.IsNullOrEmpty(progress.ErrorMessage) )
				return;

			// Find the ItemsControl
			ItemsControl itemsControl = this.FindControl<ItemsControl>("DownloadItemsControl");

			// Find the container for this specific progress item
			Control container = itemsControl?.ContainerFromItem(progress);
			if ( container == null )
				return;

			// Find the TextBlock named ErrorMessageBlock within this container
			System.Collections.Generic.IEnumerable<TextBlock> allTextBlocks = container.GetVisualDescendants().OfType<TextBlock>();
			TextBlock textBlock = allTextBlocks.FirstOrDefault(tb => tb.Name == "ErrorMessageBlock");

			if ( textBlock == null )
				return;

			// Parse the error message and create inlines with clickable links
			textBlock.Inlines = DownloadProgressWindow.ParseTextWithUrls(progress.ErrorMessage);
		}

		private static InlineCollection ParseTextWithUrls(string text)
		{
			var inlines = new InlineCollection();

			// Regex to match URLs
			string urlPattern = @"(https?://[^\s<>""{}|\\^`\[\]]+)";
			var regex = new Regex(urlPattern, RegexOptions.IgnoreCase);

			int lastIndex = 0;
			foreach ( Match match in regex.Matches(text) )
			{
				// Add text before the URL
				if ( match.Index > lastIndex )
				{
					string beforeText = text.Substring(lastIndex, match.Index - lastIndex);
					inlines.Add(new Run(beforeText));
				}

				// Add the URL as a clickable button using InlineUIContainer
				string url = match.Value;
				var button = new Button
				{
					Content = url,
					Cursor = new Cursor(StandardCursorType.Hand),
					Foreground = Brushes.LightBlue,
					Background = Brushes.Transparent,
					BorderThickness = new Thickness(0),
					Padding = new Thickness(0),
					Margin = new Thickness(0),
					VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
					FontSize = 13
				};

				// Add underline style
				button.Classes.Add("link-button");

				// Attach click handler
				button.Click += (sender, e) =>
				{
					try
					{
						UrlUtilities.OpenUrl(url);
						Logger.LogVerbose($"Opened URL: {url}");
					}
					catch ( Exception ex )
					{
						Logger.LogError($"Failed to open URL: {ex.Message}");
					}
				};

				// Wrap button in InlineUIContainer
				inlines.Add(new InlineUIContainer { Child = button });

				lastIndex = match.Index + match.Length;
			}

			// Add remaining text after the last URL
			if ( lastIndex < text.Length )
			{
				string afterText = text.Substring(lastIndex);
				inlines.Add(new Run(afterText));
			}

			// If no URLs were found, just add the whole text
			if ( inlines.Count == 0 )
				inlines.Add(new Run(text));

			return inlines;
		}
	}
}

