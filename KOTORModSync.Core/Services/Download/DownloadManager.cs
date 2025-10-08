using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class DownloadManager
	{
		private readonly List<IDownloadHandler> _handlers;
		private const double Tolerance = 0.01;
		private static readonly SemaphoreSlim _deadlyStreamConcurrencyLimiter = new SemaphoreSlim(5, 5); // Max 5 concurrent downloads

		// Per-download log throttling: track last log time and last status for each URL
		private readonly Dictionary<string, DateTime> _lastProgressLogTime = new Dictionary<string, DateTime>();
		private readonly Dictionary<string, DownloadStatus> _lastLoggedStatus = new Dictionary<string, DownloadStatus>();
		private readonly object _logThrottleLock = new object();
		private const int LogThrottleSeconds = 30;

		public DownloadManager(IEnumerable<IDownloadHandler> handlers)
		{
			_handlers = new List<IDownloadHandler>(handlers);
			Logger.LogVerbose($"[DownloadManager] Initialized with {_handlers.Count} download handlers");
			for ( int i = 0; i < _handlers.Count; i++ )
			{
				Logger.LogVerbose($"[DownloadManager] Handler {i + 1}: {_handlers[i].GetType().Name}");
			}
		}

		public async Task<List<DownloadResult>> DownloadAllWithProgressAsync(
				Dictionary<string, DownloadProgress> urlToProgressMap,
				string destinationDirectory,
				IProgress<DownloadProgress> progressReporter = null,
				CancellationToken cancellationToken = default)
		{
			var urlList = urlToProgressMap.Keys.ToList();
			await Logger.LogVerboseAsync($"[DownloadManager] Starting concurrent batch download with progress reporting for {urlList.Count} URLs");
			await Logger.LogVerboseAsync($"[DownloadManager] Destination directory: {destinationDirectory}");
			await Logger.LogVerboseAsync($"[DownloadManager] Concurrency limit: 5 concurrent DeadlyStream downloads (other handlers unlimited)");

			// Log every single URL being processed
			await Logger.LogVerboseAsync($"[DownloadManager] URLs to download:");
			foreach (var kvp in urlToProgressMap)
			{
				await Logger.LogVerboseAsync($"[DownloadManager]   URL: {kvp.Key}");
				await Logger.LogVerboseAsync($"[DownloadManager]     Mod: {kvp.Value.ModName}");
				await Logger.LogVerboseAsync($"[DownloadManager]     Status: {kvp.Value.Status}");
				await Logger.LogVerboseAsync($"[DownloadManager]     Progress: {kvp.Value.ProgressPercentage}%");
			}

			var results = new List<DownloadResult>();
			var tasks = new List<Task<DownloadResult>>();

			// Create tasks for all downloads with concurrency limiting
			foreach ( string url in urlList )
			{
				DownloadProgress progressItem = urlToProgressMap[url];
				await Logger.LogVerboseAsync($"[DownloadManager] Creating download task for URL: {url}, Mod: {progressItem.ModName}");
				tasks.Add(DownloadSingleWithConcurrencyLimit(url, progressItem, destinationDirectory, progressReporter, cancellationToken));
			}

			// Wait for all downloads to complete
			DownloadResult[] downloadResults = await Task.WhenAll(tasks).ConfigureAwait(false);
			results.AddRange(downloadResults);

			int successCount = results.Count(r => r.Success);
			int failCount = results.Count(r => !r.Success);

			await Logger.LogVerboseAsync($"[DownloadManager] Concurrent batch download completed. Success: {successCount}, Failed: {failCount}");
			return results;
		}

		private async Task<DownloadResult> DownloadSingleWithConcurrencyLimit(
			string url,
			DownloadProgress progressItem,
			string destinationDirectory,
			IProgress<DownloadProgress> progressReporter,
			CancellationToken cancellationToken)
		{
			// Check for cancellation
			if ( cancellationToken.IsCancellationRequested )
			{
				await Logger.LogVerboseAsync("[DownloadManager] Download cancelled by user");
				progressItem.Status = DownloadStatus.Failed;
				progressItem.ErrorMessage = "Download cancelled by user";
				return DownloadResult.Failed("Download cancelled by user");
			}

			IDownloadHandler handler = _handlers.FirstOrDefault(h => h.CanHandle(url));
			if ( handler == null )
			{
				await Logger.LogErrorAsync($"[DownloadManager] No handler configured for URL: {url}");
				progressItem.Status = DownloadStatus.Failed;
				progressItem.ErrorMessage = "No handler configured for this URL";
				return DownloadResult.Failed("No handler configured for URL: " + url);
			}

			await Logger.LogVerboseAsync($"[DownloadManager] Using handler: {handler.GetType().Name} for URL: {url}");
			progressItem.AddLog($"Using handler: {handler.GetType().Name}");

			// Check if this is a DeadlyStream download that needs concurrency limiting
			bool isDeadlyStream = handler.GetType().Name.Contains("DeadlyStream");
			bool concurrencyAcquired = false;

			if ( isDeadlyStream )
			{
				// Acquire concurrency limiter for DeadlyStream downloads only (max 5 concurrent)
				await _deadlyStreamConcurrencyLimiter.WaitAsync(cancellationToken);
				concurrencyAcquired = true;
				await Logger.LogVerboseAsync($"[DownloadManager] DeadlyStream download - Concurrency: {5 - _deadlyStreamConcurrencyLimiter.CurrentCount}/{5} slots in use");
			}
			else
			{
				await Logger.LogVerboseAsync($"[DownloadManager] Non-DeadlyStream download - no concurrency limit applied");
			}

			try
			{
				await Logger.LogVerboseAsync($"[DownloadManager] Starting download: {url}");

				// Update status to indicate download is starting
				progressItem.Status = DownloadStatus.InProgress;
				progressItem.StatusMessage = "Starting download...";
				progressItem.StartTime = DateTime.Now;

				// Create a progress reporter that updates the DownloadProgress object and logs changes
				var internalProgressReporter = new Progress<DownloadProgress>(update =>
				{
					// Determine if we should log this progress update based on throttling rules
					bool shouldLog = false;
					lock ( _logThrottleLock )
					{
						DateTime now = DateTime.Now;
						bool isFirstLog = !_lastProgressLogTime.ContainsKey(url);
						bool statusChanged = !_lastLoggedStatus.ContainsKey(url) || _lastLoggedStatus[url] != update.Status;
						bool isTerminalStatus = update.Status == DownloadStatus.Completed ||
						                        update.Status == DownloadStatus.Failed ||
						                        update.Status == DownloadStatus.Skipped;
						bool hasError = !string.IsNullOrEmpty(update.ErrorMessage);
						bool throttleExpired = !isFirstLog &&
						                       (now - _lastProgressLogTime[url]).TotalSeconds >= LogThrottleSeconds;

						// Log if: first update, status changed, terminal status, has error, or throttle expired
						shouldLog = isFirstLog || statusChanged || isTerminalStatus || hasError || throttleExpired;

						if ( shouldLog )
						{
							_lastProgressLogTime[url] = now;
							_lastLoggedStatus[url] = update.Status;
						}
					}

					// Only log progress updates when throttle allows it
					if ( shouldLog )
					{
						Logger.LogVerbose($"[DownloadManager] Progress update for URL: {url}");
						Logger.LogVerbose($"[DownloadManager]   Status: {update.Status}");
						Logger.LogVerbose($"[DownloadManager]   Progress: {update.ProgressPercentage:F1}%");
						Logger.LogVerbose($"[DownloadManager]   Bytes: {update.BytesDownloaded}/{update.TotalBytes}");
						Logger.LogVerbose($"[DownloadManager]   StatusMessage: {update.StatusMessage}");
						if (!string.IsNullOrEmpty(update.ErrorMessage))
							Logger.LogVerbose($"[DownloadManager]   Error: {update.ErrorMessage}");
						if (!string.IsNullOrEmpty(update.FilePath))
							Logger.LogVerbose($"[DownloadManager]   FilePath: {update.FilePath}");
					}

					// Log status changes
					if ( update.Status != DownloadStatus.Pending && !string.IsNullOrEmpty(update.StatusMessage) )
						progressItem.AddLog($"[{update.Status}] {update.StatusMessage}");

					// Log errors
					if ( !string.IsNullOrEmpty(update.ErrorMessage) )
					{
						progressItem.AddLog($"[ERROR] {update.ErrorMessage}");
						if ( update.Exception != null )
						{
							progressItem.AddLog($"[EXCEPTION] {update.Exception.GetType().Name}: {update.Exception.Message}");
							if ( !string.IsNullOrEmpty(update.Exception.StackTrace) )
								progressItem.AddLog($"[STACK TRACE] {update.Exception.StackTrace}");
						}
					}

					// Log progress milestones
					if ( update.ProgressPercentage > 0 && update.ProgressPercentage % 25 == 0 &&
						 Math.Abs(update.ProgressPercentage - progressItem.ProgressPercentage) > Tolerance )
					{
						progressItem.AddLog(update.TotalBytes > 0
							? $"Progress: {update.ProgressPercentage:F1}% ({update.BytesDownloaded}/{update.TotalBytes} bytes)"
							: $"Progress: {update.ProgressPercentage:F1}%");
					}

					// Update the progress item
					progressItem.Status = update.Status;
					progressItem.ProgressPercentage = update.ProgressPercentage;
					progressItem.BytesDownloaded = update.BytesDownloaded;
					progressItem.TotalBytes = update.TotalBytes;
					progressItem.StatusMessage = update.StatusMessage;
					progressItem.ErrorMessage = update.ErrorMessage;
					progressItem.Exception = update.Exception;
					progressItem.FilePath = update.FilePath;

					if ( update.StartTime != default )
						progressItem.StartTime = update.StartTime;
					if ( update.EndTime != null )
						progressItem.EndTime = update.EndTime;

					// Forward the update to the external progress reporter
					progressReporter?.Report(progressItem);
				});

				DownloadResult result;
				try
				{
					result = await handler.DownloadAsync(url, destinationDirectory, internalProgressReporter).ConfigureAwait(false);
				}
				catch ( Exception ex )
				{
					// Catch any unexpected exceptions from the download handler
					await Logger.LogErrorAsync($"[DownloadManager] Unexpected exception during download of '{url}': {ex.Message}");
					progressItem.AddLog($"[UNEXPECTED EXCEPTION] {ex.GetType().Name}: {ex.Message}");

					// Create a failed result instead of letting the exception bubble up
					result = DownloadResult.Failed($"Unexpected error: {ex.Message}");
					progressItem.Status = DownloadStatus.Failed;
					progressItem.StatusMessage = "Download failed due to unexpected error";
					progressItem.ErrorMessage = ex.Message;
					progressItem.Exception = ex;
					progressItem.EndTime = DateTime.Now;
				}

				if ( result.Success )
				{
					await Logger.LogVerboseAsync($"[DownloadManager] Successfully downloaded: {result.FilePath}");
					progressItem.AddLog($"Download completed successfully: {result.FilePath}");
					if ( result.WasSkipped )
					{
						progressItem.AddLog("File was skipped (already exists)");
						// Update the progress item to reflect that it was skipped - EXPLICITLY set everything
						progressItem.Status = DownloadStatus.Skipped;
						progressItem.StatusMessage = "File already exists";
						progressItem.ProgressPercentage = 100;
						progressItem.FilePath = result.FilePath; // Ensure file path is set
						progressItem.EndTime = DateTime.Now;
						if ( progressItem.StartTime == default )
							progressItem.StartTime = DateTime.Now;

						// Set file size if we can get it
						if ( !string.IsNullOrEmpty(result.FilePath) && System.IO.File.Exists(result.FilePath) )
						{
							try
							{
								long fileSize = new System.IO.FileInfo(result.FilePath).Length;
								progressItem.BytesDownloaded = fileSize;
								progressItem.TotalBytes = fileSize;
								await Logger.LogVerboseAsync($"[DownloadManager] File already exists ({fileSize} bytes): {result.FilePath}");
							}
							catch ( Exception ex )
							{
								await Logger.LogWarningAsync($"[DownloadManager] Could not get file size for skipped file: {ex.Message}");
							}
						}
					}
				}
				else
				{
					await Logger.LogErrorAsync($"[DownloadManager] Failed to download URL '{url}': {result.Message}");
					progressItem.AddLog($"Download failed: {result.Message}");

					// CRITICAL: Update status to Failed so UI shows correct state
					progressItem.Status = DownloadStatus.Failed;
					progressItem.StatusMessage = "Download failed";
					progressItem.ErrorMessage = result.Message;
					progressItem.EndTime = DateTime.Now;
				}

				return result;
			}
			finally
			{
				// Clean up throttling state for this download to prevent memory buildup
				lock ( _logThrottleLock )
				{
					_lastProgressLogTime.Remove(url);
					_lastLoggedStatus.Remove(url);
				}

				// Only release the concurrency limiter if we acquired it (DeadlyStream downloads only)
				if ( concurrencyAcquired )
				{
					_deadlyStreamConcurrencyLimiter.Release();
					await Logger.LogVerboseAsync($"[DownloadManager] Released DeadlyStream concurrency slot. Available: {_deadlyStreamConcurrencyLimiter.CurrentCount}/{5}");
				}
			}
		}
	}
}
