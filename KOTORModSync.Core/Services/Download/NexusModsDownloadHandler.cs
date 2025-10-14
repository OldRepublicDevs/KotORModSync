// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
				await Logger.LogVerboseAsync($"[NexusMods] Resolving filenames for URL: {url}");

				if ( string.IsNullOrWhiteSpace(_apiKey) )
				{
					await Logger.LogVerboseAsync("[NexusMods] No API key provided, cannot resolve filenames");
					return new List<string>();
				}

				// Use the API to get actual filenames
				var downloadLinks = await ResolveDownloadLinksAsync(url).ConfigureAwait(false);
				if ( downloadLinks != null && downloadLinks.Count > 0 )
				{
					var filenames = downloadLinks.Select(link => link.FileName).Where(name => !string.IsNullOrEmpty(name)).ToList();
					await Logger.LogVerboseAsync($"[NexusMods] Resolved {filenames.Count} filename(s) from API");
					return filenames;
				}

				await Logger.LogVerboseAsync("[NexusMods] No filenames could be resolved from API");
				return new List<string>();
			}
			catch ( Exception ex )
			{
				await Logger.LogWarningAsync($"[NexusMods] Failed to resolve filenames: {ex.Message}");
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
			await Logger.LogVerboseAsync("[NexusMods] Resolving download links from Nexus Mods API");
			List<NexusDownloadLink> linkInfos = await ResolveDownloadLinksAsync(url).ConfigureAwait(false);
			if ( linkInfos == null || linkInfos.Count == 0 )
			{
				await Logger.LogErrorAsync("[NexusMods] Failed to resolve download links from Nexus Mods API");
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = "Unable to resolve Nexus Mods download links. This could mean: no files found, mod requires manual download, or API error.",
					ProgressPercentage = 100,
					EndTime = DateTime.Now
				});
				return DownloadResult.Failed("Unable to resolve Nexus Mods download links.");
			}

			await Logger.LogVerboseAsync($"[NexusMods] Resolved {linkInfos.Count} download link(s)");

			// Download all files
			var downloadedFiles = new List<string>();
			for ( int i = 0; i < linkInfos.Count; i++ )
			{
				var linkInfo = linkInfos[i];
				await Logger.LogVerboseAsync($"[NexusMods] Downloading file {i + 1}/{linkInfos.Count}: {linkInfo.FileName}");
				await Logger.LogVerboseAsync($"[NexusMods] Download URL: {linkInfo.Url}");

				string fileName = string.IsNullOrEmpty(linkInfo.FileName) ? $"nexus_download_{linkInfo.FileId}" : linkInfo.FileName;

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = $"Downloading {fileName} ({i + 1}/{linkInfos.Count})...",
					ProgressPercentage = (i * 100) / linkInfos.Count
				});

				await Logger.LogVerboseAsync($"[NexusMods] Making HTTP GET request to download URL");
				var request = new HttpRequestMessage(HttpMethod.Get, linkInfo.Url);
				// Note: Download links from the API don't require the API key in the header
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

				HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[NexusMods] Received response with status code: {response.StatusCode}");

				_ = response.EnsureSuccessStatusCode();

				_ = Directory.CreateDirectory(destinationDirectory);
				string filePath = Path.Combine(destinationDirectory, fileName);
				await Logger.LogVerboseAsync($"[NexusMods] Writing file to: {filePath}");

				long totalBytes = response.Content.Headers.ContentLength ?? 0;

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

				downloadedFiles.Add(filePath);

				request.Dispose();
				response.Dispose();
			}

			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.Completed,
				StatusMessage = $"Downloaded {downloadedFiles.Count} file(s) from Nexus Mods",
				ProgressPercentage = 100,
				FilePath = downloadedFiles.Count > 0 ? downloadedFiles[0] : null,
				EndTime = DateTime.Now
			});

			string resultMessage = $"Downloaded {downloadedFiles.Count} file(s) from Nexus Mods: {string.Join(", ", downloadedFiles.Select(Path.GetFileName))}";
			return DownloadResult.Succeeded(downloadedFiles.Count > 0 ? downloadedFiles[0] : null, resultMessage);
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

		private async Task<List<NexusDownloadLink>> ResolveDownloadLinksAsync(string url)
		{
			await Logger.LogVerboseAsync($"[NexusMods] ResolveDownloadLinksAsync called with URL: {url}");

			// Parse URL to extract game domain and mod ID
			// URL format: https://www.nexusmods.com/{game}/mods/{modId} or https://www.nexusmods.com/{game}/mods/{modId}?tab=files
			var match = System.Text.RegularExpressions.Regex.Match(url, @"nexusmods\.com/([^/]+)/mods/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			if ( !match.Success )
			{
				await Logger.LogErrorAsync($"[NexusMods] Failed to parse Nexus Mods URL: {url}");
				return new List<NexusDownloadLink>();
			}

			string gameDomain = match.Groups[1].Value;
			string modId = match.Groups[2].Value;

			await Logger.LogVerboseAsync($"[NexusMods] Parsed URL - Game: {gameDomain}, Mod ID: {modId}");

			try
			{
				// Get list of files for this mod
				string filesUrl = $"https://api.nexusmods.com/v1/games/{gameDomain}/mods/{modId}/files.json";
				await Logger.LogVerboseAsync($"[NexusMods] Fetching file list from: {filesUrl}");

				var request = new HttpRequestMessage(HttpMethod.Get, filesUrl);
				request.Headers.Add("apikey", _apiKey);

				HttpResponseMessage response = await _httpClient.SendAsync(request).ConfigureAwait(false);
				response.EnsureSuccessStatusCode();

				string jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[NexusMods] Received file list response, length: {jsonResponse.Length}");

				// Parse JSON to get file list
				var filesData = Newtonsoft.Json.Linq.JObject.Parse(jsonResponse);
				var files = filesData["files"]?.ToObject<List<Newtonsoft.Json.Linq.JObject>>();

				if ( files == null || files.Count == 0 )
				{
					await Logger.LogWarningAsync("[NexusMods] No files found for this mod");
					return new List<NexusDownloadLink>();
				}

				await Logger.LogVerboseAsync($"[NexusMods] Found {files.Count} files for mod");

				// Get download links for all files (or just main files)
				var downloadLinks = new List<NexusDownloadLink>();

				foreach ( var file in files )
				{
					var fileIdToken = file["file_id"];
					var fileNameToken = file["file_name"];
					var categoryNameToken = file["category_name"];

					int fileId = fileIdToken != null ? (int)fileIdToken : 0;
					string fileName = ((string)fileNameToken) ?? "unknown";
					string categoryName = ((string)categoryNameToken) ?? "";

					// Skip optional files, only download main/update files
					if ( categoryName.Equals("OPTIONAL", StringComparison.OrdinalIgnoreCase) )
					{
						await Logger.LogVerboseAsync($"[NexusMods] Skipping optional file: {fileName}");
						continue;
					}

					await Logger.LogVerboseAsync($"[NexusMods] Getting download link for file: {fileName} (ID: {fileId})");

					try
					{
						// Get download URLs for this file
						string downloadLinkUrl = $"https://api.nexusmods.com/v1/games/{gameDomain}/mods/{modId}/files/{fileId}/download_link.json";
						var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadLinkUrl);
						downloadRequest.Headers.Add("apikey", _apiKey);

						HttpResponseMessage downloadResponse = await _httpClient.SendAsync(downloadRequest).ConfigureAwait(false);
						downloadResponse.EnsureSuccessStatusCode();

						string downloadJson = await downloadResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
						var downloadData = Newtonsoft.Json.Linq.JArray.Parse(downloadJson);

						if ( downloadData.Count > 0 )
						{
							// Use the first download server
							var uriToken = downloadData[0]["URI"];
							string downloadUrl = (string)uriToken;
							if ( !string.IsNullOrEmpty(downloadUrl) )
							{
								downloadLinks.Add(new NexusDownloadLink
								{
									Url = downloadUrl,
									FileName = fileName,
									FileId = fileId
								});
								await Logger.LogVerboseAsync($"[NexusMods] Added download link for: {fileName}");
							}
						}

						downloadRequest.Dispose();
						downloadResponse.Dispose();
					}
					catch ( HttpRequestException httpEx )
					{
						await Logger.LogWarningAsync($"[NexusMods] Failed to get download link for '{fileName}': {httpEx.Message}");
						continue;
					}
					catch ( Exception ex )
					{
						await Logger.LogWarningAsync($"[NexusMods] Error getting download link for '{fileName}': {ex.Message}");
						continue;
					}
				}

				request.Dispose();
				response.Dispose();

				await Logger.LogVerboseAsync($"[NexusMods] Resolved {downloadLinks.Count} download links");
				return downloadLinks;
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, $"[NexusMods] Failed to resolve download links for URL: {url}");
				return new List<NexusDownloadLink>();
			}
		}

		private sealed class NexusDownloadLink
		{
			public string Url { get; set; } = string.Empty;
			public string FileName { get; set; } = string.Empty;
			public int FileId { get; set; }
		}

		/// <summary>
		/// Validates a Nexus Mods API key by making test API calls
		/// </summary>
		public static async Task<(bool IsValid, string Message)> ValidateApiKeyAsync(string apiKey)
		{
			if ( string.IsNullOrWhiteSpace(apiKey) )
			{
				return (false, "API key cannot be empty");
			}

			try
			{
				using ( var httpClient = new HttpClient() )
				{
					// Test 1: Validate the API key format
					await Logger.LogAsync("[NexusMods] Validating API key format...");
					if ( apiKey.Length < 20 )
					{
						return (false, "API key appears to be too short. Nexus Mods API keys are typically longer.");
					}

					// Test 2: Try to authenticate with the API
					await Logger.LogAsync("[NexusMods] Testing API key authentication...");
					var request = new HttpRequestMessage(HttpMethod.Get, "https://api.nexusmods.com/v1/users/validate.json");
					request.Headers.Add("apikey", apiKey);
					request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

					HttpResponseMessage response = await httpClient.SendAsync(request);

					if ( response.StatusCode == System.Net.HttpStatusCode.Unauthorized )
					{
						return (false, "API key is invalid or unauthorized. Please check your key and try again.");
					}

					if ( response.StatusCode == System.Net.HttpStatusCode.Forbidden )
					{
						return (false, "API key is forbidden from accessing the API. Please check your Nexus Mods account permissions.");
					}

					if ( !response.IsSuccessStatusCode )
					{
						return (false, $"API validation failed with status code: {response.StatusCode}. Message: {await response.Content.ReadAsStringAsync()}");
					}

					// Read user info to confirm
					string content = await response.Content.ReadAsStringAsync();
					await Logger.LogVerboseAsync($"[NexusMods] API validation response: {content}");

					// Test 3: Try to get user info
					await Logger.LogAsync("[NexusMods] Retrieving user information...");
					if ( content.Contains("user_id") || content.Contains("name") )
					{
						await Logger.LogAsync("[NexusMods] ✓ API key validated successfully!");
						await Logger.LogAsync("[NexusMods] ✓ User authentication confirmed");
						return (true, "API key is valid and working correctly!");
					}

					return (false, "API key validation returned unexpected response format.");
				}
			}
			catch ( HttpRequestException ex )
			{
				await Logger.LogExceptionAsync(ex);
				return (false, $"Network error during API validation: {ex.Message}");
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex);
				return (false, $"Unexpected error during API validation: {ex.Message}");
			}
		}
	}
}
