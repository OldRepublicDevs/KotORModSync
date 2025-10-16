// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class DownloadManager
	{
		private readonly List<IDownloadHandler> _handlers;
		private const double Tolerance = 0.01;
		private readonly Dictionary<string, DateTime> _lastProgressLogTime = new Dictionary<string, DateTime>();
		private readonly Dictionary<string, DownloadStatus> _lastLoggedStatus = new Dictionary<string, DownloadStatus>();
		private readonly object _logThrottleLock = new object();
		private const int LogThrottleSeconds = 30;

		private CancellationTokenSource _globalCancellationTokenSource;

		public DownloadManager(IEnumerable<IDownloadHandler> handlers)
		{
			_handlers = new List<IDownloadHandler>(handlers);
			_globalCancellationTokenSource = new CancellationTokenSource();
			Logger.LogVerbose($"[DownloadManager] Initialized with {_handlers.Count} download handlers");
			for ( int i = 0; i < _handlers.Count; i++ )
			{
				Logger.LogVerbose($"[DownloadManager] Handler {i + 1}: {_handlers[i].GetType().Name}");
			}
		}

		public async Task<Dictionary<string, List<string>>> ResolveUrlsToFilenamesAsync(
			IEnumerable<string> urls,
			CancellationToken cancellationToken = default,
			bool sequential = false)
		{
			var urlList = urls.ToList();
			string mode = sequential ? "sequential" : "concurrent";
			await Logger.LogVerboseAsync($"[DownloadManager] Resolving {urlList.Count} URLs to filenames ({mode})");

			var results = new Dictionary<string, List<string>>();

			if ( sequential )
			{
				// Process URLs sequentially
				foreach ( string url in urlList )
				{
					var (resolvedUrl, filenames) = await ResolveUrlToFilenamesInternalAsync(url, cancellationToken).ConfigureAwait(false);
					results[resolvedUrl] = filenames;
				}
			}
			else
			{
				// Process URLs concurrently
				var resolutionTasks = urlList.Select(url => ResolveUrlToFilenamesInternalAsync(url, cancellationToken)).ToList();
				var resolvedItems = await Task.WhenAll(resolutionTasks).ConfigureAwait(false);

				foreach ( var (url, filenames) in resolvedItems )
				{
					results[url] = filenames;
				}
			}

			return results;
		}

		private async Task<(string url, List<string> filenames)> ResolveUrlToFilenamesInternalAsync(
			string url,
			CancellationToken cancellationToken)
		{
			IDownloadHandler handler = _handlers.FirstOrDefault(h => h.CanHandle(url));
			if ( handler == null )
			{
				await Logger.LogWarningAsync($"[DownloadManager] No handler for URL: {url}");
				return (url, filenames: new List<string>());
			}

			try
			{
				await Logger.LogVerboseAsync($"[DownloadManager] Resolving URL with {handler.GetType().Name}: {url}");
				List<string> filenames = await handler.ResolveFilenamesAsync(url, cancellationToken).ConfigureAwait(false);

				if ( filenames == null || filenames.Count == 0 )
				{
					await Logger.LogWarningAsync($"[DownloadManager] No filenames resolved for URL: {url} (Handler: {handler.GetType().Name}). The URL may be incorrect, the page structure may have changed, or the file may no longer be available.");
				}
				else
				{
					await Logger.LogVerboseAsync($"[DownloadManager] Resolved {filenames.Count} filename(s) for URL: {url}");
				}

				return (url, filenames: filenames ?? new List<string>());
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, $"[DownloadManager] Failed to resolve URL: {url}");
				return (url, filenames: new List<string>());
			}
		}




		public void CancelAll()
		{
			try
			{
				Logger.LogVerbose("[DownloadManager] CancelAll() called - using cooperative cancellation");



				_globalCancellationTokenSource?.Cancel();

				Logger.LogVerbose("[DownloadManager] Cooperative cancellation signal sent - downloads will stop gracefully");
			}
			catch ( Exception ex )
			{
				Logger.LogError($"[DownloadManager] Failed to cancel downloads: {ex.Message}");
			}
		}

		public async Task<List<DownloadResult>> DownloadAllWithProgressAsync(
				Dictionary<string, DownloadProgress> urlToProgressMap,
				string destinationDirectory,
				IProgress<DownloadProgress> progressReporter = null,
				CancellationToken cancellationToken = default)
		{

			var combinedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
				_globalCancellationTokenSource.Token,
				cancellationToken).Token;

			var urlList = urlToProgressMap.Keys.ToList();
			await Logger.LogVerboseAsync($"[DownloadManager] Starting concurrent batch download with progress reporting for {urlList.Count} URLs");
			await Logger.LogVerboseAsync($"[DownloadManager] Destination directory: {destinationDirectory}");

			var results = new List<DownloadResult>();
			var tasks = new List<Task<DownloadResult>>();


			foreach ( string url in urlList )
			{
				DownloadProgress progressItem = urlToProgressMap[url];
				await Logger.LogVerboseAsync($"[DownloadManager] Creating download task for URL: {url}, Mod: {progressItem.ModName}");
				tasks.Add(DownloadSingleWithConcurrencyLimit(url, progressItem, destinationDirectory, progressReporter, combinedCancellationToken));
			}


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
			CancellationToken combinedCancellationToken)
		{

			if ( combinedCancellationToken.IsCancellationRequested )
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

			try
			{
				await Logger.LogVerboseAsync($"[DownloadManager] Starting download: {url}");


				progressItem.Status = DownloadStatus.InProgress;
				progressItem.StatusMessage = "Starting download...";
				progressItem.StartTime = DateTime.Now;

				var internalProgressReporter = new Progress<DownloadProgress>(update =>
				{
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

					bool shouldLog = false;
					var now = DateTime.Now;

					lock ( _logThrottleLock )
					{
						bool isFirstLog = !_lastProgressLogTime.ContainsKey(url);
						bool statusChanged = !_lastLoggedStatus.ContainsKey(url) || _lastLoggedStatus[url] != update.Status;
						bool isTerminalStatus = update.Status == DownloadStatus.Completed ||
												update.Status == DownloadStatus.Failed ||
												update.Status == DownloadStatus.Skipped;
						bool hasError = !string.IsNullOrEmpty(update.ErrorMessage);
						DateTime lastLogTime;
						_lastProgressLogTime.TryGetValue(url, out lastLogTime);
						bool throttleExpired = !isFirstLog &&
											   (now - lastLogTime).TotalSeconds >= LogThrottleSeconds;

						shouldLog = isFirstLog || statusChanged || isTerminalStatus || hasError || throttleExpired;

						if ( shouldLog )
						{
							_lastProgressLogTime[url] = now;
							_lastLoggedStatus[url] = update.Status;
						}
					}

					if ( shouldLog )
					{
						_ = Task.Run(() =>
						{
							try
							{
								if ( update.Status == DownloadStatus.Pending ||
									 update.Status == DownloadStatus.Completed ||
									 update.Status == DownloadStatus.Skipped ||
									 update.Status == DownloadStatus.Failed )
								{
									Logger.Log($"[Download] {update.Status}: {Path.GetFileName(update.FilePath ?? url)}");
									if ( !string.IsNullOrEmpty(update.StatusMessage) && update.Status != DownloadStatus.InProgress )
										Logger.LogVerbose($"  {update.StatusMessage}");
								}
							}
							catch { }
						});
					}

					progressReporter?.Report(progressItem);
				});

				DownloadResult result;
				try
				{
					result = await DownloadCacheOptimizer.TryOptimizedDownload(
						url,
						destinationDirectory,
						() => handler.DownloadAsync(url, destinationDirectory, internalProgressReporter, progressItem.TargetFilenames, combinedCancellationToken),
						internalProgressReporter,
						combinedCancellationToken).ConfigureAwait(false);
				}
				catch ( OperationCanceledException )
				{
					await Logger.LogAsync($"[DownloadManager] Download cancelled by user: {url}");
					progressItem.AddLog("[CANCELLED] Download cancelled by user");

					result = DownloadResult.Failed("Download cancelled by user");
					progressItem.Status = DownloadStatus.Failed;
					progressItem.StatusMessage = "Cancelled";
					progressItem.ErrorMessage = "Download cancelled by user";
					progressItem.EndTime = DateTime.Now;
				}
				catch ( Exception ex )
				{
					await Logger.LogErrorAsync($"[DownloadManager] Unexpected exception during download of '{url}': {ex.Message}");
					progressItem.AddLog($"[UNEXPECTED EXCEPTION] {ex.GetType().Name}: {ex.Message}");

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

						progressItem.Status = DownloadStatus.Skipped;
						progressItem.StatusMessage = "File already exists";
						progressItem.ProgressPercentage = 100;
						progressItem.FilePath = result.FilePath;
						progressItem.EndTime = DateTime.Now;
						if ( progressItem.StartTime == default )
							progressItem.StartTime = DateTime.Now;

						if ( !string.IsNullOrEmpty(result.FilePath) && File.Exists(result.FilePath) )
						{
							try
							{
								long fileSize = new FileInfo(result.FilePath).Length;
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

					progressItem.Status = DownloadStatus.Failed;
					progressItem.StatusMessage = "Download failed";
					progressItem.ErrorMessage = result.Message;
					progressItem.EndTime = DateTime.Now;
				}

				return result;
			}
			finally
			{

				lock ( _logThrottleLock )
				{
					_lastProgressLogTime.Remove(url);
					_lastLoggedStatus.Remove(url);
				}
			}
		}
	}
}
