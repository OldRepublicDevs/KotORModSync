// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class DirectDownloadHandler : IDownloadHandler
	{
		private readonly HttpClient _httpClient;

		public DirectDownloadHandler(HttpClient httpClient)
		{
			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			Logger.LogVerbose("[DirectDownload] Initializing direct download handler");
		}

		public bool CanHandle(string url)
		{
			bool canHandle = Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
			Logger.LogVerbose($"[DirectDownload] CanHandle check for URL '{url}': {canHandle}");
			if ( canHandle )
				Logger.LogVerbose($"[DirectDownload] URL scheme: {uri.Scheme}, host: {uri.Host}");
			return canHandle;
		}

		public async Task<List<string>> ResolveFilenamesAsync(string url, CancellationToken cancellationToken = default)
		{
			try
			{
				await Logger.LogVerboseAsync($"[DirectDownload] Resolving filename for URL: {url}");


				if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri validatedUri) )
				{
					await Logger.LogWarningAsync($"[DirectDownload] Invalid URL format: {url}");
					return new List<string>();
				}

				string fileName = null;


				try
				{
					var request = new HttpRequestMessage(HttpMethod.Head, url);
					HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

					if ( response.IsSuccessStatusCode )
					{

						if ( response.Content.Headers.ContentDisposition != null )
						{
							fileName = response.Content.Headers.ContentDisposition.FileName?.Trim('"');
							await Logger.LogVerboseAsync($"[DirectDownload] Got filename from Content-Disposition: {fileName}");
						}

						response.Dispose();
					}
					else
					{
						await Logger.LogVerboseAsync($"[DirectDownload] HEAD request failed with status: {response.StatusCode}, will extract filename from URL");
						response.Dispose();
					}
				}
				catch ( Exception headEx )
				{

					await Logger.LogVerboseAsync($"[DirectDownload] HEAD request failed ({headEx.Message}), will extract filename from URL");
				}


				if ( string.IsNullOrWhiteSpace(fileName) )
				{
					string urlPath = Uri.UnescapeDataString(validatedUri.AbsolutePath);
					fileName = Path.GetFileName(urlPath);
					await Logger.LogVerboseAsync($"[DirectDownload] Got filename from URL path: {fileName}");
				}

				if ( string.IsNullOrWhiteSpace(fileName) || fileName == "/" )
				{
					fileName = "download";
					await Logger.LogVerboseAsync($"[DirectDownload] Using default filename: {fileName}");
				}

				return new List<string> { fileName };
			}
			catch ( Exception ex )
			{
				await Logger.LogWarningAsync($"[DirectDownload] Failed to resolve filename: {ex.Message}");
				return new List<string>();
			}
		}

		public async Task<DownloadResult> DownloadAsync(string url, string destinationDirectory, IProgress<DownloadProgress> progress = null, CancellationToken cancellationToken = default)
		{
			await Logger.LogVerboseAsync($"[DirectDownload] Starting direct download from URL: {url}");
			await Logger.LogVerboseAsync($"[DirectDownload] Destination directory: {destinationDirectory}");

			try
			{

				if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri validatedUri) )
				{
					string errorMsg = $"Invalid URL format: {url}";
					await Logger.LogErrorAsync($"[DirectDownload] {errorMsg}");
					progress?.Report(new DownloadProgress
					{
						Status = DownloadStatus.Failed,
						ErrorMessage = $"Invalid URL: {url}",
						ProgressPercentage = 0,
						EndTime = DateTime.Now
					});
					return DownloadResult.Failed(errorMsg);
				}


				string expectedFileName = Path.GetFileName(Uri.UnescapeDataString(validatedUri.AbsolutePath));
				await Logger.LogVerboseAsync($"[DirectDownload] Expected filename from URL: '{expectedFileName}'");
				await Logger.LogVerboseAsync($"[DirectDownload] Destination directory: '{destinationDirectory}'");

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Starting download...",
					ProgressPercentage = 10,
					StartTime = DateTime.Now
				});

				await Logger.LogVerboseAsync($"[DirectDownload] Making HTTP GET request to: {url}");
				HttpResponseMessage response = await _httpClient.GetAsync(url).ConfigureAwait(continueOnCapturedContext: false);

				await Logger.LogVerboseAsync($"[DirectDownload] Received response with status code: {response.StatusCode}");
				await Logger.LogVerboseAsync($"[DirectDownload] Response content type: {response.Content.Headers.ContentType}");
				await Logger.LogVerboseAsync($"[DirectDownload] Response content length: {response.Content.Headers.ContentLength}");

				_ = response.EnsureSuccessStatusCode();

				string fileName = "download";
				if ( response.RequestMessage != null && response.RequestMessage.RequestUri != null )
				{
					string urlPath = Uri.UnescapeDataString(response.RequestMessage.RequestUri.AbsolutePath);
					fileName = Path.GetFileName(urlPath);
				}

				await Logger.LogVerboseAsync($"[DirectDownload] Extracted filename from URL path: '{fileName}'");

				if ( string.IsNullOrWhiteSpace(fileName) || fileName == "/" )
				{
					fileName = "download";
					await Logger.LogWarningAsync($"[DirectDownload] Filename is empty or invalid, using default: '{fileName}'");
				}

				_ = Directory.CreateDirectory(destinationDirectory);
				string filePath = Path.Combine(destinationDirectory, fileName);
				await Logger.LogVerboseAsync($"[DirectDownload] Writing file to: {filePath}");

				long totalBytes = response.Content.Headers.ContentLength ?? 0;
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Starting download...",
					ProgressPercentage = 0,
					TotalBytes = totalBytes
				});


				using ( Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false) )
				{
					await DownloadHelper.DownloadWithProgressAsync(
						contentStream,
						filePath,
						totalBytes,
						fileName,
						url,
						progress,
						cancellationToken: cancellationToken).ConfigureAwait(false);
				}

				long fileSize = new FileInfo(filePath).Length;
				await Logger.LogVerboseAsync($"[DirectDownload] File download completed successfully. File size: {fileSize} bytes");

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Completed,
					StatusMessage = "Download complete",
					ProgressPercentage = 100,
					BytesDownloaded = fileSize,
					TotalBytes = fileSize,
					FilePath = filePath,
					EndTime = DateTime.Now
				});

				response.Dispose();

				return DownloadResult.Succeeded(filePath, message: "Downloaded via direct link");
			}
			catch ( HttpRequestException httpEx )
			{
				await Logger.LogErrorAsync($"[DirectDownload] HTTP request failed for URL '{url}': {httpEx.Message}");
				await Logger.LogExceptionAsync(httpEx);

				string userMessage = "Direct download failed. This can happen when:\n\n" +
									 "• The download link is broken or expired\n" +
									 "• The server is blocking automated downloads\n" +
									 "• The file has been moved or deleted\n\n" +
									 $"Please try downloading manually from: {url}\n\n" +
									 $"Technical details: {httpEx.Message}";

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = userMessage,
					Exception = httpEx,
					ProgressPercentage = 100,
					EndTime = DateTime.Now
				});
				return DownloadResult.Failed(userMessage);
			}
			catch ( TaskCanceledException tcEx )
			{
				await Logger.LogErrorAsync($"[DirectDownload] Request timeout for URL '{url}': {tcEx.Message}");
				await Logger.LogExceptionAsync(tcEx);

				string userMessage = "Direct download timed out. This can happen when:\n\n" +
									 "• The server is slow or experiencing high traffic\n" +
									 "• Your internet connection is unstable\n" +
									 "• The file is very large\n\n" +
									 $"Please try downloading manually from: {url}\n\n" +
									 $"Technical details: {tcEx.Message}";

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = userMessage,
					Exception = tcEx,
					ProgressPercentage = 100,
					EndTime = DateTime.Now
				});
				return DownloadResult.Failed(userMessage);
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"[DirectDownload] Download failed for URL '{url}': {ex.Message}");
				await Logger.LogExceptionAsync(ex);

				string userMessage = "Direct download failed unexpectedly.\n\n" +
									 $"Please try downloading manually from: {url}\n\n" +
									 $"Technical details: {ex.Message}";

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = userMessage,
					Exception = ex,
					ProgressPercentage = 100,
					EndTime = DateTime.Now
				});
				return DownloadResult.Failed(userMessage);
			}
		}
	}
}
