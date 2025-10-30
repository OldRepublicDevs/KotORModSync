// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class GameFrontDownloadHandler : IDownloadHandler
	{
		public GameFrontDownloadHandler(HttpClient httpClient)
		{
			if (httpClient is null) throw new ArgumentNullException(nameof(httpClient));
			Logger.LogVerbose("[GameFront] Initializing GameFront download handler");

			if (!httpClient.DefaultRequestHeaders.Contains("User-Agent"))
			{
				const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36";
				httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
				Logger.LogVerbose($"[GameFront] Added User-Agent header: {userAgent}");
			}

			if (!httpClient.DefaultRequestHeaders.Contains("Accept"))
			{
				const string acceptHeader = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
				httpClient.DefaultRequestHeaders.Add("Accept", acceptHeader);
				Logger.LogVerbose($"[GameFront] Added Accept header: {acceptHeader}");
			}

			Logger.LogVerbose("[GameFront] Handler initialized with proper browser headers");
		}

		public bool CanHandle(string url)
		{
			bool canHandle = url != null && url.IndexOf("gamefront.com", StringComparison.OrdinalIgnoreCase) >= 0;
			Logger.LogVerbose($"[GameFront] CanHandle check for URL '{url}': {canHandle}");
			return canHandle;
		}

		public async Task<List<string>> ResolveFilenamesAsync(string url, CancellationToken cancellationToken = default)

		{
			await Logger.LogVerboseAsync($"[GameFront] Cannot resolve filenames for GameFront URLs (requires JavaScript): {url}").ConfigureAwait(false);
			await Task.CompletedTask.ConfigureAwait(false);

			return new List<string>();
		}

		public async Task<DownloadResult> DownloadAsync(
			string url,
			string destinationDirectory,
			IProgress<DownloadProgress> progress = null,
			List<string> targetFilenames = null,
			CancellationToken cancellationToken = default)

		{
			await Logger.LogVerboseAsync($"[GameFront] Starting GameFront download from URL: {url}")

.ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[GameFront] Destination directory: {destinationDirectory}").ConfigureAwait(false);


			await Logger.LogWarningAsync("[GameFront] GameFront downloads require JavaScript execution and cannot be automated without a browser engine").ConfigureAwait(false);

			string errorMessage = "GameFront downloads require manual interaction. The site uses JavaScript-based countdown timers and anti-bot protection that cannot be bypassed with HttpClient alone.\n\n" +
								  $"Please download manually from: {url}\n\n" +
								  "The file will start downloading automatically after a short countdown when you visit the page in a web browser.";

			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.Failed,
				ErrorMessage = errorMessage,
				ProgressPercentage = 0,
				EndTime = DateTime.Now,
			});

			return DownloadResult.Failed(errorMessage);
		}

		public async Task<Dictionary<string, object>> GetFileMetadataAsync(string url, CancellationToken cancellationToken = default)
		{
			Dictionary<string, object> metadata = new Dictionary<string, object>(StringComparer.Ordinal);

			try
			{
				await
								// GameFront requires JavaScript, metadata extraction not feasible

								Logger.LogVerboseAsync($"[GameFront] Metadata extraction not supported for GameFront URLs: {url}").ConfigureAwait(false);
				await Task.CompletedTask.ConfigureAwait(false);
			}
			catch (Exception ex)
			{

				await Logger.LogWarningAsync($"[GameFront] Failed to extract metadata: {ex.Message}").ConfigureAwait(false);
			}

			return NormalizeMetadata(metadata);
		}

		public string GetProviderKey()
		{
			return "gamefront";
		}

		private Dictionary<string, object> NormalizeMetadata(Dictionary<string, object> metadata)
		{
			Dictionary<string, object> normalized = new Dictionary<string, object>(metadata, StringComparer.Ordinal)
			{
				// Always add provider
				["provider"] = GetProviderKey(),
			};

			// GameFront has no specific metadata fields due to JavaScript requirements
			return normalized;
		}
	}
}