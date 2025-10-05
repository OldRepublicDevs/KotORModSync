using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class DeadlyStreamDownloadHandler : IDownloadHandler
	{
		private readonly HttpClient _httpClient;

		public DeadlyStreamDownloadHandler(HttpClient httpClient)
		{
			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			Logger.LogVerbose("[DeadlyStream] Initializing download handler");

			// Ensure proper headers are set for web scraping
			if ( !_httpClient.DefaultRequestHeaders.Contains("User-Agent") )
			{
				string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
				_httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
				Logger.LogVerbose($"[DeadlyStream] Added User-Agent header: {userAgent}");
			}
			else
			{
				Logger.LogVerbose($"[DeadlyStream] User-Agent header already present: {_httpClient.DefaultRequestHeaders.UserAgent}");
			}

			if ( !_httpClient.DefaultRequestHeaders.Contains("Accept") )
			{
				const string acceptHeader = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8";
				_httpClient.DefaultRequestHeaders.Add("Accept", acceptHeader);
				Logger.LogVerbose($"[DeadlyStream] Added Accept header: {acceptHeader}");
			}
			else
			{
				Logger.LogVerbose($"[DeadlyStream] Accept header already present: {string.Join(", ", _httpClient.DefaultRequestHeaders.Accept)}");
			}

			if ( !_httpClient.DefaultRequestHeaders.Contains("Accept-Language") )
			{
				string acceptLanguage = "en-US,en;q=0.9";
				_httpClient.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);
				Logger.LogVerbose($"[DeadlyStream] Added Accept-Language header: {acceptLanguage}");
			}
			else
			{
				Logger.LogVerbose($"[DeadlyStream] Accept-Language header already present: {string.Join(", ", _httpClient.DefaultRequestHeaders.AcceptLanguage)}");
			}

			Logger.LogVerbose("[DeadlyStream] Handler initialized with proper browser headers");
		}

		public bool CanHandle(string url)
		{
			bool canHandle = url != null && url.IndexOf("deadlystream.com", StringComparison.OrdinalIgnoreCase) >= 0;
			Logger.LogVerbose($"[DeadlyStream] CanHandle check for URL '{url}': {canHandle}");
			return canHandle;
		}

	public async Task<DownloadResult> DownloadAsync(string url, string destinationDirectory, IProgress<DownloadProgress> progress = null)
	{
		await Logger.LogVerboseAsync($"[DeadlyStream] Starting download from URL: {url}");
		await Logger.LogVerboseAsync($"[DeadlyStream] Destination directory: {destinationDirectory}");

		// Validate URL first
		if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri validatedUri) )
		{
			string errorMsg = $"Invalid URL format: {url}";
			await Logger.LogErrorAsync($"[DeadlyStream] {errorMsg}");
			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.Failed,
				ErrorMessage = $"Invalid URL: {url}",
				ProgressPercentage = 0,
				EndTime = DateTime.Now
			});
			return DownloadResult.Failed(errorMsg);
		}

		// Check if file already exists (use URL path for filename guess)
		string expectedFileName = Path.GetFileName(Uri.UnescapeDataString(validatedUri.AbsolutePath));
		if ( !string.IsNullOrEmpty(expectedFileName) && expectedFileName != "/" )
		{
			string potentialPath = Path.Combine(destinationDirectory, expectedFileName);
			if ( File.Exists(potentialPath) )
			{
				await Logger.LogVerboseAsync($"[DeadlyStream] File already exists, skipping download: {potentialPath}");
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Skipped,
					StatusMessage = "File already exists",
					FilePath = potentialPath,
					ProgressPercentage = 100,
					StartTime = DateTime.Now,
					EndTime = DateTime.Now
				});
				return DownloadResult.Skipped(potentialPath, "File already exists");
			}
		}

			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.InProgress,
				StatusMessage = "Fetching download page...",
				ProgressPercentage = 10
			});

			try
			{
				// Make the request with proper headers
				await Logger.LogVerboseAsync($"[DeadlyStream] Making HTTP GET request to: {url}");
				var request = new HttpRequestMessage(HttpMethod.Get, url);
				HttpResponseMessage pageResponse = await _httpClient.SendAsync(request).ConfigureAwait(false);

				await Logger.LogVerboseAsync($"[DeadlyStream] Received response with status code: {pageResponse.StatusCode}");
				await Logger.LogVerboseAsync($"[DeadlyStream] Response headers: {string.Join(", ", pageResponse.Headers)}");

				_ = pageResponse.EnsureSuccessStatusCode();
				string html = await pageResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[DeadlyStream] Downloaded HTML content, length: {html.Length} characters");

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Extracting download link...",
					ProgressPercentage = 30
				});

				// Extract the download link from the page
				await Logger.LogVerboseAsync("[DeadlyStream] Extracting download link from HTML content");
				string downloadLink = ExtractDownloadLink(html, url);
				if ( string.IsNullOrWhiteSpace(downloadLink) )
				{
					await Logger.LogWarningAsync("[DeadlyStream] Failed to extract download link from HTML content");
					// Save the HTML for debugging purposes
					string debugPath = Path.Combine(destinationDirectory, "deadlystream_debug.html");
					try
					{
						_ = Directory.CreateDirectory(destinationDirectory);
						File.WriteAllText(debugPath, html);
						await Logger.LogVerboseAsync($"[DeadlyStream] Debug HTML saved to: {debugPath}");
					}
					catch ( Exception debugEx )
					{
						await Logger.LogWarningAsync($"[DeadlyStream] Failed to save debug HTML: {debugEx.Message}");
					}

					string userMessage = "DeadlyStream download button could not be found on the page.\n\n" +
										 "This usually means:\n" +
										 "• The page layout has changed\n" +
										 "• The mod requires login to download\n" +
										 "• The file has been removed\n\n" +
										 $"Please try downloading manually from: {url}\n\n" +
										 $"Debug HTML saved to: {debugPath}";

					progress?.Report(new DownloadProgress
					{
						Status = DownloadStatus.Failed,
						ErrorMessage = userMessage,
						ProgressPercentage = 100,
						EndTime = DateTime.Now
					});

					pageResponse.Dispose();
					return DownloadResult.Failed(userMessage);
				}

				await Logger.LogVerboseAsync($"[DeadlyStream] Extracted download link: {downloadLink}");

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Downloading file...",
					ProgressPercentage = 50
				});

				// Download the actual file
				await Logger.LogVerboseAsync($"[DeadlyStream] Making HTTP GET request to download link: {downloadLink}");
				var fileRequest = new HttpRequestMessage(HttpMethod.Get, downloadLink);
				HttpResponseMessage fileResponse = await _httpClient.SendAsync(fileRequest).ConfigureAwait(false);

				await Logger.LogVerboseAsync($"[DeadlyStream] File response status code: {fileResponse.StatusCode}");
				await Logger.LogVerboseAsync($"[DeadlyStream] File response content type: {fileResponse.Content.Headers.ContentType}");
				await Logger.LogVerboseAsync($"[DeadlyStream] File response content length: {fileResponse.Content.Headers.ContentLength}");

				_ = fileResponse.EnsureSuccessStatusCode();

				string fileName = GetFileNameFromContentDisposition(fileResponse);
				if ( string.IsNullOrWhiteSpace(fileName) )
				{
					fileName = "deadlystream_download";
					await Logger.LogWarningAsync($"[DeadlyStream] Could not determine filename from Content-Disposition, using default: {fileName}");
				}
				else
				{
					await Logger.LogVerboseAsync($"[DeadlyStream] Determined filename from Content-Disposition: {fileName}");
				}

				_ = Directory.CreateDirectory(destinationDirectory);
				string filePath = Path.Combine(destinationDirectory, fileName);
				await Logger.LogVerboseAsync($"[DeadlyStream] Writing file to: {filePath}");

				long totalBytes = fileResponse.Content.Headers.ContentLength ?? 0;
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Writing file...",
					ProgressPercentage = 75,
					TotalBytes = totalBytes,
					StartTime = DateTime.Now
				});

				using ( FileStream fileStream = File.Create(filePath) )
				{
					await fileResponse.Content.CopyToAsync(fileStream).ConfigureAwait(false);
				}

				long fileSize = new FileInfo(filePath).Length;
				await Logger.LogVerboseAsync($"[DeadlyStream] File download completed successfully. File size: {fileSize} bytes");

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

				pageResponse.Dispose();
				fileResponse.Dispose();

				return DownloadResult.Succeeded(filePath, "Downloaded from DeadlyStream");
			}
			catch ( HttpRequestException httpEx )
			{
				await Logger.LogErrorAsync($"[DeadlyStream] HTTP request failed for URL '{url}': {httpEx.Message}");
				await Logger.LogExceptionAsync(httpEx);

				string userMessage = "DeadlyStream download failed. This can happen when:\n\n" +
									 "• The download page has changed its layout\n" +
									 "• The mod file has been removed or made private\n" +
									 "• The site is experiencing issues\n\n" +
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
				await Logger.LogErrorAsync($"[DeadlyStream] Request timeout for URL '{url}': {tcEx.Message}");
				await Logger.LogExceptionAsync(tcEx);

				string userMessage = "DeadlyStream download timed out. This can happen when:\n\n" +
									 "• The site is slow or experiencing high traffic\n" +
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
				await Logger.LogErrorAsync($"[DeadlyStream] Download failed for URL '{url}': {ex.Message}");
				await Logger.LogExceptionAsync(ex);

				string userMessage = "DeadlyStream download failed unexpectedly.\n\n" +
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

		private static string ExtractDownloadLink(string html, string baseUrl)
		{
			Logger.LogVerbose($"[DeadlyStream] ExtractDownloadLink called with HTML length: {html?.Length ?? 0}, baseUrl: {baseUrl}");

			if ( string.IsNullOrEmpty(html) )
			{
				Logger.LogWarning("[DeadlyStream] HTML content is null or empty, cannot extract download link");
				return null;
			}

			var document = new HtmlDocument();
			document.LoadHtml(html);
			Logger.LogVerbose("[DeadlyStream] HTML document loaded successfully");

			// Try multiple selectors to find the download button
			string[] selectors = new[]
			{
				// Primary selector: Full download button with all classes
				"//a[contains(@class,'ipsButton') and contains(@class,'ipsButton_fullWidth') and contains(@class,'ipsButton_large') and contains(@class,'ipsButton_important')]",
				// Alternative: Any link with "download" in the href
				"//a[contains(@href,'?do=download')]",
				// Alternative: Link with text "Download this file"
				"//a[contains(text(),'Download this file')]",
				// Alternative: Any prominent download button
				"//a[contains(@class,'ipsButton_important') and contains(@href,'download')]"
			};

			Logger.LogVerbose($"[DeadlyStream] Trying {selectors.Length} different XPath selectors to find download link");

			for ( int i = 0; i < selectors.Length; i++ )
			{
				string selector = selectors[i];
				Logger.LogVerbose($"[DeadlyStream] Trying selector {i + 1}/{selectors.Length}: {selector}");

				HtmlNodeCollection nodes = document.DocumentNode.SelectNodes(selector);
				if ( nodes != null && nodes.Count > 0 )
				{
					Logger.LogVerbose($"[DeadlyStream] Found {nodes.Count} matching nodes with selector {i + 1}");

					foreach ( HtmlNode node in nodes )
					{
						string href = node.GetAttributeValue("href", string.Empty);
						Logger.LogVerbose($"[DeadlyStream] Found href attribute: '{href}'");

						if ( string.IsNullOrWhiteSpace(href) )
						{
							Logger.LogVerbose("[DeadlyStream] Href is empty, skipping this node");
							continue;
						}

						// Handle relative URLs
						if ( !href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
							 !href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) )
						{
							Logger.LogVerbose($"[DeadlyStream] Converting relative URL '{href}' to absolute using base URL '{baseUrl}'");
							var baseUri = new Uri(baseUrl);
							var absoluteUri = new Uri(baseUri, href);
							href = absoluteUri.ToString();
							Logger.LogVerbose($"[DeadlyStream] Converted to absolute URL: '{href}'");
						}
						else
							Logger.LogVerbose($"[DeadlyStream] URL is already absolute: '{href}'");

						// Decode HTML entities (e.g., &amp; to &)
						string originalHref = href;
						href = WebUtility.HtmlDecode(href);
						if ( originalHref != href )
							Logger.LogVerbose($"[DeadlyStream] Decoded HTML entities: '{originalHref}' -> '{href}'");

						Logger.LogVerbose($"[DeadlyStream] Successfully extracted download link: '{href}'");
						return href;
					}
				}
				else
				{
					Logger.LogVerbose($"[DeadlyStream] No nodes found with selector {i + 1}");
				}
			}

			Logger.LogWarning("[DeadlyStream] No download link found with any of the selectors");
			return null;
		}

		private static string GetFileNameFromContentDisposition(HttpResponseMessage response)
		{
			Logger.LogVerbose("[DeadlyStream] GetFileNameFromContentDisposition called");

			if ( response == null || response.Content == null || response.Content.Headers.ContentDisposition == null )
			{
				Logger.LogVerbose("[DeadlyStream] Response, Content, or ContentDisposition is null, cannot extract filename");
				return null;
			}

			Logger.LogVerbose($"[DeadlyStream] ContentDisposition header: {response.Content.Headers.ContentDisposition}");

			string fileName = response.Content.Headers.ContentDisposition.FileNameStar;
			Logger.LogVerbose($"[DeadlyStream] FileNameStar: '{fileName}'");

			if ( string.IsNullOrWhiteSpace(fileName) )
			{
				fileName = response.Content.Headers.ContentDisposition.FileName;
				Logger.LogVerbose($"[DeadlyStream] FileName: '{fileName}'");
			}

			if ( string.IsNullOrWhiteSpace(fileName) )
			{
				Logger.LogVerbose("[DeadlyStream] No filename found in ContentDisposition header");
				return null;
			}

			string trimmed = Regex.Replace(fileName, pattern: "^\"|\"$", string.Empty);
			string unescaped = Uri.UnescapeDataString(trimmed);
			Logger.LogVerbose($"[DeadlyStream] Extracted filename: '{fileName}' -> '{trimmed}' -> '{unescaped}'");

			return unescaped;
		}
	}
}
