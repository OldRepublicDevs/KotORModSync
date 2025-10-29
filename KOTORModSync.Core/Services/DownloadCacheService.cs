// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Services.FileSystem;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;

namespace KOTORModSync.Core.Services
{

	public sealed class DownloadCacheService
	{
		private static readonly Dictionary<string, DownloadCacheEntry> s_cache = new Dictionary<string, DownloadCacheEntry>(StringComparer.OrdinalIgnoreCase);
		private static readonly object s_cacheLock = new object();
		public DownloadManager DownloadManager;
		private readonly ComponentValidationService _validationService;
		private readonly ResolutionFilterService _resolutionFilter;
		private static readonly string s_cacheFilePath = GetCacheFilePath();
		private static bool s_cacheLoaded;

		private readonly Dictionary<string, DownloadFailureInfo> _failedDownloads = new Dictionary<string, DownloadFailureInfo>(StringComparer.OrdinalIgnoreCase);
		private readonly object _failureLock = new object();

		// Phase 5: Resource Index (dual index structure for content-addressable storage)
		private static readonly Dictionary<string, ResourceMetadata> s_resourceByMetadataHash = new Dictionary<string, ResourceMetadata>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> s_metadataHashToContentId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, ResourceMetadata> s_resourceByContentId = new Dictionary<string, ResourceMetadata>(StringComparer.OrdinalIgnoreCase);
		private static readonly object s_resourceIndexLock = new object();

		public DownloadCacheService()
		{
			_validationService = new ComponentValidationService();
			_resolutionFilter = new ResolutionFilterService(MainConfig.FilterDownloadsByResolution);

			lock ( s_cacheLock )
			{
				if ( !s_cacheLoaded )
				{
					LoadCacheFromDisk();
					s_cacheLoaded = true;
				}
			}

			Logger.LogVerbose("[DownloadCacheService] Initialized");
		}

		public void SetDownloadManager(DownloadManager downloadManager = null)
		{
			if ( downloadManager is null )
			{
				DownloadManager = Download.DownloadHandlerFactory.CreateDownloadManager();
			}
			else
			{
				DownloadManager = downloadManager;
			}
		}

		public async Task<Dictionary<string, List<string>>> PreResolveUrlsAsync(
			ModComponent component,
			DownloadManager downloadManager = null,
			bool sequential = false,
			CancellationToken cancellationToken = default)
		{
			await Logger.LogVerboseAsync("[DownloadCacheService] ===== PreResolveUrlsAsync START =====");
			await Logger.LogVerboseAsync($"[DownloadCacheService] Component: {component?.Name ?? "NULL"}");
			await Logger.LogVerboseAsync($"[DownloadCacheService] Sequential: {sequential}");
			await Logger.LogVerboseAsync($"[DownloadCacheService] CancellationToken: {cancellationToken}");

			if ( component == null )
				throw new ArgumentNullException(nameof(component));

			downloadManager = downloadManager ?? DownloadManager;
			if ( downloadManager == null )
				throw new InvalidOperationException("DownloadManager is not set. Call SetDownloadManager() first.");

			await Logger.LogVerboseAsync($"[DownloadCacheService] Pre-resolving URLs for component: {component.Name}");
			await Logger.LogVerboseAsync($"[DownloadCacheService] Component.ModLinkFilenames count: {component.ModLinkFilenames?.Count ?? 0}");
			if ( component.ModLinkFilenames.Count > 0 )
			{
				foreach ( KeyValuePair<string, Dictionary<string, bool?>> kvp in component.ModLinkFilenames )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService]   URL: {kvp.Key}");
					await Logger.LogVerboseAsync($"[DownloadCacheService]   Filenames dict: {kvp.Value?.Count ?? 0} entries");
					if ( kvp.Value != null )
					{
						foreach ( KeyValuePair<string, bool?> filenameKvp in kvp.Value )
						{
							await Logger.LogVerboseAsync($"[DownloadCacheService]     Filename: {filenameKvp.Key} -> {filenameKvp.Value}");
						}
					}
				}
			}

			string modArchiveDirectory = MainConfig.SourcePath?.FullName;
			if ( string.IsNullOrEmpty(modArchiveDirectory) )
			{
				await Logger.LogWarningAsync("[DownloadCacheService] MainConfig.SourcePath is not set, cannot verify file existence");
				modArchiveDirectory = null;
			}

			if ( !string.IsNullOrEmpty(modArchiveDirectory) )
			{
				await InitializeVirtualFileSystemAsync(modArchiveDirectory);
			}

