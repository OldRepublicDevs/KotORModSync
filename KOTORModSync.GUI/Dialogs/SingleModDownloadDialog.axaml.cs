// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Services.Download;

namespace KOTORModSync.Dialogs
{
	public partial class SingleModDownloadDialog : Window
	{
		private readonly ObservableCollection<DownloadProgress> _fileDownloads = new ObservableCollection<DownloadProgress>();
		private readonly CancellationTokenSource _cancellationTokenSource;
		private bool _isCompleted;
		private readonly ModComponent _component;
		private readonly DownloadCacheService _downloadCacheService;

		public bool WasSuccessful { get; private set; }
		public List<string> DownloadedFiles { get; private set; } = new List<string>();

		public SingleModDownloadDialog()
		{
			InitializeComponent();
		}

		public SingleModDownloadDialog(ModComponent component, DownloadCacheService downloadCacheService)
		{
			_component = component ?? throw new ArgumentNullException(nameof(component));
			_downloadCacheService = downloadCacheService ?? throw new ArgumentNullException(nameof(downloadCacheService));
			_cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromHours(24 * 7));

			InitializeComponent();

			var modNameText = this.FindControl<TextBlock>("ModNameText");
			if (modNameText != null)
				modNameText.Text = $"Downloading: {component.Name}";

			var filesListControl = this.FindControl<ItemsControl>("FilesListControl");
			if (filesListControl != null)
				filesListControl.ItemsSource = _fileDownloads;
		}

		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		public async Task StartDownloadAsync()
		{
			try
			{
				await Logger.LogVerboseAsync($"[SingleModDownloadDialog] Starting download for component: {_component.Name}").ConfigureAwait(false);

				var statusText = this.FindControl<TextBlock>("StatusText");
				if (statusText != null)
					statusText.Text = "Resolving download URLs...";

				// Setup progress reporting
				var progressReporter = new Progress<DownloadProgress>(progress =>
				{
					Dispatcher.UIThread.Post(() =>
					{
						UpdateFileProgress(progress);
					});
				});

				// Start download
				IReadOnlyList<DownloadCacheService.DownloadCacheEntry> results = await _downloadCacheService.ResolveOrDownloadAsync(
					_component,
					MainConfig.SourcePath.FullName,
					progressReporter,
					sequential: false,
					_cancellationTokenSource.Token
				).ConfigureAwait(false);

				await Logger.LogVerboseAsync($"[SingleModDownloadDialog] Download completed, {results.Count} entries returned").ConfigureAwait(false);

				// Collect downloaded files
				int successCount = 0;
				int failedCount = 0;
				var downloadedPaths = results
					.Select(entry => entry.FileName)
					.Where(fileName => !string.IsNullOrEmpty(fileName))
					.Select(fileName => System.IO.Path.Combine(MainConfig.SourcePath.FullName, fileName))
					.ToList();

				successCount = downloadedPaths.Count(path => System.IO.File.Exists(path));
				failedCount = downloadedPaths.Count - successCount;

				WasSuccessful = failedCount == 0 && successCount > 0;

				// Update UI-bound collection on UI thread
				await Dispatcher.UIThread.InvokeAsync(() =>
				{
					DownloadedFiles.Clear();
					foreach (var filePath in downloadedPaths.Where(path => System.IO.File.Exists(path)))
					{
						DownloadedFiles.Add(filePath);
					}
					MarkCompleted(successCount, failedCount);
				});
			}
			catch (Exception ex)


			{
				await Logger.LogExceptionAsync(ex, "[SingleModDownloadDialog] Download failed").ConfigureAwait(false);

				Dispatcher.UIThread.Post(() =>
				{
					var errorBorder = this.FindControl<Border>("ErrorBorder");
					var errorMessageText = this.FindControl<TextBlock>("ErrorMessageText");

					if (errorBorder != null)
						errorBorder.IsVisible = true;

					if (errorMessageText != null)
						errorMessageText.Text = $"Download failed: {ex.Message}";

					MarkCompleted(0, 1);
				});
			}
		}

