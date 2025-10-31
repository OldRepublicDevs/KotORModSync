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
			if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||

				 (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
			{
				return false;
			}

			// Exclude URLs that should be handled by specialized handlers
			string lowerUrl = url.ToLowerInvariant();
			if (lowerUrl.Contains("nexusmods.com") ||
				 lowerUrl.Contains("deadlystream.com") ||
				 lowerUrl.Contains("gamefront.com") ||
				 lowerUrl.Contains("mega.nz"))
			{
				return false;
			}

			Logger.LogVerbose($"[DirectDownload] URL scheme: {uri.Scheme}, host: {uri.Host}");
			return true;
		}

		public async Task<List<string>> ResolveFilenamesAsync(string url, CancellationToken cancellationToken = default)

		{
			try
			{
				await Logger.LogVerboseAsync($"[DirectDownload] Resolving filename for URL: {url}")

.ConfigureAwait(false);

				if (!Uri.TryCreate(url, UriKind.Absolute, out Uri validatedUri))
				{
					await Logger.LogWarningAsync($"[DirectDownload] Invalid URL format: {url}").ConfigureAwait(false);
					return new List<string>();
				}

				string fileName = null;

				try
				{
					HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url);

					HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken)





.ConfigureAwait(false);

					if (response.IsSuccessStatusCode)
					{

						if (response.Content.Headers.ContentDisposition != null)
						{
							fileName = response.Content.Headers.ContentDisposition.FileName?.Trim('"');
							await Logger.LogVerboseAsync($"[DirectDownload] Got filename from Content-Disposition: {fileName}").ConfigureAwait(false);
						}

						response.Dispose();
					}
					else
					{
						await Logger.LogVerboseAsync($"[DirectDownload] HEAD request failed with status: {response.StatusCode}, will extract filename from URL").ConfigureAwait(false);
						response.Dispose();
					}
				}
				catch (Exception headEx)
				{

					await Logger.LogVerboseAsync($"[DirectDownload] HEAD request failed ({headEx.Message}), will extract filename from URL").ConfigureAwait(false);
				}

				if (string.IsNullOrWhiteSpace(fileName))
				{
					string urlPath = Uri.UnescapeDataString(validatedUri.AbsolutePath);
					fileName = Path.GetFileName(urlPath);
					await Logger.LogVerboseAsync($"[DirectDownload] Got filename from URL path: {fileName}").ConfigureAwait(false);
				}

				if (string.IsNullOrWhiteSpace(fileName) || string.Equals(fileName, "/", StringComparison.Ordinal))
				{
					fileName = "download";

					await Logger.LogVerboseAsync($"[DirectDownload] Using default filename: {fileName}").ConfigureAwait(false);
				}

				return new List<string> { fileName };
			}
			catch (Exception ex)

			{
				await Logger.LogExceptionAsync(ex, $"[DirectDownload] Failed to resolve filename for URL: {url}").ConfigureAwait(false);
				return new List<string>();
			}
		}

		public async Task<DownloadResult> DownloadAsync(
			string url,
			string destinationDirectory,
			IProgress<DownloadProgress> progress = null,
			List<string> targetFilenames = null,
			CancellationToken cancellationToken = default)

		{
			await Logger.LogVerboseAsync($"[DirectDownload] Starting direct download from URL: {url}").ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[DirectDownload] Destination directory: {destinationDirectory}").ConfigureAwait(false);

			try
			{

				if (!Uri.TryCreate(url, UriKind.Absolute, out Uri validatedUri))
				{
					string errorMsg = $"Invalid URL format: {url}";

					await Logger.LogErrorAsync($"[DirectDownload] {errorMsg}").ConfigureAwait(false);
					progress?.Report(new DownloadProgress
					{
						Status = DownloadStatus.Failed,
						ErrorMessage = $"Invalid URL: {url}",
						ProgressPercentage = 0,
						EndTime = DateTime.Now,
					});
					return DownloadResult.Failed(errorMsg);
				}

				string expectedFileName = Path.GetFileName(Uri.UnescapeDataString(validatedUri.AbsolutePath));

				await Logger.LogVerboseAsync($"[DirectDownload] Expected filename from URL: '{expectedFileName}'")


.ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[DirectDownload] Destination directory: '{destinationDirectory}'")







.ConfigureAwait(false);

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Starting download...",
					ProgressPercentage = 10,
					StartTime = DateTime.Now,
				});

				await Logger.LogVerboseAsync($"[DirectDownload] Making HTTP GET request to: {url}").ConfigureAwait(false);
				HttpResponseMessage response = await _httpClient.GetAsync(url).ConfigureAwait(continueOnCapturedContext: false);

				await Logger.LogVerboseAsync($"[DirectDownload] Received response with status code: {response.StatusCode}").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[DirectDownload] Response content type: {response.Content.Headers.ContentType}").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[DirectDownload] Response content length: {response.Content.Headers.ContentLength}").ConfigureAwait(false);

				_ = response.EnsureSuccessStatusCode();

				string fileName = "download";

				if (response.RequestMessage != null && response.RequestMessage.RequestUri != null)
				{
					string urlPath = Uri.UnescapeDataString(response.RequestMessage.RequestUri.AbsolutePath);
					fileName = Path.GetFileName(urlPath);
				}

				await Logger.LogVerboseAsync($"[DirectDownload] Extracted filename from URL path: '{fileName}'").ConfigureAwait(false);

				if (string.IsNullOrWhiteSpace(fileName) || string.Equals(fileName, "/", StringComparison.Ordinal))
				{
					fileName = "download";
					await Logger.LogWarningAsync($"[DirectDownload] Filename is empty or invalid, using default: '{fileName}'").ConfigureAwait(false);
				}

				_ = Directory.CreateDirectory(destinationDirectory);
				string filePath = Path.Combine(destinationDirectory, fileName);
				await Logger.LogVerboseAsync($"[DirectDownload] Writing file to: {filePath}").ConfigureAwait(false);

				long totalBytes = response.Content.Headers.ContentLength ?? 0;
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Starting download...",
					ProgressPercentage = 0,
					TotalBytes = totalBytes,
				});

				using (Stream contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
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
				await Logger.LogVerboseAsync($"[DirectDownload] File download completed successfully. File size: {fileSize} bytes").ConfigureAwait(false);

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Completed,
					StatusMessage = "Download complete",
					ProgressPercentage = 100,
					BytesDownloaded = fileSize,
					TotalBytes = fileSize,
					FilePath = filePath,
					EndTime = DateTime.Now,
				});

				response.Dispose();

				return DownloadResult.Succeeded(filePath, message: "Downloaded via direct link");
			}
			catch (HttpRequestException httpEx)
			{
				await Logger.LogErrorAsync($"[DirectDownload] HTTP request failed for URL '{url}': {httpEx.Message}").ConfigureAwait(false);
				await Logger.LogExceptionAsync(httpEx).ConfigureAwait(false);

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
					EndTime = DateTime.Now,
				});
				return DownloadResult.Failed(userMessage);
			}
			catch (TaskCanceledException tcEx)
			{
				await Logger.LogErrorAsync($"[DirectDownload] Request timeout for URL '{url}': {tcEx.Message}").ConfigureAwait(false);
				await Logger.LogExceptionAsync(tcEx).ConfigureAwait(false);

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
					EndTime = DateTime.Now,
				});
				return DownloadResult.Failed(userMessage);
			}
			catch (Exception ex)
			{
				await Logger.LogErrorAsync($"[DirectDownload] Download failed for URL '{url}': {ex.Message}").ConfigureAwait(false);
				await Logger.LogExceptionAsync(ex).ConfigureAwait(false);

				string userMessage = "Direct download failed unexpectedly.\n\n" +
									 $"Please try downloading manually from: {url}\n\n" +
									 $"Technical details: {ex.Message}";

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = userMessage,
					Exception = ex,
					ProgressPercentage = 100,
					EndTime = DateTime.Now,
				});
				return DownloadResult.Failed(userMessage);
			}
		}

		public async Task<Dictionary<string, object>> GetFileMetadataAsync(string url, CancellationToken cancellationToken = default)
		{
			Dictionary<string, object> metadata = new Dictionary<string, object>(StringComparer.Ordinal);

			try
			{
				// Normalize URL for canonical representation
				string normalizedUrl = Utility.UrlNormalizer.Normalize(url);
				metadata["url"] = normalizedUrl;

				// Extract filename from URL
				string fileName = Path.GetFileName(new Uri(url).LocalPath);
				if (!string.IsNullOrEmpty(fileName))
				{
					metadata["fileName"] = fileName;
				}

				// Try HEAD request first for metadata
				try
				{
					HttpRequestMessage headRequest = new HttpRequestMessage(HttpMethod.Head, url);
					HttpResponseMessage headResponse = await _httpClient.SendAsync(headRequest, cancellationToken).ConfigureAwait(false);

					if (headResponse.IsSuccessStatusCode)
					{
						// Extract Content-Length
						if (headResponse.Content.Headers.ContentLength.HasValue)
						{
							metadata["contentLength"] = headResponse.Content.Headers.ContentLength.Value;
						}

						// Extract Last-Modified
						if (headResponse.Content.Headers.LastModified.HasValue)
						{
							metadata["lastModified"] = headResponse.Content.Headers.LastModified.Value.ToString("R");
						}

						// Extract ETag (only if present, don't include null)
						if (headResponse.Headers.ETag != null)
						{
							metadata["etag"] = headResponse.Headers.ETag.Tag?.Trim('"') ?? headResponse.Headers.ETag.ToString();
						}

						await Logger.LogVerboseAsync($"[DirectDownload] HEAD request succeeded for metadata").ConfigureAwait(false);
					}
					else
					{
						await Logger.LogVerboseAsync($"[DirectDownload] HEAD request failed: {headResponse.StatusCode}, trying Range request").ConfigureAwait(false);
						// Fallback to Range request
						await TryRangeRequestForMetadata(url, metadata, cancellationToken).ConfigureAwait(false);
					}
				}
				catch (HttpRequestException)
				{
					await
										// HEAD not supported, try Range request
										Logger.LogVerboseAsync("[DirectDownload] HEAD not supported, trying Range request").ConfigureAwait(false);
					await TryRangeRequestForMetadata(url, metadata, cancellationToken).ConfigureAwait(false);
				}

				List<string> metadataList = new List<string>();
				foreach (KeyValuePair<string, object> kvp in metadata)
				{
					metadataList.Add($"{kvp.Key}={kvp.Value}");
				}

				await Logger.LogVerboseAsync($"[DirectDownload] Extracted metadata: {string.Join(", ", metadataList)}").ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.LogWarningAsync($"[DirectDownload] Failed to extract metadata: {ex.Message}").ConfigureAwait(false);
			}

			return NormalizeMetadata(metadata);
		}

		private async Task TryRangeRequestForMetadata(string url, Dictionary<string, object> metadata, CancellationToken cancellationToken)
		{
			try
			{
				HttpRequestMessage rangeRequest = new HttpRequestMessage(HttpMethod.Get, url);
				rangeRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);

				HttpResponseMessage rangeResponse = await _httpClient.SendAsync(rangeRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

				if (rangeResponse.IsSuccessStatusCode || rangeResponse.StatusCode == System.Net.HttpStatusCode.PartialContent)
				{
					// Extract Content-Length from Content-Range header
					if (rangeResponse.Content.Headers.ContentRange != null)
					{
						metadata["contentLength"] = rangeResponse.Content.Headers.ContentRange.Length ?? 0;
					}
					else if (rangeResponse.Content.Headers.ContentLength.HasValue)
					{
						metadata["contentLength"] = rangeResponse.Content.Headers.ContentLength.Value;
					}

					// Extract Last-Modified
					if (rangeResponse.Content.Headers.LastModified.HasValue)
					{
						metadata["lastModified"] = rangeResponse.Content.Headers.LastModified.Value.ToString("R");
					}

					// Extract ETag (only if present)
					if (rangeResponse.Headers.ETag != null)
					{
						metadata["etag"] = rangeResponse.Headers.ETag.Tag?.Trim('"') ?? rangeResponse.Headers.ETag.ToString();
					}

					await Logger.LogVerboseAsync("[DirectDownload] Range request succeeded for metadata").ConfigureAwait(false);
				}
			}
			catch (Exception ex)
			{
				await Logger.LogVerboseAsync($"[DirectDownload] Range request failed: {ex.Message}").ConfigureAwait(false);
			}
		}

		public string GetProviderKey()
		{
			return "direct";
		}

		private Dictionary<string, object> NormalizeMetadata(Dictionary<string, object> raw)
		{
			Dictionary<string, object> normalized = new Dictionary<string, object>(StringComparer.Ordinal);
			string[] whitelist = new[] { "provider", "url", "contentLength", "lastModified", "etag", "fileName" };

			// Always add provider
			normalized["provider"] = GetProviderKey();

			foreach (string field in whitelist)
			{
				if (string.Equals(field, "provider", StringComparison.Ordinal)) continue; // Already added

				if (!raw.ContainsKey(field)) continue;

				object value = raw[field];

				// Type-specific normalization
				if (string.Equals(field, "contentLength", StringComparison.Ordinal))
				{
					normalized[field] = Convert.ToInt64(value);
				}
				else if (string.Equals(field, "url", StringComparison.Ordinal) && value != null)
				{
					// Apply URL canonicalization
					normalized[field] = Utility.UrlNormalizer.Normalize(value.ToString());
				}
				else if (value != null)
				{
					normalized[field] = value.ToString();
				}
				// Omit null values (especially for etag)
			}

			return normalized;
		}
	}
}