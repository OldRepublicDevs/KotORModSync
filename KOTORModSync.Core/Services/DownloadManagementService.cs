// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using KOTORModSync.Core.Services.Download;

using DownloadCacheEntry = KOTORModSync.Core.Services.DownloadCacheService.DownloadCacheEntry;

namespace KOTORModSync.Core.Services
{

	public class DownloadManagementService
	{
		private readonly DownloadCacheService _downloadCacheService;

		public DownloadManagementService(DownloadCacheService downloadCacheService)
		{
			_downloadCacheService = downloadCacheService ?? throw new ArgumentNullException(nameof(downloadCacheService));
		}

		public static async Task<string> DownloadModFromUrl(string url, ModComponent component, CancellationToken cancellationToken = default)

		{
			try
			{
				await Logger.LogVerboseAsync($"[DownloadModFromUrl] Starting download from: {url}")

.ConfigureAwait(false);

				DownloadProgress progress = new DownloadProgress
				{
					ModName = component?.Name ?? "Unknown Mod",
					Url = url,
					Status = DownloadStatus.Pending,
					StatusMessage = "Preparing download...",
					ProgressPercentage = 0,
				};

				DownloadManager downloadManager = Download.DownloadHandlerFactory.CreateDownloadManager();

				string guidString = Guid.NewGuid().ToString("N");
				string shortGuid = guidString.Substring(0, Math.Min(8, guidString.Length));
				string tempDir = Path.Combine(Path.GetTempPath(), "KOTORModSync_AutoGen_" + shortGuid);
				_ = Directory.CreateDirectory(tempDir);

				await Logger.LogVerboseAsync($"[DownloadModFromUrl] Created temporary directory: {tempDir}")






.ConfigureAwait(false);

				Progress<DownloadProgress> progressReporter = new Progress<DownloadProgress>(update =>
				{
					progress.Status = update.Status;
					progress.ProgressPercentage = update.ProgressPercentage;
					progress.StatusMessage = update.StatusMessage;
					progress.ErrorMessage = update.ErrorMessage;
					progress.FilePath = update.FilePath;
					progress.BytesDownloaded = update.BytesDownloaded;
					progress.TotalBytes = update.TotalBytes;
				});

				Dictionary<string, DownloadProgress> urlToProgressMap = new Dictionary<string, DownloadProgress>(StringComparer.Ordinal) { { url, progress } };
				List<DownloadResult> results = await downloadManager.DownloadAllWithProgressAsync(urlToProgressMap, tempDir, progressReporter, cancellationToken).ConfigureAwait(false);

				if (results.Count > 0 && results[0].Success)
				{
					string downloadedPath = results[0].FilePath;
					await Logger.LogVerboseAsync($"[DownloadModFromUrl] Download successful: {downloadedPath}").ConfigureAwait(false);












					return downloadedPath;
				}
				else
				{
					string errorMessage = results.Count > 0 ? results[0].Message : "Unknown error";
					await Logger.LogErrorAsync($"[DownloadModFromUrl] Download failed: {errorMessage}").ConfigureAwait(false);

					try
					{
						Directory.Delete(tempDir, recursive: true);
					}
					catch (Exception ex)
					{
						await Logger.LogWarningAsync($"[DownloadModFromUrl] Failed to clean up temp directory: {ex.Message}").ConfigureAwait(false);
					}

					return null;
				}
			}
			catch (Exception ex)
			{
				await Logger.LogExceptionAsync(ex, $"[DownloadModFromUrl] Exception during download from {url}").ConfigureAwait(false);
				return null;
			}
		}

		public static async Task ProcessDownloadCompletions(
			Dictionary<string, DownloadProgress> urlToProgressMap,
			IReadOnlyList<ModComponent> componentsToDownload)
		{
			if (urlToProgressMap is null || componentsToDownload is null)
				return;

			await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] Processing {urlToProgressMap.Count} downloads for {componentsToDownload.Count} components").ConfigureAwait(false);

			foreach (ModComponent component in componentsToDownload)
			{
				if (component is null || component.ModLinkFilenames is null || component.ModLinkFilenames.Count == 0)
					continue;

				await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] Processing component: {component.Name} (GUID: {component.Guid})").ConfigureAwait(false);

				foreach (string modLink in component.ModLinkFilenames.Keys)
				{
					if (string.IsNullOrWhiteSpace(modLink))
						continue;

					if (!urlToProgressMap.TryGetValue(modLink, out DownloadProgress progress))
					{
						await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] URL not in progress map: {modLink}").ConfigureAwait(false);
						continue;
					}

					if (progress.Status != DownloadStatus.Completed)
					{
						await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] Skipping non-completed download: {modLink} (Status: {progress.Status})").ConfigureAwait(false);
						continue;
					}

					string filePath = progress.FilePath;
					if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
					{
						await Logger.LogWarningAsync($"[ProcessDownloadCompletions] Downloaded file not found: {filePath}").ConfigureAwait(false);
						continue;
					}

					string fileName = Path.GetFileName(filePath);
					bool isArchive = Utility.ArchiveHelper.IsArchive(filePath);

					await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] Processing file: {fileName}, IsArchive: {isArchive}").ConfigureAwait(false);

					bool existsInInstructions = component.Instructions.Any(inst =>
						inst.Source != null && inst.Source.Any(src => src.IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0));

					if (existsInInstructions)
					{
						await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] File already referenced in instructions: {fileName}").ConfigureAwait(false);

						Guid existingExtractGuid = DownloadCacheService.GetExtractInstructionGuid(modLink);

						DownloadCacheEntry cacheEntry = new DownloadCacheEntry
						{
							Url = modLink,
							FileName = fileName,
							IsArchiveFile = isArchive,
							ExtractInstructionGuid = existingExtractGuid,
						};
						DownloadCacheService.AddOrUpdate(modLink, cacheEntry);
						continue;
					}

					if (isArchive)
					{

						Instruction extractInstruction = new Instruction
						{
							Guid = Guid.NewGuid(),
							Action = Instruction.ActionType.Extract,
							Source = new List<string> { $@"<<modDirectory>>\{fileName}" },
							Destination = $@"<<modDirectory>>\{Path.GetFileNameWithoutExtension(fileName)}",
						};
						extractInstruction.SetParentComponent(component);

						component.Instructions.Insert(0, extractInstruction);

						await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] Created Extract instruction for archive: {fileName}").ConfigureAwait(false);

						DownloadCacheEntry cacheEntry = new DownloadCacheEntry
						{
							Url = modLink,
							FileName = fileName,
							IsArchiveFile = true,
							ExtractInstructionGuid = extractInstruction.Guid,
						};
						DownloadCacheService.AddOrUpdate(modLink, cacheEntry);
					}
					else
					{

						await Logger.LogWarningAsync($"[ProcessDownloadCompletions] Downloaded single file (not an archive): {fileName}").ConfigureAwait(false);

						DownloadCacheEntry cacheEntry = new DownloadCacheEntry
						{
							Url = modLink,
							FileName = fileName,
							IsArchiveFile = false,
							ExtractInstructionGuid = Guid.Empty,
						};
						DownloadCacheService.AddOrUpdate(modLink, cacheEntry);
					}
				}
			}

			await Logger.LogVerboseAsync("[ProcessDownloadCompletions] Completed processing download results").ConfigureAwait(false);
		}
	}
}