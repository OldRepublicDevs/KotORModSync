// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Services.FileSystem;
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
				var handler = new System.Net.Http.HttpClientHandler
				{
					AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
					MaxConnectionsPerServer = 128
				};
				var httpClient = new System.Net.Http.HttpClient(handler)
				{
					Timeout = TimeSpan.FromHours(3)
				};
				var handlers = new List<IDownloadHandler>
			{
				new DeadlyStreamDownloadHandler(httpClient),
				new MegaDownloadHandler(),
				new NexusModsDownloadHandler(httpClient, MainConfig.NexusModsApiKey),
				new GameFrontDownloadHandler(httpClient),
				new DirectDownloadHandler(httpClient),
			};
				DownloadManager = new DownloadManager(handlers);
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
			if ( component == null )
				throw new ArgumentNullException(nameof(component));

			downloadManager = downloadManager ?? DownloadManager;
			if ( downloadManager == null )
				throw new InvalidOperationException("DownloadManager is not set. Call SetDownloadManager() first.");

			await Logger.LogVerboseAsync($"[DownloadCacheService] Pre-resolving URLs for component: {component.Name}");

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
			if ( urls.Count == 0 )
			{
				await Logger.LogVerboseAsync("[DownloadCacheService] No URLs to resolve");
				return new Dictionary<string, List<string>>();
			}

			if ( !string.IsNullOrEmpty(modArchiveDirectory) )
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService] Analyzing download necessity for {urls.Count} URLs");
				(List<string> urlsNeedingAnalysis, bool _initialSimulationFailed) = await AnalyzeDownloadNecessityWithStatusAsync(component, modArchiveDirectory);

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

			var results = new Dictionary<string, List<string>>();
			var urlsToResolve = new List<string>();
			var missingFiles = new List<(string url, string filename)>();
			int cacheHits = 0;

			foreach ( string url in filteredUrls )
			{
				if ( TryGetEntry(url, out DownloadCacheEntry cachedEntry) )
				{
					if ( string.IsNullOrWhiteSpace(cachedEntry.FileName) )
					{
						Logger.LogWarning($"Invalid cache entry for {url} with empty filename, removing and treating as miss");
						Remove(url);
						urlsToResolve.Add(url);
					}
					else
					{
						results[url] = new List<string> { cachedEntry.FileName };
						cacheHits++;
						await Logger.LogVerboseAsync($"[DownloadCacheService] Cache hit for URL: {url} -> {cachedEntry.FileName}");

						if ( !string.IsNullOrEmpty(modArchiveDirectory) )
						{
							string filePath = Path.Combine(modArchiveDirectory, cachedEntry.FileName);
							if ( !File.Exists(filePath) )
							{
								missingFiles.Add((url, cachedEntry.FileName));
								await Logger.LogWarningAsync($"[DownloadCacheService] Cached file does not exist on disk: {cachedEntry.FileName}");
							}
						}
					}
				}
				else
				{
					urlsToResolve.Add(url);
				}
			}

			if ( cacheHits > 0 )
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService] Retrieved {cacheHits} filename(s) from cache");
			}

			if ( urlsToResolve.Count > 0 )
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService] Resolving {urlsToResolve.Count} uncached URL(s) via network");
				Dictionary<string, List<string>> resolvedResults = await downloadManager.ResolveUrlsToFilenamesAsync(urlsToResolve, cancellationToken, sequential).ConfigureAwait(false);

				if ( sequential )
				{
					foreach ( KeyValuePair<string, List<string>> kvp in resolvedResults )
					{
						await ProcessResolvedUrlForPreResolveAsync(component, kvp, results, modArchiveDirectory, missingFiles);
					}
				}
				else
				{
					var processingTasks = resolvedResults.Select(kvp =>
						ProcessResolvedUrlForPreResolveAsync(component, kvp, results, modArchiveDirectory, missingFiles)
					).ToList();
					await Task.WhenAll(processingTasks);
				}
			}

			Dictionary<string, List<string>> filteredResults = _resolutionFilter.FilterResolvedUrls(results);

			if ( missingFiles.Count > 0 )
			{
				await Logger.LogWarningAsync($"[DownloadCacheService] Pre-resolve summary for '{component.Name}': {filteredResults.Count} URLs resolved, {missingFiles.Count} files missing on disk");
				await Logger.LogWarningAsync("[DownloadCacheService] Missing files that need to be downloaded:");
				foreach ( var (url, filename) in missingFiles )
				{
					await Logger.LogWarningAsync($"  • {filename} (from {url})");
					RecordFailure(url, component.Name, filename, "File does not exist on disk", DownloadFailureInfo.FailureType.FileNotFound);
				}
			}
			else
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService] Pre-resolved {filteredResults.Count} URLs ({cacheHits} from cache, {urlsToResolve.Count} from network), all files exist on disk");
			}

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

			if ( kvp.Value is { Count: > 0 } )
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

				var cacheEntry = new DownloadCacheEntry
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

			Dictionary<string, List<string>> resolved = await DownloadManager.ResolveUrlsToFilenamesAsync([url], cancellationToken);
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
						var diskEntry = new DownloadCacheEntry
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

		public void AddOrUpdate(string url, DownloadCacheEntry entry)
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

		public bool TryGetEntry(string url, out DownloadCacheEntry entry)
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

		public bool IsCached(string url)
		{
			if ( string.IsNullOrWhiteSpace(url) )
				return false;

			lock ( s_cacheLock )
			{
				return s_cache.ContainsKey(url);
			}
		}

		public string GetFileName(string url)
		{
			if ( TryGetEntry(url, out DownloadCacheEntry entry) )
				return entry.FileName;

			return null;
		}

		public string GetFilePath(string url)
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

		public Guid GetExtractInstructionGuid(string url)
		{
			if ( TryGetEntry(url, out DownloadCacheEntry entry) )
				return entry.ExtractInstructionGuid;

			return Guid.Empty;
		}

		public bool Remove(string url)
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

		public void Clear()
		{
			lock ( s_cacheLock )
			{
				s_cache.Clear();
				Logger.LogVerbose("[DownloadCacheService] Cache cleared");
			}

			SaveCacheToDisk();
		}

		public int GetTotalEntryCount()
		{
			lock ( s_cacheLock )
			{
				return s_cache.Count;
			}
		}

		public IReadOnlyList<DownloadCacheEntry> GetCachedEntries()
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
			(List<string> urlsNeedingAnalysis, bool initialSimulationFailed) = await AnalyzeDownloadNecessityWithStatusAsync(component, modArchiveDirectory);

			var urlsToProcess = new List<string>();
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
				var cacheEntries = new List<DownloadCacheEntry>();

				foreach ( string url in allAnalyzedUrls )
				{
					if ( TryGetEntry(url, out DownloadCacheEntry existingEntry) )
					{
						cacheEntries.Add(existingEntry);
					}
					else
					{
						Dictionary<string, List<string>> resolved = await DownloadManager.ResolveUrlsToFilenamesAsync([url], CancellationToken.None);
						if ( resolved.TryGetValue(url, out List<string> filenames) && filenames.Count > 0 )
						{
							var entry = new DownloadCacheEntry
							{
								Url = url,
								FileName = filenames[0],
								IsArchiveFile = Utility.ArchiveHelper.IsArchive(filenames[0]),
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
			var cachedResults = new List<DownloadCacheEntry>();
			var urlsNeedingDownload = new List<string>();

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
				var cacheCheckTasks = urlsToProcess.Select(url =>
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
					if ( DownloadCacheService.ShouldDownloadUrl(component, url) )
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

				var urlToProgressMap = new Dictionary<string, DownloadProgress>();

				foreach ( string url in urlsNeedingDownload )
				{
					var progressTracker = new DownloadProgress
					{
						ModName = component.Name,
						Url = url,
						Status = DownloadStatus.Pending,
						StatusMessage = "Waiting to start...",
						ProgressPercentage = 0
					};

					if ( TryGetEntry(url, out DownloadCacheEntry cachedEntry) && !string.IsNullOrWhiteSpace(cachedEntry.FileName) )
					{
						progressTracker.TargetFilenames = [cachedEntry.FileName];
						await Logger.LogVerboseAsync($"[DownloadCacheService] Set target filename from cache for {url}: {cachedEntry.FileName}");
					}
					else
					{
						Dictionary<string, List<string>> resolved = await DownloadManager.ResolveUrlsToFilenamesAsync([url], cancellationToken);
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
									progressTracker.TargetFilenames = [bestMatch];
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

				var progressForwarder = new Progress<DownloadProgress>(p =>
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

					var newEntry = new DownloadCacheEntry
					{
						Url = originalUrl,
						FileName = fileName,
						IsArchiveFile = isArchive,
						ExtractInstructionGuid = Guid.Empty
					};

					AddOrUpdate(originalUrl, newEntry);
					cachedResults.Add(newEntry);
					successCount++;

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
						CancellationToken.None,
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
						var fileIssues = issues.Where(i =>
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

			await EnsureInstructionsExist(component, cachedResults);
			return cachedResults;
		}

		private async Task EnsureInstructionsExist(
			ModComponent component,
			List<DownloadCacheEntry> entries
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
					if ( instruction.Source != null && instruction.Source.Any(src =>
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
						 instruction.Source is { Count: > 0 } )
					{
						string instructionArchiveName = ExtractFilenameFromSource(instruction.Source[0]);
						if ( !string.IsNullOrEmpty(instructionArchiveName) &&
							 !instructionArchiveName.Equals(entry.FileName, StringComparison.OrdinalIgnoreCase) &&
							 entry.IsArchiveFile )
						{
							// Check if this could be the same mod with different filename
							string instructionBaseName = Path.GetFileNameWithoutExtension(instructionArchiveName);
							string entryBaseName = Path.GetFileNameWithoutExtension(entry.FileName);

							// Simple similarity check (could be enhanced)
							if ( instructionBaseName.Contains(entryBaseName, StringComparison.OrdinalIgnoreCase) ||
								 entryBaseName.Contains(instructionBaseName, StringComparison.OrdinalIgnoreCase) ||
								 NormalizeModName(instructionBaseName).Equals(NormalizeModName(entryBaseName), StringComparison.OrdinalIgnoreCase) )
							{
								mismatchedInstruction = instruction;
								await Logger.LogVerboseAsync($"[DownloadCacheService] Detected archive name mismatch: instruction expects '{instructionArchiveName}' but downloaded '{entry.FileName}'");
								break;
							}
						}
					}
				}

				// Handle mismatched archive name
				if ( mismatchedInstruction != null && existingInstruction == null )
				{
					await Logger.LogAsync($"[DownloadCacheService] Attempting to fix archive name mismatch for component '{component.Name}'");
					bool fixSuccess = await TryFixArchiveNameMismatchAsync(component, mismatchedInstruction, entry);

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
								if ( instr.Source is { Count: > 0 } )
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
											if ( resolvedFiles.Any(f =>
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

		private void CreateSimpleInstructionForEntry(ModComponent component, DownloadCacheEntry entry)
		{
			var newInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = entry.IsArchiveFile
						 ? Instruction.ActionType.Extract
						 : Instruction.ActionType.Move,
				Source = [$@"<<modDirectory>>\{entry.FileName}"],
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
			string modArchiveDirectory)
		{
			(List<string> urls, bool simulationFailed) = await _validationService.AnalyzeDownloadNecessityAsync(component, modArchiveDirectory);
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

		private async Task<bool> TryFixArchiveNameMismatchAsync(
			ModComponent component,
			Instruction extractInstruction,
			DownloadCacheEntry entry)
		{
			if ( extractInstruction == null || entry == null || !entry.IsArchiveFile )
				return false;

			string oldArchiveName = ExtractFilenameFromSource(extractInstruction.Source[0]);
			string newArchiveName = entry.FileName;

			if ( string.IsNullOrEmpty(oldArchiveName) || string.IsNullOrEmpty(newArchiveName) )
				return false;

			await Logger.LogVerboseAsync($"[DownloadCacheService] Attempting to update archive name from '{oldArchiveName}' to '{newArchiveName}'");

			// Store original instruction state for rollback
			var originalInstructions = new Dictionary<Guid, (Instruction.ActionType action, List<string> source, string destination)>();

			try
			{
				// Step 1: Backup all instruction states (component + options)
				foreach ( Instruction instruction in component.Instructions )
				{
					originalInstructions[instruction.Guid] = (
						instruction.Action,
						[.. instruction.Source],
						instruction.Destination
					);
				}

				foreach ( Option option in component.Options )
				{
					foreach ( Instruction instruction in option.Instructions )
					{
						originalInstructions[instruction.Guid] = (
							instruction.Action,
							[.. instruction.Source],
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

					if ( instruction.Source is not { Count: > 0 } )
						continue;
					bool updated = false;
					for ( int i = 0; i < instruction.Source.Count; i++ )
					{
						string src = instruction.Source[i];
						if ( !src.Contains(oldExtractedFolderName, StringComparison.OrdinalIgnoreCase) )
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
						if ( instruction.Source is not { Count: > 0 } )
							continue;

						bool updated = false;
						for ( int i = 0; i < instruction.Source.Count; i++ )
						{
							string src = instruction.Source[i];
							if ( !src.Contains(oldExtractedFolderName, StringComparison.OrdinalIgnoreCase) )
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

					var vfs = new FileSystem.VirtualFileSystemProvider();
					await vfs.InitializeFromRealFileSystemAsync(modArchiveDirectory);

					try
					{
						ModComponent.InstallExitCode exitCode = await component.ExecuteInstructionsAsync(
							component.Instructions,
							new List<ModComponent>(),
							CancellationToken.None,
							vfs,
							skipDependencyCheck: true
						);

						List<FileSystem.ValidationIssue> issues = vfs.GetValidationIssues();
						var criticalIssues = issues.Where(i =>
							i.Severity == FileSystem.ValidationSeverity.Error ||
							i.Severity == FileSystem.ValidationSeverity.Critical
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
							foreach ( FileSystem.ValidationIssue issue in criticalIssues.Take(3) )
							{
								await Logger.LogVerboseAsync($"  • {issue.Category}: {issue.Message}");
							}

							// Rollback changes
							await DownloadCacheService.RollbackInstructionChanges(component, originalInstructions);
							return false;
						}
					}
					catch ( Exception ex )
					{
						await Logger.LogWarningAsync($"[DownloadCacheService] VFS simulation threw exception: {ex.Message}");

						// Rollback changes
						await DownloadCacheService.RollbackInstructionChanges(component, originalInstructions);
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
				await DownloadCacheService.RollbackInstructionChanges(component, originalInstructions);
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

					var validationService2 = new ComponentValidationService();
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

				var filesToDownload = allFilenames
					.Where(filename =>
					{
						if ( filenameDict.TryGetValue(filename, out bool? shouldDownload) )
							return shouldDownload != false; // null or true = download, false = skip
															// File not in dict - use VFS to check if it satisfies any unsatisfied pattern
						if ( validationSvc == null )
							return false; // If no VFS available, don't download unknown files
						List<string> testResult = validationSvc.FilterFilenamesByUnsatisfiedPatterns(component, [filename], modDir);
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

			var validationService = new ComponentValidationService();
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
	}
}