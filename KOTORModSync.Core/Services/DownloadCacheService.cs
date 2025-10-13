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
using Newtonsoft.Json;

namespace KOTORModSync.Core.Services
{

	public sealed class DownloadCacheService
	{
		// Cache keyed by URL only (filename resolution doesn't depend on component)
		private readonly Dictionary<string, DownloadCacheEntry> _cache;
		private readonly object _cacheLock = new object();
		public DownloadManager _downloadManager;
		private VirtualFileSystemProvider _virtualFileSystem;
		private ResolutionFilterService _resolutionFilter;
		private readonly string _cacheFilePath;
		private static readonly object _staticLock = new object();
		private static bool _cacheLoaded = false;

		public DownloadCacheService()
		{
			_cache = new Dictionary<string, DownloadCacheEntry>(StringComparer.OrdinalIgnoreCase);
			_virtualFileSystem = new VirtualFileSystemProvider();
			_resolutionFilter = new ResolutionFilterService(MainConfig.FilterDownloadsByResolution);
			_cacheFilePath = GetCacheFilePath();

			// Load cache from disk only once per application lifetime
			lock ( _staticLock )
			{
				if ( !_cacheLoaded )
				{
					LoadCacheFromDisk();
					_cacheLoaded = true;
				}
			}

			Logger.LogVerbose("[DownloadCacheService] Initialized");
		}

		public void SetDownloadManager(DownloadManager downloadManager = null)
		{
			if ( downloadManager is null )
			{
				// Configure a shared HttpClient with optimized handler for resolution speed (compat with netstandard2.0)
				var handler = new System.Net.Http.HttpClientHandler
				{
					AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
					MaxConnectionsPerServer = 128
				};
				var httpClient = new System.Net.Http.HttpClient(handler)
				{
					Timeout = TimeSpan.FromHours(3)
				};
				var handlers = new List<Core.Services.Download.IDownloadHandler>
				{
					new Core.Services.Download.DeadlyStreamDownloadHandler(httpClient),
					new Core.Services.Download.MegaDownloadHandler(),
					new Core.Services.Download.NexusModsDownloadHandler(httpClient, MainConfig.NexusModsApiKey),
					new Core.Services.Download.GameFrontDownloadHandler(httpClient),
					new Core.Services.Download.DirectDownloadHandler(httpClient),
				};
				_downloadManager = new Core.Services.Download.DownloadManager(handlers);
			}
			else
			{
				_downloadManager = downloadManager;
			}
		}

		public async Task<Dictionary<string, List<string>>> PreResolveUrlsAsync(
			ModComponent component,
			DownloadManager downloadManager = null,
			CancellationToken cancellationToken = default)
		{
			if ( component == null )
				throw new ArgumentNullException(nameof(component));

			downloadManager = downloadManager ?? _downloadManager;
			if ( downloadManager == null )
				throw new InvalidOperationException("DownloadManager is not set. Call SetDownloadManager() first.");

			await Logger.LogVerboseAsync($"[DownloadCacheService] Pre-resolving URLs for component: {component.Name}");

			var urls = component.ModLink.Where(url => !string.IsNullOrWhiteSpace(url)).ToList();
			if ( urls.Count == 0 )
			{
				await Logger.LogVerboseAsync("[DownloadCacheService] No URLs to resolve");
				return new Dictionary<string, List<string>>();
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

			// Check cache first and only resolve URLs that aren't cached
			var results = new Dictionary<string, List<string>>();
			var urlsToResolve = new List<string>();
			int cacheHits = 0;

			foreach ( string url in filteredUrls )
			{
				if ( TryGetEntry(url, out DownloadCacheEntry cachedEntry) )
				{
					// Found in cache - use cached filename
					results[url] = new List<string> { cachedEntry.FileName };
					cacheHits++;
					await Logger.LogVerboseAsync($"[DownloadCacheService] Cache hit for URL: {url} -> {cachedEntry.FileName}");
				}
				else
				{
					// Not in cache - need to resolve
					urlsToResolve.Add(url);
				}
			}

			if ( cacheHits > 0 )
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService] Retrieved {cacheHits} filename(s) from cache");
			}

