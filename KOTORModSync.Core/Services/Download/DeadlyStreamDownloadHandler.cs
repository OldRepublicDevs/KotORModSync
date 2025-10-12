using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class DeadlyStreamDownloadHandler : IDownloadHandler
	{
		private readonly HttpClient _httpClient;
		private const long MaxBytesPerSecond = 700 * 1024; // 700 KB/s in bytes

		// Cookie container to maintain session across requests
		private readonly System.Net.CookieContainer _cookieContainer;

		public DeadlyStreamDownloadHandler(HttpClient httpClient)
		{
			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			_cookieContainer = new System.Net.CookieContainer();
			Logger.LogVerbose("[DeadlyStream] Initializing download handler with session cookie management");

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

			// Add custom identification headers (non-standard, ignored by servers)
			_httpClient.DefaultRequestHeaders.Add("X-KOTORModSync-App", "Installer/1.0");
			_httpClient.DefaultRequestHeaders.Add("X-KOTORModSync-Repo", "https://github.com/KOTORModSync/KOTORModSync");
			_httpClient.DefaultRequestHeaders.Add("X-Accept-KOTORModSync", "true");
			Logger.LogVerbose("[DeadlyStream] Added custom identification headers: X-KOTORModSync-App, X-KOTORModSync-Repo, X-Accept-KOTORModSync");

			// Add static bearer token (public, non-secret)
			_httpClient.DefaultRequestHeaders.Authorization =
				new AuthenticationHeaderValue("Bearer", "KOTOR_MODSYNC_PUBLIC");
			Logger.LogVerbose("[DeadlyStream] Added identification bearer token: KOTOR_MODSYNC_PUBLIC");

			Logger.LogVerbose("[DeadlyStream] Handler initialized with proper browser headers and identification markers");
			Logger.LogVerbose($"[DeadlyStream] Bandwidth throttling enabled: {MaxBytesPerSecond / 1024} KB/s using ThrottledStream");
		}

		public bool CanHandle(string url)
		{
			bool canHandle = url != null && url.IndexOf("deadlystream.com", StringComparison.OrdinalIgnoreCase) >= 0;
			Logger.LogVerbose($"[DeadlyStream] CanHandle check for URL '{url}': {canHandle}");
			return canHandle;
		}

		/// <summary>
		/// Extracts cookies from HTTP response and stores them in the cookie container
		/// </summary>
		private void ExtractAndStoreCookies(HttpResponseMessage response, Uri uri)
		{
			try
			{
				// Extract Set-Cookie headers
				if ( response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders) )
				{
					foreach ( string cookieHeader in cookieHeaders )
					{
						try
						{
							// Parse the cookie header
							_cookieContainer.SetCookies(uri, cookieHeader);
							Logger.LogVerbose($"[DeadlyStream] Stored cookie from response: {cookieHeader.Substring(0, Math.Min(50, cookieHeader.Length))}...");
						}
						catch ( Exception ex )
						{
							Logger.LogWarning($"[DeadlyStream] Failed to parse cookie: {ex.Message}");
						}
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"[DeadlyStream] Failed to extract cookies: {ex.Message}");
			}
		}

		/// <summary>
		/// Applies stored cookies to an HTTP request
		/// </summary>
		private void ApplyCookiesToRequest(HttpRequestMessage request, Uri uri)
		{
			try
			{
				string cookieHeader = _cookieContainer.GetCookieHeader(uri);
				if ( !string.IsNullOrEmpty(cookieHeader) )
				{
					request.Headers.Add("Cookie", cookieHeader);
					// Cookie applied (verbose logging disabled to reduce log clutter)
				}
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"[DeadlyStream] Failed to apply cookies: {ex.Message}");
			}
		}

		public async Task<DownloadResult> DownloadAsync(string url, string destinationDirectory, IProgress<DownloadProgress> progress = null, CancellationToken cancellationToken = default)
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

			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.InProgress,
				StatusMessage = "Extracting download links...",
				ProgressPercentage = 10
			});

            try
			{
				// Fetch the page and extract cookies
				await Logger.LogVerboseAsync($"[DeadlyStream] Fetching page to establish session: {url}");
                // Normalize to HTTPS – DeadlyStream rejects plain HTTP with 403
                if (validatedUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
                {
                    validatedUri = new UriBuilder(validatedUri) { Scheme = "https", Port = -1 }.Uri;
                    url = validatedUri.ToString();
                    await Logger.LogVerboseAsync($"[DeadlyStream] Normalized URL to HTTPS: {url}");
                }

                var request = new HttpRequestMessage(HttpMethod.Get, url);

				// Apply any existing cookies from previous requests
				ApplyCookiesToRequest(request, validatedUri);

				HttpResponseMessage pageResponse = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
				_ = pageResponse.EnsureSuccessStatusCode();

				// Extract and store cookies from the response
				ExtractAndStoreCookies(pageResponse, validatedUri);

				string html = await pageResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[DeadlyStream] Downloaded HTML content, length: {html.Length} characters");

				// Extract csrfKey from the initial page
				string csrfKey = ExtractCsrfKey(html);
				if ( string.IsNullOrEmpty(csrfKey) )
				{
					await Logger.LogWarningAsync("[DeadlyStream] Could not extract csrfKey from page, downloads may fail");
				}
				else
				{
					await Logger.LogVerboseAsync($"[DeadlyStream] Extracted csrfKey: {csrfKey.Substring(0, Math.Min(8, csrfKey.Length))}...");
				}

				// Check if this is a multi-file download page by trying the download URL with csrfKey
				string downloadPageUrl = !string.IsNullOrEmpty(csrfKey)
					? $"{url}?do=download&csrfKey={csrfKey}"
					: $"{url}?do=download";

				await Logger.LogVerboseAsync($"[DeadlyStream] Checking for multi-file download at: {downloadPageUrl}");
				var downloadPageRequest = new HttpRequestMessage(HttpMethod.Get, downloadPageUrl);
				ApplyCookiesToRequest(downloadPageRequest, validatedUri);

				HttpResponseMessage downloadPageResponse = await _httpClient.SendAsync(downloadPageRequest, cancellationToken).ConfigureAwait(false);

				if ( downloadPageResponse.IsSuccessStatusCode )
				{
					string downloadPageHtml = await downloadPageResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
					ExtractAndStoreCookies(downloadPageResponse, validatedUri);

					// Check if this is a multi-file selection page
					if ( downloadPageHtml.Contains("Download your files") || downloadPageHtml.Contains("data-action=\"download\"") )
					{
						await Logger.LogVerboseAsync("[DeadlyStream] Detected multi-file selection page");
						List<string> confirmedLinks = ExtractConfirmedDownloadLinks(downloadPageHtml, url);

						if ( confirmedLinks != null && confirmedLinks.Count > 0 )
						{
							await Logger.LogVerboseAsync($"[DeadlyStream] Found {confirmedLinks.Count} files to download");
							downloadPageResponse.Dispose();
							pageResponse.Dispose();

							// Download all files
							var multiFileDownloads = new List<string>();
							int multiFileIndex = 0;

							foreach ( string downloadLink in confirmedLinks )
							{
								multiFileIndex++;
								double multiFileBaseProgress = 30 + (multiFileIndex - 1) * (60.0 / confirmedLinks.Count);
								double multiFileProgressRange = 60.0 / confirmedLinks.Count;

								progress?.Report(new DownloadProgress
								{
									Status = DownloadStatus.InProgress,
									StatusMessage = $"Downloading file {multiFileIndex} of {confirmedLinks.Count}...",
									ProgressPercentage = multiFileBaseProgress
								});

								string filePath = await DownloadSingleFile(downloadLink, destinationDirectory, progress, multiFileBaseProgress, multiFileProgressRange, cancellationToken);
								if ( !string.IsNullOrEmpty(filePath) )
								{
									multiFileDownloads.Add(filePath);
								}
							}

							if ( multiFileDownloads.Count == 0 )
							{
								string errorMsg = "Failed to download any files from multi-file selection page";
								progress?.Report(new DownloadProgress
								{
									Status = DownloadStatus.Failed,
									ErrorMessage = errorMsg,
									ProgressPercentage = 100,
									EndTime = DateTime.Now
								});
								return DownloadResult.Failed(errorMsg);
							}

							// Report completion
							string multiFileResultMessage = multiFileDownloads.Count == 1
								? "Downloaded from DeadlyStream"
								: $"Downloaded {multiFileDownloads.Count} files from DeadlyStream";

							progress?.Report(new DownloadProgress
							{
								Status = DownloadStatus.Completed,
								StatusMessage = multiFileResultMessage,
								ProgressPercentage = 100,
								FilePath = multiFileDownloads[0],
								EndTime = DateTime.Now
							});

							return DownloadResult.Succeeded(multiFileDownloads[0], multiFileResultMessage);
						}
					}

					downloadPageResponse.Dispose();
				}
				else
				{
					await Logger.LogVerboseAsync($"[DeadlyStream] Download page request returned {downloadPageResponse.StatusCode}, trying direct download");
					downloadPageResponse.Dispose();
				}

				pageResponse.Dispose();

				// Fallback to extracting links from the main page
				List<string> downloadLinks = ExtractAllDownloadLinks(html, url);

				if ( downloadLinks == null || downloadLinks.Count == 0 )
				{
					string debugPath = Path.Combine(destinationDirectory, "deadlystream_debug.html");
					try
					{
						Directory.CreateDirectory(destinationDirectory);
						File.WriteAllText(debugPath, html);
						await Logger.LogVerboseAsync($"[DeadlyStream] Debug HTML saved to: {debugPath}");
					}
					catch ( Exception debugEx )
					{
						await Logger.LogWarningAsync($"[DeadlyStream] Failed to save debug HTML: {debugEx.Message}");
					}

					string userMessage = "DeadlyStream download link could not be extracted.\n\n" +
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

				pageResponse.Dispose();

				// Download all files
				var downloadedFiles = new List<string>();
				int fileIndex = 0;

				foreach ( string downloadLink in downloadLinks )
				{
					fileIndex++;
					double baseProgress = 30 + (fileIndex - 1) * (60.0 / downloadLinks.Count);
					double fileProgressRange = 60.0 / downloadLinks.Count;

					progress?.Report(new DownloadProgress
					{
						Status = DownloadStatus.InProgress,
						StatusMessage = $"Downloading file {fileIndex} of {downloadLinks.Count}...",
						ProgressPercentage = baseProgress
					});

					string filePath = await DownloadSingleFile(downloadLink, destinationDirectory, progress, baseProgress, fileProgressRange, cancellationToken);
					if ( !string.IsNullOrEmpty(filePath) )
					{
						downloadedFiles.Add(filePath);
					}
				}

				if ( downloadedFiles.Count == 0 )
				{
					string errorMsg = "Failed to download any files";
					progress?.Report(new DownloadProgress
					{
						Status = DownloadStatus.Failed,
						ErrorMessage = errorMsg,
						ProgressPercentage = 100,
						EndTime = DateTime.Now
					});
					return DownloadResult.Failed(errorMsg);
				}

				// Report completion
				string resultMessage = downloadedFiles.Count == 1
					? "Downloaded from DeadlyStream"
					: $"Downloaded {downloadedFiles.Count} files from DeadlyStream";

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Completed,
					StatusMessage = resultMessage,
					ProgressPercentage = 100,
					FilePath = downloadedFiles[0], // Primary file
					EndTime = DateTime.Now
				});

				return DownloadResult.Succeeded(downloadedFiles[0], resultMessage);
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

		/// <summary>
		/// Downloads a single file from a direct download link
		/// </summary>
		private async Task<string> DownloadSingleFile(
			string downloadLink,
			string destinationDirectory,
			IProgress<DownloadProgress> progress,
			double baseProgress,
			double progressRange,
			CancellationToken cancellationToken = default)
		{
			try
			{
				// Check for cancellation before starting download
				cancellationToken.ThrowIfCancellationRequested();

				await Logger.LogVerboseAsync($"[DeadlyStream] Downloading from: {downloadLink}");

				// Make download request with session cookies
				Uri downloadUri = new Uri(downloadLink);
				var fileRequest = new HttpRequestMessage(HttpMethod.Get, downloadLink);
				ApplyCookiesToRequest(fileRequest, downloadUri);
				HttpResponseMessage fileResponse = await _httpClient.SendAsync(fileRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

				// Only log non-success status codes
				if ( !fileResponse.IsSuccessStatusCode )
					await Logger.LogErrorAsync($"[DeadlyStream] File response status: {fileResponse.StatusCode}");

				_ = fileResponse.EnsureSuccessStatusCode();

				// Check if we got an HTML file selection page instead of the actual file
				string contentType = fileResponse.Content.Headers.ContentType?.MediaType ?? string.Empty;
				if ( contentType.Contains("text/html") )
				{
					await Logger.LogVerboseAsync("[DeadlyStream] Received HTML response - this appears to be a file selection page");
					string html = await fileResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
					fileResponse.Dispose();

					// Check if this is the multi-file selection page
					if ( html.Contains("Download your files") || html.Contains("data-action=\"download\"") )
					{
						await Logger.LogVerboseAsync("[DeadlyStream] Detected multi-file selection page, extracting actual download links");
						List<string> actualDownloadLinks = ExtractConfirmedDownloadLinks(html, downloadLink);

						if ( actualDownloadLinks.Count > 0 )
						{
							await Logger.LogVerboseAsync($"[DeadlyStream] Found {actualDownloadLinks.Count} confirmed download link(s) on selection page");

							// Download all files from the selection page
							string lastDownloadedFile = null;
							for ( int i = 0; i < actualDownloadLinks.Count; i++ )
							{
								double fileBaseProgress = baseProgress + (i * progressRange / actualDownloadLinks.Count);
								double fileProgressRange = progressRange / actualDownloadLinks.Count;

								progress?.Report(new DownloadProgress
								{
									Status = DownloadStatus.InProgress,
									StatusMessage = $"Downloading file {i + 1} of {actualDownloadLinks.Count} from selection page...",
									ProgressPercentage = fileBaseProgress
								});

								string downloadedFile = await DownloadSingleFile(actualDownloadLinks[i], destinationDirectory, progress, fileBaseProgress, fileProgressRange, cancellationToken);
								if ( !string.IsNullOrEmpty(downloadedFile) )
								{
									lastDownloadedFile = downloadedFile;
								}
							}

							return lastDownloadedFile;
						}
						else
						{
							await Logger.LogWarningAsync("[DeadlyStream] Multi-file selection page detected but no download links found");
						}
					}

					// If we couldn't extract links or it's not a selection page, this is an error
					string errorMsg = "Received HTML instead of file - download link may be invalid or require authentication";
					await Logger.LogErrorAsync($"[DeadlyStream] {errorMsg}");
					fileResponse.Dispose();
					return null;
				}

				// Get filename from Content-Disposition header
				string fileName = GetFileNameFromContentDisposition(fileResponse);
				if ( string.IsNullOrWhiteSpace(fileName) )
				{
					// Try to extract from URL
					fileName = Path.GetFileName(Uri.UnescapeDataString(downloadUri.AbsolutePath));
					if ( string.IsNullOrWhiteSpace(fileName) || fileName.Contains("?") )
					{
						fileName = $"deadlystream_download_{Guid.NewGuid():N}.zip";
					}
					await Logger.LogWarningAsync($"[DeadlyStream] Could not determine filename, using: {fileName}");
				}
				else
				{
					await Logger.LogVerboseAsync($"[DeadlyStream] Filename: {fileName}");
				}

				// Prepare to download the file
				_ = Directory.CreateDirectory(destinationDirectory);
				string filePath = Path.Combine(destinationDirectory, fileName);

				long totalBytes = fileResponse.Content.Headers.ContentLength ?? 0;
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = $"Downloading {fileName}...",
					ProgressPercentage = baseProgress + (progressRange * 0.1),
					TotalBytes = totalBytes
				});

				// Download with bandwidth throttling using ThrottledStream (700 KB/s cap)
				using ( FileStream fileStream = File.Create(filePath) )
				using ( Stream contentStream = await fileResponse.Content.ReadAsStreamAsync().ConfigureAwait(false) )
				using ( ThrottledStream throttledStream = new ThrottledStream(contentStream, MaxBytesPerSecond) )
				{
					byte[] buffer = new byte[8192]; // Standard buffer size
					int bytesRead;
					long totalBytesRead = 0;
					var lastProgressUpdate = DateTimeOffset.UtcNow;

					while ( (bytesRead = await throttledStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0 )
					{
						// Check for cancellation IMMEDIATELY before writing
						cancellationToken.ThrowIfCancellationRequested();

						await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
						totalBytesRead += bytesRead;

						// Update progress (but not too frequently to avoid UI spam)
						var now = DateTimeOffset.UtcNow;
						if ( totalBytes > 0 && (now - lastProgressUpdate).TotalMilliseconds >= 250 )
						{
							lastProgressUpdate = now;
							double fileProgress = (double)totalBytesRead / totalBytes;
							double currentProgress = baseProgress + (progressRange * 0.1) + (progressRange * 0.9 * fileProgress);
							progress?.Report(new DownloadProgress
							{
								Status = DownloadStatus.InProgress,
								StatusMessage = $"Downloading {fileName}... ({totalBytesRead:N0} / {totalBytes:N0} bytes)",
								ProgressPercentage = Math.Min(currentProgress, baseProgress + progressRange),
								BytesDownloaded = totalBytesRead,
								TotalBytes = totalBytes
							});
						}
					}

					// Ensure all data is written to disk
					await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
				}

				long fileSize = new FileInfo(filePath).Length;
				await Logger.LogVerboseAsync($"[DeadlyStream] File downloaded successfully: {filePath} ({fileSize} bytes)");

				fileResponse.Dispose();
				return filePath;
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"[DeadlyStream] Failed to download file from {downloadLink}: {ex.Message}");
				await Logger.LogExceptionAsync(ex);
				return null;
			}
		}

		/// <summary>
		/// Extracts the csrfKey from the page HTML (either from JavaScript or from links)
		/// </summary>
		private static string ExtractCsrfKey(string html)
		{
			if ( string.IsNullOrEmpty(html) )
				return null;

			// Try to extract from JavaScript: ipsSettings.csrfKey
			var jsMatch = Regex.Match(html, @"csrfKey:\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
			if ( jsMatch.Success )
			{
				Logger.LogVerbose($"[DeadlyStream] Extracted csrfKey from JavaScript: {jsMatch.Groups[1].Value}");
				return jsMatch.Groups[1].Value;
			}

			// Fallback: extract from any link with csrfKey parameter
			var linkMatch = Regex.Match(html, @"csrfKey=([^&""'<>\s]+)", RegexOptions.IgnoreCase);
			if ( linkMatch.Success )
			{
				Logger.LogVerbose($"[DeadlyStream] Extracted csrfKey from link: {linkMatch.Groups[1].Value}");
				return linkMatch.Groups[1].Value;
			}

			Logger.LogWarning("[DeadlyStream] Could not extract csrfKey from page");
			return null;
		}

		/// <summary>
		/// Extracts confirmed download links from a file selection page
		/// (handles the intermediate "Download your files" page with multiple file options)
		/// </summary>
		private static List<string> ExtractConfirmedDownloadLinks(string html, string baseUrl)
		{
			Logger.LogVerbose($"[DeadlyStream] ExtractConfirmedDownloadLinks called with HTML length: {html?.Length ?? 0}, baseUrl: {baseUrl}");

			if ( string.IsNullOrEmpty(html) )
			{
				Logger.LogWarning("[DeadlyStream] HTML content is null or empty");
				return new List<string>();
			}

			var document = new HtmlDocument();
			document.LoadHtml(html);
			Logger.LogVerbose("[DeadlyStream] HTML document loaded successfully");

			// Find all links with data-action="download" (these are the actual file download buttons)
			// Also look for links with &confirm=1 parameter as a fallback
			string[] selectors = new[]
			{
				"//a[@data-action='download' and contains(@href,'?do=download')]",
				"//a[contains(@href,'?do=download') and contains(@href,'&confirm=1')]",
				"//a[contains(@class,'ipsButton') and contains(@href,'?do=download') and contains(@href,'&r=')]"
			};

			var downloadLinks = new List<string>();

			foreach ( string selector in selectors )
			{
				Logger.LogVerbose($"[DeadlyStream] Trying selector: {selector}");
				HtmlNodeCollection nodes = document.DocumentNode.SelectNodes(selector);

				if ( nodes != null && nodes.Count > 0 )
				{
					Logger.LogVerbose($"[DeadlyStream] Found {nodes.Count} matching nodes");

					foreach ( HtmlNode node in nodes )
					{
						string href = node.GetAttributeValue("href", string.Empty);
						if ( string.IsNullOrWhiteSpace(href) )
							continue;

						// Handle relative URLs
						if ( !href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
							!href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) )
						{
							var baseUri = new Uri(baseUrl);
							var absoluteUri = new Uri(baseUri, href);
							href = absoluteUri.ToString();
						}

						// Decode HTML entities
						href = WebUtility.HtmlDecode(href);

						// Add unique links only
						if ( !downloadLinks.Contains(href) )
						{
							downloadLinks.Add(href);
							Logger.LogVerbose($"[DeadlyStream] Added confirmed download link #{downloadLinks.Count}: {href}");
						}
					}
				}

				// If we found links with this selector, stop searching
				if ( downloadLinks.Count > 0 )
					break;
			}

			if ( downloadLinks.Count > 0 )
			{
				Logger.LogVerbose($"[DeadlyStream] Successfully extracted {downloadLinks.Count} confirmed download link(s) from selection page");
			}
			else
			{
				Logger.LogWarning("[DeadlyStream] No confirmed download links found in selection page HTML");
			}

			return downloadLinks;
		}

		/// <summary>
		/// Extracts ALL download links from the page (supports multi-file downloads)
		/// </summary>
		private static List<string> ExtractAllDownloadLinks(string html, string baseUrl)
		{
			Logger.LogVerbose($"[DeadlyStream] ExtractAllDownloadLinks called with HTML length: {html?.Length ?? 0}, baseUrl: {baseUrl}");

			if ( string.IsNullOrEmpty(html) )
			{
				Logger.LogWarning("[DeadlyStream] HTML content is null or empty");
				return new List<string>();
			}

			var document = new HtmlDocument();
			document.LoadHtml(html);
			Logger.LogVerbose("[DeadlyStream] HTML document loaded successfully");

			// Find ALL links with ?do=download parameter
			string selector = "//a[contains(@href,'?do=download')]";
			Logger.LogVerbose($"[DeadlyStream] Using XPath selector: {selector}");

			HtmlNodeCollection nodes = document.DocumentNode.SelectNodes(selector);
			if ( nodes == null || nodes.Count == 0 )
			{
				Logger.LogWarning("[DeadlyStream] No download links found");
				return new List<string>();
			}

			Logger.LogVerbose($"[DeadlyStream] Found {nodes.Count} potential download links");

			var downloadLinks = new List<string>();
			foreach ( HtmlNode node in nodes )
			{
				string href = node.GetAttributeValue("href", string.Empty);
				if ( string.IsNullOrWhiteSpace(href) )
					continue;

				// Handle relative URLs
				if ( !href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
					!href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) )
				{
					var baseUri = new Uri(baseUrl);
					var absoluteUri = new Uri(baseUri, href);
					href = absoluteUri.ToString();
				}

				// Decode HTML entities
				href = WebUtility.HtmlDecode(href);

				// Add unique links only
				if ( !downloadLinks.Contains(href) )
				{
					downloadLinks.Add(href);
					Logger.LogVerbose($"[DeadlyStream] Added download link #{downloadLinks.Count}: {href}");
				}
			}

			if ( downloadLinks.Count > 1 )
			{
				Logger.LogVerbose($"[DeadlyStream] Multi-file download detected - {downloadLinks.Count} files will be downloaded");
			}

			return downloadLinks;
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

			// Try multiple selectors to find the download button/link
			// Updated 2025-10 to match current DeadlyStream page structure
			string[] selectors = new[]
			{
				// Primary selector: Current structure - "Download this file" link with ?do=download
				"//a[contains(text(),'Download this file') and contains(@href,'?do=download')]",
				// Alternative: Any link with ?do=download parameter (catches all download links)
				"//a[contains(@href,'?do=download')]",
				// Alternative: Legacy button structure (kept for backwards compatibility)
				"//a[contains(@class,'ipsButton') and contains(@class,'ipsButton_fullWidth') and contains(@class,'ipsButton_large') and contains(@class,'ipsButton_important')]",
				// Alternative: Any prominent download button
				"//a[contains(@class,'ipsButton_important') and contains(@href,'download')]"
			};

			Logger.LogVerbose($"[DeadlyStream] Trying {selectors.Length} different XPath selectors to find download link(s)");

			// Track all found download links
			var allDownloadLinks = new System.Collections.Generic.List<string>();

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

						// Add to list of all found links
						if ( !allDownloadLinks.Contains(href) )
						{
							allDownloadLinks.Add(href);
							Logger.LogVerbose($"[DeadlyStream] Added download link #{allDownloadLinks.Count}: '{href}'");
						}
					}

					// If we found links with this selector, use the first one
					if ( allDownloadLinks.Count > 0 )
					{
						if ( allDownloadLinks.Count > 1 )
						{
							Logger.LogVerbose($"[DeadlyStream] Found {allDownloadLinks.Count} download links on this page:");
							for ( int j = 0; j < allDownloadLinks.Count; j++ )
							{
								Logger.LogVerbose($"[DeadlyStream]   Link {j + 1}: {allDownloadLinks[j]}");
							}
							Logger.LogVerbose("[DeadlyStream] NOTE: Multiple files available - currently downloading primary file only");
							Logger.LogVerbose("[DeadlyStream] To download all files, add each as a separate ModLink in the component");
						}

						string primaryLink = allDownloadLinks[0];
						Logger.LogVerbose($"[DeadlyStream] Successfully extracted primary download link: '{primaryLink}'");
						return primaryLink;
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
