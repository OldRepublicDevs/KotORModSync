using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	/// <summary>
	/// Unified helper for downloading with consistent progress reporting across all handlers
	/// </summary>
	public static class DownloadHelper
	{
		private const int BufferSize = 8192; // Standard buffer size
		private const int ProgressUpdateIntervalMs = 250; // Update UI every 250ms

		/// <summary>
		/// Downloads a stream to a file with consistent progress reporting
		/// </summary>
		/// <param name="sourceStream">The source stream to read from</param>
		/// <param name="destinationPath">The destination file path</param>
		/// <param name="totalBytes">Total bytes to download (0 if unknown)</param>
		/// <param name="fileName">File name for progress messages</param>
		/// <param name="url">URL for progress identification</param>
		/// <param name="progress">Progress reporter</param>
		/// <param name="modName">Optional mod name for progress identification</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>Total bytes downloaded</returns>
		public static async Task<long> DownloadWithProgressAsync(
			Stream sourceStream,
			string destinationPath,
			long totalBytes,
			string fileName,
			string url,
			IProgress<DownloadProgress> progress = null,
			string modName = null,
			CancellationToken cancellationToken = default)
		{
			// Track start time for speed calculation
			DateTime startTime = DateTime.Now;

			using ( FileStream fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true) )
			{
				byte[] buffer = new byte[BufferSize];
				int bytesRead;
				long totalBytesRead = 0;
				var lastProgressUpdate = DateTimeOffset.UtcNow;

				while ( (bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0 )
				{
					// Check for cancellation
					cancellationToken.ThrowIfCancellationRequested();

					// Write to file
					await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
					totalBytesRead += bytesRead;

					// Update progress (throttled to avoid UI spam)
					var now = DateTimeOffset.UtcNow;
					if ( (now - lastProgressUpdate).TotalMilliseconds >= ProgressUpdateIntervalMs )
					{
						lastProgressUpdate = now;

						// Calculate progress percentage
						double progressPercentage = totalBytes > 0
							? (double)totalBytesRead / totalBytes * 100.0
							: 0;

						// Report progress with StartTime so DownloadSpeed can be calculated
						progress?.Report(new DownloadProgress
						{
							ModName = modName,
							Url = url,
							Status = DownloadStatus.InProgress,
							StatusMessage = totalBytes > 0
								? $"Downloading {fileName}... ({totalBytesRead:N0} / {totalBytes:N0} bytes)"
								: $"Downloading {fileName}... ({totalBytesRead:N0} bytes)",
							ProgressPercentage = totalBytes > 0 ? Math.Min(progressPercentage, 100) : 0,
							BytesDownloaded = totalBytesRead,
							TotalBytes = totalBytes,
							StartTime = startTime,
							FilePath = destinationPath
						});
					}
				}

				// Ensure all data is written to disk
				await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

				// Final progress update to ensure 100% is reported
				if ( totalBytes > 0 )
				{
					progress?.Report(new DownloadProgress
					{
						ModName = modName,
						Url = url,
						Status = DownloadStatus.InProgress,
						StatusMessage = $"Download complete: {fileName}",
						ProgressPercentage = 100,
						BytesDownloaded = totalBytesRead,
						TotalBytes = totalBytes,
						StartTime = startTime,
						FilePath = destinationPath
					});
				}

				return totalBytesRead;
			}
		}
	}
}

