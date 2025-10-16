// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;

namespace KOTORModSync.Core.Services.Download
{
	public sealed class MegaDownloadHandler : IDownloadHandler
	{
		private readonly MegaApiClient _client = new MegaApiClient();
		private readonly SemaphoreSlim _sessionLock = new SemaphoreSlim(1, 1);
		private static readonly char[] s_separator = new[] { '!' };
		private const int TimeoutSeconds = 15;

		public MegaDownloadHandler() => Logger.LogVerbose("[MEGA] Initializing MEGA download handler");

		public bool CanHandle(string url)
		{
			bool canHandle = url != null && url.IndexOf("mega.nz", StringComparison.OrdinalIgnoreCase) >= 0;
			Logger.LogVerbose($"[MEGA] CanHandle check for URL '{url}': {canHandle}");
			return canHandle;
		}

		public async Task<List<string>> ResolveFilenamesAsync(string url, CancellationToken cancellationToken = default)
		{
			await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);

			try
			{
				await Logger.LogVerboseAsync($"[MEGA] Resolving filename for URL: {url}");

				try
				{
					await _client.LogoutAsync().ConfigureAwait(false);
				}
				catch { }

				var loginTask = _client.LoginAnonymousAsync();
				var loginTimeoutTask = Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds), cancellationToken);

				var loginCompletedTask = await Task.WhenAny(loginTask, loginTimeoutTask).ConfigureAwait(false);
				if ( loginCompletedTask == loginTimeoutTask )
				{
					throw new OperationCanceledException($"MEGA login timed out after {TimeoutSeconds} seconds");
				}
				await loginTask.ConfigureAwait(false);

				string processedUrl = ConvertMegaUrl(url);

