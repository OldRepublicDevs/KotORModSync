using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class GameFrontDownloadHandler : IDownloadHandler
	{
		private readonly HttpClient _httpClient;

		public GameFrontDownloadHandler(HttpClient httpClient)
		{
			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			Logger.LogVerbose("[GameFront] Initializing GameFront download handler");

			// Set up proper headers for GameFront
			if ( !_httpClient.DefaultRequestHeaders.Contains("User-Agent") )
			{
				const string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36";
				_httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
				Logger.LogVerbose($"[GameFront] Added User-Agent header: {userAgent}");
			}

			if ( !_httpClient.DefaultRequestHeaders.Contains("Accept") )
			{
				const string acceptHeader = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
				_httpClient.DefaultRequestHeaders.Add("Accept", acceptHeader);
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

		public async Task<DownloadResult> DownloadAsync(string url, string destinationDirectory, IProgress<DownloadProgress> progress = null)
		{
			await Logger.LogVerboseAsync($"[GameFront] Starting GameFront download from URL: {url}");
			await Logger.LogVerboseAsync($"[GameFront] Destination directory: {destinationDirectory}");

			// GameFront requires JavaScript execution for automatic downloads (countdown timer)
			// Without a full browser engine, we cannot automate this
			await Logger.LogWarningAsync("[GameFront] GameFront downloads require JavaScript execution and cannot be automated without a browser engine");

			string errorMessage = "GameFront downloads require manual interaction. The site uses JavaScript-based countdown timers and anti-bot protection that cannot be bypassed with HttpClient alone.\n\n" +
								  $"Please download manually from: {url}\n\n" +
								  "The file will start downloading automatically after a short countdown when you visit the page in a web browser.";

			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.Failed,
				ErrorMessage = errorMessage,
				ProgressPercentage = 0,
				EndTime = DateTime.Now
			});

			return DownloadResult.Failed(errorMessage);

			/* Original implementation kept for reference - requires browser automation
			try
			{
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Accessing GameFront page...",
					ProgressPercentage = 10,
					StartTime = DateTime.Now
				});

				// Ensure URL ends with /download
				string downloadUrl = url.EndsWith("/download", StringComparison.OrdinalIgnoreCase) ? url : url + "/download";
				await Logger.LogVerboseAsync($"[GameFront] Using download URL: {downloadUrl}");

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Fetching download page...",
					ProgressPercentage = 30
				});

				// Fetch the download page
				await Logger.LogVerboseAsync($"[GameFront] Making HTTP GET request to: {downloadUrl}");
				HttpResponseMessage response = await _httpClient.GetAsync(downloadUrl).ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[GameFront] Received response with status code: {response.StatusCode}");
				response.EnsureSuccessStatusCode();

				string html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[GameFront] Downloaded HTML content, length: {html.Length} characters");

				// Parse the HTML to find the actual file link
				var htmlDoc = new HtmlDocument();
				htmlDoc.LoadHtml(html);

				// GameFront download pages have a meta refresh or JavaScript that triggers the download
				// Look for script tags that contain the download URL
				string directDownloadUrl = ExtractDirectDownloadUrl(htmlDoc, downloadUrl);

				if ( string.IsNullOrEmpty(directDownloadUrl) )
				{
					await Logger.LogErrorAsync("[GameFront] Could not find direct download URL in page HTML");
					progress?.Report(new DownloadProgress
					{
					Status = DownloadStatus.Failed,
					ErrorMessage = "Could not extract direct download link from GameFront page",
					ProgressPercentage = 100
					});
					return DownloadResult.Failed("Could not extract direct download link from GameFront page");
				}

				await Logger.LogVerboseAsync($"[GameFront] Found direct download URL: {directDownloadUrl}");

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Downloading file...",
					ProgressPercentage = 50
				});

				// Download the actual file
				await Logger.LogVerboseAsync($"[GameFront] Downloading file from: {directDownloadUrl}");
				HttpResponseMessage fileResponse = await _httpClient.GetAsync(directDownloadUrl).ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[GameFront] File download response status: {fileResponse.StatusCode}");
				fileResponse.EnsureSuccessStatusCode();

				// Get filename from Content-Disposition header or URL
				string fileName = GetFileNameFromResponse(fileResponse, directDownloadUrl);
				await Logger.LogVerboseAsync($"[GameFront] Determined filename: {fileName}");

				string filePath = Path.Combine(destinationDirectory, fileName);

				// Check if file already exists
				if ( File.Exists(filePath) )
				{
					await Logger.LogVerboseAsync($"[GameFront] File already exists, skipping download: {filePath}");
					progress?.Report(new DownloadProgress
					{
						Status = DownloadStatus.Skipped,
						StatusMessage = "File already exists",
						FilePath = filePath,
						ProgressPercentage = 100
					});
					return DownloadResult.Skipped(filePath, "File already exists");
				}

				// Write file
				await Logger.LogVerboseAsync($"[GameFront] Writing file to: {filePath}");
				_ = Directory.CreateDirectory(destinationDirectory);
				byte[] fileBytes = await fileResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
				using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
				{
					await fs.WriteAsync(fileBytes, 0, fileBytes.Length).ConfigureAwait(false);
				}
				await Logger.LogVerboseAsync($"[GameFront] File download completed successfully. File size: {fileBytes.Length} bytes");

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Completed,
					StatusMessage = "Download complete",
					FilePath = filePath,
					ProgressPercentage = 100,
					BytesDownloaded = fileBytes.Length,
					TotalBytes = fileBytes.Length,
					EndTime = DateTime.Now
				});

				return DownloadResult.Succeeded(filePath, "Downloaded from GameFront");
			}
			catch ( HttpRequestException httpEx )
			{
				await Logger.LogErrorAsync($"[GameFront] HTTP request failed for URL '{url}': {httpEx.Message}");
				await Logger.LogExceptionAsync(httpEx);
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = $"HTTP request failed: {httpEx.Message}",
					Exception = httpEx,
					ProgressPercentage = 100
				});
				return DownloadResult.Failed($"GameFront HTTP request failed: {httpEx.Message}");
			}
			catch ( TaskCanceledException tcEx )
			{
				await Logger.LogErrorAsync($"[GameFront] Request timeout for URL '{url}': {tcEx.Message}");
				await Logger.LogExceptionAsync(tcEx);
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = $"Request timeout: {tcEx.Message}",
					Exception = tcEx,
					ProgressPercentage = 100
				});
				return DownloadResult.Failed($"GameFront request timeout: {tcEx.Message}");
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"[GameFront] Download failed for URL '{url}': {ex.Message}");
				await Logger.LogExceptionAsync(ex);
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = ex.Message,
					Exception = ex,
					ProgressPercentage = 100
				});
				return DownloadResult.Failed("GameFront download failed: " + ex.Message);
			}
			*/
		}

		private static string ExtractDirectDownloadUrl(HtmlDocument htmlDoc, string baseUrl)
		{
			// Try to find meta refresh tag
			HtmlNode metaRefresh = htmlDoc.DocumentNode.SelectSingleNode("//meta[@http-equiv='refresh']");
			if ( metaRefresh != null )
			{
				string content = metaRefresh.GetAttributeValue("content", "");
				if ( !string.IsNullOrEmpty(content) && content.Contains("url=") )
				{
					string[] parts = content.Split(new[] { "url=" }, StringSplitOptions.None);
					if ( parts.Length > 1 )
					{
						string refreshUrl = parts[1].Trim();
						Logger.LogVerbose($"[GameFront] Found meta refresh URL: {refreshUrl}");
						return new Uri(new Uri(baseUrl), refreshUrl).AbsoluteUri;
					}
				}
			}

			// Try to find script tags containing download URLs
			HtmlNodeCollection scriptNodes = htmlDoc.DocumentNode.SelectNodes("//script");
			if ( scriptNodes != null )
			{
				foreach ( HtmlNode script in scriptNodes )
				{
					string scriptContent = script.InnerHtml;
					// Look for common patterns like window.location, location.href, or direct download URLs
					if ( scriptContent.Contains("window.location") || scriptContent.Contains("location.href") )
					{
						// Extract URL from JavaScript (simplified)
						int urlStart = scriptContent.IndexOf("http", StringComparison.OrdinalIgnoreCase);
						if ( urlStart >= 0 )
						{
							int urlEnd = scriptContent.IndexOfAny(new[] { '\'', '"', ';', ')' }, urlStart);
							if ( urlEnd > urlStart )
							{
								string jsUrl = scriptContent.Substring(urlStart, urlEnd - urlStart);
								Logger.LogVerbose($"[GameFront] Found URL in script: {jsUrl}");
								return jsUrl;
							}
						}
					}
				}
			}

			// Look for download links in the page
			HtmlNodeCollection links = htmlDoc.DocumentNode.SelectNodes("//a[contains(@href, '/download') or contains(@href, '.zip') or contains(@href, '.rar')]");
			if ( links != null && links.Count > 0 )
			{
				string href = links[0].GetAttributeValue("href", "");
				if ( !string.IsNullOrEmpty(href) )
				{
					Logger.LogVerbose($"[GameFront] Found download link: {href}");
					return new Uri(new Uri(baseUrl), href).AbsoluteUri;
				}
			}

			return null;
		}

		private static string GetFileNameFromResponse(HttpResponseMessage response, string url)
		{
			// Try Content-Disposition header first
			if ( response.Content.Headers.ContentDisposition?.FileName != null )
			{
				string fileName = response.Content.Headers.ContentDisposition.FileName.Trim('"');
				Logger.LogVerbose($"[GameFront] Got filename from Content-Disposition: {fileName}");
				return fileName;
			}

			// Fallback to URL
			var uri = new Uri(url);
			string pathFileName = Path.GetFileName(uri.AbsolutePath);
			if ( !string.IsNullOrEmpty(pathFileName) )
			{
				Logger.LogVerbose($"[GameFront] Got filename from URL path: {pathFileName}");
				return pathFileName;
			}

			// Last resort: use a generic name
			Logger.LogVerbose("[GameFront] Using generic filename: gamefront_download.zip");
			return "gamefront_download.zip";
		}
	}
}

