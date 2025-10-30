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

using Newtonsoft.Json.Linq;

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

			ValidateApiKeyCompliance();

			Logger.LogVerbose("[NexusMods] Initializing Nexus Mods download handler");
			Logger.LogVerbose($"[NexusMods] API key provided: {!string.IsNullOrWhiteSpace(_apiKey)}");

			SetDefaultHeaders();

			Logger.LogVerbose("[NexusMods] Handler initialized with Nexus Mods API compliance");
		}

		private void SetDefaultHeaders()
		{
			if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
			{
				const string userAgent = "KOTORModSync/2.0.0 (https://github.com/th3w1zard1/KOTORModSync)";
				_httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
				Logger.LogVerbose($"[NexusMods] Added User-Agent header: {userAgent}");
			}

			if (!_httpClient.DefaultRequestHeaders.Contains("Accept"))
			{
				const string acceptHeader = "application/json";
				_httpClient.DefaultRequestHeaders.Add("Accept", acceptHeader);
				Logger.LogVerbose($"[NexusMods] Added Accept header: {acceptHeader}");
			}
		}

		private void ValidateApiKeyCompliance()
		{

			if (string.IsNullOrWhiteSpace(_apiKey))
			{
				Logger.LogWarning("[NexusMods] No API key provided. Nexus Mods downloads will be limited.");
				Logger.LogWarning("[NexusMods] For full functionality, please configure a Nexus Mods API key.");
			}
			else
			{
				Logger.LogVerbose("[NexusMods] API key provided - ensure it complies with Nexus Mods Acceptable Use Policy");
				Logger.LogVerbose("[NexusMods] For production use, consider registering this application with Nexus Mods:");
				Logger.LogVerbose("[NexusMods] Contact: support@nexusmods.com with application details for registration");
			}
		}

		private void SetApiRequestHeaders(HttpRequestMessage request)
		{
			request.Headers.Add("Application-Name", "KOTORModSync");
			request.Headers.Add("Application-Version", "2.0.0");

			if (!string.IsNullOrWhiteSpace(_apiKey))
			{
				request.Headers.Add("apikey", _apiKey);
			}

			if (!request.Headers.Contains("User-Agent"))
			{
				request.Headers.Add("User-Agent", "KOTORModSync/2.0.0 (https://github.com/th3w1zard1/KOTORModSync)");
			}
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
				await Logger.LogVerboseAsync("[NexusMods] ===== ResolveFilenamesAsync START =====").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[NexusMods] Resolving filenames for URL: {url}").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[NexusMods] CancellationToken: {cancellationToken}").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[NexusMods] API Key provided: {!string.IsNullOrWhiteSpace(_apiKey)}").ConfigureAwait(false);

				System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(url, @"nexusmods\.com/([^/]+)/mods/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
				if (!match.Success)

				{
					await Logger.LogErrorAsync($"[NexusMods] Failed to parse Nexus Mods URL: {url}").ConfigureAwait(false);
					return new List<string>();
				}

				string gameDomain = match.Groups[1].Value;
				string modId = match.Groups[2].Value;

				await Logger.LogVerboseAsync($"[NexusMods] Parsed URL - Game: {gameDomain}, Mod ID: {modId}").ConfigureAwait(false);

				string filesUrl = $"https://api.nexusmods.com/v1/games/{gameDomain}/mods/{modId}/files.json";
				await Logger.LogVerboseAsync($"[NexusMods] Fetching file list from: {filesUrl}").ConfigureAwait(false);

				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, filesUrl);

				// Set API headers if we have a key, otherwise use basic headers
				if (!string.IsNullOrWhiteSpace(_apiKey))
				{
					SetApiRequestHeaders(request);
					await Logger.LogVerboseAsync("[NexusMods] Using authenticated API request").ConfigureAwait(false);
				}
				else
				{
					// Use basic headers for unauthenticated API requests
					if (!request.Headers.Contains("User-Agent"))
					{
						request.Headers.Add("User-Agent", "KOTORModSync/2.0.0 (https://github.com/th3w1zard1/KOTORModSync)");
					}
					if (!request.Headers.Contains("Accept"))
					{
						request.Headers.Add("Accept", "application/json");

					}
					await Logger.LogVerboseAsync("[NexusMods] Using unauthenticated API request (public file list)").ConfigureAwait(false);
				}

				HttpResponseMessage response = await MakeApiRequestAsync(request, cancellationToken).ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[NexusMods] API response status: {response.StatusCode}").ConfigureAwait(false);
				response.EnsureSuccessStatusCode();
				string jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

				await Logger.LogVerboseAsync($"[NexusMods] Received file list response, length: {jsonResponse.Length}").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[NexusMods] JSON response preview: {jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length))}...").ConfigureAwait(false);

				JObject filesData = JObject.Parse(jsonResponse);
				List<JObject> files = filesData["files"]?.ToObject<List<JObject>>();

				await Logger.LogVerboseAsync($"[NexusMods] Parsed files array, count: {files?.Count ?? 0}").ConfigureAwait(false);
				if (files is null || files.Count == 0)
				{
					await Logger.LogWarningAsync("[NexusMods] No files found for this mod").ConfigureAwait(false);
					request.Dispose();
					response.Dispose();
					return new List<string>();
				}

				await Logger.LogVerboseAsync($"[NexusMods] Found {files.Count} files for mod").ConfigureAwait(false);

				List<string> filenames = new List<string>();

				await Logger.LogVerboseAsync($"[NexusMods] Processing {files.Count} files from API response:").ConfigureAwait(false);
				for (int i = 0; i < files.Count; i++)
				{
					JObject file = files[i];
					await Logger.LogVerboseAsync($"[NexusMods] Processing file {i + 1}/{files.Count}").ConfigureAwait(false);

					JToken fileNameToken = file["file_name"];
					JToken categoryNameToken = file["category_name"];
					JToken fileIdToken = file["file_id"];

					string fileName = ((string)fileNameToken) ?? "unknown";
					string categoryName = ((string)categoryNameToken) ?? "";
					int fileId = fileIdToken != null ? (int)fileIdToken : 0;

					await Logger.LogVerboseAsync($"[NexusMods]   File ID: {fileId}").ConfigureAwait(false);
					await Logger.LogVerboseAsync($"[NexusMods]   File Name: {fileName}").ConfigureAwait(false);
					await Logger.LogVerboseAsync($"[NexusMods]   Category: {categoryName}").ConfigureAwait(false);

					if (categoryName.Equals("OPTIONAL", StringComparison.OrdinalIgnoreCase))
					{
						await Logger.LogVerboseAsync($"[NexusMods] Skipping optional file: {fileName}").ConfigureAwait(false);
						continue;
					}

					if (!categoryName.Equals("MAIN", StringComparison.OrdinalIgnoreCase) &&
						 !categoryName.Equals("UPDATE", StringComparison.OrdinalIgnoreCase) &&
						 !categoryName.Equals("MISCELLANEOUS", StringComparison.OrdinalIgnoreCase))
					{
						await Logger.LogVerboseAsync($"[NexusMods] Skipping non-main file: {fileName} (category: {categoryName})").ConfigureAwait(false);
						continue;
					}

					await Logger.LogVerboseAsync($"[NexusMods] ✓ Found available file: {fileName}").ConfigureAwait(false);
					filenames.Add(fileName);
				}

				request.Dispose();
				response.Dispose();

				await Logger.LogVerboseAsync($"[NexusMods] Resolved {filenames.Count} filename(s) from API").ConfigureAwait(false);
				await Logger.LogVerboseAsync("[NexusMods] Final filenames list:").ConfigureAwait(false);
				for (int i = 0; i < filenames.Count; i++)
				{
					await Logger.LogVerboseAsync($"[NexusMods]   {i + 1}. {filenames[i]}").ConfigureAwait(false);
				}
				await Logger.LogVerboseAsync("[NexusMods] ===== ResolveFilenamesAsync END =====").ConfigureAwait(false);
				return filenames;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, $"[NexusMods] Failed to resolve filenames").ConfigureAwait(false);
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
			await Logger.LogVerboseAsync($"[NexusMods] Starting Nexus Mods download from URL: {url}").ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[NexusMods] Destination directory: {destinationDirectory}").ConfigureAwait(false);
			if (targetFilenames != null && targetFilenames.Count > 0)
			{
				await Logger.LogVerboseAsync($"[NexusMods] Target filename(s): {string.Join(", ", targetFilenames)}").ConfigureAwait(false);
			}

			try
			{

				if (!Uri.TryCreate(url, UriKind.Absolute, out Uri validatedUri))
				{
					string errorMsg = $"Invalid URL format: {url}";
					await Logger.LogErrorAsync($"[NexusMods] {errorMsg}").ConfigureAwait(false);
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
				await Logger.LogVerboseAsync($"[NexusMods] Expected filename: {expectedFileName}").ConfigureAwait(false);

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Accessing Nexus Mods page...",
					ProgressPercentage = 10,
					StartTime = DateTime.Now,
				});

				if (!string.IsNullOrWhiteSpace(_apiKey))
				{
					await Logger.LogVerboseAsync("[NexusMods] Using API key for download").ConfigureAwait(false);
					return await DownloadWithApiKey(url, destinationDirectory, progress, targetFilenames, cancellationToken).ConfigureAwait(false);
				}
				else
				{
					await Logger.LogVerboseAsync("[NexusMods] No API key provided, attempting free download").ConfigureAwait(false);
					return await DownloadWithoutApiKey(url, progress).ConfigureAwait(false);
				}
			}
			catch (HttpRequestException httpEx)
			{
				await Logger.LogErrorAsync($"[NexusMods] HTTP request failed for URL '{url}': {httpEx.Message}").ConfigureAwait(false);
				await Logger.LogExceptionAsync(httpEx).ConfigureAwait(false);

				string userMessage;
				if (httpEx.Message.Contains("403") || httpEx.Message.Contains("Forbidden"))
				{
					userMessage = "Nexus Mods download failed due to access restrictions. This usually happens when:\n\n" +
								  "• The file requires Nexus Mods Premium membership\n" +
								  "• The mod author has restricted downloads to Premium users only\n" +
								  "• The file is temporarily unavailable\n\n" +
								  $"Please download manually from: {url}\n\n" +
								  "Consider upgrading to Nexus Mods Premium for automated downloads of premium files.\n\n" +
								  $"Technical details: {httpEx.Message}";
				}
				else
				{
					userMessage = "Nexus Mods download failed. This usually happens when:\n\n" +
								  "• An API key is required but not configured\n" +
								  "• The mod page requires login/authentication\n" +
								  "• Network connectivity issues\n\n" +
								  $"Please download manually from: {url}\n\n" +
								  "Or ensure your Nexus Mods API key is correctly configured.\n\n" +
								  $"Technical details: {httpEx.Message}";
				}

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
				await Logger.LogErrorAsync($"[NexusMods] Request timeout for URL '{url}': {tcEx.Message}").ConfigureAwait(false);
				await Logger.LogExceptionAsync(tcEx).ConfigureAwait(false);

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
					EndTime = DateTime.Now,
				});

				return DownloadResult.Failed(userMessage);
			}
			catch (Exception ex)
			{
				await Logger.LogErrorAsync($"[NexusMods] Download failed for URL '{url}': {ex.Message}").ConfigureAwait(false);
				await Logger.LogExceptionAsync(ex).ConfigureAwait(false);

				string userMessage = $"Nexus Mods download failed unexpectedly.\n\n" +
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

		private async Task<DownloadResult> DownloadWithApiKey(
			string url,
			string destinationDirectory,
			IProgress<DownloadProgress> progress,
			List<string> targetFilenames,
			CancellationToken cancellationToken)

		{
			await Logger.LogVerboseAsync($"[NexusMods] Resolving download links from Nexus Mods API for URL: {url}").ConfigureAwait(false);
			if (targetFilenames != null && targetFilenames.Count > 0)
			{
				await Logger.LogVerboseAsync($"[NexusMods] Target filename(s): {string.Join(", ", targetFilenames)}").ConfigureAwait(false);
			}

			List<NexusDownloadLink> linkInfos = await ResolveDownloadLinksAsync(url, targetFilenames).ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[NexusMods] Resolved {linkInfos.Count} download link(s)").ConfigureAwait(false);





			List<string> downloadedFiles = new List<string>();
			for (int i = 0; i < linkInfos.Count; i++)
			{
				NexusDownloadLink linkInfo = linkInfos[i];
				await Logger.LogVerboseAsync($"[NexusMods] Downloading file {i + 1}/{linkInfos.Count}: {linkInfo.FileName}").ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[NexusMods] Download URL: {linkInfo.Url}").ConfigureAwait(false);

				string fileName = string.IsNullOrEmpty(linkInfo.FileName) ? $"nexus_download_{linkInfo.FileId}" : linkInfo.FileName;

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = $"Downloading {fileName} ({i + 1}/{linkInfos.Count})...",
					ProgressPercentage = (i * 100) / linkInfos.Count,
				});

				await Logger.LogVerboseAsync($"[NexusMods] Making HTTP GET request to download URL").ConfigureAwait(false);
				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, linkInfo.Url);
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
				request.Headers.Add("User-Agent", "KOTORModSync/2.0.0 (https://github.com/th3w1zard1/KOTORModSync)");

				HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[NexusMods] Received response with status code: {response.StatusCode}").ConfigureAwait(false);

				_ = response.EnsureSuccessStatusCode();

				_ = Directory.CreateDirectory(destinationDirectory);
				string filePath = Path.Combine(destinationDirectory, fileName);
				await Logger.LogVerboseAsync($"[NexusMods] Writing file to: {filePath}").ConfigureAwait(false);

				long totalBytes = response.Content.Headers.ContentLength ?? 0;

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
				await Logger.LogVerboseAsync($"[NexusMods] File download completed successfully. File size: {fileSize} bytes").ConfigureAwait(false);

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
				EndTime = DateTime.Now,
			});

			string resultMessage = $"Downloaded {downloadedFiles.Count} file(s) from Nexus Mods: {string.Join(", ", downloadedFiles.Select(Path.GetFileName))}";
			return DownloadResult.Succeeded(downloadedFiles.Count > 0 ? downloadedFiles[0] : null, resultMessage);
		}

		private async Task<DownloadResult> DownloadWithoutApiKey(string url, IProgress<DownloadProgress> progress)
		{
			await Logger.LogVerboseAsync("[NexusMods] Attempting free download from Nexus Mods page").ConfigureAwait(false);

			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.InProgress,
				StatusMessage = "Loading mod page...",
				ProgressPercentage = 20,
			});

			await Logger.LogVerboseAsync($"[NexusMods] Making HTTP GET request to mod page: {url}").ConfigureAwait(false);
			HttpResponseMessage pageResponse = await _httpClient.GetAsync(url).ConfigureAwait(false);

			await Logger.LogVerboseAsync($"[NexusMods] Received page response with status code: {pageResponse.StatusCode}").ConfigureAwait(false);
			_ = pageResponse.EnsureSuccessStatusCode();

			string html = await pageResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
			await Logger.LogVerboseAsync($"[NexusMods] Downloaded page HTML, length: {html.Length} characters").ConfigureAwait(false);

			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.InProgress,
				StatusMessage = "Looking for download link...",
				ProgressPercentage = 40,
			});

			await Logger.LogWarningAsync("[NexusMods] Free downloads from Nexus Mods require manual interaction and cannot be automated").ConfigureAwait(false);

			progress?.Report(new DownloadProgress
			{
				Status = DownloadStatus.Failed,
				ErrorMessage = "Free downloads from Nexus Mods require manual interaction. Please download manually or provide an API key.",
				ProgressPercentage = 100,
				EndTime = DateTime.Now,
			});

			pageResponse.Dispose();
			return DownloadResult.Failed("Free downloads from Nexus Mods require manual interaction. Please download the mod manually from the website or provide an API key for automated downloads.");
		}

		private async Task<HttpResponseMessage> MakeApiRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
		{
			HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

			if (response.StatusCode == (System.Net.HttpStatusCode)429)
			{
				RetryConditionHeaderValue retryAfter = response.Headers.RetryAfter;
				if (retryAfter?.Delta.HasValue == true)
				{
					int retrySeconds = (int)retryAfter.Delta.Value.TotalSeconds;
					await Logger.LogWarningAsync($"[NexusMods] Rate limited by API. Waiting {retrySeconds} seconds before retry...").ConfigureAwait(false);
					await Task.Delay(TimeSpan.FromSeconds(retrySeconds), cancellationToken).ConfigureAwait(false);

					response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
				}
				else
				{
					await Logger.LogWarningAsync("[NexusMods] Rate limited by API (no Retry-After header). Waiting 60 seconds...").ConfigureAwait(false);
					await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);

					response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
				}
			}

			return response;
		}

		private async Task<List<NexusDownloadLink>> ResolveDownloadLinksAsync(string url, List<string> targetFilenames = null)
		{
			await Logger.LogVerboseAsync($"[NexusMods] ResolveDownloadLinksAsync called with URL: {url}").ConfigureAwait(false);
			if (targetFilenames != null && targetFilenames.Count > 0)
			{
				await Logger.LogVerboseAsync($"[NexusMods] Target filename(s): {string.Join(", ", targetFilenames)}").ConfigureAwait(false);
			}

			System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(url, @"nexusmods\.com/([^/]+)/mods/(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			if (!match.Success)
			{
				await Logger.LogErrorAsync($"[NexusMods] Failed to parse Nexus Mods URL: {url}").ConfigureAwait(false);
				return new List<NexusDownloadLink>();
			}

			string gameDomain = match.Groups[1].Value;
			string modId = match.Groups[2].Value;

			await Logger.LogVerboseAsync($"[NexusMods] Parsed URL - Game: {gameDomain}, Mod ID: {modId}").ConfigureAwait(false);

			try
			{
				string filesUrl = $"https://api.nexusmods.com/v1/games/{gameDomain}/mods/{modId}/files.json";
				await Logger.LogVerboseAsync($"[NexusMods] Fetching file list from: {filesUrl}").ConfigureAwait(false);

				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, filesUrl);
				SetApiRequestHeaders(request);

				HttpResponseMessage response = await MakeApiRequestAsync(request).ConfigureAwait(false);
				response.EnsureSuccessStatusCode();

				string jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[NexusMods] Received file list response, length: {jsonResponse.Length}").ConfigureAwait(false);

				JObject filesData = JObject.Parse(jsonResponse);
				List<JObject> files = filesData["files"]?.ToObject<List<JObject>>();

				if (files is null || files.Count == 0)
				{
					await Logger.LogWarningAsync("[NexusMods] No files found for this mod").ConfigureAwait(false);
					return new List<NexusDownloadLink>();
				}

				await Logger.LogVerboseAsync($"[NexusMods] Found {files.Count} files for mod").ConfigureAwait(false);

				List<NexusDownloadLink> downloadLinks = new List<NexusDownloadLink>();

				foreach (JObject file in files)
				{
					JToken fileIdToken = file["file_id"];
					JToken fileNameToken = file["file_name"];
					JToken categoryNameToken = file["category_name"];

					int fileId = fileIdToken != null ? (int)fileIdToken : 0;
					string fileName = ((string)fileNameToken) ?? "unknown";
					string categoryName = ((string)categoryNameToken) ?? "";

					if (categoryName.Equals("OPTIONAL", StringComparison.OrdinalIgnoreCase))
					{
						await Logger.LogVerboseAsync($"[NexusMods] Skipping optional file: {fileName}").ConfigureAwait(false);
						continue;
					}

					if (!categoryName.Equals("MAIN", StringComparison.OrdinalIgnoreCase) &&
						 !categoryName.Equals("UPDATE", StringComparison.OrdinalIgnoreCase) &&
						 !categoryName.Equals("MISCELLANEOUS", StringComparison.OrdinalIgnoreCase))
					{
						await Logger.LogVerboseAsync($"[NexusMods] Skipping non-main file: {fileName} (category: {categoryName})").ConfigureAwait(false);
						continue;
					}

					if (targetFilenames != null && targetFilenames.Count > 0)
					{
						bool matches = FileMatchesPatterns(fileName, targetFilenames);
						if (!matches)
						{
							await Logger.LogVerboseAsync($"[NexusMods] Skipping file '{fileName}' - doesn't match any target patterns: {string.Join(", ", targetFilenames)}").ConfigureAwait(false);
							continue;
						}
						await Logger.LogVerboseAsync($"[NexusMods] File '{fileName}' matches one of the target patterns").ConfigureAwait(false);
					}

					await Logger.LogVerboseAsync($"[NexusMods] Getting download link for file: {fileName} (ID: {fileId})").ConfigureAwait(false);

					try
					{
						string downloadLinkUrl = $"https://api.nexusmods.com/v1/games/{gameDomain}/mods/{modId}/files/{fileId}/download_link.json";
						HttpRequestMessage downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadLinkUrl);
						SetApiRequestHeaders(downloadRequest);

						HttpResponseMessage downloadResponse = await MakeApiRequestAsync(downloadRequest).ConfigureAwait(false);
						downloadResponse.EnsureSuccessStatusCode();

						string downloadJson = await downloadResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
						await Logger.LogVerboseAsync($"[NexusMods] Download link response: {downloadJson}").ConfigureAwait(false);

						JObject downloadData = JObject.Parse(downloadJson);

						JArray uriArray = downloadData["download_links"] as JArray;
						if (uriArray != null && uriArray.Count > 0)
						{
							JToken firstLink = uriArray[0];
							JToken uriToken = firstLink["URI"];
							string downloadUrl = (string)uriToken;
							if (!string.IsNullOrEmpty(downloadUrl))
							{
								downloadLinks.Add(new NexusDownloadLink
								{
									Url = downloadUrl,
									FileName = fileName,
									FileId = fileId,
								});
								await Logger.LogVerboseAsync($"[NexusMods] Added download link for: {fileName}").ConfigureAwait(false);
							}
						}

						downloadRequest.Dispose();
						downloadResponse.Dispose();
					}
					catch (Exception ex)
					{
						await Logger.LogWarningAsync($"[NexusMods] Error getting download link for '{fileName}': {ex.Message}").ConfigureAwait(false);
						continue;
					}
				}

				request.Dispose();
				response.Dispose();

				await Logger.LogVerboseAsync($"[NexusMods] Resolved {downloadLinks.Count} download links").ConfigureAwait(false);
				return downloadLinks;
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, $"[NexusMods] Failed to resolve download links for URL: {url}").ConfigureAwait(false);
				return new List<NexusDownloadLink>();
			}
		}

		private static bool FileMatchesPatterns(string filename, List<string> patterns)
		{
			if (string.IsNullOrWhiteSpace(filename) || patterns is null || patterns.Count == 0)
				return false;

			foreach (string pattern in patterns)
			{
				string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
					.Replace("\\*", ".*")
					.Replace("\\?", ".") + "$";
				try
				{
					if (System.Text.RegularExpressions.Regex.IsMatch(filename, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
						return true;
				}
				catch
				{
					// Fallback: simple substring check
					if (filename.IndexOf(pattern.Replace("*", ""), StringComparison.OrdinalIgnoreCase) >= 0)
						return true;
				}
			}
			return false;
		}

		private sealed class NexusDownloadLink
		{
			public string Url { get; set; } = string.Empty;
			public string FileName { get; set; } = string.Empty;
			public int FileId { get; set; }
		}

		public static async Task<(bool IsValid, string Message)> ValidateApiKeyAsync(string apiKey)
		{
			if (string.IsNullOrWhiteSpace(apiKey))
			{
				return (false, "API key cannot be empty");
			}

			try
			{
				using (HttpClient httpClient = new HttpClient())
				{
					await Logger.LogAsync("[NexusMods] Validating API key format...").ConfigureAwait(false);
					if (apiKey.Length < 20)
					{
						return (false, "API key appears to be too short. Nexus Mods API keys are typically longer.");
					}

					await Logger.LogAsync("[NexusMods] Testing API key authentication...").ConfigureAwait(false);
					HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://api.nexusmods.com/v1/users/validate.json");
					request.Headers.Add("Application-Name", "KOTORModSync");
					request.Headers.Add("Application-Version", "2.0.0");
					request.Headers.Add("apikey", apiKey);
					request.Headers.Add("User-Agent", "KOTORModSync/2.0.0 (https://github.com/th3w1zard1/KOTORModSync)");

					HttpResponseMessage response = await httpClient.SendAsync(request).ConfigureAwait(false);

					if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
					{
						return (false, "API key is invalid or unauthorized. Please check your key and try again.");
					}

					if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
					{
						return (false, "API key is forbidden from accessing the API. Please check your Nexus Mods account permissions.");
					}

					if (response.StatusCode == (System.Net.HttpStatusCode)429)
					{
						return (false, "API rate limit exceeded. Please wait a few minutes and try again.");
					}

					if (!response.IsSuccessStatusCode)
					{
						return (false, $"API validation failed with status code: {response.StatusCode}. Message: {await response.Content.ReadAsStringAsync().ConfigureAwait(false)}");
					}

					string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					await Logger.LogVerboseAsync($"[NexusMods] API validation response: {content}").ConfigureAwait(false);

					await Logger.LogAsync("[NexusMods] Retrieving user information...").ConfigureAwait(false);
					if (content.Contains("user_id") || content.Contains("name"))
					{
						await Logger.LogAsync("[NexusMods] ✓ API key validated successfully!").ConfigureAwait(false);
						await Logger.LogAsync("[NexusMods] ✓ User authentication confirmed").ConfigureAwait(false);
						return (true, "API key is valid and working correctly!");
					}

					return (false, "API key validation returned unexpected response format.");
				}
			}
			catch (HttpRequestException ex)
			{
				await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
				return (false, $"Network error during API validation: {ex.Message}");
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex).ConfigureAwait(false);
				return (false, $"Unexpected error during API validation: {ex.Message}");
			}
		}

		public async Task<Dictionary<string, object>> GetFileMetadataAsync(string url, CancellationToken cancellationToken = default)
		{
			Dictionary<string, object> metadata = new Dictionary<string, object>(StringComparer.Ordinal);

			try
			{
				if (string.IsNullOrWhiteSpace(MainConfig.NexusModsApiKey))
				{
					await Logger.LogWarningAsync("[NexusMods] API key not configured, cannot retrieve metadata").ConfigureAwait(false);
					return metadata;
				}

				// Parse URL to extract game domain and mod ID
				System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(url, @"nexusmods\.com/([^/]+)/mods/(\d+)");
				if (!match.Success)
				{
					await Logger.LogWarningAsync($"[NexusMods] Could not parse Nexus URL: {url}").ConfigureAwait(false);
					return metadata;
				}

				string gameDomain = match.Groups[1].Value;
				string modId = match.Groups[2].Value;

				// Extract file ID from URL if present
				System.Text.RegularExpressions.Match fileMatch = System.Text.RegularExpressions.Regex.Match(url, @"\?tab=files&file_id=(\d+)");
				string fileId = fileMatch.Success ? fileMatch.Groups[1].Value : null;

				if (fileId != null)
				{
					// Get specific file metadata
					HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get,
						$"https://api.nexusmods.com/v1/games/{gameDomain}/mods/{modId}/files/{fileId}.json");
					request.Headers.Add("apikey", MainConfig.NexusModsApiKey);
					request.Headers.Add("User-Agent", "KOTORModSync/2.0.0");

					HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
					if (response.IsSuccessStatusCode)
					{
						string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
						JObject fileData = JObject.Parse(content);

						metadata["fileId"] = fileId;
						metadata["fileName"] = fileData["file_name"]?.ToString() ?? "";
						long sizeKb = 0;
						if (fileData["size_kb"] != null)
						{
							JToken sizeKbToken = fileData["size_kb"];
							if (sizeKbToken.Type == JTokenType.Integer)
								sizeKb = sizeKbToken.Value<long>();
							else if (sizeKbToken.Type == JTokenType.Float)
								sizeKb = (long)sizeKbToken.Value<double>();
						}
						metadata["size"] = sizeKb * 1024;

						long timestamp = 0;
						if (fileData["uploaded_timestamp"] != null)
						{
							JToken timestampToken = fileData["uploaded_timestamp"];
							if (timestampToken.Type == JTokenType.Integer)
								timestamp = timestampToken.Value<long>();
						}
						metadata["uploadedTimestamp"] = timestamp;
						metadata["md5Hash"] = fileData["md5"]?.ToString() ?? "";
					}
				}

				List<string> metadataList = new List<string>();
				foreach (KeyValuePair<string, object> kvp in metadata)
				{
					metadataList.Add($"{kvp.Key}={kvp.Value}");
				}

				await Logger.LogVerboseAsync($"[NexusMods] Extracted metadata: {string.Join(", ", metadataList)}").ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				await Logger.LogWarningAsync($"[NexusMods] Failed to extract metadata: {ex.Message}").ConfigureAwait(false);
			}

			return NormalizeMetadata(metadata);
		}

		public string GetProviderKey()
		{
			return "nexus";
		}

		private Dictionary<string, object> NormalizeMetadata(Dictionary<string, object> raw)
		{
			Dictionary<string, object> normalized = new Dictionary<string, object>(StringComparer.Ordinal);
			string[] whitelist = new[] { "provider", "fileId", "fileName", "size", "uploadedTimestamp", "md5Hash" };

			// Always add provider
			normalized["provider"] = GetProviderKey();

			foreach (string field in whitelist)
			{
				if (string.Equals(field, "provider", StringComparison.Ordinal)) continue; // Already added

				if (!raw.ContainsKey(field)) continue;

				object value = raw[field];

				// Type-specific normalization
				if (string.Equals(field, "size", StringComparison.Ordinal) || string.Equals(field, "uploadedTimestamp", StringComparison.Ordinal))
				{
					normalized[field] = Convert.ToInt64(value);
				}
				else if (string.Equals(field, "md5Hash", StringComparison.Ordinal) && value != null)
				{
					// Normalize to lowercase hex
					normalized[field] = value.ToString().ToLowerInvariant();
				}
				else if (value != null)
				{
					normalized[field] = value.ToString();
				}
			}

			return normalized;
		}
	}
}