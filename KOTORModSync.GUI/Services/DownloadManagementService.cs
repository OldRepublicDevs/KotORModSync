// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Services.Download;

namespace KOTORModSync.Services
{
	/// <summary>
	/// Service responsible for managing mod downloads, including cache management and download coordination
	/// </summary>
	public class DownloadManagementService
	{
		private readonly DownloadCacheService _downloadCacheService;
		private DownloadProgressWindow _currentDownloadWindow;

		public DownloadManagementService(DownloadCacheService downloadCacheService)
		{
			_downloadCacheService = downloadCacheService ?? throw new ArgumentNullException(nameof(downloadCacheService));
		}

		/// <summary>
		/// Downloads a mod from a URL using the existing download system
		/// </summary>
		public static async Task<string> DownloadModFromUrl(string url, ModComponent component, CancellationToken cancellationToken = default)
		{
			try
			{
				await Logger.LogVerboseAsync($"[DownloadModFromUrl] Starting download from: {url}");

				// Create a temporary download progress item
				var progress = new DownloadProgress
				{
					ModName = component?.Name ?? "Unknown Mod",
					Url = url,
					Status = DownloadStatus.Pending,
					StatusMessage = "Preparing download...",
					ProgressPercentage = 0
				};

				// Create download manager with all available handlers
				var httpClient = new System.Net.Http.HttpClient();
				var handlers = new List<IDownloadHandler>
				{
					new DeadlyStreamDownloadHandler(httpClient),
					new DirectDownloadHandler(httpClient),
					new GameFrontDownloadHandler(httpClient),
					new NexusModsDownloadHandler(httpClient, MainConfig.NexusModsApiKey),
					new MegaDownloadHandler()
				};
				var downloadManager = new DownloadManager(handlers);

				// Create a temporary directory for the download
				string guidString = Guid.NewGuid().ToString("N");
				string shortGuid = guidString.Substring(0, Math.Min(8, guidString.Length));
				string tempDir = Path.Combine(Path.GetTempPath(), "KOTORModSync_AutoGen_" + shortGuid);
				_ = Directory.CreateDirectory(tempDir);

				await Logger.LogVerboseAsync($"[DownloadModFromUrl] Created temporary directory: {tempDir}");

				// Create progress reporter
				var progressReporter = new Progress<DownloadProgress>(update =>
				{
					progress.Status = update.Status;
					progress.ProgressPercentage = update.ProgressPercentage;
					progress.StatusMessage = update.StatusMessage;
					progress.ErrorMessage = update.ErrorMessage;
					progress.FilePath = update.FilePath;
					progress.BytesDownloaded = update.BytesDownloaded;
					progress.TotalBytes = update.TotalBytes;
				});

				// Download the file
				var urlToProgressMap = new Dictionary<string, DownloadProgress> { { url, progress } };
				List<DownloadResult> results = await downloadManager.DownloadAllWithProgressAsync(urlToProgressMap, tempDir, progressReporter, cancellationToken);

				// Clean up HTTP client
				httpClient.Dispose();

				if ( results.Count > 0 && results[0].Success )
				{
					string downloadedPath = results[0].FilePath;
					await Logger.LogVerboseAsync($"[DownloadModFromUrl] Download successful: {downloadedPath}");
					return downloadedPath;
				}
				else
				{
					string errorMessage = results.Count > 0 ? results[0].Message : "Unknown error";
					await Logger.LogErrorAsync($"[DownloadModFromUrl] Download failed: {errorMessage}");

					// Clean up temp directory
					try
					{
						Directory.Delete(tempDir, recursive: true);
					}
					catch ( Exception ex )
					{
						await Logger.LogWarningAsync($"[DownloadModFromUrl] Failed to clean up temp directory: {ex.Message}");
					}

					return null;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, $"[DownloadModFromUrl] Exception during download from {url}");
				return null;
			}
		}

		/// <summary>
		/// Processes completed downloads to create Extract instructions and update the cache
		/// </summary>
		public async Task ProcessDownloadCompletions(Dictionary<string, DownloadProgress> urlToProgressMap, IReadOnlyList<ModComponent> componentsToDownload)
		{
			if ( urlToProgressMap == null || componentsToDownload == null )
				return;

			await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] Processing {urlToProgressMap.Count} downloads for {componentsToDownload.Count} components");