			// Resolve URLs that weren't cached
			if ( urlsToResolve.Count > 0 )
			{
				await Logger.LogVerboseAsync($"[DownloadCacheService] Resolving {urlsToResolve.Count} uncached URL(s) via network");
				var resolvedResults = await downloadManager.ResolveUrlsToFilenamesAsync(urlsToResolve, cancellationToken).ConfigureAwait(false);

				// Add resolved results to our results dictionary and cache them
				foreach ( var kvp in resolvedResults )
				{
					results[kvp.Key] = kvp.Value;

					// Cache the resolved filename
					if ( kvp.Value != null && kvp.Value.Count > 0 )
					{
						string resolvedFilename = kvp.Value[0];
						var cacheEntry = new DownloadCacheEntry
						{
							Url = kvp.Key,
							FileName = resolvedFilename,
							IsArchive = IsArchive(resolvedFilename),
							ExtractInstructionGuid = Guid.Empty
						};

						AddOrUpdate(kvp.Key, cacheEntry);
						await Logger.LogVerboseAsync($"[DownloadCacheService] Cached resolved filename: {kvp.Key} -> {resolvedFilename}");
					}
				}
			}

			var filteredResults = _resolutionFilter.FilterResolvedUrls(results);

			await Logger.LogVerboseAsync($"[DownloadCacheService] Pre-resolved {filteredResults.Count} URLs ({cacheHits} from cache, {urlsToResolve.Count} from network)");
			return filteredResults;
		}

		public async Task InitializeVirtualFileSystemAsync(string rootPath)
		{
			if ( string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath) )
				return;