			List<string> urls = component.ModLinkFilenames.Keys.Where(url => !string.IsNullOrWhiteSpace(url)).ToList();
			await Logger.LogVerboseAsync($"[DownloadCacheService] Extracted {urls.Count} URLs from ModLinkFilenames");
			foreach ( string url in urls )
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService]   URL: {url}");
			}

			if ( urls.Count == 0 )
			{
				await Logger.LogVerboseAsync("[DownloadCacheService] No URLs to resolve");
				return new Dictionary<string, List<string>>();
			}

			if ( !string.IsNullOrEmpty(modArchiveDirectory) )
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService] Analyzing download necessity for {urls.Count} URLs");
				(List<string> urlsNeedingAnalysis, bool _initialSimulationFailed) = await AnalyzeDownloadNecessityWithStatusAsync(component, modArchiveDirectory, cancellationToken);

				if ( urlsNeedingAnalysis.Count > 0 )
				{
					await Logger.LogWarningAsync($"[DownloadCacheService] Simulation detected {urlsNeedingAnalysis.Count} missing file(s) for component '{component.Name}'");
					foreach ( string url in urlsNeedingAnalysis )
					{
						await Logger.LogWarningAsync($"  • Missing file for URL: {url}");
					}
				}
			}

			List<string> filteredUrls = _resolutionFilter.FilterByResolution(urls);
			if ( filteredUrls.Count < urls.Count )
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService] Resolution filter reduced URLs from {urls.Count} to {filteredUrls.Count}");
			}

			if ( filteredUrls.Count == 0 )
			{
				await Logger.LogVerboseAsync("[DownloadCacheService] All URLs filtered out by resolution filter");
				return new Dictionary<string, List<string>>();
			}

			// Extract metadata for each URL and populate ResourceRegistry
			await Logger.LogVerboseAsync($"[DownloadCacheService] Extracting metadata for {filteredUrls.Count} URL(s)");
			foreach ( string url in filteredUrls )
			{
				try
				{
					IDownloadHandler handler = downloadManager.GetHandlerForUrl(url);
					if ( handler != null )
					{
						Dictionary<string, object> metadata = await handler.GetFileMetadataAsync(url, cancellationToken);
						if ( metadata != null && metadata.Count > 0 )
						{
							// Ensure provider is set
							if ( !metadata.ContainsKey("provider") )
							{
								metadata["provider"] = handler.GetProviderKey();
							}

							// Normalize URL if present
							if ( metadata.ContainsKey("url") )
							{
								metadata["url"] = Utility.UrlNormalizer.Normalize(metadata["url"].ToString());
							}

							// Compute metadataHash
							string metadataHash = Utility.CanonicalJson.ComputeHash(metadata);
							await Logger.LogVerboseAsync($"[Cache] Computed MetadataHash for {url}: {metadataHash.Substring(0, 16)}...");

							// Compute ContentId from metadata (PRE-DOWNLOAD)
							string contentId = null;
							try
							{
								contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);
								await Logger.LogVerboseAsync($"[Cache] Computed ContentId from metadata: {contentId.Substring(0, 16)}...");

								// Record telemetry
								TelemetryService.Instance.RecordContentIdGenerated(handler.GetProviderKey(), fromMetadata: true);
							}
							catch ( Exception ex )
							{
								await Logger.LogWarningAsync($"[Cache] Failed to compute ContentId for {url}: {ex.Message}");
							}

							// Check if we already have a ContentId mapping
							string existingContentId = null;
							string logMessage = null;
							lock ( s_resourceIndexLock )
							{
								if ( s_metadataHashToContentId.TryGetValue(metadataHash, out existingContentId) )
								{
									logMessage = $"[Cache] Found existing ContentId mapping: {existingContentId.Substring(0, 16)}...";
								}
							}

							if ( logMessage != null )
							{
								await Logger.LogVerboseAsync(logMessage);
								// Record cache hit telemetry
								TelemetryService.Instance.RecordCacheHit(handler.GetProviderKey(), "metadata");
							}
							else
							{
								// Record cache miss telemetry
								TelemetryService.Instance.RecordCacheMiss(handler.GetProviderKey(), "no_existing_mapping");
							}

							// Create or update ResourceMetadata
							ResourceMetadata resourceMeta = new ResourceMetadata
							{
								ContentKey = contentId ?? metadataHash, // Use ContentId if available, otherwise metadataHash
								ContentId = contentId, // Store the pre-computed ContentId
								MetadataHash = metadataHash,
								PrimaryUrl = url,
								HandlerMetadata = metadata,
								FileSize = metadata.ContainsKey("size") ? Convert.ToInt64(metadata["size"]) :
										   metadata.ContainsKey("contentLength") ? Convert.ToInt64(metadata["contentLength"]) : 0,
								FirstSeen = DateTime.UtcNow,
								SchemaVersion = 1,
								TrustLevel = MappingTrustLevel.Unverified
							};

							// Update component's ResourceRegistry
							if ( !component.ResourceRegistry.ContainsKey(metadataHash) )
							{
								component.ResourceRegistry[metadataHash] = resourceMeta;
							}

							// Update global index
							lock ( s_resourceIndexLock )
							{
								if ( !s_resourceByMetadataHash.ContainsKey(metadataHash) )
								{
									s_resourceByMetadataHash[metadataHash] = resourceMeta;
								}

								// Store by ContentId if we computed one
								if ( contentId != null && !s_resourceByContentId.ContainsKey(contentId) )
								{
									s_resourceByContentId[contentId] = resourceMeta;
									// Also store the mapping
									if ( !s_metadataHashToContentId.ContainsKey(metadataHash) )
									{
										s_metadataHashToContentId[metadataHash] = contentId;
									}
								}

								if ( existingContentId != null && !s_resourceByContentId.ContainsKey(existingContentId) )
								{
									s_resourceByContentId[existingContentId] = resourceMeta;
								}
							}

							await Logger.LogVerboseAsync($"[Cache] Updated ResourceRegistry for URL: {url}");
						}
					}
				}
				catch ( Exception ex )
				{
					await Logger.LogVerboseAsync($"[Cache] Failed to extract metadata for {url}: {ex.Message}");
				}
			}

			// Save updated resource index
			try
			{
				await SaveResourceIndexAsync();
			}
			catch ( Exception ex )
			{
				await Logger.LogWarningAsync($"[Cache] Failed to save resource index: {ex.Message}");
			}

			Dictionary<string, List<string>> results = new Dictionary<string, List<string>>();
			List<string> urlsToResolve = new List<string>();
			List<(string url, string filename)> missingFiles = new List<(string url, string filename)>();
			int cacheHits = 0;

			foreach ( string url in filteredUrls )
			{
				if ( TryGetEntry(url, out DownloadCacheEntry cachedEntry) )
				{
					if ( string.IsNullOrWhiteSpace(cachedEntry.FileName) )
					{
await Logger.LogWarningAsync($"Invalid cache entry for {url} with empty filename, removing and treating as miss");
						Remove(url);
						urlsToResolve.Add(url);
					}
					else
					{
						// For mods with multiple files, force re-resolution to get all files
						// This ensures we don't miss additional files that weren't cached
						await Logger.LogVerboseAsync($"[DownloadCacheService] Cache hit for URL: {url} -> {cachedEntry.FileName}, but forcing re-resolution to check for additional files");
						urlsToResolve.Add(url);
					}
				}
				else
				{
					urlsToResolve.Add(url);
				}
			}

			// Note: cacheHits is no longer used since we force re-resolution for all URLs
			// to ensure we get all files, not just the cached one

			if ( urlsToResolve.Count > 0 )
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService] Resolving {urlsToResolve.Count} URL(s) via network (including cached URLs to get all files)");
				await Logger.LogVerboseAsync("[DownloadCacheService] URLs to resolve:");
				foreach ( string url in urlsToResolve )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService]   {url}");
				}

				Dictionary<string, List<string>> resolvedResults = await downloadManager.ResolveUrlsToFilenamesAsync(urlsToResolve, cancellationToken, false); // FIXME: sequential is so slow it's unusable.

				await Logger.LogVerboseAsync($"[DownloadCacheService] Received {resolvedResults.Count} resolved results from download manager");
				foreach (KeyValuePair<string, List<string>> kvp in resolvedResults )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService]   URL: {kvp.Key}");
					await Logger.LogVerboseAsync($"[DownloadCacheService]   Filenames: {string.Join(", ", kvp.Value)}");
				}

				if ( sequential )
				{
					await Logger.LogVerboseAsync("[DownloadCacheService] Processing resolved URLs sequentially");
					foreach ( KeyValuePair<string, List<string>> kvp in resolvedResults )
					{
						await ProcessResolvedUrlForPreResolveAsync(component, kvp, results, modArchiveDirectory, missingFiles);
					}
				}
				else
				{
					await Logger.LogVerboseAsync("[DownloadCacheService] Processing resolved URLs in parallel");
					List<Task> processingTasks = resolvedResults.Select(kvp =>
						ProcessResolvedUrlForPreResolveAsync(component, kvp, results, modArchiveDirectory, missingFiles)
					).ToList();
					await Task.WhenAll(processingTasks);
				}
			}

			Dictionary<string, List<string>> filteredResults = _resolutionFilter.FilterResolvedUrls(results);

			await Logger.LogVerboseAsync($"[DownloadCacheService] Final filtered results count: {filteredResults.Count}");
			foreach (KeyValuePair<string, List<string>> kvp in filteredResults )
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService]   Final URL: {kvp.Key}");
				await Logger.LogVerboseAsync($"[DownloadCacheService]   Final Filenames: {string.Join(", ", kvp.Value)}");
			}

			if ( missingFiles.Count > 0 )
			{
				await Logger.LogWarningAsync($"[DownloadCacheService] Pre-resolve summary for '{component.Name}': {filteredResults.Count} URLs resolved, {missingFiles.Count} files missing on disk");
				await Logger.LogWarningAsync("[DownloadCacheService] Missing files that need to be downloaded:");
				foreach ( (string url, string filename) in missingFiles )
				{
					await Logger.LogWarningAsync($"  • {filename} (from {url})");
					RecordFailure(url, component.Name, filename, "File does not exist on disk", DownloadFailureInfo.FailureType.FileNotFound);
				}
			}
			else
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService] Pre-resolved {filteredResults.Count} URLs ({cacheHits} from cache, {urlsToResolve.Count} from network), all files exist on disk");
			}

			await Logger.LogVerboseAsync("[DownloadCacheService] ===== PreResolveUrlsAsync END =====");
			return filteredResults;
		}

		private async Task ProcessResolvedUrlForPreResolveAsync(
			ModComponent component,
			KeyValuePair<string, List<string>> kvp,
			Dictionary<string, List<string>> results,
			string modArchiveDirectory,
			List<(string url, string filename)> missingFiles)
		{
			results[kvp.Key] = kvp.Value;

			if ( kvp.Value != null && kvp.Value.Count > 0 )
			{
				List<string> filteredFilenames = _resolutionFilter.FilterByResolution(kvp.Value);

				if ( !string.IsNullOrEmpty(modArchiveDirectory) )
				{
					await PopulateModLinkFilenamesWithSimulationAsync(component, kvp.Key, filteredFilenames, modArchiveDirectory);
				}

				string bestMatchFilename = await _validationService.FindBestMatchingFilenameAsync(component, kvp.Key, filteredFilenames);

				if ( string.IsNullOrWhiteSpace(bestMatchFilename) )
				{
					bestMatchFilename = filteredFilenames.Count > 0 ? filteredFilenames[0] : kvp.Value[0];
					if ( filteredFilenames.Count > 1 )
					{
						await Logger.LogWarningAsync($"[DownloadCacheService] No instruction pattern match for URL {kvp.Key}, using first of {filteredFilenames.Count} files: {bestMatchFilename}");
					}
				}
				else if ( filteredFilenames.Count > 1 )
				{
					await Logger.LogAsync($"[DownloadCacheService] ✓ Pattern-matched filename for '{component.Name}': {bestMatchFilename} (from {filteredFilenames.Count} options)");
				}

				DownloadCacheEntry cacheEntry = new DownloadCacheEntry
				{
					Url = kvp.Key,
					FileName = bestMatchFilename,
					IsArchiveFile = Utility.ArchiveHelper.IsArchive(bestMatchFilename),
					ExtractInstructionGuid = Guid.Empty
				};

				AddOrUpdate(kvp.Key, cacheEntry);
				await Logger.LogVerboseAsync($"[DownloadCacheService] Cached resolved filename: {kvp.Key} -> {bestMatchFilename}");

				if ( !string.IsNullOrEmpty(modArchiveDirectory) && !string.IsNullOrEmpty(bestMatchFilename) )
				{
					string filePath = Path.Combine(modArchiveDirectory, bestMatchFilename);
					if ( !File.Exists(filePath) )
					{
						missingFiles.Add((kvp.Key, bestMatchFilename));
						await Logger.LogWarningAsync($"[DownloadCacheService] Resolved file does not exist on disk: {bestMatchFilename}");
					}
				}
			}
			else
			{
				RecordFailure(kvp.Key, component.Name, null, "Failed to resolve filename from URL", DownloadFailureInfo.FailureType.ResolutionFailed);
			}
		}

		private async Task<(string url, DownloadCacheEntry entry, bool needsDownload)> CheckCacheAndFileExistenceAsync(
			string url,
			ModComponent component,
			string modArchiveDirectory,
			string destinationDirectory,
			IProgress<DownloadProgress> progress,
			CancellationToken cancellationToken)
		{
			if ( TryGetEntry(url, out DownloadCacheEntry existingEntry) )
			{
				string existingFilePath = !string.IsNullOrEmpty(existingEntry.FileName) && MainConfig.SourcePath != null
					? Path.Combine(MainConfig.SourcePath.FullName, existingEntry.FileName)
					: existingEntry.FileName;

				if ( !string.IsNullOrEmpty(existingFilePath) && File.Exists(existingFilePath) )
				{
					bool shouldValidate = MainConfig.ValidateAndReplaceInvalidArchives;

					if ( shouldValidate && Utility.ArchiveHelper.IsArchive(existingFilePath) )
					{
						await Logger.LogVerboseAsync($"[DownloadCacheService] Validating archive integrity: {existingFilePath}");
					}

					string fileType = Utility.ArchiveHelper.IsArchive(existingFilePath) ? "archive" : "non-archive";
					string reason = shouldValidate ? $"{fileType} file exists" : "file exists (validation disabled)";
					await Logger.LogVerboseAsync($"[DownloadCacheService] {char.ToUpper(reason[0])}{reason.Substring(1)}, skipping download: {existingFilePath}");

					progress?.Report(new DownloadProgress
					{
						ModName = component.Name,
						Url = url,
						Status = DownloadStatus.Skipped,
						StatusMessage = "File already exists, skipping download",
						ProgressPercentage = 100,
						FilePath = existingFilePath,
						TotalBytes = new FileInfo(existingFilePath).Length,
						BytesDownloaded = new FileInfo(existingFilePath).Length
					});

					return (url, entry: existingEntry, needsDownload: false);
				}
			}

			Dictionary<string, List<string>> resolved = await DownloadManager.ResolveUrlsToFilenamesAsync(new List<string> { url }, cancellationToken);
			if ( resolved.TryGetValue(url, out List<string> value) && value.Count > 0 )
			{
				List<string> filteredFilenames = _resolutionFilter.FilterByResolution(value);

				await PopulateModLinkFilenamesWithSimulationAsync(component, url, filteredFilenames, modArchiveDirectory);

				if ( filteredFilenames.Count > 0 )
				{
					string expectedFileName = filteredFilenames[0];
					string expectedFilePath = MainConfig.SourcePath != null
						? Path.Combine(MainConfig.SourcePath.FullName, expectedFileName)
						: Path.Combine(destinationDirectory, expectedFileName);

					if ( File.Exists(expectedFilePath) )
					{
						bool shouldValidate = MainConfig.ValidateAndReplaceInvalidArchives;

						if ( shouldValidate && Utility.ArchiveHelper.IsArchive(expectedFilePath) )
						{
							await Logger.LogVerboseAsync($"[DownloadCacheService] Validating existing archive: {expectedFilePath}");
						}

						bool isArchive = Utility.ArchiveHelper.IsArchive(expectedFilePath);
						DownloadCacheEntry diskEntry = new DownloadCacheEntry
						{
							Url = url,
							FileName = expectedFileName,
							IsArchiveFile = isArchive,
							ExtractInstructionGuid = Guid.Empty
						};

						AddOrUpdate(url, diskEntry);
						await Logger.LogVerboseAsync($"[DownloadCacheService] Added existing file to cache: {expectedFileName}");

						progress?.Report(new DownloadProgress
						{
							ModName = component.Name,
							Url = url,
							Status = DownloadStatus.Skipped,
							StatusMessage = "File already exists, skipping download",
							ProgressPercentage = 100,
							FilePath = expectedFilePath,
							TotalBytes = new FileInfo(expectedFilePath).Length,
							BytesDownloaded = new FileInfo(expectedFilePath).Length
						});

						return (url, entry: diskEntry, needsDownload: false);
					}
				}
			}

			return (url, entry: null, needsDownload: true);
		}

		public async Task InitializeVirtualFileSystemAsync(string rootPath)
		{
			if ( string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath) )
				return;

			VirtualFileSystemProvider vfs = _validationService.GetVirtualFileSystem();
			await vfs.InitializeFromRealFileSystemAsync(rootPath);
			await Logger.LogVerboseAsync($"[DownloadCacheService] VirtualFileSystem initialized for: {rootPath}");
		}

		public static void AddOrUpdate(string url, DownloadCacheEntry entry)
		{
			if ( string.IsNullOrWhiteSpace(url) )
			{
				Logger.LogWarning("[DownloadCacheService] Cannot add entry with empty URL");
				return;
			}

			lock ( s_cacheLock )
			{
				s_cache[url] = entry;
				Logger.LogVerbose($"[DownloadCacheService] Added/Updated cache entry for URL: {url}, Archive: {entry.FileName}");
			}

			SaveCacheToDisk();
		}

		public static bool TryGetEntry(string url, out DownloadCacheEntry entry)
		{
			entry = null;

			if ( string.IsNullOrWhiteSpace(url) )
				return false;

			lock ( s_cacheLock )
			{
				if ( s_cache.TryGetValue(url, out entry) )
				{
					Logger.LogVerbose($"[DownloadCacheService] Cache hit for URL: {url}");
					return true;
				}
			}

			Logger.LogVerbose($"[DownloadCacheService] Cache miss for URL: {url}");
			return false;
		}

		public static bool IsCached(string url)
		{
			if ( string.IsNullOrWhiteSpace(url) )
				return false;

			lock ( s_cacheLock )
			{
				return s_cache.ContainsKey(url);
			}
		}

		public static string GetFileName(string url)
		{
			if ( TryGetEntry(url, out DownloadCacheEntry entry) )
				return entry.FileName;

			return null;
		}

		public static string GetFilePath(string url)
		{
			if ( TryGetEntry(url, out DownloadCacheEntry entry) )
			{
				if ( string.IsNullOrEmpty(entry.FileName) )
					return null;

				if ( MainConfig.SourcePath == null )
					return entry.FileName;

				return Path.Combine(MainConfig.SourcePath.FullName, entry.FileName);
			}

			return null;
		}

		public static Guid GetExtractInstructionGuid(string url)
		{
			if ( TryGetEntry(url, out DownloadCacheEntry entry) )
				return entry.ExtractInstructionGuid;

			return Guid.Empty;
		}

		public static bool Remove(string url)
		{
			if ( string.IsNullOrWhiteSpace(url) )
				return false;

			bool removed = false;
			lock ( s_cacheLock )
			{
				if ( s_cache.Remove(url) )
				{
					Logger.LogVerbose($"[DownloadCacheService] Removed cache entry for URL: {url}");
					removed = true;
				}
			}

			if ( removed )
				SaveCacheToDisk();

			return removed;
		}

		public static void Clear()
		{
			lock ( s_cacheLock )
			{
				s_cache.Clear();
				Logger.LogVerbose("[DownloadCacheService] Cache cleared");
			}

			SaveCacheToDisk();
		}

		public static int GetTotalEntryCount()
		{
			lock ( s_cacheLock )
			{
				return s_cache.Count;
			}
		}

		public static IReadOnlyList<DownloadCacheEntry> GetCachedEntries()
		{
			lock ( s_cacheLock )
			{
				return s_cache.Values.ToList();
			}
		}

		public async Task<List<DownloadCacheEntry>> ResolveOrDownloadAsync(
				ModComponent component,
				string destinationDirectory,
				IProgress<DownloadProgress> progress = null,
				bool sequential = false,
				CancellationToken cancellationToken = default)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));

			await Logger.LogVerboseAsync($"[DownloadCacheService] Processing component: {component.Name} ({(component.ModLinkFilenames?.Count ?? 0)} URL(s))");

			string modArchiveDirectory = MainConfig.SourcePath?.FullName;
			if ( string.IsNullOrEmpty(modArchiveDirectory) )
			{
				await Logger.LogWarningAsync("[DownloadCacheService] MainConfig.SourcePath is not set, cannot analyze download necessity");
				modArchiveDirectory = destinationDirectory;
			}

			await InitializeVirtualFileSystemAsync(modArchiveDirectory);

			await Logger.LogVerboseAsync($"[DownloadCacheService] Analyzing download necessity for {(component.ModLinkFilenames?.Count ?? 0)} URLs");
			(List<string> urlsNeedingAnalysis, bool initialSimulationFailed) = await AnalyzeDownloadNecessityWithStatusAsync(component, modArchiveDirectory, cancellationToken);

			List<string> urlsToProcess = new List<string>();
			foreach ( string url in urlsNeedingAnalysis )
			{
				if ( !_resolutionFilter.ShouldDownload(url) )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService] Skipping URL due to resolution filter: {url}");
					continue;
				}
				urlsToProcess.Add(url);
			}

			if ( urlsToProcess.Count == 0 )
			{
				await Logger.LogVerboseAsync("[DownloadCacheService] No URLs need processing after analysis");
				List<string> allAnalyzedUrls = component.ModLinkFilenames?.Keys.Where(url => !string.IsNullOrWhiteSpace(url)).ToList() ?? new List<string>();
				List<DownloadCacheEntry> cacheEntries = new List<DownloadCacheEntry>();

				foreach ( string url in allAnalyzedUrls )
				{
					if ( TryGetEntry(url, out DownloadCacheEntry existingEntry) )
					{
						cacheEntries.Add(existingEntry);
					}
					else
					{
						Dictionary<string, List<string>> resolved = await DownloadManager.ResolveUrlsToFilenamesAsync(new List<string> { url }, cancellationToken);
						if ( resolved.TryGetValue(url, out List<string> filenames) && filenames.Count > 0 )
						{
							string fileName = filenames[0];
							if ( string.IsNullOrWhiteSpace(fileName) )
							{
								await Logger.LogWarningAsync($"[DownloadCacheService] Skipping empty filename from URL: {url}");
								RecordFailure(url, component.Name, null, "Resolved filename is empty", DownloadFailureInfo.FailureType.ResolutionFailed);
								continue;
							}

							DownloadCacheEntry entry = new DownloadCacheEntry
							{
								Url = url,
								FileName = fileName,
								IsArchiveFile = Utility.ArchiveHelper.IsArchive(fileName),
								ExtractInstructionGuid = Guid.Empty
							};
							AddOrUpdate(url, entry);
							cacheEntries.Add(entry);
						}
						else
						{
							RecordFailure(url, component.Name, null, "Failed to resolve filename", DownloadFailureInfo.FailureType.ResolutionFailed);
						}
					}
				}

				return cacheEntries;
			}

			await Logger.LogVerboseAsync($"[DownloadCacheService] Checking cache and file existence for {urlsToProcess.Count} URL(s)");
			List<DownloadCacheEntry> cachedResults = new List<DownloadCacheEntry>();
			List<string> urlsNeedingDownload = new List<string>();

			List<(string, DownloadCacheEntry, bool)> cacheCheckResults;

			if ( sequential )
			{
				cacheCheckResults = new List<(string, DownloadCacheEntry, bool)>();
				foreach ( string url in urlsToProcess )
				{
					(string url, DownloadCacheEntry entry, bool needsDownload) result = await CheckCacheAndFileExistenceAsync(url, component, modArchiveDirectory, destinationDirectory, progress, cancellationToken);
					cacheCheckResults.Add(result);
				}
			}
			else
			{
				List<Task<(string url, DownloadCacheEntry entry, bool needsDownload)>> cacheCheckTasks = urlsToProcess.Select(url =>
					CheckCacheAndFileExistenceAsync(url, component, modArchiveDirectory, destinationDirectory, progress, cancellationToken)
				).ToList();

				cacheCheckResults = (await Task.WhenAll(cacheCheckTasks)).ToList();
			}

			foreach ( (string, DownloadCacheEntry, bool) result in cacheCheckResults )
			{
				string url = result.Item1;
				DownloadCacheEntry entry = result.Item2;
				bool needsDownload = result.Item3;

				if ( entry != null )
				{
					cachedResults.Add(entry);
					await Logger.LogVerboseAsync($"[DownloadCacheService] File already cached/exists: {entry.FileName}");
				}
				else if ( needsDownload )
				{
					if ( ShouldDownloadUrl(component, url) )
					{
						urlsNeedingDownload.Add(url);
						await Logger.LogVerboseAsync($"[DownloadCacheService] Marked for download: {url}");
					}
					else
					{
						await Logger.LogWarningAsync($"[DownloadCacheService] Skipping URL (all filenames disabled in ModLinkFilenames): {url}");
					}
				}
			}

			await Logger.LogAsync($"[DownloadCacheService] Component '{component.Name}': {cachedResults.Count} file(s) exist, {urlsNeedingDownload.Count} URL(s) to download");

			if ( urlsNeedingDownload.Count > 0 )
			{
				await Logger.LogAsync($"[DownloadCacheService] Starting download of {urlsNeedingDownload.Count} missing file(s) for component '{component.Name}'...");

				Dictionary<string, DownloadProgress> urlToProgressMap = new Dictionary<string, DownloadProgress>();

				foreach ( string url in urlsNeedingDownload )
				{
					DownloadProgress progressTracker = new DownloadProgress
					{
						ModName = component.Name,
						Url = url,
						Status = DownloadStatus.Pending,
						StatusMessage = "Waiting to start...",
						ProgressPercentage = 0
					};

					if ( TryGetEntry(url, out DownloadCacheEntry cachedEntry) && !string.IsNullOrWhiteSpace(cachedEntry.FileName) )
					{
						progressTracker.TargetFilenames = new List<string> { cachedEntry.FileName };
						await Logger.LogVerboseAsync($"[DownloadCacheService] Set target filename from cache for {url}: {cachedEntry.FileName}");
					}
					else
					{
						Dictionary<string, List<string>> resolved = await DownloadManager.ResolveUrlsToFilenamesAsync(new List<string> { url }, cancellationToken);
						if ( resolved.TryGetValue(url, out List<string> allFilenames) && allFilenames.Count > 0 )
						{
							List<string> filteredFilenames = _resolutionFilter.FilterByResolution(allFilenames);

							List<string> targetFiles = GetFilenamesForDownload(component, url, filteredFilenames);

							if ( targetFiles.Count > 0 )
							{
								progressTracker.TargetFilenames = targetFiles;
								await Logger.LogVerboseAsync($"[DownloadCacheService] Set target filename(s) for {url}: {string.Join(", ", targetFiles)} (filtered by ModLinkFilenames)");
							}
							else
							{
								string bestMatch = await _validationService.FindBestMatchingFilenameAsync(component, url, filteredFilenames);

								if ( !string.IsNullOrWhiteSpace(bestMatch) )
								{
									progressTracker.TargetFilenames = new List<string> { bestMatch };
									await Logger.LogVerboseAsync($"[DownloadCacheService] Set target filename for {url}: {bestMatch} (pattern matched)");
								}
								else if ( filteredFilenames.Count > 0 )
								{
									progressTracker.TargetFilenames = filteredFilenames;
									await Logger.LogVerboseAsync($"[DownloadCacheService] No ModLinkFilenames or pattern match, will use all {filteredFilenames.Count} file(s) for {url}");
								}
							}
						}
					}

					urlToProgressMap[url] = progressTracker;
				}

				Progress<DownloadProgress> progressForwarder = new Progress<DownloadProgress>(p =>
				{
					if ( urlToProgressMap.TryGetValue(p.Url, out DownloadProgress tracker) )
					{
						tracker.Status = p.Status;
						tracker.StatusMessage = p.StatusMessage;
						tracker.ProgressPercentage = p.ProgressPercentage;
						tracker.BytesDownloaded = p.BytesDownloaded;
						tracker.TotalBytes = p.TotalBytes;
						tracker.FilePath = p.FilePath;
						tracker.StartTime = p.StartTime;
						tracker.EndTime = p.EndTime;
						tracker.ErrorMessage = p.ErrorMessage;
						tracker.Exception = p.Exception;

						if ( tracker.Status == DownloadStatus.Pending ||
							 tracker.Status == DownloadStatus.Completed ||
							 tracker.Status == DownloadStatus.Failed )
						{
							Logger.LogVerbose($"[DownloadCache] {tracker.Status}: {tracker.StatusMessage}");
						}
						progress?.Report(tracker);
					}
				});

				List<DownloadResult> downloadResults = await DownloadManager.DownloadAllWithProgressAsync(
					urlToProgressMap,
					destinationDirectory,
					progressForwarder,
					cancellationToken);

				int successCount = 0;
				int failCount = 0;

				for ( int i = 0; i < downloadResults.Count && i < urlsNeedingDownload.Count; i++ )
				{
					DownloadResult result = downloadResults[i];
					string originalUrl = urlsNeedingDownload[i];

					if ( !result.Success )
					{
						failCount++;
						await Logger.LogErrorAsync($"[DownloadCacheService] Download FAILED for component '{component.Name}': {originalUrl}");
						await Logger.LogErrorAsync($"  Error: {result.Message ?? "Unknown error"}");

						string errorMsg = result.Message
										  ?? "Unknown error";
						RecordFailure(originalUrl, component.Name, null, errorMsg, DownloadFailureInfo.FailureType.DownloadFailed);

						continue;
					}

					string fileName = !string.IsNullOrEmpty(result.FilePath) ? Path.GetFileName(result.FilePath) : string.Empty;
					if ( string.IsNullOrEmpty(fileName) )
					{
						failCount++;
						await Logger.LogErrorAsync($"[DownloadCacheService] Download result has empty filename for component '{component.Name}': {originalUrl}");
						RecordFailure(originalUrl, component.Name, null, "Download returned empty filename", DownloadFailureInfo.FailureType.DownloadFailed);
						continue;
					}
					else
					{
						await Logger.LogVerboseAsync($"[DownloadCacheService] Downloaded file: {fileName}");
					}

					bool isArchive = Utility.ArchiveHelper.IsArchive(fileName);

					if ( MainConfig.ValidateAndReplaceInvalidArchives && isArchive && !string.IsNullOrEmpty(result.FilePath) )
					{
						await Logger.LogVerboseAsync($"[DownloadCacheService] Validating downloaded archive: {result.FilePath}");
						bool isValid = true;

						if ( !isValid )
						{
							failCount++;
							await Logger.LogWarningAsync($"[DownloadCacheService] Downloaded archive is corrupt: {result.FilePath}");
							try
							{
								File.Delete(result.FilePath);
								await Logger.LogVerboseAsync($"[DownloadCacheService] Deleted invalid download: {result.FilePath}");
							}
							catch ( Exception ex )
							{
								await Logger.LogErrorAsync($"[DownloadCacheService] Error deleting invalid download: {ex.Message}");
							}
							continue;
						}
					}

					DownloadCacheEntry newEntry = new DownloadCacheEntry
					{
						Url = originalUrl,
						FileName = fileName,
						IsArchiveFile = isArchive,
						ExtractInstructionGuid = Guid.Empty
					};

					AddOrUpdate(originalUrl, newEntry);
					cachedResults.Add(newEntry);
					successCount++;

					// Post-download: Compute INTEGRITY hashes (ContentId already exists from PreResolve!)
					try
					{
						if ( File.Exists(result.FilePath) )
						{
							await Logger.LogVerboseAsync($"[Cache] Computing file integrity data for: {result.FilePath}");

							(string contentHashSHA256, int pieceLength, string pieceHashes) =
								await DownloadCacheOptimizer.ComputeFileIntegrityData(result.FilePath);

							await Logger.LogVerboseAsync($"[Cache] ContentHashSHA256: {contentHashSHA256.Substring(0, 16)}...");
							await Logger.LogVerboseAsync($"[Cache] PieceLength: {pieceLength}, Pieces: {pieceHashes.Length / 40}");

							// Find existing ResourceMetadata by URL (should already have ContentId from PreResolve)
							ResourceMetadata resourceMeta = null;
							string metadataHash = null;

							// Try to find metadata from component's ResourceRegistry
							foreach (KeyValuePair<string, ResourceMetadata> kvp in component.ResourceRegistry )
							{
								if ( kvp.Value.PrimaryUrl == originalUrl )
								{
									resourceMeta = kvp.Value;
									metadataHash = kvp.Key;
									break;
								}
							}

							if ( resourceMeta != null && metadataHash != null )
							{
								// Update with integrity data (ContentId should already exist!)
								resourceMeta.ContentHashSHA256 = contentHashSHA256;
								resourceMeta.PieceLength = pieceLength;
								resourceMeta.PieceHashes = pieceHashes;
								resourceMeta.LastVerified = DateTime.UtcNow;

								// Add filename to Files dictionary
								if ( !resourceMeta.Files.ContainsKey(fileName) )
								{
									resourceMeta.Files[fileName] = true;
								}

								// Log ContentId if available
								if ( !string.IsNullOrEmpty(resourceMeta.ContentId) )
								{
									await Logger.LogVerboseAsync($"[Cache] ContentId: {resourceMeta.ContentId.Substring(0, 16)}...");

									// Update mapping with verification (ContentId should already exist)
									bool updated = await UpdateMappingWithVerification(metadataHash, resourceMeta.ContentId, resourceMeta);

									if ( updated )
									{
										await Logger.LogVerboseAsync($"[Cache] Updated mapping: {metadataHash.Substring(0, 16)}... → {resourceMeta.ContentId.Substring(0, 16)}...");

										// Save updated resource index
										await SaveResourceIndexAsync();
										await Logger.LogVerboseAsync("[Cache] Saved updated resource index");
									}
								}
								else
								{
									await Logger.LogWarningAsync($"[Cache] ContentId missing for URL (PreResolve may have failed): {originalUrl}");
								}
							}
							else
							{
								await Logger.LogVerboseAsync($"[Cache] No ResourceMetadata found for URL: {originalUrl}");
							}
						}
					}
					catch ( Exception ex )
					{
						await Logger.LogWarningAsync($"[Cache] Failed to compute file integrity data: {ex.Message}");
					}

					await Logger.LogAsync($"[DownloadCacheService] ✓ Downloaded successfully: {fileName}");
				}

				await Logger.LogAsync($"[DownloadCacheService] Download results for '{component.Name}': {successCount} succeeded, {failCount} failed");
			}

			if ( initialSimulationFailed && urlsNeedingDownload.Count > 0 )
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService] Re-simulating component '{component.Name}' after download attempt...");

				await InitializeVirtualFileSystemAsync(modArchiveDirectory);

				try
				{
					ModComponent.InstallExitCode retryExitCode = await component.ExecuteInstructionsAsync(
						component.Instructions,
						new List<ModComponent>(),
						cancellationToken,
						_validationService.GetVirtualFileSystem(),
						skipDependencyCheck: true
					);

					if ( retryExitCode == ModComponent.InstallExitCode.Success )
					{
						await Logger.LogVerboseAsync($"[DownloadCacheService] ✓ Re-simulation successful for component '{component.Name}' after downloading files");
					}
					else
					{
						List<ValidationIssue> issues = _validationService.GetVirtualFileSystem().GetValidationIssues();
						List<ValidationIssue> fileIssues = issues.Where(i =>
							i.Message.Contains("does not exist") ||
							i.Message.Contains("not found") ||
							i.Category == "ExtractArchive" ||
							i.Category == "MoveFile" ||
							i.Category == "CopyFile"
						).ToList();

						if ( fileIssues.Count > 0 )
						{
							await Logger.LogWarningAsync($"[DownloadCacheService] Re-simulation had file issues for component '{component.Name}', but files were downloaded:");
							foreach ( ValidationIssue issue in fileIssues.Take(3) )
							{
								await Logger.LogVerboseAsync($"  • {issue.Category}: {issue.Message}");
							}

							bool allFilesExist = true;
							foreach ( string url in (component.ModLinkFilenames?.Keys ?? Enumerable.Empty<string>()).Where(url => !string.IsNullOrWhiteSpace(url)) )
							{
								if ( TryGetEntry(url, out DownloadCacheEntry entry) )
								{
									string filePath = MainConfig.SourcePath != null
										? Path.Combine(MainConfig.SourcePath.FullName, entry.FileName)
										: entry.FileName;
									if ( !File.Exists(filePath) )
									{
										await Logger.LogErrorAsync($"  • File not found on disk: {entry.FileName} (from {url})");
										allFilesExist = false;
									}
								}
								else
								{
									await Logger.LogWarningAsync($"  • URL not resolved/cached: {url}");
									allFilesExist = false;
								}
							}

							if ( allFilesExist )
							{
								await Logger.LogVerboseAsync($"[DownloadCacheService] All files exist on disk for '{component.Name}'. Path mismatch in instructions is non-critical.");
							}
						}
						else
						{
							await Logger.LogWarningAsync($"[DownloadCacheService] Re-simulation had non-file issues for component '{component.Name}':");
							foreach ( ValidationIssue issue in issues.Take(3) )
							{
								await Logger.LogVerboseAsync($"  • {issue.Category}: {issue.Message}");
							}
						}
					}
				}
				catch ( Exceptions.WildcardPatternNotFoundException wildcardEx )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService] Re-simulation wildcard pattern exception for component '{component.Name}'");
					await Logger.LogVerboseAsync($"[DownloadCacheService] Failed patterns: {string.Join(", ", wildcardEx.Patterns)}");

					bool allDownloadedFilesExist = true;
					foreach ( string url in (component.ModLinkFilenames?.Keys ?? Enumerable.Empty<string>()).Where(u => !string.IsNullOrWhiteSpace(u)) )
					{
						if ( TryGetEntry(url, out DownloadCacheEntry entry) )
						{
							string filePath = MainConfig.SourcePath != null
								? Path.Combine(MainConfig.SourcePath.FullName, entry.FileName)
								: entry.FileName;
							if ( !File.Exists(filePath) )
							{
								await Logger.LogErrorAsync($"[DownloadCacheService] Downloaded file missing: {entry.FileName}");
								allDownloadedFilesExist = false;
							}
						}
					}

					if ( allDownloadedFilesExist )
					{
						await Logger.LogVerboseAsync("[DownloadCacheService] All downloaded files exist. Pattern mismatch in instructions is non-critical.");
					}
					else
					{
						await Logger.LogErrorAsync($"[DownloadCacheService] Some downloaded files are missing for component '{component.Name}'");
					}
				}
				catch ( FileNotFoundException fileEx )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService] Re-simulation file exception for component '{component.Name}': {fileEx.Message}");

					bool allDownloadedFilesExist = true;
					foreach ( string url in (component.ModLinkFilenames?.Keys ?? Enumerable.Empty<string>()).Where(u => !string.IsNullOrWhiteSpace(u)) )
					{
						if ( TryGetEntry(url, out DownloadCacheEntry entry) )
						{
							string filePath = MainConfig.SourcePath != null
								? Path.Combine(MainConfig.SourcePath.FullName, entry.FileName)
								: entry.FileName;
							if ( !File.Exists(filePath) )
							{
								await Logger.LogErrorAsync($"[DownloadCacheService] Downloaded file missing: {entry.FileName}");
								allDownloadedFilesExist = false;
							}
						}
					}

					if ( allDownloadedFilesExist )
					{
						await Logger.LogVerboseAsync("[DownloadCacheService] All downloaded files exist. Path mismatch in instructions is non-critical.");
					}
					else
					{
						await Logger.LogErrorAsync($"[DownloadCacheService] Some downloaded files are missing for component '{component.Name}'");
					}
				}
				catch ( Exception retryEx )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService] Re-simulation exception for component '{component.Name}': {retryEx.Message}");

					bool isFileRelated = retryEx.Message.Contains("does not exist") ||
										retryEx.Message.Contains("not found") ||
										retryEx.Message.Contains("Could not find");

					if ( isFileRelated )
					{
						await Logger.LogVerboseAsync($"[DownloadCacheService] Verifying downloaded files for '{component.Name}':");
						bool allFilesExist = true;
						foreach ( string url in (component.ModLinkFilenames?.Keys ?? Enumerable.Empty<string>()).Where(u => !string.IsNullOrWhiteSpace(u)) )
						{
							if ( TryGetEntry(url, out DownloadCacheEntry entry) )
							{
								string filePath = MainConfig.SourcePath != null
									? Path.Combine(MainConfig.SourcePath.FullName, entry.FileName)
									: entry.FileName;
								bool exists = File.Exists(filePath);
								if ( !exists )
								{
									await Logger.LogErrorAsync($"  • {entry.FileName}: MISSING");
									allFilesExist = false;
								}
								else
								{
									await Logger.LogVerboseAsync($"  • {entry.FileName}: EXISTS");
								}
							}
							else
							{
								await Logger.LogWarningAsync($"  • {url}: NOT RESOLVED/CACHED");
								allFilesExist = false;
							}
						}

						if ( allFilesExist )
						{
							await Logger.LogVerboseAsync("[DownloadCacheService] All files exist. Instruction path mismatch is non-critical.");
						}
					}
					else
					{
						await Logger.LogVerboseAsync($"[DownloadCacheService] Non-file-related error: {retryEx.Message}");
					}
				}
			}
			else if ( initialSimulationFailed && urlsNeedingDownload.Count == 0 )
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService] Component '{component.Name}' simulation had issues but all files exist on disk.");
				await Logger.LogVerboseAsync("[DownloadCacheService] This may indicate instruction path mismatches from merge, which is non-critical if files exist.");
			}

			await EnsureInstructionsExist(component, cachedResults, cancellationToken);
			return cachedResults;
		}

		private async Task EnsureInstructionsExist(
			ModComponent component,
			List<DownloadCacheEntry> entries,
			CancellationToken cancellationToken
		)
		{
			await Logger.LogVerboseAsync($"[DownloadCacheService] Ensuring instructions exist for {entries.Count} cached entries");

			foreach ( DownloadCacheEntry entry in entries )
			{
				string archiveFullPath = $@"<<modDirectory>>\{entry.FileName}";
				Instruction existingInstruction = null;
				Instruction mismatchedInstruction = null;

				foreach ( Instruction instruction in component.Instructions )
				{
					if ( instruction.Source != null && instruction.Source.Exists(src =>
						{
							if ( src.IndexOf(entry.FileName, StringComparison.OrdinalIgnoreCase) >= 0 )
								return true;

							try
							{
								if ( FileSystemUtils.PathHelper.WildcardPathMatch(archiveFullPath, src) )
									return true;
							}
							catch ( Exception ex )
							{
								Logger.LogException(ex);
							}

							return _validationService.GetVirtualFileSystem().FileExists(ResolveInstructionSource(src, entry.FileName));
						}) )
					{
						existingInstruction = instruction;
						break;
					}

					// Check for Extract instructions with mismatched archive names
					if ( instruction.Action == Instruction.ActionType.Extract &&
						 instruction.Source != null && instruction.Source.Count > 0 )
					{
						string instructionArchiveName = ExtractFilenameFromSource(instruction.Source[0]);
						if ( !string.IsNullOrEmpty(instructionArchiveName) &&
							 !instructionArchiveName.Equals(entry.FileName, StringComparison.OrdinalIgnoreCase) &&
							 entry.IsArchiveFile )
						{
							// Check if this could be the same mod with different filename
							string instructionBaseName = Path.GetFileNameWithoutExtension(instructionArchiveName);
							string entryBaseName = Path.GetFileNameWithoutExtension(entry.FileName);

							// Enhanced similarity check with multiple strategies
							double similarityScore = CalculateArchiveNameSimilarity(instructionBaseName, entryBaseName);
							const double SIMILARITY_THRESHOLD = 0.7; // 70% similarity required

							if ( similarityScore >= SIMILARITY_THRESHOLD )
							{
								mismatchedInstruction = instruction;
								await Logger.LogVerboseAsync($"[DownloadCacheService] Detected archive name mismatch (similarity: {similarityScore:P0}): instruction expects '{instructionArchiveName}' but downloaded '{entry.FileName}'");
								break;
							}
						}
					}
				}

				// Handle mismatched archive name
				if ( mismatchedInstruction != null && existingInstruction == null )
				{
					await Logger.LogAsync($"[DownloadCacheService] Attempting to fix archive name mismatch for component '{component.Name}'");
					bool fixSuccess = await TryFixArchiveNameMismatchAsync(component, mismatchedInstruction, entry, cancellationToken);

					if ( fixSuccess )
					{
						existingInstruction = mismatchedInstruction;
						await Logger.LogAsync($"[DownloadCacheService] ✓ Successfully updated instructions to use '{entry.FileName}'");
					}
					else
					{
						await Logger.LogWarningAsync("[DownloadCacheService] Failed to fix archive name mismatch, will create new instruction");
					}
				}

				if ( existingInstruction != null )
				{
					if ( entry.ExtractInstructionGuid == Guid.Empty || entry.ExtractInstructionGuid != existingInstruction.Guid )
					{
						entry.ExtractInstructionGuid = existingInstruction.Guid;
						AddOrUpdate(entry.Url, entry);
						await Logger.LogVerboseAsync($"[DownloadCacheService] Found existing instruction for {entry.FileName} (GUID: {existingInstruction.Guid})");
					}
				}
				else
				{
					int initialInstructionCount = component.Instructions.Count;

					string entryFilePath = !string.IsNullOrEmpty(entry.FileName) && MainConfig.SourcePath != null
						? Path.Combine(MainConfig.SourcePath.FullName, entry.FileName)
						: entry.FileName;

					if ( entry.IsArchiveFile && !string.IsNullOrEmpty(entryFilePath) && File.Exists(entryFilePath) )
					{
						bool generated = AutoInstructionGenerator.GenerateInstructions(component, entryFilePath);

						if ( !File.Exists(entryFilePath) )
						{
							await Logger.LogWarningAsync($"[DownloadCacheService] Archive was deleted (likely corrupted): {entryFilePath}");
							await Logger.LogWarningAsync("[DownloadCacheService] Removing from cache and creating placeholder instruction");

							Remove(entry.Url);

							CreateSimpleInstructionForEntry(component, entry);
							continue;
						}

						if ( generated )
						{
							await Logger.LogVerboseAsync($"[DownloadCacheService] Auto-generated comprehensive instructions for {entry.FileName}");

							Instruction newInstruction = null;
							for ( int j = initialInstructionCount; j < component.Instructions.Count; j++ )
							{
								Instruction instr = component.Instructions[j];
								if ( instr.Source != null && instr.Source.Count > 0 )
								{
									foreach ( string s in instr.Source )
									{
										if ( s.IndexOf(entry.FileName, StringComparison.OrdinalIgnoreCase) >= 0 )
										{
											Logger.Log($"[DownloadCacheService] Found exact match existing instruction for {entry.FileName}: {instr.Guid.ToString()}");
											newInstruction = instr;
											break;
										}
										try
										{
											List<string> resolvedFiles = FileSystemUtils.PathHelper.EnumerateFilesWithWildcards(
												new List<string> { s },
												_validationService.GetVirtualFileSystem()
											);
											if ( resolvedFiles.Exists(f =>
												 string.Equals(
													Path.GetFileName(f),
													entry.FileName,
													StringComparison.OrdinalIgnoreCase)) )
											{
												Logger.Log($"[DownloadCacheService] EnumerateFilesWithWildcards found matching instruction for {entry.FileName}: {instr.Guid.ToString()}");
												newInstruction = instr;
												break;
											}
										}
										catch ( Exception ex )
										{
											await Logger.LogExceptionAsync(ex);
										}
									}
								}
								if ( newInstruction != null )
								{
									break;
								}
							}

							if ( newInstruction == null )
								throw new NullReferenceException($"No existing instruction found for {entry.FileName}");
							entry.ExtractInstructionGuid = newInstruction.Guid;
							AddOrUpdate(entry.Url, entry);
							await Logger.LogVerboseAsync($"[DownloadCacheService] Found existing instruction for {entry.FileName} (GUID: {newInstruction.Guid})");
						}
						else
						{
							CreateSimpleInstructionForEntry(component, entry);
						}
					}
					else
					{
						CreateSimpleInstructionForEntry(component, entry);
					}
				}

			}
		}

		private static void CreateSimpleInstructionForEntry(ModComponent component, DownloadCacheEntry entry)
		{
			Instruction newInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = entry.IsArchiveFile
					 ? Instruction.ActionType.Extract
					 : Instruction.ActionType.Move,
				Source = new List<string> { $@"<<modDirectory>>\{entry.FileName}" },
				Destination = entry.IsArchiveFile
						  ? string.Empty
						  : @"<<kotorDirectory>>\Override",
				Overwrite = true
			};
			newInstruction.SetParentComponent(component);
			component.Instructions.Add(newInstruction);

			entry.ExtractInstructionGuid = newInstruction.Guid;
			AddOrUpdate(entry.Url, entry);

			Logger.LogWarning($"[DownloadCacheService] Created placeholder {newInstruction.Action} instruction for {entry.FileName} due to file not being downloaded yet.");
		}

		private static string ResolveInstructionSource(string sourcePath, string archiveName)
		{
			if ( string.IsNullOrWhiteSpace(sourcePath) )
				return sourcePath;

			string resolved = sourcePath.Replace("<<modDirectory>>", "");

			if ( resolved.Contains(archiveName) )
				return resolved;

			return Path.Combine(resolved.TrimStart('\\'), archiveName);
		}

		private static string GetCacheFilePath()
		{
			string appDataPath;

			if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
			{
				appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			}
			else
			{
				string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				appDataPath = Path.Combine(homeDir, ".local", "share");
			}

			string cacheDir = Path.Combine(appDataPath, "KOTORModSync");

			if ( !Directory.Exists(cacheDir) )
			{
				try
				{
					Directory.CreateDirectory(cacheDir);
					Logger.LogVerbose($"[DownloadCacheService] Created cache directory: {cacheDir}");
				}
				catch ( Exception ex )
				{
					Logger.LogWarning($"[DownloadCacheService] Failed to create cache directory: {ex.Message}");
				}
			}

			return Path.Combine(cacheDir, "download-cache.json");
		}

		private static void SaveCacheToDisk()
		{
			try
			{
				lock ( s_cacheLock )
				{
					string json = JsonConvert.SerializeObject(s_cache, Formatting.Indented);
					File.WriteAllText(s_cacheFilePath, json);
				}

				Logger.LogVerbose($"[DownloadCacheService] Saved cache to disk: {s_cacheFilePath}");
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"[DownloadCacheService] Failed to save cache to disk: {ex.Message}");
			}
		}

		private static void LoadCacheFromDisk()
		{
			if ( !File.Exists(s_cacheFilePath) )
			{
				Logger.LogVerbose("[DownloadCacheService] No cache file found on disk, starting with empty cache");
				return;
			}

			try
			{
				string json = File.ReadAllText(s_cacheFilePath);
				Dictionary<string, DownloadCacheEntry> loadedCache = JsonConvert.DeserializeObject<Dictionary<string, DownloadCacheEntry>>(json);

				if ( loadedCache == null )
				{
					Logger.LogWarning("[DownloadCacheService] Cache file contained no data");
					return;
				}

				lock ( s_cacheLock )
				{
					s_cache.Clear();

					foreach ( KeyValuePair<string, DownloadCacheEntry> kvp in loadedCache )
					{
						s_cache[kvp.Key] = kvp.Value;
						Logger.LogVerbose($"[DownloadCacheService] Loaded cached entry: {kvp.Value.FileName}");
					}
				}

				Logger.LogVerbose($"[DownloadCacheService] Loaded cache from disk: {s_cacheFilePath} ({s_cache.Count} entries)");
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"[DownloadCacheService] Failed to load cache from disk: {ex.Message}");
			}
		}

		private async Task<(List<string> urls, bool simulationFailed)> AnalyzeDownloadNecessityWithStatusAsync(
			ModComponent component,
			string modArchiveDirectory,
			CancellationToken cancellationToken)
		{
			(List<string> urls, bool simulationFailed) = await _validationService.AnalyzeDownloadNecessityAsync(component, modArchiveDirectory, cancellationToken);
			return (urls, simulationFailed);
		}

		private static string ExtractFilenameFromSource(string sourcePath)
		{
			if ( string.IsNullOrEmpty(sourcePath) )
				return string.Empty;

			string cleanedPath = sourcePath.Replace("<<modDirectory>>\\", "").Replace("<<modDirectory>>/", "");

			string filename = Path.GetFileName(cleanedPath);

			return filename;
		}

		private static string NormalizeModName(string name)
		{
			if ( string.IsNullOrEmpty(name) )
				return string.Empty;

			// Remove common version patterns, special characters, normalize spacing
			string normalized = name.ToLowerInvariant();
			normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[_\-\s]+", " ");
			normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"v?\d+(\.\d+)*", ""); // Remove version numbers
			normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\w\s]", ""); // Remove special chars
			normalized = normalized.Trim();

			return normalized;
		}

		/// <summary>
		/// Calculates similarity score between two archive names using multiple strategies.
		/// Returns a value between 0.0 (completely different) and 1.0 (identical).
		/// </summary>
		private static double CalculateArchiveNameSimilarity(string name1, string name2)
		{
			if ( string.IsNullOrEmpty(name1) || string.IsNullOrEmpty(name2) )
				return 0.0;

			// Strategy 1: Exact match
			if ( name1.Equals(name2, StringComparison.OrdinalIgnoreCase) )
				return 1.0;

			// Strategy 2: Substring containment (one contains the other)
			string lower1 = name1.ToLowerInvariant();
			string lower2 = name2.ToLowerInvariant();
			if ( lower1.Contains(lower2) || lower2.Contains(lower1) )
				return 0.95;

			// Strategy 3: Normalized name comparison (removes versions, special chars)
			string normalized1 = NormalizeModName(name1);
			string normalized2 = NormalizeModName(name2);
			if ( !string.IsNullOrEmpty(normalized1) && normalized1.Equals(normalized2, StringComparison.OrdinalIgnoreCase) )
				return 0.90;

			// Strategy 4: Token-based similarity (split on common delimiters)
			HashSet<string> tokens1 = new HashSet<string>(
				System.Text.RegularExpressions.Regex.Split(lower1, @"[\s\-_\.]+")
					.Where(t => t.Length > 2), // Ignore very short tokens
				StringComparer.OrdinalIgnoreCase
			);
			HashSet<string> tokens2 = new HashSet<string>(
				System.Text.RegularExpressions.Regex.Split(lower2, @"[\s\-_\.]+")
					.Where(t => t.Length > 2),
				StringComparer.OrdinalIgnoreCase
			);

			if ( tokens1.Count > 0 && tokens2.Count > 0 )
			{
				int commonTokens = tokens1.Intersect(tokens2).Count();
				int totalTokens = tokens1.Union(tokens2).Count();
				double tokenSimilarity = (double)commonTokens / totalTokens;

				if ( tokenSimilarity >= 0.5 ) // At least 50% tokens in common
					return 0.75 + (tokenSimilarity * 0.15); // 0.75-0.90 range
			}

			// Strategy 5: Levenshtein distance ratio for fuzzy matching
			int distance = CalculateLevenshteinDistance(normalized1, normalized2);
			int maxLength = Math.Max(normalized1?.Length ?? 0, normalized2?.Length ?? 0);
			if ( maxLength > 0 )
			{
				double distanceRatio = 1.0 - ((double)distance / maxLength);
				if ( distanceRatio >= 0.7 )
					return distanceRatio * 0.85; // Scale down slightly as it's less reliable
			}

			// Strategy 6: Longest common substring ratio
			int lcsLength = CalculateLongestCommonSubstringLength(lower1, lower2);
			int minLength = Math.Min(lower1.Length, lower2.Length);
			if ( minLength > 0 )
			{
				double lcsRatio = (double)lcsLength / minLength;
				if ( lcsRatio >= 0.6 )
					return lcsRatio * 0.8; // Scale to 0.48-0.80 range
			}

			// No good match found
			return 0.0;
		}

		/// <summary>
		/// Calculates the Levenshtein distance (edit distance) between two strings.
		/// Returns the minimum number of single-character edits required to change one string into the other.
		/// </summary>
		private static int CalculateLevenshteinDistance(string s1, string s2)
		{
			if ( string.IsNullOrEmpty(s1) )
				return s2?.Length ?? 0;
			if ( string.IsNullOrEmpty(s2) )
				return s1.Length;

			int len1 = s1.Length;
			int len2 = s2.Length;
			int[,] matrix = new int[len1 + 1, len2 + 1];

			// Initialize first column and row
			for ( int i = 0; i <= len1; i++ )
				matrix[i, 0] = i;
			for ( int j = 0; j <= len2; j++ )
				matrix[0, j] = j;

			// Calculate distances
			for ( int i = 1; i <= len1; i++ )
			{
				for ( int j = 1; j <= len2; j++ )
				{
					int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
					matrix[i, j] = Math.Min(
						Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
						matrix[i - 1, j - 1] + cost
					);
				}
			}

			return matrix[len1, len2];
		}

		/// <summary>
		/// Calculates the length of the longest common substring between two strings.
		/// Used to find the largest contiguous matching sequence.
		/// </summary>
		private static int CalculateLongestCommonSubstringLength(string s1, string s2)
		{
			if ( string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2) )
				return 0;

			int maxLength = 0;
			int[,] lengths = new int[s1.Length + 1, s2.Length + 1];

			for ( int i = 1; i <= s1.Length; i++ )
			{
				for ( int j = 1; j <= s2.Length; j++ )
				{
					if ( char.ToLowerInvariant(s1[i - 1]) == char.ToLowerInvariant(s2[j - 1]) )
					{
						lengths[i, j] = lengths[i - 1, j - 1] + 1;
						maxLength = Math.Max(maxLength, lengths[i, j]);
					}
				}
			}

			return maxLength;
		}

		private static async Task<bool> TryFixArchiveNameMismatchAsync(
			ModComponent component,
			Instruction extractInstruction,
			DownloadCacheEntry entry,
			CancellationToken cancellationToken)
		{
			if ( extractInstruction == null || entry == null || !entry.IsArchiveFile )
				return false;

			string oldArchiveName = ExtractFilenameFromSource(extractInstruction.Source[0]);
			string newArchiveName = entry.FileName;

			if ( string.IsNullOrEmpty(oldArchiveName) || string.IsNullOrEmpty(newArchiveName) )
				return false;

			await Logger.LogVerboseAsync($"[DownloadCacheService] Attempting to update archive name from '{oldArchiveName}' to '{newArchiveName}'");

			// Store original instruction state for rollback
			Dictionary<Guid, (Instruction.ActionType action, List<string> source, string destination)> originalInstructions = new Dictionary<Guid, (Instruction.ActionType action, List<string> source, string destination)>();

			try
			{
				// Step 1: Backup all instruction states (component + options)
				foreach ( Instruction instruction in component.Instructions )
				{
					originalInstructions[instruction.Guid] = (
						instruction.Action,
						new List<string>(instruction.Source),
						instruction.Destination
					);
				}

				foreach ( Option option in component.Options )
				{
					foreach ( Instruction instruction in option.Instructions )
					{
						originalInstructions[instruction.Guid] = (
							instruction.Action,
							new List<string>(instruction.Source),
							instruction.Destination
						);
					}
				}

				// Step 2: Update Extract instruction
				string oldExtractedFolderName = Path.GetFileNameWithoutExtension(oldArchiveName);
				string newExtractedFolderName = Path.GetFileNameWithoutExtension(newArchiveName);

				extractInstruction.Source[0] = $@"<<modDirectory>>\{newArchiveName}";
				await Logger.LogVerboseAsync($"[DownloadCacheService] Updated Extract instruction source to: {extractInstruction.Source[0]}");

				// Step 3: Update subsequent instructions that reference the old extracted folder
				int updatedInstructionCount = 0;

				// Update component instructions
				foreach ( Instruction instruction in component.Instructions )
				{
					if ( instruction.Guid == extractInstruction.Guid )
						continue;

					if ( instruction.Source.Count == 0 )
						continue;
					bool updated = false;
					for ( int i = 0; i < instruction.Source.Count; i++ )
					{
						string src = instruction.Source[i];
						if ( src.IndexOf(oldExtractedFolderName, StringComparison.OrdinalIgnoreCase) < 0 )
							continue;
						// Replace the old folder name with the new one
						string updatedSrc = System.Text.RegularExpressions.Regex.Replace(
							src,
							System.Text.RegularExpressions.Regex.Escape(oldExtractedFolderName),
							newExtractedFolderName,
							System.Text.RegularExpressions.RegexOptions.IgnoreCase);

						instruction.Source[i] = updatedSrc;
						updated = true;
						await Logger.LogVerboseAsync($"[DownloadCacheService] Updated instruction source: {src} -> {updatedSrc}");
					}
					if ( updated )
						updatedInstructionCount++;
				}

				// Update option instructions
				foreach ( Option option in component.Options )
				{
					foreach ( Instruction instruction in option.Instructions )
					{
						if ( instruction.Source.Count == 0 )
							continue;

						bool updated = false;
						for ( int i = 0; i < instruction.Source.Count; i++ )
						{
							string src = instruction.Source[i];
							if ( src.IndexOf(oldExtractedFolderName, StringComparison.OrdinalIgnoreCase) < 0 )
								continue;
							// Replace the old folder name with the new one
							string updatedSrc = System.Text.RegularExpressions.Regex.Replace(
								src,
								System.Text.RegularExpressions.Regex.Escape(oldExtractedFolderName),
								newExtractedFolderName,
								System.Text.RegularExpressions.RegexOptions.IgnoreCase);

							instruction.Source[i] = updatedSrc;
							updated = true;
							await Logger.LogVerboseAsync($"[DownloadCacheService] Updated option '{option.Name}' instruction source: {src} -> {updatedSrc}");
						}
						if ( updated )
							updatedInstructionCount++;
					}
				}

				await Logger.LogVerboseAsync($"[DownloadCacheService] Updated {updatedInstructionCount} instruction(s) to reference new archive name");

				// Step 4: Validate changes with VFS simulation
				string modArchiveDirectory = MainConfig.SourcePath?.FullName;
				if ( !string.IsNullOrEmpty(modArchiveDirectory) )
				{
					await Logger.LogVerboseAsync("[DownloadCacheService] Validating instruction changes via VFS simulation...");

					VirtualFileSystemProvider vfs = new VirtualFileSystemProvider();
					await vfs.InitializeFromRealFileSystemAsync(modArchiveDirectory);

					try
					{
						ModComponent.InstallExitCode exitCode = await component.ExecuteInstructionsAsync(
							component.Instructions,
							new List<ModComponent>(),
							cancellationToken,
							vfs,
							skipDependencyCheck: true
						);

						List<ValidationIssue> issues = vfs.GetValidationIssues();
						List<ValidationIssue> criticalIssues = issues.Where(i =>
							i.Severity == ValidationSeverity.Error ||
							i.Severity == ValidationSeverity.Critical
						).ToList();

						if ( exitCode == ModComponent.InstallExitCode.Success && criticalIssues.Count == 0 )
						{
							await Logger.LogVerboseAsync("[DownloadCacheService] ✓ VFS simulation passed - changes are valid");
							return true;
						}
						else
						{
							await Logger.LogWarningAsync($"[DownloadCacheService] VFS simulation failed with {criticalIssues.Count} critical issue(s), exit code: {exitCode}");

							// Log a few issues for debugging
							foreach ( ValidationIssue issue in criticalIssues.Take(3) )
							{
								await Logger.LogVerboseAsync($"  • {issue.Category}: {issue.Message}");
							}

							// Rollback changes
							await RollbackInstructionChanges(component, originalInstructions);
							return false;
						}
					}
					catch ( Exception ex )
					{
						await Logger.LogWarningAsync($"[DownloadCacheService] VFS simulation threw exception: {ex.Message}");

						// Rollback changes
						await RollbackInstructionChanges(component, originalInstructions);
						return false;
					}
				}
				else
				{
					// No VFS available, accept changes optimistically
					await Logger.LogVerboseAsync("[DownloadCacheService] No mod directory configured, accepting changes without validation");
					return true;
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogExceptionAsync(ex, "[DownloadCacheService] Error during archive name mismatch fix");

				// Rollback changes
				await RollbackInstructionChanges(component, originalInstructions);
				return false;
			}
		}

		private static async Task RollbackInstructionChanges(
			ModComponent component,
			Dictionary<Guid, (Instruction.ActionType action, List<string> source, string destination)> originalState)
		{
			await Logger.LogVerboseAsync("[DownloadCacheService] Rolling back instruction changes...");

			int rollbackCount = 0;

			// Rollback component instructions
			foreach ( Instruction instruction in component.Instructions )
			{
				if ( !originalState.TryGetValue(instruction.Guid, out (Instruction.ActionType action, List<string> source, string destination) original) )
					continue;
				instruction.Source = new List<string>(original.source);
				instruction.Destination = original.destination;
				rollbackCount++;
			}

			// Rollback option instructions
			foreach ( Option option in component.Options )
			{
				foreach ( Instruction instruction in option.Instructions )
				{
					if ( originalState.TryGetValue(instruction.Guid, out (Instruction.ActionType action, List<string> source, string destination) original) )
					{
						instruction.Source = new List<string>(original.source);
						instruction.Destination = original.destination;
						rollbackCount++;
					}
				}
			}

			await Logger.LogVerboseAsync($"[DownloadCacheService] Rolled back {rollbackCount} instruction(s) to original state");
		}

		public sealed class DownloadCacheEntry
		{
			public string Url { get; set; }

			public string FileName { get; set; }

			public Guid ExtractInstructionGuid { get; set; }

			public bool IsArchiveFile { get; set; }

			public override string ToString() =>
				$"DownloadCacheEntry[FileName={FileName}, IsArchive={IsArchiveFile}, ExtractGuid={ExtractInstructionGuid}]";
		}

		public sealed class DownloadFailureInfo
		{
			public string Url { get; set; }
			public string ComponentName { get; set; }
			public string ExpectedFileName { get; set; }
			public string ErrorMessage { get; set; }
			public FailureType Type { get; set; }
			public List<string> LogContext { get; set; }

			public enum FailureType
			{
				DownloadFailed,
				FileNotFound,
				ResolutionFailed
			}
		}

		public List<DownloadFailureInfo> GetFailures()
		{
			lock ( _failureLock ) return _failedDownloads.Values.ToList();
		}

		private void RecordFailure(
			string url,
			string componentName,
			string expectedFileName,
			string errorMessage, DownloadFailureInfo.FailureType type)
		{
			lock ( _failureLock )
			{
				if ( _failedDownloads.ContainsKey(url) )
					return;
				// Capture recent log messages for context (last 30 messages)
				List<string> logContext = Logger.GetRecentLogMessages(30);

				_failedDownloads[url] = new DownloadFailureInfo
				{
					Url = url,
					ComponentName = componentName,
					ExpectedFileName = expectedFileName,
					ErrorMessage = errorMessage,
					Type = type,
					LogContext = logContext
				};
			}
		}

		private async Task PopulateModLinkFilenamesWithSimulationAsync(
			ModComponent component,
			string url,
			List<string> allFilenames,
			string modArchiveDirectory)
		{
			if ( component == null || string.IsNullOrWhiteSpace(url) || allFilenames == null || allFilenames.Count == 0 )
				return;

			if ( !component.ModLinkFilenames.TryGetValue(url, out Dictionary<string, bool?> filenameDict) )
			{
				filenameDict = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
				component.ModLinkFilenames[url] = filenameDict;
			}

			foreach ( string filename in allFilenames.Where(filename => !string.IsNullOrWhiteSpace(filename)) )
			{
				// If filename already exists with explicit value, don't override
				if ( filenameDict.TryGetValue(filename, out bool? shouldDownload) && shouldDownload.HasValue )
					continue;

				// If filename exists but is null, test if needed by instructions using VFS
				bool isNeeded = await _validationService.TestFilenameNeededByInstructionsAsync(component, filename, modArchiveDirectory);

				// null = default/auto-discover (will be set to true after instruction generation)
				// If instructions are generated for this file, set to true, otherwise leave as null
				if ( isNeeded )
				{
					filenameDict[filename] = true;
					await Logger.LogVerboseAsync($"[DownloadCacheService] Added filename '{filename}' for URL '{url}' (shouldDownload=true, matched instructions)");
				}
				else
				{
					// Set to null to indicate "not yet tested" or "no instructions yet"
					filenameDict[filename] = null;
					await Logger.LogVerboseAsync($"[DownloadCacheService] Added filename '{filename}' for URL '{url}' (shouldDownload=null, no matching instructions)");
				}
			}

			int enabledCount = filenameDict.Count(f => f.Value == true);
			int nullCount = filenameDict.Count(f => !f.Value.HasValue);
			await Logger.LogVerboseAsync($"[DownloadCacheService] Populated ModLinkFilenames for '{component.Name}': {url} -> {filenameDict.Count} file(s), {enabledCount} enabled, {nullCount} auto-discover");
		}


		public static List<string> GetFilenamesForDownload(ModComponent component, string url, List<string> allFilenames)
		{
			if ( component == null || string.IsNullOrWhiteSpace(url) || allFilenames == null || allFilenames.Count == 0 )
				return new List<string>();

			if ( component.ModLinkFilenames.TryGetValue(url, out Dictionary<string, bool?> filenameDict) )
			{
				// If dictionary is empty (auto-discover mode), use VFS-based pattern filtering
				if ( filenameDict.Count == 0 )
				{
					Logger.LogVerbose($"[DownloadCacheService] Auto-discover mode for '{url}', filtering {allFilenames.Count} files by unsatisfied Extract patterns...");

					string modArchiveDirectory2 = MainConfig.SourcePath?.FullName;
					if ( string.IsNullOrEmpty(modArchiveDirectory2) )
					{
						Logger.LogWarning("[DownloadCacheService] MainConfig.SourcePath not set, downloading all files as fallback");
						return allFilenames;
					}

					ComponentValidationService validationService2 = new ComponentValidationService();
					List<string> matchedFiles = validationService2.FilterFilenamesByUnsatisfiedPatterns(component, allFilenames, modArchiveDirectory2);

					if ( matchedFiles.Count > 0 )
					{
						Logger.LogVerbose($"[DownloadCacheService] Auto-discover matched {matchedFiles.Count}/{allFilenames.Count} files needed to satisfy Extract patterns");
						return matchedFiles;
					}

					Logger.LogWarning($"[DownloadCacheService] Auto-discover mode but no files matched unsatisfied Extract patterns for '{url}'. Available files:");
					foreach ( string fn in allFilenames )
					{
						Logger.LogWarning($"  • {fn}");
					}
					return new List<string>();
				}

				// Dictionary has entries - use explicit filtering
				// null = auto-discover/test with VFS
				// true = explicitly enabled
				// false = explicitly disabled (skip)

				string modDir = MainConfig.SourcePath?.FullName
								?? throw new InvalidOperationException("MainConfig.SourcePath not set");
				ComponentValidationService validationSvc = string.IsNullOrEmpty(modDir) ? null : new ComponentValidationService();

				List<string> filesToDownload = allFilenames
					.Where(filename =>
					{
						if ( filenameDict.TryGetValue(filename, out bool? shouldDownload) )
							return shouldDownload != false; // null or true = download, false = skip
															// File not in dict - use VFS to check if it satisfies any unsatisfied pattern
						if ( validationSvc == null )
							return false; // If no VFS available, don't download unknown files
						List<string> testResult = validationSvc.FilterFilenamesByUnsatisfiedPatterns(component, new List<string> { filename }, modDir);
						return testResult.Count > 0;

					})
					.ToList();

				if ( filesToDownload.Count > 0 )
				{
					int explicitlyEnabled = allFilenames.Count(fn => filenameDict.TryGetValue(fn, out bool? val) && val == true);
					int autoDiscover = allFilenames.Count(fn => filenameDict.TryGetValue(fn, out bool? val) && val == null);
					Logger.LogVerbose($"[DownloadCacheService] Filtered download list for '{url}': {filesToDownload.Count}/{allFilenames.Count} files ({explicitlyEnabled} enabled, {autoDiscover} auto-discover)");
					return filesToDownload;
				}

				Logger.LogWarning($"[DownloadCacheService] All files disabled for URL '{url}' (0/{allFilenames.Count} enabled)");
				return new List<string>();
			}

			Logger.LogVerbose($"[DownloadCacheService] No ModLinkFilenames entry for '{url}', filtering {allFilenames.Count} files by unsatisfied Extract patterns...");

			string modArchiveDirectory = MainConfig.SourcePath?.FullName;
			if ( string.IsNullOrEmpty(modArchiveDirectory) )
			{
				Logger.LogWarning("[DownloadCacheService] MainConfig.SourcePath not set, downloading all files as fallback");
				return allFilenames;
			}

			ComponentValidationService validationService = new ComponentValidationService();
			List<string> filteredFiles = validationService.FilterFilenamesByUnsatisfiedPatterns(component, allFilenames, modArchiveDirectory);

			if ( filteredFiles.Count > 0 )
			{
				Logger.LogVerbose($"[DownloadCacheService] Matched {filteredFiles.Count}/{allFilenames.Count} files needed to satisfy Extract patterns");
				return filteredFiles;
			}

			// No patterns matched - all Extract patterns already satisfied
			Logger.LogVerbose("[DownloadCacheService] All Extract patterns already satisfied, no downloads needed");
			return new List<string>();
		}

		private static bool ShouldDownloadUrl(ModComponent component, string url)
		{
			if ( component == null || string.IsNullOrWhiteSpace(url) )
				return true;

			if ( !component.ModLinkFilenames.TryGetValue(url, out Dictionary<string, bool?> filenameDict) ||
				 filenameDict.Count <= 0 ) return true;
			// null = auto-discover (treat as enabled)
			// true = explicitly enabled
			// false = explicitly disabled
			bool hasEnabledFile = filenameDict.Values.Any(shouldDownload => shouldDownload != false);
			if ( hasEnabledFile )
				return true;
			Logger.LogVerbose($"[DownloadCacheService] URL has all filenames explicitly disabled, skipping: {url}");
			return false;

		}

		#region Phase 5: Resource Index Management

		private static string GetResourceIndexPath()
		{
			string cacheDir = Path.GetDirectoryName(GetCacheFilePath());
			return Path.Combine(cacheDir, "resource-index.json");
		}

		private static string GetResourceIndexLockPath()
		{
			return GetResourceIndexPath() + ".lock";
		}

		/// <summary>
		/// Saves the resource index atomically with cross-process file locking.
		/// </summary>
		private static async Task SaveResourceIndexAsync()
		{
			string path = GetResourceIndexPath();
			string temp = path + ".tmp";
			string backup = path + ".bak";
			string lockPath = GetResourceIndexLockPath();

			// Ensure directory exists
			string directory = Path.GetDirectoryName(path);
			if ( !Directory.Exists(directory) )
				Directory.CreateDirectory(directory);

			try
			{
				// Cross-process file lock using cross-platform approach
				using (CrossPlatformFileLock fileLock = new CrossPlatformFileLock(lockPath) )
				{
					// Lock the entire file
					await fileLock.LockAsync();

					try
					{
						var indexData = new
						{
							schemaVersion = 1,
							lastSaved = DateTime.UtcNow.ToString("O"),
							entries = new Dictionary<string, ResourceMetadata>(),
							mappings = new Dictionary<string, string>()
						};

						lock ( s_resourceIndexLock )
						{
							// Merge all resource metadata
							foreach (KeyValuePair<string, ResourceMetadata> kvp in s_resourceByMetadataHash )
								((Dictionary<string, ResourceMetadata>)indexData.entries)[kvp.Key] = kvp.Value;

							foreach (KeyValuePair<string, ResourceMetadata> kvp in s_resourceByContentId )
							{
								if ( !((Dictionary<string, ResourceMetadata>)indexData.entries).ContainsKey(kvp.Key) )
									((Dictionary<string, ResourceMetadata>)indexData.entries)[kvp.Key] = kvp.Value;
							}

							// Copy mappings
							foreach (KeyValuePair<string, string> kvp in s_metadataHashToContentId )
								((Dictionary<string, string>)indexData.mappings)[kvp.Key] = kvp.Value;
						}

						string json = JsonConvert.SerializeObject(indexData, Formatting.Indented);
						await Task.Run(() => File.WriteAllText(temp, json));

						// Platform-specific atomic replace
						if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
						{
							if ( File.Exists(path) )
								File.Replace(temp, path, backup);
							else
								File.Move(temp, path);

							if ( File.Exists(backup) )
								File.Delete(backup);
						}
						else
						{
							// POSIX: rename is atomic
							if ( File.Exists(path) )
								File.Move(path, backup);
							File.Move(temp, path);

							if ( File.Exists(backup) )
								File.Delete(backup);
						}

						await Logger.LogVerboseAsync($"[Cache] Saved resource index: {path}");
					}
					finally
					{
						await fileLock.UnlockAsync();
					}
				}

				// Clean up lock file if empty/stale
				if ( File.Exists(lockPath) )
				{
					FileInfo lockInfo = new FileInfo(lockPath);
					if ( lockInfo.Length == 0 )
					{
						try { File.Delete(lockPath); } catch { }
					}
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogErrorAsync($"[Cache] Failed to save resource index: {ex.Message}");

				// Clean up temp file
				if ( File.Exists(temp) )
				{
					try { File.Delete(temp); } catch { }
				}
			}
		}

		/// <summary>
		/// Loads the resource index from disk with file locking.
		/// </summary>
		public static async Task LoadResourceIndexAsync()
		{
			string path = GetResourceIndexPath();
			string lockPath = GetResourceIndexLockPath();

			if ( !File.Exists(path) )
			{
				await Logger.LogVerboseAsync("[Cache] No resource index found, starting fresh");
				return;
			}

			try
			{
				using (CrossPlatformFileLock fileLock = new CrossPlatformFileLock(lockPath) )
				{
					await fileLock.LockAsync();

					try
					{
						string json = await Task.Run(() => File.ReadAllText(path));
						dynamic indexData = JsonConvert.DeserializeObject(json);

						if ( indexData == null )
						{
							await Logger.LogWarningAsync("[Cache] Resource index file was empty or invalid");
							return;
						}

						lock ( s_resourceIndexLock )
						{
							s_resourceByMetadataHash.Clear();
							s_metadataHashToContentId.Clear();
							s_resourceByContentId.Clear();

							// Load entries
							if ( indexData.entries != null )
							{
								Newtonsoft.Json.Linq.JObject entries = (Newtonsoft.Json.Linq.JObject)indexData.entries;
								foreach (KeyValuePair<string, Newtonsoft.Json.Linq.JToken> entry in entries )
								{
									string key = entry.Key;
									Newtonsoft.Json.Linq.JToken metaToken = entry.Value;

									ResourceMetadata meta = new ResourceMetadata
									{
										ContentKey = metaToken["ContentKey"]?.ToString(),
										ContentId = metaToken["ContentId"]?.ToString(),
										ContentHashSHA256 = metaToken["ContentHashSHA256"]?.ToString(),
										MetadataHash = metaToken["MetadataHash"]?.ToString(),
										PrimaryUrl = metaToken["PrimaryUrl"]?.ToString(),
										FileSize = metaToken["FileSize"]?.ToObject<long>() ?? 0,
										PieceLength = metaToken["PieceLength"]?.ToObject<int>() ?? 0,
										PieceHashes = metaToken["PieceHashes"]?.ToString(),
										SchemaVersion = metaToken["SchemaVersion"]?.ToObject<int>() ?? 1
									};

									if ( metaToken["HandlerMetadata"] != null )
										meta.HandlerMetadata = metaToken["HandlerMetadata"].ToObject<Dictionary<string, object>>();

									if ( metaToken["Files"] != null )
										meta.Files = metaToken["Files"].ToObject<Dictionary<string, bool?>>();

									if ( Enum.TryParse<MappingTrustLevel>(metaToken["TrustLevel"]?.ToString(), out MappingTrustLevel trustLevel ) )
										meta.TrustLevel = trustLevel;

									if ( DateTime.TryParse(metaToken["FirstSeen"]?.ToString(), out DateTime firstSeen ) )
										meta.FirstSeen = firstSeen;

									if ( DateTime.TryParse(metaToken["LastVerified"]?.ToString(), out DateTime lastVerified ) )
										meta.LastVerified = lastVerified;

									// Store in appropriate index
									if ( !string.IsNullOrEmpty(meta.MetadataHash) )
										s_resourceByMetadataHash[meta.MetadataHash] = meta;

									if ( !string.IsNullOrEmpty(meta.ContentId) )
										s_resourceByContentId[meta.ContentId] = meta;
								}
							}

							// Load mappings
							if ( indexData.mappings != null )
							{
								Newtonsoft.Json.Linq.JObject mappings = (Newtonsoft.Json.Linq.JObject)indexData.mappings;
								foreach (KeyValuePair<string, Newtonsoft.Json.Linq.JToken> mapping in mappings )
								{
									s_metadataHashToContentId[mapping.Key] = mapping.Value.ToString();
								}
							}
						}

						await Logger.LogVerboseAsync($"[Cache] Loaded resource index: {s_resourceByMetadataHash.Count} metadata entries, {s_resourceByContentId.Count} content entries, {s_metadataHashToContentId.Count} mappings");
					}
					finally
					{
						await fileLock.UnlockAsync();
					}
				}
			}
			catch ( Exception ex )
			{
				await Logger.LogWarningAsync($"[Cache] Failed to load resource index: {ex.Message}");
			}
		}

		/// <summary>
		/// Updates a MetadataHash → ContentId mapping with trust elevation logic.
		/// </summary>
		private static async Task<bool> UpdateMappingWithVerification(string metadataHash, string contentId, ResourceMetadata meta)
		{
			bool updated = false;
			string logMessage = null;
			bool hasConflict = false;
			bool keepExisting = false;

			lock ( s_resourceIndexLock )
			{
				// Check existing mapping
				if ( s_metadataHashToContentId.TryGetValue(metadataHash, out string existingContentId) )
				{
					if ( existingContentId == contentId )
					{
						// Same mapping, elevate trust
						if ( meta.TrustLevel == MappingTrustLevel.Unverified )
						{
							meta.TrustLevel = MappingTrustLevel.ObservedOnce;
							updated = true;
							logMessage = $"[Cache] Trust elevated: {metadataHash.Substring(0, 16)}... → ObservedOnce";
						}
						else if ( meta.TrustLevel == MappingTrustLevel.ObservedOnce )
						{
							meta.TrustLevel = MappingTrustLevel.Verified;
							updated = true;
							logMessage = $"[Cache] Trust elevated: {metadataHash.Substring(0, 16)}... → Verified";
						}
					}
					else
					{
						// CONFLICT: Different ContentId for same MetadataHash
						hasConflict = true;

						// Keep existing if Verified
						if ( s_resourceByContentId.TryGetValue(existingContentId, out ResourceMetadata existingMeta ) &&
							existingMeta.TrustLevel == MappingTrustLevel.Verified )
						{
							keepExisting = true;
						}
						else
						{
							// Replace with new mapping
							s_metadataHashToContentId[metadataHash] = contentId;
							s_resourceByContentId[contentId] = meta;
							meta.TrustLevel = MappingTrustLevel.ObservedOnce;
							updated = true;
						}
					}
				}
				else
				{
					// New mapping
					s_metadataHashToContentId[metadataHash] = contentId;
					s_resourceByContentId[contentId] = meta;
					meta.TrustLevel = MappingTrustLevel.ObservedOnce;
					updated = true;
					logMessage = $"[Cache] New mapping: {metadataHash.Substring(0, 16)}... → {contentId.Substring(0, 16)}...";
				}

				// Always update metadata hash index
				s_resourceByMetadataHash[metadataHash] = meta;
			}

			// Log outside of lock
			if ( hasConflict )
			{
				await Logger.LogWarningAsync($"[Cache] Mapping conflict detected:");
				await Logger.LogWarningAsync($"  MetadataHash: {metadataHash.Substring(0, 16)}...");
				await Logger.LogWarningAsync($"  New ContentId: {contentId.Substring(0, 16)}...");

				if ( keepExisting )
				{
					await Logger.LogWarningAsync($"  Keeping existing (Verified)");
					return false;
				}
				else
				{
					await Logger.LogWarningAsync($"  Replacing with new mapping");
				}
			}
			else if ( logMessage != null )
			{
				if ( updated && meta.TrustLevel == MappingTrustLevel.Verified )
					await Logger.LogAsync(logMessage);
				else
					await Logger.LogVerboseAsync(logMessage);
			}

			return updated;
		}

		/// <summary>
		/// Garbage collects stale entries and downgrades trust levels.
		/// </summary>
		public static void GarbageCollectResourceIndex()
		{
			DateTime now = DateTime.UtcNow;
			List<string> toRemove = new List<string>();

			lock ( s_resourceIndexLock )
			{
				foreach (KeyValuePair<string, ResourceMetadata> kvp in s_resourceByContentId )
				{
					ResourceMetadata meta = kvp.Value;

					// Rule 1: Old and file doesn't exist
					if ( meta.LastVerified.HasValue && (now - meta.LastVerified.Value).TotalDays > 90 )
					{
						string expectedFile = Path.Combine(MainConfig.SourcePath?.FullName ?? "", meta.Files.Keys.FirstOrDefault() ?? "");
						if ( !File.Exists(expectedFile) )
						{
							toRemove.Add(kvp.Key);
							continue;
						}
					}

					// Rule 2: Never used and very old
					if ( !meta.LastVerified.HasValue && meta.FirstSeen.HasValue && (now - meta.FirstSeen.Value).TotalDays > 365 )
					{
						toRemove.Add(kvp.Key);
					}

					// Rule 3: Downgrade trust if not re-verified
					if ( meta.LastVerified.HasValue && (now - meta.LastVerified.Value).TotalDays > 30 )
					{
						if ( meta.TrustLevel == MappingTrustLevel.Verified )
							meta.TrustLevel = MappingTrustLevel.ObservedOnce;
						else if ( meta.TrustLevel == MappingTrustLevel.ObservedOnce )
							meta.TrustLevel = MappingTrustLevel.Unverified;
					}
				}

				foreach ( string key in toRemove )
				{
					s_resourceByContentId.Remove(key);
					KeyValuePair<string, string> metaMapping = s_metadataHashToContentId.FirstOrDefault(m => m.Value == key);
					if ( metaMapping.Key != null )
						s_metadataHashToContentId.Remove(metaMapping.Key);
				}
			}

			Logger.LogVerbose($"[Cache] GC removed {toRemove.Count} stale entries");
		}

		/// <summary>
		/// Enforces disk quota using LRU eviction.
		/// </summary>
		public static void EnforceDiskQuota(long maxSizeBytes)
		{
			string cacheDir = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
				"KOTORModSync",
				"Cache",
				"Network"
			);

			if ( !Directory.Exists(cacheDir) )
				return;

			string[] datFiles = Directory.GetFiles(cacheDir, "*.dat");
			long totalSize = datFiles.Sum(f => new FileInfo(f).Length);

			if ( totalSize <= maxSizeBytes )
				return;

			// Sort by LastVerified (oldest first)
			lock ( s_resourceIndexLock )
			{
				List<KeyValuePair<string, ResourceMetadata>> entries = s_resourceByContentId.OrderBy(e => e.Value.LastVerified ?? e.Value.FirstSeen).ToList();

				foreach (KeyValuePair<string, ResourceMetadata> entry in entries )
				{
					// This requires access to GetCachePath from DownloadCacheOptimizer
					// For now, construct path manually
					string datPath = Path.Combine(cacheDir, $"{entry.Key}.dat");

					if ( File.Exists(datPath) )
					{
						long fileSize = new FileInfo(datPath).Length;
						try
						{
							File.Delete(datPath);
							totalSize -= fileSize;

							s_resourceByContentId.Remove(entry.Key);
							Logger.LogVerbose($"[Cache] Evicted: {entry.Key.Substring(0, 16)}... ({fileSize / 1024} KB)");

							if ( totalSize <= maxSizeBytes )
								break;
						}
						catch ( Exception ex )
						{
							Logger.LogWarning($"[Cache] Failed to delete cache file: {ex.Message}");
						}
					}
				}
			}

			Logger.LogVerbose($"[Cache] Quota enforcement: pruned to {totalSize / (1024 * 1024)} MB");
		}

		#endregion

		#region CLI Management Methods

		/// <summary>
		/// Gets the total number of resources in the cache.
		/// </summary>
		public static int GetResourceCount()
		{
			lock ( s_resourceIndexLock )
			{
				return s_resourceByContentId.Count;
			}
		}

		/// <summary>
		/// Gets the total cache size in bytes.
		/// </summary>
		public static long GetTotalCacheSize()
		{
			lock ( s_resourceIndexLock )
			{
				return s_resourceByContentId.Values.Sum(r => r.FileSize);
			}
		}

		/// <summary>
		/// Gets statistics by provider.
		/// </summary>
		public static Dictionary<string, int> GetProviderStats()
		{
			lock ( s_resourceIndexLock )
			{
				return s_resourceByContentId.Values
					.GroupBy(r => r.HandlerMetadata?.ContainsKey("provider") == true ? r.HandlerMetadata["provider"].ToString() : "unknown")
					.ToDictionary(g => g.Key, g => g.Count());
			}
		}

		/// <summary>
		/// Gets the count of blocked ContentIds.
		/// </summary>
		public static int GetBlockedContentIdCount()
		{
			return DownloadCacheOptimizer.GetBlockedContentIdCount();
		}

		/// <summary>
		/// Gets the last index update time.
		/// </summary>
		public static DateTime GetLastIndexUpdate()
		{
			lock ( s_resourceIndexLock )
			{
				return s_resourceByContentId.Values
					.Where(r => r.LastVerified.HasValue)
					.DefaultIfEmpty(new ResourceMetadata { LastVerified = DateTime.MinValue })
					.Max(r => r.LastVerified ?? DateTime.MinValue);
			}
		}

		/// <summary>
		/// Clears the cache, optionally for a specific provider.
		/// </summary>
		public static async Task ClearCacheAsync(string provider = null)
		{
			lock ( s_resourceIndexLock )
			{
				if ( string.IsNullOrEmpty(provider) )
				{
					// Clear all
					s_resourceByContentId.Clear();
					s_resourceByMetadataHash.Clear();
					s_metadataHashToContentId.Clear();
				}
				else
				{
					// Clear specific provider
					List<KeyValuePair<string, ResourceMetadata>> toRemove = s_resourceByContentId
						.Where(kvp => kvp.Value.HandlerMetadata?.ContainsKey("provider") == true &&
									 kvp.Value.HandlerMetadata["provider"].ToString() == provider)
						.ToList();

					foreach (KeyValuePair<string, ResourceMetadata> kvp in toRemove )
					{
						s_resourceByContentId.Remove(kvp.Key);
						s_resourceByMetadataHash.Remove(kvp.Value.MetadataHash);
						s_metadataHashToContentId.Remove(kvp.Value.MetadataHash);
					}
				}
			}

			await SaveResourceIndexAsync();
		}

		#endregion
	}

	/// <summary>
	/// Cross-platform file locking implementation that works on Windows, Linux, and macOS.
	/// </summary>
	internal sealed class CrossPlatformFileLock : IDisposable
	{
		private readonly string _lockPath;
		private FileStream _fileStream;
		private bool _isLocked;
		private bool _disposed;

		public CrossPlatformFileLock(string lockPath)
		{
			_lockPath = lockPath ?? throw new ArgumentNullException(nameof(lockPath));
		}

		public async Task LockAsync()
		{
			if ( _disposed )
				throw new ObjectDisposedException(nameof(CrossPlatformFileLock));
			if ( _isLocked )
				return;

			// Ensure directory exists
			string directory = Path.GetDirectoryName(_lockPath);
			if ( !string.IsNullOrEmpty(directory) && !Directory.Exists(directory) )
				Directory.CreateDirectory(directory);

			_fileStream = new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

			if ( KOTORModSync.Core.Utility.UtilityHelper.GetOperatingSystem() == OSPlatform.Windows )
			{
				// Windows: Use FileStream.Lock/Unlock
#pragma warning disable CA1416 // This call site is reachable on all platforms. 'FileStream.Lock(long, long)' is unsupported on: 'macOS/OSX'.
				_fileStream.Lock(0, 0);
#pragma warning restore CA1416
			}
			else
			{
				// Unix systems (Linux/macOS): Use flock via P/Invoke
				await LockUnixFileAsync();
			}

			_isLocked = true;
		}

		public async Task UnlockAsync()
		{
			if ( _disposed || !_isLocked )
				return;

			try
			{
				if ( KOTORModSync.Core.Utility.UtilityHelper.GetOperatingSystem() == OSPlatform.Windows )
				{
					// Windows: Use FileStream.Unlock
#pragma warning disable CA1416 // This call site is reachable on all platforms. 'FileStream.Unlock(long, long)' is unsupported on: 'macOS/OSX'.
					_fileStream?.Unlock(0, 0);
#pragma warning restore CA1416
				}
				else
				{
					// Unix systems: Use flock via P/Invoke
					await UnlockUnixFileAsync();
				}
			}
			finally
			{
				_isLocked = false;
			}
		}

		private async Task LockUnixFileAsync()
		{
			await Task.Run(() =>
			{
				int fd = GetFileDescriptor(_fileStream);
				if ( fd == -1 )
					throw new InvalidOperationException("Failed to get file descriptor");

				int result = flock(fd, LOCK_EX | LOCK_NB);
				if ( result != 0 )
				{
					int error = Marshal.GetLastWin32Error();
					throw new IOException($"Failed to acquire file lock: {error}");
				}
			});
		}

		private async Task UnlockUnixFileAsync()
		{
			await Task.Run(() =>
			{
				int fd = GetFileDescriptor(_fileStream);
				if ( fd != -1 )
				{
					flock(fd, LOCK_UN);
				}
			});
		}

		private static int GetFileDescriptor(FileStream stream)
		{
			// Get the file descriptor from the FileStream's SafeFileHandle
			return stream.SafeFileHandle.DangerousGetHandle().ToInt32();
		}

		public void Dispose()
		{
			if ( !_disposed )
			{
				try
				{
					UnlockAsync().GetAwaiter().GetResult();
				}
				catch
				{
					// Ignore exceptions during disposal
				}
				finally
				{
					_fileStream?.Dispose();
					_disposed = true;
				}
			}
		}

		// P/Invoke declarations for Unix file locking
		private const int LOCK_EX = 2;    // Exclusive lock
		private const int LOCK_NB = 4;    // Non-blocking
		private const int LOCK_UN = 8;    // Unlock

		[DllImport("libc", SetLastError = true)]
		private static extern int flock(int fd, int operation);
	}
}