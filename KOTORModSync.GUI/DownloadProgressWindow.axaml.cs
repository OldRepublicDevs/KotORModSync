// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
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
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Utility;
using KOTORModSync.Dialogs;

namespace KOTORModSync
{
	public partial class DownloadProgressWindow : Window
	{
		private readonly List<DownloadProgress> _allDownloadItems = new List<DownloadProgress>();
		private readonly ObservableCollection<DownloadProgress> _activeDownloads = new ObservableCollection<DownloadProgress>();
		private readonly ObservableCollection<DownloadProgress> _pendingDownloads = new ObservableCollection<DownloadProgress>();
		private readonly ObservableCollection<DownloadProgress> _completedDownloads = new ObservableCollection<DownloadProgress>();

		private CancellationTokenSource _cancellationTokenSource;
		private bool _isCompleted;
		private bool _mouseDownForWindowMoving;
		private PointerPoint _originalPoint;

		// Events for download control
		public event EventHandler<DownloadControlEventArgs> DownloadControlRequested;

		/// <summary>
		/// Creates a new cancellation token source (used for retries after cancellation)
		/// </summary>
		public void ResetCancellationToken()
		{
			_cancellationTokenSource?.Dispose();
			_cancellationTokenSource = new CancellationTokenSource();
		}

		/// <summary>
		/// Gets the download timeout in minutes from the UI control
		/// </summary>
		public int DownloadTimeoutMinutes
		{
			get
			{
				NumericUpDown timeoutControl = this.FindControl<NumericUpDown>("TimeoutNumericUpDown");
				if ( timeoutControl != null && timeoutControl.Value.HasValue )
				{
					return (int)timeoutControl.Value.Value;
				}
				return 10; // Default to 10 minutes
			}
		}