			await _virtualFileSystem.InitializeFromRealFileSystemAsync(rootPath);
			await Logger.LogVerboseAsync($"[DownloadCacheService] VirtualFileSystem initialized for: {rootPath}");
		}

		public void AddOrUpdate(string url, DownloadCacheEntry entry)
		{
			if ( string.IsNullOrWhiteSpace(url) )
			{
				Logger.LogWarning($"[DownloadCacheService] Cannot add entry with empty URL");
				return;
			}

			lock ( _cacheLock )
			{
				_cache[url] = entry;
				Logger.LogVerbose($"[DownloadCacheService] Added/Updated cache entry for URL: {url}, Archive: {entry.FileName}");
			}

			SaveCacheToDisk();
		}

		public bool TryGetEntry(string url, out DownloadCacheEntry entry)
		{
			entry = null;

			if ( string.IsNullOrWhiteSpace(url) )
				return false;

			lock ( _cacheLock )
			{
				if ( _cache.TryGetValue(url, out entry) )
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

			lock ( _cacheLock )
			{
				return _cache.ContainsKey(url);
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
				// Compute full path on-the-fly from FileName + MainConfig.SourcePath
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

		public static bool IsArchive(string filePath)
		{
			if ( string.IsNullOrWhiteSpace(filePath) )
				return false;

			string extension = Path.GetExtension(filePath).ToLowerInvariant();
			return extension == ".zip" || extension == ".rar" || extension == ".7z";
		}

		public IReadOnlyList<string> GetCachedUrls()
		{
			lock ( _cacheLock )
			{
				return _cache.Keys.ToList();
			}
		}

		public IReadOnlyList<DownloadCacheEntry> GetCachedEntries()
		{
			lock ( _cacheLock )
			{
				return _cache.Values.ToList();
			}
		}

		public bool Remove(string url)
		{
			if ( string.IsNullOrWhiteSpace(url) )
				return false;

			bool removed = false;
			lock ( _cacheLock )
			{
				if ( _cache.Remove(url) )
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
			lock ( _cacheLock )
			{
				_cache.Clear();
				Logger.LogVerbose("[DownloadCacheService] Cache cleared");
			}

			SaveCacheToDisk();
		}

		public int GetTotalEntryCount()
		{
			lock ( _cacheLock )
			{
				return _cache.Count;
			}
		}

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

			await InitializeVirtualFileSystemAsync(destinationDirectory);

			for ( int i = 0; i < component.ModLink.Count; i++ )
			{
				string url = component.ModLink[i];
				if ( string.IsNullOrWhiteSpace(url) )
					continue;

				if ( !_resolutionFilter.ShouldDownload(url) )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService] Skipping URL due to resolution filter: {url}");
					continue;
				}

				await Logger.LogVerboseAsync($"[DownloadCacheService] Processing URL {i + 1}/{component.ModLink.Count}: {url}");

				if ( TryGetEntry(url, out DownloadCacheEntry existingEntry) )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService] URL already cached: {existingEntry.FileName}");

					// Compute full path from FileName + MainConfig.SourcePath
					string existingFilePath = !string.IsNullOrEmpty(existingEntry.FileName) && MainConfig.SourcePath != null
						? Path.Combine(MainConfig.SourcePath.FullName, existingEntry.FileName)
						: existingEntry.FileName;

					if ( !string.IsNullOrEmpty(existingFilePath) && File.Exists(existingFilePath) )
					{

						bool shouldValidate = MainConfig.ValidateAndReplaceInvalidArchives;

						if ( shouldValidate && Utility.ArchiveHelper.IsArchive(existingFilePath) )
						{
							await Logger.LogVerboseAsync($"[DownloadCacheService] Validating archive integrity: {existingFilePath}");
							bool isValid = Utility.ArchiveHelper.IsValidArchive(existingFilePath);

							if ( !isValid )
							{
								await Logger.LogWarningAsync($"[DownloadCacheService] Archive validation failed (corrupt/incomplete): {existingFilePath}");
								await Logger.LogWarningAsync($"[DownloadCacheService] Deleting invalid archive and re-downloading...");

								try
								{
									File.Delete(existingFilePath);
									await Logger.LogVerboseAsync($"[DownloadCacheService] Deleted invalid archive: {existingFilePath}");

									Remove(url);
								}
								catch ( Exception ex )
								{
									await Logger.LogErrorAsync($"[DownloadCacheService] Failed to delete invalid archive: {ex.Message}");

								}

							}
							else
							{
								await Logger.LogVerboseAsync($"[DownloadCacheService] Archive validation successful: {existingFilePath}");

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

								results.Add(existingEntry);
								continue;
							}
						}
						else
						{

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

							results.Add(existingEntry);
							continue;
						}
					}
					else
					{
						await Logger.LogWarningAsync($"[DownloadCacheService] Cached file no longer exists: {existingFilePath}, will re-download");
					}
				}

				if ( !TryGetEntry(url, out _) )
				{
					await Logger.LogVerboseAsync($"[DownloadCacheService] Cache miss, checking if file exists on disk from previous session...");

					var resolved = await _downloadManager.ResolveUrlsToFilenamesAsync(new List<string> { url }, cancellationToken);

					if ( resolved.TryGetValue(url, out List<string> value) && value.Count > 0 )
					{

						List<string> filteredFilenames = _resolutionFilter.FilterByResolution(value);
						if ( filteredFilenames.Count == 0 )
						{
							await Logger.LogVerboseAsync($"[DownloadCacheService] All resolved filenames filtered out by resolution filter");
							continue;
						}

						string expectedFileName = filteredFilenames[0];
						// Always use MainConfig.SourcePath since it can change at runtime
						string expectedFilePath = MainConfig.SourcePath != null
							? Path.Combine(MainConfig.SourcePath.FullName, expectedFileName)
							: Path.Combine(destinationDirectory, expectedFileName);

						await Logger.LogVerboseAsync($"[DownloadCacheService] Resolved filename: {expectedFileName}");

						if ( File.Exists(expectedFilePath) )
						{
							await Logger.LogVerboseAsync($"[DownloadCacheService] File exists on disk from previous session: {expectedFilePath}");

							bool shouldValidate = MainConfig.ValidateAndReplaceInvalidArchives;
							bool isValid = true;

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

								bool isArchive2 = IsArchive(expectedFilePath);
								var diskEntry = new DownloadCacheEntry
								{
									Url = url,
									FileName = expectedFileName,
									IsArchive = isArchive2,
									ExtractInstructionGuid = Guid.Empty
								};

								AddOrUpdate(url, diskEntry);

								string validationInfo = shouldValidate ? (isArchive2 ? " (validated)" : "") : " (validation disabled)";
								await Logger.LogVerboseAsync($"[DownloadCacheService] Added existing file to cache{validationInfo}: {expectedFileName}");

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
								continue;
							}
						}
					}
				}

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

				var progressForwarder = new Progress<DownloadProgress>(p =>
				{

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

					if ( progressTracker.Status == DownloadStatus.Pending ||
						 progressTracker.Status == DownloadStatus.Completed ||
						 progressTracker.Status == DownloadStatus.Failed )
					{
						Logger.LogVerbose($"[DownloadCache] {progressTracker.Status}: {progressTracker.StatusMessage}");
					}
					progress?.Report(progressTracker);
				});

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

				string fileName = Path.GetFileName(progressTracker.FilePath);
				bool isArchive = IsArchive(fileName);

				var newEntry = new DownloadCacheEntry
				{
					Url = url,
					FileName = fileName,
					IsArchive = isArchive,
					ExtractInstructionGuid = Guid.Empty
				};

				AddOrUpdate(url, newEntry);
				results.Add(newEntry);

				await Logger.LogVerboseAsync($"[DownloadCacheService] Download completed and cached: {fileName}");
			}

			await EnsureInstructionsExist(component, results);

			return results;
		}

		private async Task EnsureInstructionsExist(ModComponent component, List<DownloadCacheEntry> entries)
		{
			await Logger.LogVerboseAsync($"[DownloadCacheService] Ensuring instructions exist for {entries.Count} cached entries");

			foreach ( var entry in entries )
			{
				// Check if instructions already exist for this file using wildcard matching
				string archiveFullPath = $@"<<modDirectory>>\{entry.FileName}";
				Instruction existingInstruction = null;

				foreach ( var instruction in component.Instructions )
				{
					if ( instruction.Source != null && instruction.Source.Any(src =>
						{
							// Check basic substring match
							if ( src.IndexOf(entry.FileName, StringComparison.OrdinalIgnoreCase) >= 0 )
								return true;

							// Use wildcard matching for pattern-based sources
							try
							{
								if ( FileSystemUtils.PathHelper.WildcardPathMatch(archiveFullPath, src) )
									return true;
							}
							catch ( Exception ex )
							{
								Logger.LogException(ex);
							}

							// Check if resolved path exists in virtual file system
							return _virtualFileSystem.FileExists(ResolveInstructionSource(src, entry.FileName));
						}) )
					{
						existingInstruction = instruction;
						break;
					}
				}

				if ( existingInstruction != null )
				{
					// Update cache entry with existing instruction GUID
					if ( entry.ExtractInstructionGuid == Guid.Empty || entry.ExtractInstructionGuid != existingInstruction.Guid )
					{
						entry.ExtractInstructionGuid = existingInstruction.Guid;
						AddOrUpdate(entry.Url, entry);
						await Logger.LogVerboseAsync($"[DownloadCacheService] Found existing instruction for {entry.FileName} (GUID: {existingInstruction.Guid})");
					}
				}
				else
				{
					// No existing instruction - use AutoInstructionGenerator for comprehensive analysis
					int initialInstructionCount = component.Instructions.Count;

					// Compute full path from FileName
					string entryFilePath = !string.IsNullOrEmpty(entry.FileName) && MainConfig.SourcePath != null
						? Path.Combine(MainConfig.SourcePath.FullName, entry.FileName)
						: entry.FileName;

					if ( entry.IsArchive && !string.IsNullOrEmpty(entryFilePath) && File.Exists(entryFilePath) )
					{
						// For archives, do comprehensive analysis (TSLPatcher detection, folder structure, etc.)
						bool generated = AutoInstructionGenerator.GenerateInstructions(component, entryFilePath);

						// Check if the file was deleted due to corruption
						if ( !File.Exists(entryFilePath) )
						{
							await Logger.LogWarningAsync($"[DownloadCacheService] Archive was deleted (likely corrupted): {entryFilePath}");
							await Logger.LogWarningAsync($"[DownloadCacheService] Removing from cache and creating placeholder instruction");

							// Remove from cache
							Remove(entry.Url);

							// Create placeholder instruction since file doesn't exist anymore
							CreateSimpleInstructionForEntry(component, entry);
							continue;
						}

						if ( generated )
						{
							await Logger.LogVerboseAsync($"[DownloadCacheService] Auto-generated comprehensive instructions for {entry.FileName}");

							// Find the instruction that was just created for this archive using wildcard matching
							Instruction newInstruction = null;
							for ( int j = initialInstructionCount; j < component.Instructions.Count; j++ )
							{
								var instr = component.Instructions[j];
								if ( instr.Source != null )
								{
									foreach ( var s in instr.Source )
									{
										// Check if the source pattern matches the archive path
										if ( s.IndexOf(entry.FileName, StringComparison.OrdinalIgnoreCase) >= 0 )
										{
											Logger.Log($"[DownloadCacheService] Found exact match existing instruction for {entry.FileName}: {instr.Guid.ToString()}");
											newInstruction = instr;
											break;
										}
										// Use wildcard matching for more complex patterns
										try
										{
											if ( FileSystemUtils.PathHelper.WildcardPathMatch(archiveFullPath, s) )
											{
												Logger.Log($"[DownloadCacheService] WildcardPathMatch found existing instruction for {entry.FileName}: {instr.Guid.ToString()}");
												newInstruction = instr;
												break;
											}
										}
										catch ( Exception ex )
										{
											Logger.LogException(ex);
										}
									}
								}
								if ( newInstruction != null )
								{
									break;
								}
							}

							if ( newInstruction != null )
							{
								entry.ExtractInstructionGuid = newInstruction.Guid;
								AddOrUpdate(entry.Url, entry);
								Logger.LogVerbose($"[DownloadCacheService] Found existing instruction for {entry.FileName} (GUID: {newInstruction.Guid})");
							}
						}
						else
						{
							// Fallback to simple Extract instruction if comprehensive generation failed
							CreateSimpleInstructionForEntry(component, entry);
						}
					}
					else
					{
						// For non-archives or files that don't exist, create simple Move/Extract instruction
						CreateSimpleInstructionForEntry(component, entry);
					}
				}
			}
		}

		/// <summary>
		/// Creates a placeholder Extract or Move instruction when comprehensive generation isn't applicable.
		/// </summary>
		private void CreateSimpleInstructionForEntry(ModComponent component, DownloadCacheEntry entry)
		{
			var newInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = entry.IsArchive
						 ? Instruction.ActionType.Extract
						 : Instruction.ActionType.Move,
				Source = new List<string> { $@"<<modDirectory>>\{entry.FileName}" },
				Destination = entry.IsArchive
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

		public DownloadCacheEntry GetCachedAsync(string url)
		{
			if ( TryGetEntry(url, out DownloadCacheEntry entry) )
			{
				// Compute full path from FileName
				if ( !string.IsNullOrEmpty(entry.FileName) )
				{
					string entryFilePath = MainConfig.SourcePath != null
						? Path.Combine(MainConfig.SourcePath.FullName, entry.FileName)
						: entry.FileName;

					if ( File.Exists(entryFilePath) )
						return entry;
				}
			}
			return null;
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

		/// <summary>
		/// Gets the cross-platform cache file path.
		/// </summary>
		private static string GetCacheFilePath()
		{
			string appDataPath;

			// Use appropriate application data directory based on platform
			if ( Environment.OSVersion.Platform == PlatformID.Win32NT )
			{
				// Windows: %LOCALAPPDATA%\KOTORModSync\
				appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			}
			else
			{
				// Unix/Linux/Mac: ~/.local/share/KOTORModSync/ or ~/.config/KOTORModSync/
				string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				appDataPath = Path.Combine(homeDir, ".local", "share");
			}

			string cacheDir = Path.Combine(appDataPath, "KOTORModSync");

			// Create directory if it doesn't exist
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

		/// <summary>
		/// Saves the cache to disk.
		/// </summary>
		private void SaveCacheToDisk()
		{
			try
			{
				lock ( _cacheLock )
				{
					string json = JsonConvert.SerializeObject(_cache, Formatting.Indented);
					File.WriteAllText(_cacheFilePath, json);
				}

				Logger.LogVerbose($"[DownloadCacheService] Saved cache to disk: {_cacheFilePath}");
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"[DownloadCacheService] Failed to save cache to disk: {ex.Message}");
			}
		}

		/// <summary>
		/// Loads the cache from disk.
		/// </summary>
		private void LoadCacheFromDisk()
		{
			if ( !File.Exists(_cacheFilePath) )
			{
				Logger.LogVerbose("[DownloadCacheService] No cache file found on disk, starting with empty cache");
				return;
			}

			try
			{
				string json = File.ReadAllText(_cacheFilePath);
				var loadedCache = JsonConvert.DeserializeObject<Dictionary<string, DownloadCacheEntry>>(json);

				if ( loadedCache == null )
				{
					Logger.LogWarning("[DownloadCacheService] Cache file contained no data");
					return;
				}

				lock ( _cacheLock )
				{
					_cache.Clear();

					foreach ( var kvp in loadedCache )
					{
						// Load all cached entries - do NOT validate file existence
						// The cache stores filenames, not full paths, so we can't validate without SourcePath
						_cache[kvp.Key] = kvp.Value;
						Logger.LogVerbose($"[DownloadCacheService] Loaded cached entry: {kvp.Value.FileName}");
					}
				}

				Logger.LogVerbose($"[DownloadCacheService] Loaded cache from disk: {_cacheFilePath} ({GetTotalEntryCount()} entries)");
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"[DownloadCacheService] Failed to load cache from disk: {ex.Message}");
			}
		}
	}

	public sealed class DownloadCacheEntry
	{
		public string Url { get; set; }

		/// <summary>
		/// The filename only (NOT full path). Combine with MainConfig.SourcePath to get full path.
		/// </summary>
		public string FileName { get; set; }

		public Guid ExtractInstructionGuid { get; set; }

		public bool IsArchive { get; set; }

		public DateTime CachedAt { get; set; }

		public DownloadCacheEntry()
		{
			CachedAt = DateTime.Now;
		}

		public override string ToString() =>
			$"DownloadCacheEntry[FileName={FileName}, IsArchive={IsArchive}, ExtractGuid={ExtractInstructionGuid}]";
	}
}