			foreach ( ModComponent component in componentsToDownload )
			{
				if ( component == null || component.ModLink == null || component.ModLink.Count == 0 )
					continue;

				await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] Processing component: {component.Name} (GUID: {component.Guid})");

				foreach ( string modLink in component.ModLink )
				{
					if ( string.IsNullOrWhiteSpace(modLink) )
						continue;

					// Check if this URL was in our download map
					if ( !urlToProgressMap.TryGetValue(modLink, out DownloadProgress progress) )
					{
						await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] URL not in progress map: {modLink}");
						continue;
					}

					// Only process completed downloads
					if ( progress.Status != DownloadStatus.Completed )
					{
						await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] Skipping non-completed download: {modLink} (Status: {progress.Status})");
						continue;
					}

					// Get the downloaded file path
					string filePath = progress.FilePath;
					if ( string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) )
					{
						await Logger.LogWarningAsync($"[ProcessDownloadCompletions] Downloaded file not found: {filePath}");
						continue;
					}

					string fileName = Path.GetFileName(filePath);
					bool isArchive = DownloadCacheService.IsArchive(filePath);

					await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] Processing file: {fileName}, IsArchive: {isArchive}");

					// Check if this file is already referenced in instructions
					bool existsInInstructions = component.Instructions.Any(inst =>
						inst.Source != null && inst.Source.Any(src => src.IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0));

					if ( existsInInstructions )
					{
						await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] File already referenced in instructions: {fileName}");

						// Preserve existing ExtractInstructionGuid if it exists in cache
						Guid existingExtractGuid = _downloadCacheService.GetExtractInstructionGuid(component.Guid, modLink);

						// Still add to cache even if instruction exists, but preserve the ExtractInstructionGuid
						var cacheEntry = new DownloadCacheEntry
						{
							Url = modLink,
							ArchiveName = fileName,
							FilePath = filePath,
							IsArchive = isArchive,
							ExtractInstructionGuid = existingExtractGuid
						};
						_downloadCacheService.AddOrUpdate(component.Guid, modLink, cacheEntry);
						continue;
					}

					if ( isArchive )
					{
						// Create Extract instruction at index 0
						var extractInstruction = new Instruction
						{
							Guid = Guid.NewGuid(),
							Action = Instruction.ActionType.Extract,
							Source = new List<string> { $@"<<modDirectory>>\{fileName}" },
							Destination = $@"<<modDirectory>>\{Path.GetFileNameWithoutExtension(fileName)}"
						};
						extractInstruction.SetParentComponent(component);

						component.Instructions.Insert(0, extractInstruction);

						await Logger.LogVerboseAsync($"[ProcessDownloadCompletions] Created Extract instruction for archive: {fileName}");

						// Add to cache
						var cacheEntry = new DownloadCacheEntry
						{
							Url = modLink,
							ArchiveName = fileName,
							FilePath = filePath,
							IsArchive = true,
							ExtractInstructionGuid = extractInstruction.Guid
						};
						_downloadCacheService.AddOrUpdate(component.Guid, modLink, cacheEntry);
					}
					else
					{
						// It's a single file - log a warning
						await Logger.LogWarningAsync($"[ProcessDownloadCompletions] Downloaded single file (not an archive): {fileName}");

						// Add to cache without Extract instruction
						var cacheEntry = new DownloadCacheEntry
						{
							Url = modLink,
							ArchiveName = fileName,
							FilePath = filePath,
							IsArchive = false,
							ExtractInstructionGuid = Guid.Empty
						};
						_downloadCacheService.AddOrUpdate(component.Guid, modLink, cacheEntry);
					}
				}
			}

			await Logger.LogVerboseAsync("[ProcessDownloadCompletions] Completed processing download results");
		}

		/// <summary>
		/// Gets or sets the current download window
		/// </summary>
		public DownloadProgressWindow CurrentDownloadWindow
		{
			get => _currentDownloadWindow;
			set => _currentDownloadWindow = value;
		}
	}
}