		public DownloadProgressWindow()
		{
			InitializeComponent();
			_cancellationTokenSource = new CancellationTokenSource();

			// Set up the three items controls
			ItemsControl activeControl = this.FindControl<ItemsControl>("ActiveDownloadsControl");
			if ( activeControl != null )
				activeControl.ItemsSource = _activeDownloads;

			ItemsControl pendingControl = this.FindControl<ItemsControl>("PendingDownloadsControl");
			if ( pendingControl != null )
				pendingControl.ItemsSource = _pendingDownloads;

			ItemsControl completedControl = this.FindControl<ItemsControl>("CompletedDownloadsControl");
			if ( completedControl != null )
				completedControl.ItemsSource = _completedDownloads;

			// Wire up button events
			Button closeButton = this.FindControl<Button>("CloseButton");
			if ( closeButton != null )
				closeButton.Click += CloseButton_Click;

			Button cancelButton = this.FindControl<Button>("CancelButton");
			if ( cancelButton != null )
				cancelButton.Click += CancelButton_Click;

			// Attach window move event handlers
			PointerPressed += InputElement_OnPointerPressed;
			PointerMoved += InputElement_OnPointerMoved;
			PointerReleased += InputElement_OnPointerReleased;
			PointerExited += InputElement_OnPointerReleased;
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
				var detailsDialog = new ModDownloadDetailsDialog(progress, RetryDownload);
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
				var detailsDialog = new ModDownloadDetailsDialog(progress, RetryDownload);
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
				_allDownloadItems.Add(progress);

				// Add to appropriate category (this will call UpdateSummary internally)
				CategorizeDownload(progress);

				// Subscribe to property changes to update clickable links and reorganize
				progress.PropertyChanged += DownloadProgress_PropertyChanged;

				// For grouped downloads, also subscribe to child changes to update summary and recategorize parent
				if ( progress.IsGrouped )
				{
					foreach ( DownloadProgress child in progress.ChildDownloads )
					{
						child.PropertyChanged += (sender, e) =>
						{
							// When a child's status changes, the parent's status will also change
							// which will trigger recategorization via the parent's PropertyChanged handler
							// But we still need to update the summary
							Dispatcher.UIThread.Post(UpdateSummary);
						};
					}
				}
			});
		}

		private void CategorizeDownload(DownloadProgress progress)
		{
			// Remove from all categories first
			_activeDownloads.Remove(progress);
			_pendingDownloads.Remove(progress);
			_completedDownloads.Remove(progress);

			// Add to appropriate category based on status, sorted by timestamp (newest first)
			switch ( progress.Status )
			{
				case DownloadStatus.InProgress:
					InsertSorted(_activeDownloads, progress, p => p.StartTime);
					break;
				case DownloadStatus.Pending:
					InsertSorted(_pendingDownloads, progress, p => p.StartTime);
					break;
				case DownloadStatus.Completed:
				case DownloadStatus.Failed:
				case DownloadStatus.Skipped:
					InsertSorted(_completedDownloads, progress, p => p.EndTime ?? p.StartTime);
					break;
			}

			// Update section header counts
			UpdateSummary();
		}

		/// <summary>
		/// Inserts an item into an ObservableCollection in sorted order (newest first)
		/// </summary>
		private void InsertSorted(ObservableCollection<DownloadProgress> collection, DownloadProgress item, Func<DownloadProgress, DateTime> timestampSelector)
		{
			DateTime itemTimestamp = timestampSelector(item);

			// Find the correct position to insert (newest first = descending order)
			int insertIndex = 0;
			for ( int i = 0; i < collection.Count; i++ )
			{
				DateTime existingTimestamp = timestampSelector(collection[i]);
				if ( itemTimestamp > existingTimestamp )
				{
					// Item is newer than this one, insert here
					insertIndex = i;
					break;
				}
				insertIndex = i + 1;
			}

			collection.Insert(insertIndex, item);
		}

		public void UpdateDownloadProgress(DownloadProgress progress)
		{
			Dispatcher.UIThread.Post(() =>
			{
				var existing = _allDownloadItems.FirstOrDefault(p => p.Url == progress.Url);
				if ( existing != null )
				{
					// Temporarily unsubscribe from property changes to avoid cascading updates
					existing.PropertyChanged -= DownloadProgress_PropertyChanged;

					// Update all properties in one batch
					existing.Status = progress.Status;
					existing.StatusMessage = progress.StatusMessage;
					existing.ProgressPercentage = progress.ProgressPercentage;
					existing.BytesDownloaded = progress.BytesDownloaded;
					existing.TotalBytes = progress.TotalBytes;
					existing.FilePath = progress.FilePath;
					existing.StartTime = progress.StartTime;
					existing.EndTime = progress.EndTime;
					existing.ErrorMessage = progress.ErrorMessage;
					existing.Exception = progress.Exception;

					// Re-subscribe to property changes
					existing.PropertyChanged += DownloadProgress_PropertyChanged;

					// Manually trigger categorization and summary update once
					CategorizeDownload(existing);
				}
				else
				{
					AddDownload(progress);
				}
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
			TextBlock overallProgressText = this.FindControl<TextBlock>("OverallProgressText");
			ProgressBar overallProgressBar = this.FindControl<ProgressBar>("OverallProgressBar");

			// Update section headers
			TextBlock activeHeader = this.FindControl<TextBlock>("ActiveSectionHeader");
			TextBlock pendingHeader = this.FindControl<TextBlock>("PendingSectionHeader");
			TextBlock completedHeader = this.FindControl<TextBlock>("CompletedSectionHeader");

			if ( activeHeader != null )
				activeHeader.Text = $"🔄 Active Downloads ({_activeDownloads.Count})";
			if ( pendingHeader != null )
				pendingHeader.Text = $"⏳ Pending Downloads ({_pendingDownloads.Count})";
			if ( completedHeader != null )
				completedHeader.Text = $"✅ Completed Downloads ({_completedDownloads.Count})";

			if ( summaryText == null )
				return;

			// Use collection counts instead of LINQ queries for better performance
			int inProgress = _activeDownloads.Count;
			int pending = _pendingDownloads.Count;

			// Count completed/failed/skipped from the completed collection
			int completedCount = _completedDownloads.Count(x => x.Status == DownloadStatus.Completed);
			int skippedCount = _completedDownloads.Count(x => x.Status == DownloadStatus.Skipped);
			int failedCount = _completedDownloads.Count(x => x.Status == DownloadStatus.Failed);
			int totalFinished = completedCount + skippedCount + failedCount;

			// Calculate overall progress
			double overallProgress = _allDownloadItems.Count > 0 ? (double)totalFinished / _allDownloadItems.Count * 100 : 0;

			// Update overall progress display
			if ( overallProgressText != null )
			{
				string progressText = $"Overall Progress: {totalFinished} / {_allDownloadItems.Count} URLs";
				if ( completedCount > 0 || skippedCount > 0 || failedCount > 0 )
				{
					var parts = new System.Collections.Generic.List<string>();
					if ( completedCount > 0 ) parts.Add($"{completedCount} downloaded");
					if ( skippedCount > 0 ) parts.Add($"{skippedCount} skipped");
					if ( failedCount > 0 ) parts.Add($"{failedCount} failed");
					progressText += $" ({string.Join(", ", parts)})";
				}
				overallProgressText.Text = progressText;
			}

			if ( overallProgressBar != null )
				overallProgressBar.Value = overallProgress;

			if ( _isCompleted )
			{
				var messageParts = new System.Collections.Generic.List<string>();
				if ( completedCount > 0 ) messageParts.Add($"{completedCount} downloaded");
				if ( skippedCount > 0 ) messageParts.Add($"{skippedCount} skipped");
				if ( failedCount > 0 ) messageParts.Add($"{failedCount} failed");

				string message = messageParts.Count > 0
					? $"Download complete! {string.Join(", ", messageParts)}"
					: "Download complete!";
				summaryText.Text = message;
			}
			else
			{
				if ( inProgress > 0 )
					summaryText.Text = $"Downloading {inProgress} URL(s)... {completedCount + skippedCount}/{_allDownloadItems.Count} complete";
				else if ( pending > 0 )
					summaryText.Text = $"Preparing downloads... {pending} URL(s) pending";
				else
					summaryText.Text = "Initializing downloads...";
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

		[UsedImplicitly]
		private void MinimizeButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e) => WindowState = WindowState.Minimized;

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

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			CancelDownloads();
		}

		/// <summary>
		/// Cancels all ongoing downloads
		/// </summary>
		public void CancelDownloads()
		{
			try
			{
				Logger.LogVerbose("[DownloadProgressWindow] CancelDownloads() called");

				_cancellationTokenSource?.Cancel();

				// Update UI safely using normal priority (not Send which can cause crashes)
				Dispatcher.UIThread.Post(() =>
				{
					Button cancelButton = this.FindControl<Button>("CancelButton");
					if ( cancelButton != null )
					{
						cancelButton.IsEnabled = false;
						cancelButton.Content = "Cancelling...";
					}

					// Mark all in-progress downloads as cancelled
					foreach ( var download in _allDownloadItems.Where(d => d.Status == DownloadStatus.InProgress) )
					{
						download.Status = DownloadStatus.Failed;
						download.StatusMessage = "Download cancelled by user";
						download.ErrorMessage = "Download was cancelled by user";
					}

					UpdateSummary();
				}); // Use default priority - safe and won't cause crashes

				Logger.LogVerbose("[DownloadProgressWindow] Cooperative cancellation initiated - downloads will stop gracefully");
			}
			catch ( Exception ex )
			{
				Logger.LogError($"[DownloadProgressWindow] Failed to cancel downloads: {ex.Message}");
			}
		}

		public CancellationToken CancellationToken => _cancellationTokenSource.Token;

		protected override void OnClosing(WindowClosingEventArgs e)
		{
			// If downloads are still in progress, ask for confirmation
			if ( !_isCompleted && _allDownloadItems.Any(x => x.Status == DownloadStatus.InProgress || x.Status == DownloadStatus.Pending) )
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

			// Only reorganize when status changes (sorting is handled by InsertSorted in CategorizeDownload)
			if ( e.PropertyName == nameof(DownloadProgress.Status) && sender is DownloadProgress progressItem )
				Dispatcher.UIThread.Post(() => CategorizeDownload(progressItem));
		}

		private void UpdateErrorMessageWithLinks(DownloadProgress progress)
		{
			if ( string.IsNullOrEmpty(progress.ErrorMessage) )
				return;

			// Find the appropriate ItemsControl based on download status
			ItemsControl itemsControl = null;
			if ( progress.Status == DownloadStatus.InProgress )
				itemsControl = this.FindControl<ItemsControl>("ActiveDownloadsControl");
			else if ( progress.Status == DownloadStatus.Pending )
				itemsControl = this.FindControl<ItemsControl>("PendingDownloadsControl");
			else
				itemsControl = this.FindControl<ItemsControl>("CompletedDownloadsControl");

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
			textBlock.Inlines = ParseTextWithUrls(progress.ErrorMessage);
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

		private void ControlButton_Click(object sender, RoutedEventArgs e)
		{
			if ( !(sender is Button button) || !(button.DataContext is DownloadProgress progress) )
				return;

			try
			{
				DownloadControlAction action;
				switch ( progress.Status )
				{
					case DownloadStatus.InProgress:
						action = DownloadControlAction.Stop;
						break;
					case DownloadStatus.Completed:
					case DownloadStatus.Skipped:
					case DownloadStatus.Failed:
						action = DownloadControlAction.Retry;
						break;
					case DownloadStatus.Pending:
					default:
						action = DownloadControlAction.Start;
						break;
				}

				Logger.LogVerbose($"Download control requested: {action} for {progress.ModName}");
				DownloadControlRequested?.Invoke(this, new DownloadControlEventArgs(progress, action));
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Failed to handle control button click: {ex.Message}");
			}
		}

		private void RetryDownload(DownloadProgress progress)
		{
			try
			{
				Logger.LogVerbose($"Retry requested for: {progress.ModName} ({progress.Url})");
				DownloadControlRequested?.Invoke(this, new DownloadControlEventArgs(progress, DownloadControlAction.Retry));
			}
			catch ( Exception ex )
			{
				Logger.LogError($"Failed to trigger retry: {ex.Message}");
			}
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

	public enum DownloadControlAction
	{
		Start,
		Stop,
		Resume,
		Retry
	}

	public class DownloadControlEventArgs : EventArgs
	{
		public DownloadProgress Progress { get; }
		public DownloadControlAction Action { get; }

		public DownloadControlEventArgs(DownloadProgress progress, DownloadControlAction action)
		{
			Progress = progress;
			Action = action;
		}
	}
}