				var getNodeTask = _client.GetNodeFromLinkAsync(new Uri(processedUrl));
				var timeoutTask = Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds), cancellationToken);

				var completedTask = await Task.WhenAny(getNodeTask, timeoutTask).ConfigureAwait(false);

				if ( completedTask == timeoutTask )
				{
					throw new OperationCanceledException($"MEGA GetNodeFromLink timed out after {TimeoutSeconds} seconds");
				}

				INode node = await getNodeTask.ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[MEGA] Resolved filename: {node.Name}");

				await _client.LogoutAsync().ConfigureAwait(false);

				return new List<string> { node.Name };
			}
			catch ( OperationCanceledException )
			{
				await Logger.LogWarningAsync($"[MEGA] Filename resolution timed out after {TimeoutSeconds} seconds for URL: {url}");
				try
				{
					await _client.LogoutAsync().ConfigureAwait(false);
				}
				catch { }
				return new List<string>();
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, $"[MEGA] Failed to resolve filename for URL: {url}");
				try
				{
					await _client.LogoutAsync().ConfigureAwait(false);
				}
				catch { }
				return new List<string>();
			}
			finally
			{
				_sessionLock.Release();
			}
		}

		public async Task<DownloadResult> DownloadAsync(
			string url,
			string destinationDirectory,
			IProgress<DownloadProgress> progress = null,
			List<string> targetFilenames = null,
			CancellationToken cancellationToken = default)
		{
			await Logger.LogVerboseAsync($"[MEGA] Starting MEGA download from URL: {url}");
			await Logger.LogVerboseAsync($"[MEGA] Destination directory: {destinationDirectory}");

			await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);

			try
			{
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Logging in to MEGA...",
					ProgressPercentage = 10,
					StartTime = DateTime.Now
				});

				try
				{
					var preLogoutTask = _client.LogoutAsync();
					var preLogoutTimeout = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
					var preLogoutCompleted = await Task.WhenAny(preLogoutTask, preLogoutTimeout).ConfigureAwait(false);
					if ( preLogoutCompleted == preLogoutTask )
					{
						await preLogoutTask.ConfigureAwait(false);
						await Logger.LogVerboseAsync("[MEGA] Performed pre-login logout (if any session was active)");
					}
					else
					{
						await Logger.LogVerboseAsync("[MEGA] Pre-login logout timed out (not critical)");
					}
				}
				catch ( Exception preLogoutEx )
				{
					await Logger.LogVerboseAsync($"[MEGA] Pre-login logout not required or failed: {preLogoutEx.Message}");
				}

				await Logger.LogVerboseAsync("[MEGA] Logging in anonymously to MEGA");

				var loginTask = _client.LoginAnonymousAsync();
				var loginTimeoutTask = Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds), cancellationToken);

				var loginCompletedTask = await Task.WhenAny(loginTask, loginTimeoutTask).ConfigureAwait(false);
				if ( loginCompletedTask == loginTimeoutTask )
				{
					throw new OperationCanceledException($"MEGA login timed out after {TimeoutSeconds} seconds");
				}
				await loginTask.ConfigureAwait(false);

				await Logger.LogVerboseAsync("[MEGA] Successfully logged in to MEGA");

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Fetching file information...",
					ProgressPercentage = 30
				});

				await Logger.LogVerboseAsync($"[MEGA] BEFORE conversion: {url}");
				string processedUrl = ConvertMegaUrl(url);
				await Logger.LogVerboseAsync($"[MEGA] AFTER conversion: {processedUrl}");
				await Logger.LogVerboseAsync($"[MEGA] Getting node information from URL: {processedUrl}");

				var getNodeTask = _client.GetNodeFromLinkAsync(new Uri(processedUrl));
				var nodeTimeoutTask = Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds), cancellationToken);

				var nodeCompletedTask = await Task.WhenAny(getNodeTask, nodeTimeoutTask).ConfigureAwait(false);
				if ( nodeCompletedTask == nodeTimeoutTask )
				{
					throw new OperationCanceledException($"MEGA GetNodeFromLink timed out after {TimeoutSeconds} seconds");
				}

				INode node = await getNodeTask.ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[MEGA] Retrieved node: Name='{node.Name}', Size={node.Size} bytes, Type={node.Type}");

				_ = Directory.CreateDirectory(destinationDirectory);
				string filePath = Path.Combine(destinationDirectory, node.Name);
				await Logger.LogVerboseAsync($"[MEGA] Downloading file to: {filePath}");

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Downloading from MEGA...",
					ProgressPercentage = 50,
					TotalBytes = node.Size,
					StartTime = DateTime.Now
				});


				var megaProgress = new Progress<double>(percent =>
				{

					double progressPercent = Math.Max(0, Math.Min(percent, 100));

					progress?.Report(new DownloadProgress
					{
						Status = DownloadStatus.InProgress,
						StatusMessage = $"Downloading {node.Name}... ({progressPercent:F1}%)",
						ProgressPercentage = progressPercent,
						BytesDownloaded = (long)(node.Size * (progressPercent / 100.0)),
						TotalBytes = node.Size,
						StartTime = DateTime.Now,
						FilePath = filePath
					});
				});

				int downloadTimeoutSeconds = Math.Max(300, (int)(node.Size / (100 * 1024)));
				await Logger.LogVerboseAsync($"[MEGA] Calculated download timeout: {downloadTimeoutSeconds} seconds for {node.Size} byte file");

				var downloadTask = Task.Run(async () =>
				{
					using ( Stream downloadStream = await _client.DownloadAsync(node, megaProgress, cancellationToken).ConfigureAwait(false) )
					using ( FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true) )
					{
						await downloadStream.CopyToAsync(fileStream, 8192, cancellationToken).ConfigureAwait(false);
						await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
					}
				}, cancellationToken);

				var downloadTimeoutTask = Task.Delay(TimeSpan.FromSeconds(downloadTimeoutSeconds), cancellationToken);
				var downloadCompletedTask = await Task.WhenAny(downloadTask, downloadTimeoutTask).ConfigureAwait(false);

				if ( downloadCompletedTask == downloadTimeoutTask )
				{
					throw new OperationCanceledException($"MEGA file download timed out after {downloadTimeoutSeconds} seconds (no progress detected)");
				}

				await downloadTask.ConfigureAwait(false);

				long fileSize = new FileInfo(filePath).Length;
				await Logger.LogVerboseAsync($"[MEGA] File download completed successfully. File size: {fileSize} bytes");

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

				await Logger.LogVerboseAsync("[MEGA] Logging out from MEGA");

				var logoutTask = _client.LogoutAsync();
				var logoutTimeoutTask = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
				var logoutCompletedTask = await Task.WhenAny(logoutTask, logoutTimeoutTask).ConfigureAwait(false);

				if ( logoutCompletedTask == logoutTask )
				{
					await logoutTask.ConfigureAwait(false);
					await Logger.LogVerboseAsync("[MEGA] Successfully logged out from MEGA");
				}
				else
				{
					await Logger.LogVerboseAsync("[MEGA] Logout timed out (not critical, session will expire)");
				}

				return DownloadResult.Succeeded(filePath, "Downloaded from MEGA");
			}
			catch ( ArgumentException argEx )
			{
				await Logger.LogErrorAsync($"[MEGA] Invalid URL '{url}': {argEx.Message}");
				await Logger.LogExceptionAsync(argEx);

				string userMessage = "MEGA.nz URL is invalid or malformed.\n\n" +
									 "This can happen when:\n" +
									 "• The URL format is incorrect\n" +
									 "• The file ID or encryption key is missing\n" +
									 "• The link has been truncated or corrupted\n\n" +
									 $"Please verify the URL and try downloading manually from: {url}\n\n" +
									 $"Technical details: {argEx.Message}";

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = userMessage,
					Exception = argEx,
					ProgressPercentage = 100,
					EndTime = DateTime.Now
				});
				return DownloadResult.Failed(userMessage);
			}
			catch ( UnauthorizedAccessException authEx )
			{
				await Logger.LogErrorAsync($"[MEGA] Authentication failed for URL '{url}': {authEx.Message}");
				await Logger.LogExceptionAsync(authEx);

				string userMessage = "MEGA.nz authentication or access failed.\n\n" +
									 "This can happen when:\n" +
									 "• The file requires a password or login\n" +
									 "• The file is private or restricted\n" +
									 "• MEGA API limits have been reached\n\n" +
									 $"Please try downloading manually from: {url}\n\n" +
									 $"Technical details: {authEx.Message}";

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = userMessage,
					Exception = authEx,
					ProgressPercentage = 100,
					EndTime = DateTime.Now
				});
				return DownloadResult.Failed(userMessage);
			}
			catch ( FileNotFoundException fileEx )
			{
				await Logger.LogErrorAsync($"[MEGA] File not found for URL '{url}': {fileEx.Message}");
				await Logger.LogExceptionAsync(fileEx);

				string userMessage = "MEGA.nz file not found.\n\n" +
									 "This can happen when:\n" +
									 "• The file has been deleted by the owner\n" +
									 "• The link has expired\n" +
									 "• The file was moved to a different location\n\n" +
									 $"Please check if the file still exists at: {url}\n\n" +
									 "You may need to contact the mod author for an updated link.\n\n" +
									 $"Technical details: {fileEx.Message}";

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = userMessage,
					Exception = fileEx,
					ProgressPercentage = 100,
					EndTime = DateTime.Now
				});
				return DownloadResult.Failed(userMessage);
			}
			catch ( OperationCanceledException )
			{
				await Logger.LogErrorAsync($"[MEGA] Download timed out or was cancelled for URL '{url}'");

				string userMessage = $"MEGA.nz download timed out after {TimeoutSeconds} seconds.\n\n" +
									 "This can happen when:\n" +
									 "• MEGA servers are slow or experiencing issues\n" +
									 "• Your internet connection is unstable\n" +
									 "• MEGA API is rate-limiting requests\n\n" +
									 $"Please try downloading manually from: {url}";

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.Failed,
					ErrorMessage = userMessage,
					ProgressPercentage = 100,
					EndTime = DateTime.Now
				});
				return DownloadResult.Failed(userMessage);
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"[MEGA] Download failed for URL '{url}': {ex.Message}");
				await Logger.LogExceptionAsync(ex);

				string userMessage = "MEGA.nz download failed unexpectedly.\n\n" +
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
			finally
			{
				try
				{
					var finalLogoutTask = _client.LogoutAsync();
					var finalLogoutTimeout = Task.Delay(TimeSpan.FromSeconds(5));
					var finalLogoutCompleted = await Task.WhenAny(finalLogoutTask, finalLogoutTimeout).ConfigureAwait(false);

					if ( finalLogoutCompleted == finalLogoutTask )
					{
						await finalLogoutTask.ConfigureAwait(false);
						await Logger.LogVerboseAsync("[MEGA] Ensured logout after operation");
					}
					else
					{
						await Logger.LogVerboseAsync("[MEGA] Final logout timed out (session will expire naturally)");
					}
				}
				catch
				{

				}
				_sessionLock.Release();
			}
		}






		private static string ConvertMegaUrl(string url)
		{
			if ( string.IsNullOrEmpty(url) )
				return url;


			if ( url.Contains("#!") )
			{

				int hashIndex = url.IndexOf("#!", StringComparison.Ordinal);
				if ( hashIndex >= 0 )
				{
					string baseUrl = url.Substring(0, hashIndex);
					string fragment = url.Substring(hashIndex + 2);


					string[] parts = fragment.Split(s_separator, StringSplitOptions.None);
					if ( parts.Length >= 2 )
					{
						string fileId = parts[0];
						string key = parts[1];


						string newUrl = $"{baseUrl}file/{fileId}#{key}";
						return newUrl;
					}
				}
			}


			if ( !url.Contains("#F!") )
				return url;
			int hashIndex2 = url.IndexOf("#F!", StringComparison.Ordinal);
			if ( hashIndex2 < 0 )
				return url;
			string baseUrl2 = url.Substring(0, hashIndex2);
			string fragment2 = url.Substring(hashIndex2 + 3);

			string[] parts2 = fragment2.Split(s_separator, StringSplitOptions.None);
			if ( parts2.Length < 2 )
				return url;
			string folderId = parts2[0];
			string key2 = parts2[1];

			string newUrl2 = $"{baseUrl2}folder/{folderId}#{key2}";
			return newUrl2;
		}
	}
}
