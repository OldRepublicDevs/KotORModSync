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

				// Ensure logged out before login
				try
				{
					await _client.LogoutAsync().ConfigureAwait(false);
				}
				catch { }

				await _client.LoginAnonymousAsync().ConfigureAwait(false);

				// Convert URL format if needed
				string processedUrl = ConvertMegaUrl(url);

				// Get node information to extract filename
				INode node = await _client.GetNodeFromLinkAsync(new Uri(processedUrl)).ConfigureAwait(false);
				await Logger.LogVerboseAsync($"[MEGA] Resolved filename: {node.Name}");

				await _client.LogoutAsync().ConfigureAwait(false);

				return new List<string> { node.Name };
			}
			catch ( Exception ex )
			{
				await Logger.LogWarningAsync($"[MEGA] Failed to resolve filename: {ex.Message}");
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

		public async Task<DownloadResult> DownloadAsync(string url, string destinationDirectory, IProgress<DownloadProgress> progress = null, CancellationToken cancellationToken = default)
		{
			await Logger.LogVerboseAsync($"[MEGA] Starting MEGA download from URL: {url}");
			await Logger.LogVerboseAsync($"[MEGA] Destination directory: {destinationDirectory}");

			await _sessionLock.WaitAsync().ConfigureAwait(false);

			try
			{
				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Logging in to MEGA...",
					ProgressPercentage = 10,
					StartTime = DateTime.Now
				});

				// Ensure we are logged out before attempting a new login (handles prior sessions)
				try
				{
					await _client.LogoutAsync().ConfigureAwait(false);
					await Logger.LogVerboseAsync("[MEGA] Performed pre-login logout (if any session was active)");
				}
				catch ( Exception preLogoutEx )
				{
					await Logger.LogVerboseAsync($"[MEGA] Pre-login logout not required or failed: {preLogoutEx.Message}");
				}

				await Logger.LogVerboseAsync("[MEGA] Logging in anonymously to MEGA");
				await _client.LoginAnonymousAsync().ConfigureAwait(false);
				await Logger.LogVerboseAsync("[MEGA] Successfully logged in to MEGA");

				progress?.Report(new DownloadProgress
				{
					Status = DownloadStatus.InProgress,
					StatusMessage = "Fetching file information...",
					ProgressPercentage = 30
				});

				// Convert old MEGA URL format (#!) to new format (/file/)
				await Logger.LogVerboseAsync($"[MEGA] BEFORE conversion: {url}");
				string processedUrl = ConvertMegaUrl(url);
				await Logger.LogVerboseAsync($"[MEGA] AFTER conversion: {processedUrl}");
				await Logger.LogVerboseAsync($"[MEGA] Getting node information from URL: {processedUrl}");

				INode node = await _client.GetNodeFromLinkAsync(new Uri(processedUrl)).ConfigureAwait(false);
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

				// Create a progress wrapper that converts MEGA's double progress (0.0-1.0) to our format
				var megaProgress = new Progress<double>(percent =>
				{
					progress?.Report(new DownloadProgress
					{
						Status = DownloadStatus.InProgress,
						StatusMessage = $"Downloading {node.Name}... ({percent:P0})",
						ProgressPercentage = percent * 100,
						BytesDownloaded = (long)(node.Size * percent),
						TotalBytes = node.Size,
						StartTime = DateTime.Now,
						FilePath = filePath
					});
				});

				// Use Download method with progress wrapper and cancellation token
				using ( Stream downloadStream = await _client.DownloadAsync(node, megaProgress, cancellationToken).ConfigureAwait(false) )
				{
					await DownloadHelper.DownloadWithProgressAsync(
						downloadStream,
						filePath,
						node.Size,
						node.Name,
						url,
						progress,
						cancellationToken: cancellationToken).ConfigureAwait(false);
				}

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
				await _client.LogoutAsync().ConfigureAwait(false);
				await Logger.LogVerboseAsync("[MEGA] Successfully logged out from MEGA");

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
					ProgressPercentage = 100, // Mark as complete so UI shows it's done
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
					ProgressPercentage = 100, // Mark as complete so UI shows it's done
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
					ProgressPercentage = 100, // Mark as complete so UI shows it's done
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
					ProgressPercentage = 100, // Mark as complete so UI shows it's done
					EndTime = DateTime.Now
				});
				return DownloadResult.Failed(userMessage);
			}
			finally
			{
				try
				{
					await _client.LogoutAsync().ConfigureAwait(false);
					await Logger.LogVerboseAsync("[MEGA] Ensured logout after operation");
				}
				catch
				{
					// ignore
				}
				_sessionLock.Release();
			}
		}

		/// <summary>
		/// Converts old MEGA URL format to new format.
		/// Old: https://mega.nz/#!1A4RCLha!Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4
		/// New: https://mega.nz/file/1A4RCLha#Ro2GNVUPRfgot-woqh80jVaukixr-cnUmTdakuc0Ca4
		/// </summary>
		private static string ConvertMegaUrl(string url)
		{
			if ( string.IsNullOrEmpty(url) )
				return url;

			// Check if it's the old format with #!
			if ( url.Contains("#!") )
			{
				// Extract the parts: #!{fileId}!{key}
				int hashIndex = url.IndexOf("#!", StringComparison.Ordinal);
				if ( hashIndex >= 0 )
				{
					string baseUrl = url.Substring(0, hashIndex);
					string fragment = url.Substring(hashIndex + 2); // Skip the #!

					// Split by ! to get fileId and key
					string[] parts = fragment.Split(s_separator, StringSplitOptions.None);
					if ( parts.Length >= 2 )
					{
						string fileId = parts[0];
						string key = parts[1];

						// Convert to new format: {baseUrl}file/{fileId}#{key}
						string newUrl = $"{baseUrl}file/{fileId}#{key}";
						return newUrl;
					}
				}
			}

			// Check if it's the old folder format with #F!
			if ( !url.Contains("#F!") )
				return url;
			int hashIndex2 = url.IndexOf("#F!", StringComparison.Ordinal);
			if ( hashIndex2 < 0 )
				return url;
			string baseUrl2 = url.Substring(0, hashIndex2);
			string fragment2 = url.Substring(hashIndex2 + 3); // Skip the #F!

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