		private void UpdateFileProgress(DownloadProgress progress)
		{
			var existing = _fileDownloads.FirstOrDefault(p => string.Equals(p.Url, progress.Url, StringComparison.Ordinal));
			if (existing != null)
			{
				// Update existing entry
				existing.Status = progress.Status;
				existing.StatusMessage = progress.StatusMessage;
				existing.ProgressPercentage = progress.ProgressPercentage;
				existing.BytesDownloaded = progress.BytesDownloaded;
				existing.TotalBytes = progress.TotalBytes;
				existing.FilePath = progress.FilePath;
				existing.ErrorMessage = progress.ErrorMessage;
			}
			else
			{
				// Add new entry
				_fileDownloads.Add(progress);
			}

			UpdateOverallProgress();
		}

		private void UpdateOverallProgress()
		{
			var overallProgressBar = this.FindControl<ProgressBar>("OverallProgressBar");
			var overallProgressText = this.FindControl<TextBlock>("OverallProgressText");
			var statusText = this.FindControl<TextBlock>("StatusText");
			var footerStatusText = this.FindControl<TextBlock>("FooterStatusText");

			if (_fileDownloads.Count == 0)
				return;

			int completed = _fileDownloads.Count(f => f.Status == DownloadStatus.Completed || f.Status == DownloadStatus.Skipped);
			int failed = _fileDownloads.Count(f => f.Status == DownloadStatus.Failed);
			int inProgress = _fileDownloads.Count(f => f.Status == DownloadStatus.InProgress);
			double avgProgress = _fileDownloads.Average(f => f.ProgressPercentage);

			if (overallProgressBar != null)
				overallProgressBar.Value = avgProgress;

			if (overallProgressText != null)
				overallProgressText.Text = $"{completed + failed} / {_fileDownloads.Count} files";

			if (statusText != null)
			{
				if (inProgress > 0)
					statusText.Text = $"Downloading {inProgress} file(s)...";
				else if (completed + failed == _fileDownloads.Count)
					statusText.Text = "Download complete";
				else
					statusText.Text = "Preparing download...";
			}

			if (footerStatusText != null)
			{
				if (inProgress > 0)
					footerStatusText.Text = $"Downloading... {completed}/{_fileDownloads.Count} complete";
				else
					footerStatusText.Text = $"{completed} completed, {failed} failed";
			}
		}

		private void MarkCompleted(int successCount, int failedCount)
		{
			_isCompleted = true;

			var closeButton = this.FindControl<Button>("CloseButton");
			var cancelButton = this.FindControl<Button>("CancelButton");
			var statusText = this.FindControl<TextBlock>("StatusText");
			var footerStatusText = this.FindControl<TextBlock>("FooterStatusText");

			if (closeButton != null)
				closeButton.IsEnabled = true;

			if (cancelButton != null)
				cancelButton.IsEnabled = false;

			string statusMessage;
			if (failedCount == 0 && successCount > 0)
			{
				statusMessage = $"✓ Successfully downloaded {successCount} file(s)";
			}
			else if (failedCount > 0 && successCount > 0)
			{
				statusMessage = $"⚠ Partially complete: {successCount} succeeded, {failedCount} failed";
			}
			else if (failedCount > 0)
			{
				statusMessage = $"✗ Download failed for {failedCount} file(s)";
			}
			else
			{
				statusMessage = "No files to download";
			}

			if (statusText != null)
				statusText.Text = statusMessage;

			if (footerStatusText != null)
				footerStatusText.Text = statusMessage;
		}

		private void CancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			try
			{
				_cancellationTokenSource?.Cancel();

				var cancelButton = this.FindControl<Button>("CancelButton");
				if (cancelButton != null)
				{
					cancelButton.IsEnabled = false;
					cancelButton.Content = "Cancelling...";
				}

				foreach (var download in _fileDownloads.Where(d => d.Status == DownloadStatus.InProgress))
				{
					download.Status = DownloadStatus.Failed;
					download.StatusMessage = "Cancelled by user";
					download.ErrorMessage = "Download was cancelled";
				}

				Logger.LogVerbose("[SingleModDownloadDialog] Download cancelled by user");
			}
			catch (Exception ex)
			{
				Logger.LogError($"[SingleModDownloadDialog] Failed to cancel downloads: {ex.Message}");
			}
		}

		private void CloseButton_Click(
			object sender,
			Avalonia.Interactivity.RoutedEventArgs e)
		{
			Close();
		}

		protected override void OnClosing(WindowClosingEventArgs e)
		{
			if (!_isCompleted && _fileDownloads.Any(
				x => x.Status == DownloadStatus.InProgress || x.Status == DownloadStatus.Pending))
			{
				_cancellationTokenSource?.Cancel();
			}

			base.OnClosing(e);
		}
	}
}