using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class DownloadManager
	{
		private readonly List<IDownloadHandler> _handlers;
		private const double Tolerance = 0.01;

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
			System.Threading.CancellationToken cancellationToken = default)
		{
			var urlList = urlToProgressMap.Keys.ToList();
			await Logger.LogVerboseAsync($"[DownloadManager] Starting batch download with progress reporting for {urlList.Count} URLs");
			await Logger.LogVerboseAsync($"[DownloadManager] Destination directory: {destinationDirectory}");

			var results = new List<DownloadResult>();
			int successCount = 0;
			int failCount = 0;

			for ( int i = 0; i < urlList.Count; i++ )
			{
				// Check for cancellation
				if ( cancellationToken.IsCancellationRequested )
				{
					await Logger.LogVerboseAsync("[DownloadManager] Download cancelled by user");
					break;
				}

				string url = urlList[i];
				DownloadProgress progressItem = urlToProgressMap[url];
				await Logger.LogVerboseAsync($"[DownloadManager] Processing URL {i + 1}/{urlList.Count}: {url}");

				IDownloadHandler handler = _handlers.FirstOrDefault(h => h.CanHandle(url));
				if ( handler == null )
				{
					await Logger.LogErrorAsync($"[DownloadManager] No handler configured for URL: {url}");
					progressItem.Status = DownloadStatus.Failed;
					progressItem.ErrorMessage = "No handler configured for this URL";
					results.Add(DownloadResult.Failed("No handler configured for URL: " + url));
					failCount++;
					continue;
				}

				await Logger.LogVerboseAsync($"[DownloadManager] Using handler: {handler.GetType().Name} for URL: {url}");
				progressItem.AddLog($"Using handler: {handler.GetType().Name}");

				// Create a progress reporter that updates the DownloadProgress object and logs changes
				var progressReporter = new Progress<DownloadProgress>(update =>
				{
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
				});

				DownloadResult result = await handler.DownloadAsync(url, destinationDirectory, progressReporter).ConfigureAwait(false);

				if ( result.Success )
				{
					successCount++;
					await Logger.LogVerboseAsync($"[DownloadManager] Successfully downloaded: {result.FilePath}");
					progressItem.AddLog($"Download completed successfully: {result.FilePath}");
					if ( result.WasSkipped )
						progressItem.AddLog("File was skipped (already exists)");
				}
				else
				{
					failCount++;
					await Logger.LogErrorAsync($"[DownloadManager] Failed to download URL '{url}': {result.Message}");
					progressItem.AddLog($"Download failed: {result.Message}");
				}

				results.Add(result);
			}

			await Logger.LogVerboseAsync($"[DownloadManager] Batch download completed. Success: {successCount}, Failed: {failCount}");
			return results;
		}
	}
}
