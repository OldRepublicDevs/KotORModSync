// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class NexusModsDownloadHandler : IDownloadHandler
	{
		private readonly HttpClient _httpClient;
		private readonly string _apiKey;

		public NexusModsDownloadHandler(HttpClient httpClient, string apiKey)
		{
			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			_apiKey = apiKey;
			Logger.LogVerbose("[NexusMods] Initializing Nexus Mods download handler");
			Logger.LogVerbose($"[NexusMods] API key provided: {!string.IsNullOrWhiteSpace(_apiKey)}");

			if ( !_httpClient.DefaultRequestHeaders.Contains("User-Agent") )
			{
				const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
				_httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
				Logger.LogVerbose($"[NexusMods] Added User-Agent header: {userAgent}");
			}

			if ( !_httpClient.DefaultRequestHeaders.Contains("Accept") )
			{
				const string acceptHeader = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8";
				_httpClient.DefaultRequestHeaders.Add("Accept", acceptHeader);
				Logger.LogVerbose($"[NexusMods] Added Accept header: {acceptHeader}");
			}

			if ( !_httpClient.DefaultRequestHeaders.Contains("Accept-Language") )
			{
				const string acceptLanguage = "en-US,en;q=0.9";
				_httpClient.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);
				Logger.LogVerbose($"[NexusMods] Added Accept-Language header: {acceptLanguage}");
			}

			Logger.LogVerbose("[NexusMods] Handler initialized with proper browser headers");
		}

		public bool CanHandle(string url)
		{
			bool canHandle = url != null && url.IndexOf("nexusmods.com", StringComparison.OrdinalIgnoreCase) >= 0;
			Logger.LogVerbose($"[NexusMods] CanHandle check for URL '{url}': {canHandle}");
			return canHandle;
		}

		public async Task<List<string>> ResolveFilenamesAsync(string url, CancellationToken cancellationToken = default)
		{
			try
			{
				await Logger.LogVerboseAsync($"[NexusMods] Resolving filename for URL: {url}");

				if ( string.IsNullOrWhiteSpace(_apiKey) )
				{
					await Logger.LogVerboseAsync("[NexusMods] No API key provided, cannot resolve filename");
					return new List<string>();
				}

				if ( Uri.TryCreate(url, UriKind.Absolute, out Uri validatedUri) )
				{
					string fileName = Path.GetFileName(Uri.UnescapeDataString(validatedUri.AbsolutePath));
					if ( !string.IsNullOrWhiteSpace(fileName) && fileName != "/" )
					{
						await Logger.LogVerboseAsync($"[NexusMods] Extracted filename from URL: {fileName}");
						return new List<string> { fileName };
					}
				}

				await Logger.LogVerboseAsync("[NexusMods] Cannot resolve filename without downloading");
				return new List<string>();
			}
			catch ( Exception ex )
			{
				await Logger.LogWarningAsync($"[NexusMods] Failed to resolve filename: {ex.Message}");
				return new List<string>();
			}
		}

		public async Task<DownloadResult> DownloadAsync(string url, string destinationDirectory, IProgress<DownloadProgress> progress = null, CancellationToken cancellationToken = default)
		{
			await Logger.LogVerboseAsync($"[NexusMods] Starting Nexus Mods download from URL: {url}");
			await Logger.LogVerboseAsync($"[NexusMods] Destination directory: {destinationDirectory}");

			try
			{

				if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri validatedUri) )
				{
					string errorMsg = $"Invalid URL format: {url}";
					await Logger.LogErrorAsync($"[NexusMods] {errorMsg}");
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
				await Logger.LogVerboseAsync($"[NexusMods] Expected filename: {expectedFileName}");

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Accessing Nexus Mods page...",
					ProgressPercentage = 10,
					StartTime = DateTime.Now
				});

				if ( !string.IsNullOrWhiteSpace(_apiKey) )
				{
					await Logger.LogVerboseAsync("[NexusMods] Using API key for download");
					return await DownloadWithApiKey(url, destinationDirectory, progress, cancellationToken);
				}
				else
				{
					await Logger.LogVerboseAsync("[NexusMods] No API key provided, attempting free download");
					return await DownloadWithoutApiKey(url, progress);
				}
			}
			catch ( HttpRequestException httpEx )
			{
				await Logger.LogErrorAsync($"[NexusMods] HTTP request failed for URL '{url}': {httpEx.Message}");
				await Logger.LogExceptionAsync(httpEx);

				string userMessage = "Nexus Mods download failed. This usually happens when:\n\n" +
									 "• The site is blocking automated downloads (403 Forbidden)\n" +
									 "• An API key is required but not configured\n" +
									 "• The mod page requires login/authentication\n\n" +
									 $"Please download manually from: {url}\n\n" +
									 "Or configure a Nexus Mods API key in settings (coming soon) for automated downloads.\n\n" +
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
				await Logger.LogErrorAsync($"[NexusMods] Request timeout for URL '{url}': {tcEx.Message}");
				await Logger.LogExceptionAsync(tcEx);

				string userMessage = "Nexus Mods download timed out. This can happen when:\n\n" +
									 "• The site is slow or experiencing high traffic\n" +
									 "• Your internet connection is unstable\n" +
									 "• The mod file is very large\n\n" +
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
				await Logger.LogErrorAsync($"[NexusMods] Download failed for URL '{url}': {ex.Message}");
				await Logger.LogExceptionAsync(ex);

				string userMessage = $"Nexus Mods download failed unexpectedly.\n\n" +
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

		private async Task<DownloadResult> DownloadWithApiKey(string url, string destinationDirectory, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
		{
			await Logger.LogVerboseAsync("[NexusMods] Resolving download link from Nexus Mods API");
			NexusDownloadLink linkInfo = await ResolveDownloadLinkAsync(url).ConfigureAwait(false);
			if ( linkInfo == null || string.IsNullOrEmpty(linkInfo.Url) )
			{
				await Logger.LogErrorAsync("[NexusMods] Failed to resolve download link from Nexus Mods API");
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = "Unable to resolve Nexus Mods download link",
					ProgressPercentage = 100,
					EndTime = DateTime.Now
				});
				return DownloadResult.Failed("Unable to resolve Nexus Mods download link.");
			}

			await Logger.LogVerboseAsync($"[NexusMods] Resolved download URL: {linkInfo.Url}");
			await Logger.LogVerboseAsync($"[NexusMods] Resolved filename: {linkInfo.FileName}");

			string fileName = string.IsNullOrEmpty(linkInfo.FileName) ? "nexus_download" : linkInfo.FileName;
			if ( string.IsNullOrEmpty(linkInfo.FileName) )
				await Logger.LogWarningAsync("[NexusMods] No filename provided by API, using default: 'nexus_download'");

			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.InProgress,
				StatusMessage = "Downloading from Nexus Mods...",
				ProgressPercentage = 50
			});

			await Logger.LogVerboseAsync($"[NexusMods] Making HTTP GET request to download URL: {linkInfo.Url}");
			var request = new HttpRequestMessage(HttpMethod.Get, linkInfo.Url);
			request.Headers.Add("apikey", _apiKey);
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
			await Logger.LogVerboseAsync("[NexusMods] Added API key and Accept header to request");

			HttpResponseMessage response = await _httpClient.SendAsync(request).ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[NexusMods] Received response with status code: {response.StatusCode}");
			await Logger.LogVerboseAsync($"[NexusMods] Response content type: {response.Content.Headers.ContentType}");
			await Logger.LogVerboseAsync($"[NexusMods] Response content length: {response.Content.Headers.ContentLength}");

			_ = response.EnsureSuccessStatusCode();

			_ = Directory.CreateDirectory(destinationDirectory);
			string filePath = Path.Combine(destinationDirectory, fileName);
			await Logger.LogVerboseAsync($"[NexusMods] Writing file to: {filePath}");

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
			await Logger.LogVerboseAsync($"[NexusMods] File download completed successfully. File size: {fileSize} bytes");

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

			request.Dispose();
			response.Dispose();

			return DownloadResult.Succeeded(filePath, "Downloaded from Nexus Mods");
		}

		private async Task<DownloadResult> DownloadWithoutApiKey(string url, IProgress<DownloadProgress> progress)
		{
			await Logger.LogVerboseAsync("[NexusMods] Attempting free download from Nexus Mods page");

			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.InProgress,
				StatusMessage = "Loading mod page...",
				ProgressPercentage = 20
			});

			await Logger.LogVerboseAsync($"[NexusMods] Making HTTP GET request to mod page: {url}");
			HttpResponseMessage pageResponse = await _httpClient.GetAsync(url).ConfigureAwait(false);

			await Logger.LogVerboseAsync($"[NexusMods] Received page response with status code: {pageResponse.StatusCode}");
			_ = pageResponse.EnsureSuccessStatusCode();

			string html = await pageResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[NexusMods] Downloaded page HTML, length: {html.Length} characters");

			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.InProgress,
				StatusMessage = "Looking for download link...",
				ProgressPercentage = 40
			});

			await Logger.LogWarningAsync("[NexusMods] Free downloads from Nexus Mods require manual interaction and cannot be automated");

			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.Failed,
				ErrorMessage = "Free downloads from Nexus Mods require manual interaction. Please download manually or provide an API key.",
				ProgressPercentage = 100,
				EndTime = DateTime.Now
			});

			pageResponse.Dispose();
			return DownloadResult.Failed("Free downloads from Nexus Mods require manual interaction. Please download the mod manually from the website or provide an API key for automated downloads.");
		}

		private static async Task<NexusDownloadLink> ResolveDownloadLinkAsync(string url)
		{
			await Logger.LogVerboseAsync($"[NexusMods] ResolveDownloadLinkAsync called with URL: {url}");
			await Logger.LogWarningAsync("[NexusMods] ResolveDownloadLinkAsync is not implemented - returning null");

			await Task.Delay(0).ConfigureAwait(false);
			return null;
		}

		private sealed class NexusDownloadLink
		{
			public string Url { get; set; } = string.Empty;
			public string FileName { get; set; } = string.Empty;
		}
	}
}
