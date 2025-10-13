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

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Service that manages the cache of downloaded files and their associated Extract instructions.
	/// This is the ONLY entry point for download operations - all downloads go through this service.
	/// Maps: ModComponent GUID → URL → Extract Instruction GUID + Metadata
	/// </summary>
	public sealed class DownloadCacheService
	{
		private readonly Dictionary<Guid, Dictionary<string, DownloadCacheEntry>> _cache;
		private readonly object _cacheLock = new object();
		private DownloadManager _downloadManager;
		private VirtualFileSystemProvider _virtualFileSystem;
		private ResolutionFilterService _resolutionFilter;

		public DownloadCacheService()
		{
			_cache = new Dictionary<Guid, Dictionary<string, DownloadCacheEntry>>();
			_virtualFileSystem = new VirtualFileSystemProvider();
			_resolutionFilter = new ResolutionFilterService(MainConfig.FilterDownloadsByResolution);
			Logger.LogVerbose("[DownloadCacheService] Initialized");
		}

		/// <summary>
		/// Sets the download manager to use for downloading files.
		/// </summary>
		public void SetDownloadManager(DownloadManager downloadManager)
		{
			_downloadManager = downloadManager;
		}

		/// <summary>
		/// Pre-resolves URLs to filenames for all ModLinks in a component without downloading.
		/// </summary>
		public async Task<Dictionary<string, List<string>>> PreResolveUrlsAsync(
			ModComponent component,
			DownloadManager downloadManager,
			CancellationToken cancellationToken = default)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));
			if ( downloadManager == null )
				throw new ArgumentNullException(nameof(downloadManager));

			await Logger.LogVerboseAsync($"[DownloadCacheService] Pre-resolving URLs for component: {component.Name}");

			var urls = component.ModLink.Where(url => !string.IsNullOrWhiteSpace(url)).ToList();
			if ( urls.Count == 0 )
			{
				await Logger.LogVerboseAsync("[DownloadCacheService] No URLs to resolve");
				return new Dictionary<string, List<string>>();
			}

			// Apply resolution filtering to URLs before resolving
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

			var results = await downloadManager.ResolveUrlsToFilenamesAsync(filteredUrls, cancellationToken).ConfigureAwait(false);

			// Apply resolution filtering to resolved filenames
			var filteredResults = _resolutionFilter.FilterResolvedUrls(results);

			await Logger.LogVerboseAsync($"[DownloadCacheService] Pre-resolved {filteredResults.Count} URLs (after resolution filtering)");
			return filteredResults;
		}

		/// <summary>
		/// Initializes the virtual file system with the current state of the real file system.
		/// This should be called before processing downloads to ensure proper archive filename detection.
		/// </summary>
		public async Task InitializeVirtualFileSystemAsync(string rootPath)
		{
			if ( string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath) )
				return;

			await _virtualFileSystem.InitializeFromRealFileSystemAsync(rootPath);
			await Logger.LogVerboseAsync($"[DownloadCacheService] VirtualFileSystem initialized for: {rootPath}");
		}

		/// <summary>
		/// Adds or updates a cache entry for a downloaded file.
		/// </summary>
		public void AddOrUpdate(Guid componentGuid, string url, DownloadCacheEntry entry)
		{
			if ( componentGuid == Guid.Empty )
			{
				Logger.LogWarning($"[DownloadCacheService] Cannot add entry with empty component GUID for URL: {url}");
				return;
			}

			if ( string.IsNullOrWhiteSpace(url) )
			{
				Logger.LogWarning($"[DownloadCacheService] Cannot add entry with empty URL for component: {componentGuid}");
				return;
			}

			lock ( _cacheLock )
			{
				if ( !_cache.ContainsKey(componentGuid) )
					_cache[componentGuid] = new Dictionary<string, DownloadCacheEntry>(StringComparer.OrdinalIgnoreCase);

				_cache[componentGuid][url] = entry;
				Logger.LogVerbose($"[DownloadCacheService] Added/Updated cache entry for component {componentGuid}, URL: {url}, Archive: {entry.ArchiveName}");
			}
		}

		/// <summary>
		/// Tries to get a cached entry for a component and URL.
		/// </summary>
		public bool TryGetEntry(Guid componentGuid, string url, out DownloadCacheEntry entry)
		{
			entry = null;

			if ( componentGuid == Guid.Empty || string.IsNullOrWhiteSpace(url) )
				return false;

			lock ( _cacheLock )
			{
				if ( _cache.TryGetValue(componentGuid, out Dictionary<string, DownloadCacheEntry> componentCache) )
				{
					if ( componentCache.TryGetValue(url, out entry) )
					{
						Logger.LogVerbose($"[DownloadCacheService] Cache hit for component {componentGuid}, URL: {url}");
						return true;
					}
				}
			}

			Logger.LogVerbose($"[DownloadCacheService] Cache miss for component {componentGuid}, URL: {url}");
			return false;
		}

		/// <summary>
		/// Checks if a URL is cached for a component.
		/// </summary>
		public bool IsCached(Guid componentGuid, string url)
		{
			if ( componentGuid == Guid.Empty || string.IsNullOrWhiteSpace(url) )
				return false;

			lock ( _cacheLock )
			{
				return _cache.ContainsKey(componentGuid) && _cache[componentGuid].ContainsKey(url);
			}
		}

		/// <summary>
		/// Gets the archive name for a cached download, or null if not cached.
		/// </summary>
		public string GetArchiveName(Guid componentGuid, string url)
		{
			if ( TryGetEntry(componentGuid, url, out DownloadCacheEntry entry) )
				return entry.ArchiveName;

			return null;
		}

		/// <summary>
		/// Gets the file path for a cached download, or null if not cached.
		/// </summary>
		public string GetFilePath(Guid componentGuid, string url)
		{
			if ( TryGetEntry(componentGuid, url, out DownloadCacheEntry entry) )
				return entry.FilePath;

			return null;
		}

		/// <summary>
		/// Gets the Extract instruction GUID for a cached download, or Guid.Empty if not cached or no instruction exists.
		/// </summary>
		public Guid GetExtractInstructionGuid(Guid componentGuid, string url)
		{
			if ( TryGetEntry(componentGuid, url, out DownloadCacheEntry entry) )
				return entry.ExtractInstructionGuid;

			return Guid.Empty;
		}

		/// <summary>
		/// Checks if a file is an archive based on its extension.
		/// </summary>
		public static bool IsArchive(string filePath)
		{
			if ( string.IsNullOrWhiteSpace(filePath) )
				return false;

			string extension = Path.GetExtension(filePath).ToLowerInvariant();
			return extension == ".zip" || extension == ".rar" || extension == ".7z";
		}

		/// <summary>
		/// Gets all cached URLs for a component.
		/// </summary>
		public IReadOnlyList<string> GetCachedUrls(Guid componentGuid)
		{
			if ( componentGuid == Guid.Empty )
				return Array.Empty<string>();

			lock ( _cacheLock )
			{
				if ( _cache.TryGetValue(componentGuid, out Dictionary<string, DownloadCacheEntry> componentCache) )
					return componentCache.Keys.ToList();

				return Array.Empty<string>();
			}
		}

		/// <summary>
		/// Gets all cached entries for a component.
		/// </summary>
		public IReadOnlyList<DownloadCacheEntry> GetCachedEntries(Guid componentGuid)
		{
			if ( componentGuid == Guid.Empty )
				return Array.Empty<DownloadCacheEntry>();

			lock ( _cacheLock )
			{
				if ( _cache.TryGetValue(componentGuid, out Dictionary<string, DownloadCacheEntry> componentCache) )
					return componentCache.Values.ToList();

				return Array.Empty<DownloadCacheEntry>();
			}
		}

		/// <summary>
		/// Removes a cache entry for a specific URL.
		/// </summary>
		public bool Remove(Guid componentGuid, string url)
		{
			if ( componentGuid == Guid.Empty || string.IsNullOrWhiteSpace(url) )
				return false;

			lock ( _cacheLock )
			{
				if ( _cache.TryGetValue(componentGuid, out Dictionary<string, DownloadCacheEntry> componentCache) )
				{
					if ( componentCache.Remove(url) )
					{
						Logger.LogVerbose($"[DownloadCacheService] Removed cache entry for component {componentGuid}, URL: {url}");

						// Clean up empty component entries
						if ( componentCache.Count == 0 )
							_cache.Remove(componentGuid);

						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Removes all cache entries for a component.
		/// </summary>
		public bool RemoveComponent(Guid componentGuid)
		{
			if ( componentGuid == Guid.Empty )
				return false;

			lock ( _cacheLock )
			{
				if ( _cache.Remove(componentGuid) )
				{
					Logger.LogVerbose($"[DownloadCacheService] Removed all cache entries for component {componentGuid}");
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Clears the entire cache.
		/// </summary>
		public void Clear()
		{
			lock ( _cacheLock )
			{
				_cache.Clear();
				Logger.LogVerbose("[DownloadCacheService] Cache cleared");
			}
		}

		/// <summary>
		/// Gets the total number of cached components.
		/// </summary>
		public int GetComponentCount()
		{
			lock ( _cacheLock )
			{
				return _cache.Count;
			}
		}

		/// <summary>
		/// Gets the total number of cached entries across all components.
		/// </summary>
		public int GetTotalEntryCount()
		{
			lock ( _cacheLock )
			{
				return _cache.Values.Sum(componentCache => componentCache.Count);
			}
		}

		/// <summary>
		/// Central method: Resolves or downloads a component's mod links, ensures index-0 instruction exists,
		/// and returns the cached entry. This is the ONLY way to get downloads.
		/// </summary>
		public async Task<List<DownloadCacheEntry>> ResolveOrDownloadAsync(
			ModComponent component,
			string destinationDirectory,
			IProgress<DownloadProgress> progress = null,
			CancellationToken cancellationToken = default)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));

			var results = new List<DownloadCacheEntry>();

			await Logger.LogVerboseAsync($"[DownloadCacheService] ResolveOrDownloadAsync for component: {component.Name} (GUID: {component.Guid})");
			await Logger.LogVerboseAsync($"[DownloadCacheService] ModComponent has {component.ModLink.Count} URLs");

			// Initialize virtual file system for this directory if not already done
			await InitializeVirtualFileSystemAsync(destinationDirectory);

			// Process each ModLink
			for ( int i = 0; i < component.ModLink.Count; i++ )
			{
				string url = component.ModLink[i];
				if ( string.IsNullOrWhiteSpace(url) )
					continue;

				// Apply resolution filter - skip URLs that don't match system resolution
				if ( !_resolutionFilter.ShouldDownload(url) )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService] Skipping URL due to resolution filter: {url}");
					continue;
				}

				await Logger.LogVerboseAsync($"[DownloadCacheService] Processing URL {i + 1}/{component.ModLink.Count}: {url}");

				// STEP 1: Check if already cached in memory
				if ( TryGetEntry(component.Guid, url, out DownloadCacheEntry existingEntry) )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService] URL already cached: {existingEntry.ArchiveName}");

					// Verify file still exists
					if ( !string.IsNullOrEmpty(existingEntry.FilePath) && File.Exists(existingEntry.FilePath) )
					{
						// Check if validation is enabled
						bool shouldValidate = MainConfig.ValidateAndReplaceInvalidArchives;

						// Validate archive integrity for archive files (only if validation is enabled)
						if ( shouldValidate && Utility.ArchiveHelper.IsArchive(existingEntry.FilePath) )
						{
							await Logger.LogVerboseAsync($"[DownloadCacheService] Validating archive integrity: {existingEntry.FilePath}");
							bool isValid = Utility.ArchiveHelper.IsValidArchive(existingEntry.FilePath);

							if ( !isValid )
							{
								await Logger.LogWarningAsync($"[DownloadCacheService] Archive validation failed (corrupt/incomplete): {existingEntry.FilePath}");
								await Logger.LogWarningAsync($"[DownloadCacheService] Deleting invalid archive and re-downloading...");

								try
								{
									File.Delete(existingEntry.FilePath);
									await Logger.LogVerboseAsync($"[DownloadCacheService] Deleted invalid archive: {existingEntry.FilePath}");

									// Remove from cache so it will be re-downloaded
									Remove(component.Guid, url);
								}
								catch ( Exception ex )
								{
									await Logger.LogErrorAsync($"[DownloadCacheService] Failed to delete invalid archive: {ex.Message}");
									// Continue anyway to attempt re-download
								}

								// Don't skip - fall through to download logic below
							}
							else
							{
								await Logger.LogVerboseAsync($"[DownloadCacheService] Archive validation successful: {existingEntry.FilePath}");

								// Report as skipped - file is valid
								progress?.Report(new DownloadProgress
								{
									ModName = component.Name,
									Url = url,
									Status = DownloadStatus.Skipped,
									StatusMessage = "File already exists, skipping download",
									ProgressPercentage = 100,
									FilePath = existingEntry.FilePath,
									TotalBytes = new FileInfo(existingEntry.FilePath).Length,
									BytesDownloaded = new FileInfo(existingEntry.FilePath).Length
								});

								results.Add(existingEntry);
								continue;
							}
						}
						else
						{
							// Validation disabled or non-archive file - skip without validation
							string fileType = Utility.ArchiveHelper.IsArchive(existingEntry.FilePath) ? "archive" : "non-archive";
							string reason = shouldValidate ? $"{fileType} file exists" : "file exists (validation disabled)";
							await Logger.LogVerboseAsync($"[DownloadCacheService] {char.ToUpper(reason[0])}{reason.Substring(1)}, skipping download: {existingEntry.FilePath}");

							// Report as skipped
							progress?.Report(new DownloadProgress
							{
								ModName = component.Name,
								Url = url,
								Status = DownloadStatus.Skipped,
								StatusMessage = "File already exists, skipping download",
								ProgressPercentage = 100,
								FilePath = existingEntry.FilePath,
								TotalBytes = new FileInfo(existingEntry.FilePath).Length,
								BytesDownloaded = new FileInfo(existingEntry.FilePath).Length
							});

							results.Add(existingEntry);
							continue;
						}
					}
					else
					{
						await Logger.LogWarningAsync($"[DownloadCacheService] Cached file no longer exists: {existingEntry.FilePath}, will re-download");
					}
				}

				// STEP 2: Check if file exists on disk (cache miss - file downloaded in previous session)
				// Need to resolve URL to filename first to check disk
				if ( !TryGetEntry(component.Guid, url, out _) )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService] Cache miss, checking if file exists on disk from previous session...");

					// Try to resolve URL to filename without downloading
					var resolved = await _downloadManager.ResolveUrlsToFilenamesAsync(new List<string> { url }, cancellationToken);

					if ( resolved.TryGetValue(url, out List<string> value) && value.Count > 0 )
					{
						// Apply resolution filter to resolved filenames
						List<string> filteredFilenames = _resolutionFilter.FilterByResolution(value);
						if ( filteredFilenames.Count == 0 )
						{
							await Logger.LogVerboseAsync($"[DownloadCacheService] All resolved filenames filtered out by resolution filter");
							continue; // Skip to next URL
						}

						string expectedFileName = filteredFilenames[0]; // Use first filename if multiple
						string expectedFilePath = Path.Combine(destinationDirectory, expectedFileName);

						await Logger.LogVerboseAsync($"[DownloadCacheService] Resolved filename: {expectedFileName}");

						if ( File.Exists(expectedFilePath) )
						{
							await Logger.LogVerboseAsync($"[DownloadCacheService] File exists on disk from previous session: {expectedFilePath}");

							// Check if validation is enabled
							bool shouldValidate = MainConfig.ValidateAndReplaceInvalidArchives;
							bool isValid = true;

							// Validate archive integrity if enabled
							if ( shouldValidate && Utility.ArchiveHelper.IsArchive(expectedFilePath) )
							{
								await Logger.LogVerboseAsync($"[DownloadCacheService] Validating existing archive: {expectedFilePath}");
								isValid = Utility.ArchiveHelper.IsValidArchive(expectedFilePath);

								if ( !isValid )
								{
									await Logger.LogWarningAsync($"[DownloadCacheService] Existing archive is invalid: {expectedFilePath}");
									await Logger.LogWarningAsync($"[DownloadCacheService] Deleting invalid archive and will re-download...");

									try
									{
										File.Delete(expectedFilePath);
										await Logger.LogVerboseAsync($"[DownloadCacheService] Deleted invalid archive: {expectedFilePath}");
									}
									catch ( Exception ex )
									{
										await Logger.LogErrorAsync($"[DownloadCacheService] Failed to delete invalid archive: {ex.Message}");
									}
								}
							}

							if ( isValid )
							{
								// File exists and is valid - add to cache and skip download
								bool isArchive2 = IsArchive(expectedFilePath);
								var diskEntry = new DownloadCacheEntry
								{
									Url = url,
									ArchiveName = expectedFileName,
									FilePath = expectedFilePath,
									IsArchive = isArchive2,
									ExtractInstructionGuid = Guid.Empty
								};

								AddOrUpdate(component.Guid, url, diskEntry);

								string validationInfo = shouldValidate ? (isArchive2 ? " (validated)" : "") : " (validation disabled)";
								await Logger.LogVerboseAsync($"[DownloadCacheService] Added existing file to cache{validationInfo}: {expectedFileName}");

								// Report as skipped
								progress?.Report(new DownloadProgress
								{
									ModName = component.Name,
									Url = url,
									Status = DownloadStatus.Skipped,
									StatusMessage = $"File already exists{validationInfo}, skipping download",
									ProgressPercentage = 100,
									FilePath = expectedFilePath,
									TotalBytes = new FileInfo(expectedFilePath).Length,
									BytesDownloaded = new FileInfo(expectedFilePath).Length
								});

								results.Add(diskEntry);
								continue; // Skip to next URL
							}
						}
					}
				}

				// STEP 3: Download if not cached and not on disk (or validation failed)
				if ( _downloadManager == null )
					throw new InvalidOperationException("Download manager not set. Call SetDownloadManager() first.");

				await Logger.LogVerboseAsync($"[DownloadCacheService] Downloading: {url}");

				var progressTracker = new DownloadProgress
				{
					ModName = component.Name,
					Url = url,
					Status = DownloadStatus.Pending,
					StatusMessage = "Waiting to start...",
					ProgressPercentage = 0
				};

				// Forward progress updates
				var progressForwarder = new Progress<DownloadProgress>(p =>
				{
					// Update the progress tracker with the new values
					progressTracker.Status = p.Status;
					progressTracker.StatusMessage = p.StatusMessage;
					progressTracker.ProgressPercentage = p.ProgressPercentage;
					progressTracker.BytesDownloaded = p.BytesDownloaded;
					progressTracker.TotalBytes = p.TotalBytes;
					progressTracker.FilePath = p.FilePath;
					progressTracker.StartTime = p.StartTime;
					progressTracker.EndTime = p.EndTime;
					progressTracker.ErrorMessage = p.ErrorMessage;
					progressTracker.Exception = p.Exception;

					// Report the updated progress to the UI (log only significant events)
					if ( progressTracker.Status == DownloadStatus.Pending ||
						 progressTracker.Status == DownloadStatus.Completed ||
						 progressTracker.Status == DownloadStatus.Failed )
					{
						Logger.LogVerbose($"[DownloadCache] {progressTracker.Status}: {progressTracker.StatusMessage}");
					}
					progress?.Report(progressTracker);
				});

				// Download using the manager
				var urlMap = new Dictionary<string, DownloadProgress> { { url, progressTracker } };
				var downloadResults = await _downloadManager.DownloadAllWithProgressAsync(
					urlMap,
					destinationDirectory,
					progressForwarder,
					cancellationToken);

				if ( downloadResults.Count == 0 || !downloadResults[0].Success || progressTracker.Status != DownloadStatus.Completed )
				{
					await Logger.LogErrorAsync($"[DownloadCacheService] Download failed for URL: {url}");
					continue;
				}

				// Create cache entry for completed download
				string fileName = Path.GetFileName(progressTracker.FilePath);
				bool isArchive = IsArchive(fileName);

				var newEntry = new DownloadCacheEntry
				{
					Url = url,
					ArchiveName = fileName,
					FilePath = progressTracker.FilePath,
					IsArchive = isArchive,
					ExtractInstructionGuid = Guid.Empty // Will be set when instruction is created
				};

				AddOrUpdate(component.Guid, url, newEntry);
				results.Add(newEntry);

				await Logger.LogVerboseAsync($"[DownloadCacheService] Download completed and cached: {fileName}");
			}

			// Now ensure each cached entry has a corresponding index-N instruction
			await EnsureInstructionsExist(component, results);

			return results;
		}

		/// <summary>
		/// Ensures that instructions exist for each cached entry.
		/// Checks ALL existing instructions to avoid creating duplicates.
		/// Appends new instructions to the end if not found.
		/// </summary>
		private async Task EnsureInstructionsExist(ModComponent component, List<DownloadCacheEntry> entries)
		{
			await Logger.LogVerboseAsync($"[DownloadCacheService] Ensuring instructions exist for {entries.Count} cached entries");

			foreach ( var entry in entries )
			{
				// Check if an instruction for this archive already exists ANYWHERE in the component
				Instruction existingInstruction = null;
				foreach ( var instruction in component.Instructions )
				{
					// Check if this instruction's source references our archive
					if ( instruction.Source != null && instruction.Source.Any(src =>
						{
							// Check both direct filename match and resolved path match
							if ( src.IndexOf(entry.ArchiveName, StringComparison.OrdinalIgnoreCase) >= 0 )
								return true;
							return _virtualFileSystem.FileExists(ResolveInstructionSource(src, entry.ArchiveName));
						}) )
					{
						existingInstruction = instruction;
						break;
					}
				}

				if ( existingInstruction != null )
				{
					// Instruction already exists - just update cache entry if needed
					if ( entry.ExtractInstructionGuid == Guid.Empty || entry.ExtractInstructionGuid != existingInstruction.Guid )
					{
						entry.ExtractInstructionGuid = existingInstruction.Guid;
						AddOrUpdate(component.Guid, entry.Url, entry);
						await Logger.LogVerboseAsync($"[DownloadCacheService] Found existing instruction for {entry.ArchiveName} (GUID: {existingInstruction.Guid})");
					}
				}
				else
				{
					// No instruction exists for this archive - create a new one and APPEND to the end
					var newInstruction = new Instruction
					{
						Guid = Guid.NewGuid(),
						Action = entry.IsArchive ? Instruction.ActionType.Extract : Instruction.ActionType.Move,
						Source = new List<string> { $@"<<modDirectory>>\{entry.ArchiveName}" },
						Destination = entry.IsArchive
							? string.Empty
							: @"<<kotorDirectory>>\Override",
						Overwrite = true
					};
					newInstruction.SetParentComponent(component);

					// Append to the end instead of inserting at a specific index
					component.Instructions.Add(newInstruction);

					// Update cache entry with instruction GUID
					entry.ExtractInstructionGuid = newInstruction.Guid;
					AddOrUpdate(component.Guid, entry.Url, entry);

					await Logger.LogVerboseAsync($"[DownloadCacheService] Created new instruction for {entry.ArchiveName} (GUID: {newInstruction.Guid})");
				}
			}
		}

		/// <summary>
		/// Gets cached entry without downloading. Returns null if not cached or file doesn't exist.
		/// </summary>
		public DownloadCacheEntry GetCachedAsync(Guid componentGuid, string url)
		{
			if ( TryGetEntry(componentGuid, url, out DownloadCacheEntry entry) )
			{
				// Verify file still exists
				if ( !string.IsNullOrEmpty(entry.FilePath) && File.Exists(entry.FilePath) )
					return entry;
			}
			return null;
		}

		/// <summary>
		/// Resolves instruction source path by replacing placeholders and ensuring it matches the archive name.
		/// </summary>
		private static string ResolveInstructionSource(string sourcePath, string archiveName)
		{
			if ( string.IsNullOrWhiteSpace(sourcePath) )
				return sourcePath;

			// Replace <<modDirectory>> placeholder with actual path
			string resolved = sourcePath.Replace("<<modDirectory>>", "");

			// Ensure the resolved path contains the archive name
			if ( resolved.Contains(archiveName) )
				return resolved;

			// If it doesn't contain the archive name, construct the expected path
			return Path.Combine(resolved.TrimStart('\\'), archiveName);
		}
	}

	/// <summary>
	/// Represents a cached download entry.
	/// </summary>
	public sealed class DownloadCacheEntry
	{
		/// <summary>
		/// The URL from which the file was downloaded.
		/// </summary>
		public string Url { get; set; }

		/// <summary>
		/// The name of the downloaded archive/file.
		/// </summary>
		public string ArchiveName { get; set; }

		/// <summary>
		/// The full path to the downloaded file.
		/// </summary>
		public string FilePath { get; set; }

		/// <summary>
		/// The GUID of the Extract instruction that was created for this download.
		/// Empty if no instruction was created (e.g., for non-archive files).
		/// </summary>
		public Guid ExtractInstructionGuid { get; set; }

		/// <summary>
		/// Whether this is an archive file that needs extraction.
		/// </summary>
		public bool IsArchive { get; set; }

		/// <summary>
		/// Timestamp when this entry was cached.
		/// </summary>
		public DateTime CachedAt { get; set; }

		public DownloadCacheEntry()
		{
			CachedAt = DateTime.Now;
		}

		public override string ToString() =>
			$"DownloadCacheEntry[Archive={ArchiveName}, IsArchive={IsArchive}, ExtractGuid={ExtractInstructionGuid}]";
	}
}


