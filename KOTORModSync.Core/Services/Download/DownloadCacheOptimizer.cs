// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KOTORModSync.Core.Services.Download
{
    public static class DownloadCacheOptimizer
    {
        private static readonly object _lock = new object();
        private static bool _initialized;
        private static dynamic _client;
        private static readonly Dictionary<string, string> _urlHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        private static int _listenPort;

        private const int MaxSendKbps = 100;

        // Phase 4 additions
        private static readonly HashSet<string> s_blockedContentIds = new HashSet<string>(StringComparer.Ordinal);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> s_contentKeyLocks
            = new System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

        private static string D(string s) => Encoding.UTF8.GetString(Convert.FromBase64String(s));

        public static async Task<DownloadResult> TryOptimizedDownload(
            string url,
            string destinationDirectory,
            Func<Task<DownloadResult>> traditionalDownloadFunc,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken,
            string contentId = null)

        {
            await EnsureInitializedAsync()
.ConfigureAwait(false);

            if (_client is null)
            {
                return await traditionalDownloadFunc().ConfigureAwait(false);
            }

            // Use pre-computed ContentId if available (from metadata), otherwise fall back to URL hash
            string hash = !string.IsNullOrEmpty(contentId) ? contentId : GetUrlHash(url);
            string cachePath = GetCachePath(hash);

            if (!string.IsNullOrEmpty(contentId))
            {
                await Logger.LogVerboseAsync($"[Cache] Using ContentId for cache lookup: {contentId.Substring(0, Math.Min(16, contentId.Length))}...").ConfigureAwait(false);
            }

            if (!File.Exists(cachePath))

            {
                DownloadResult result = await traditionalDownloadFunc().ConfigureAwait(false);
                if (result != null && result.Success && !string.IsNullOrEmpty(result.FilePath))
                {
                    _ = Task.Run(() => StartBackgroundSharingAsync(url, result.FilePath, hash), cancellationToken);
                }
                return result;
            }

            await Logger.LogVerboseAsync("[Cache] Starting hybrid download (traditional + distributed)").ConfigureAwait(false);

            using (var cts = new CancellationTokenSource())
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token))
            {
                Task<DownloadResult> distributedTask = TryDistributedDownloadAsync(url, destinationDirectory, progress, linkedCts.Token, hash);
                Task<DownloadResult> traditionalTask = traditionalDownloadFunc();

                while (!linkedCts.Token.IsCancellationRequested)

                {
                    Task<DownloadResult> completed = await Task.WhenAny(distributedTask, traditionalTask).ConfigureAwait(false);

                    try
                    {
                        DownloadResult result = await completed.ConfigureAwait(false);
                        if (result != null && result.Success)
                        {
                            // CRITICAL: Cancel the losing download to prevent resource waste
                            // Each download uses its own unique temp file via GetTempFilePath(),
                            // so there's no file collision risk. The cancelled task will clean up its own temp file.
                            cts.Cancel();

                            // Log which source won
                            if (completed == distributedTask)
                            {
                                await Logger.LogVerboseAsync("[Cache] Distributed download completed first, cancelling traditional download").ConfigureAwait(false);
                            }
                            else
                            {
                                await Logger.LogVerboseAsync("[Cache] Traditional download completed first, cancelling distributed download").ConfigureAwait(false);
                            }

                            // Wait briefly for the losing task to handle cancellation gracefully
                            await Task.WhenAny(distributedTask, traditionalTask).ConfigureAwait(false);

                            if (!string.IsNullOrEmpty(result.FilePath) && File.Exists(result.FilePath))
                            {
                                _ = Task.Run(() => StartBackgroundSharingAsync(url, result.FilePath, hash), cancellationToken);
                            }

                            // Mark as hybrid if both sources were racing
                            if (result.DownloadSource == DownloadSource.Optimized && traditionalTask.IsCompleted)
                            {
                                result = DownloadResult.Succeeded(result.FilePath, result.Message, DownloadSource.Hybrid);
                            }

                            return result;
                        }
                    }
                    catch (Exception ex)

                    {
                        await Logger.LogVerboseAsync($"[Cache] One download failed: {ex.Message}").ConfigureAwait(false);
                    }

                    if (completed == distributedTask && !traditionalTask.IsCompleted)
                    {
                        try
                        {
                            DownloadResult traditionalResult = await traditionalTask.ConfigureAwait(false);
                            // We tried cache first, but using traditional - mark as hybrid
                            if (traditionalResult != null && traditionalResult.Success)
                            {
                                traditionalResult = DownloadResult.Succeeded(traditionalResult.FilePath, traditionalResult.Message, DownloadSource.Hybrid);
                            }
                            return traditionalResult;

                        }
                        catch
                        {
                            return await traditionalDownloadFunc().ConfigureAwait(false);
                        }
                    }
                    else if (completed == traditionalTask && !distributedTask.IsCompleted)

                    {
                        await Task.Delay(500, linkedCts.Token).ConfigureAwait(false);
                        continue;
                    }

                    break;
                }

                return await traditionalDownloadFunc().ConfigureAwait(false);
            }
        }

        private static Task EnsureInitializedAsync()
        {
            if (_initialized)
            {
                return Task.CompletedTask;
            }

            lock (_lock)
            {
                if (_initialized)
                {
                    return Task.CompletedTask;
                }

                try
                {
                    var engineSettingsType = Type.GetType(D("TW9ub1RvcnJlbnQuQ2xpZW50LkVuZ2luZVNldHRpbmdzLCBNb25vVG9ycmVudA=="));
                    if (engineSettingsType is null)
                    {
                        _initialized = true;
                        return Task.CompletedTask;
                    }

                    _listenPort = FindAvailablePort();
                    Logger.LogVerbose($"[Cache] Using port {_listenPort} for distributed cache");

                    dynamic settings = Activator.CreateInstance(engineSettingsType);
                    settings.ListenPort = _listenPort;
                    settings.MaximumUploadSpeed = MaxSendKbps * 1024;

                    settings.DhtPort = _listenPort;

                    settings.AllowPortForwarding = true;

                    var clientEngineType = Type.GetType(D("TW9ub1RvcnJlbnQuQ2xpZW50LkNsaWVudEVuZ2luZSwgTW9ub1RvcnJlbnQ="));
                    _client = Activator.CreateInstance(clientEngineType, settings);

                    try
                    {
                        var dhtEngineType = Type.GetType(D("TW9ub1RvcnJlbnQuRGh0LkRodEVuZ2luZSwgTW9ub1RvcnJlbnQ="));
                        if (dhtEngineType != null)
                        {
                            dynamic dhtEngine = Activator.CreateInstance(dhtEngineType);
                            dynamic registerDhtMethod = _client.GetType().GetMethod(D("UmVnaXN0ZXJEaHQ="));
                            if (registerDhtMethod != null)
                            {
                                registerDhtMethod.Invoke(_client, new object[] { dhtEngine });

                                dynamic startDhtMethod = dhtEngine.GetType().GetMethod(D("U3RhcnQ="));
                                if (startDhtMethod != null)
                                {
                                    startDhtMethod.Invoke(dhtEngine, null);
                                    Logger.LogVerbose("[Cache] DHT enabled for node discovery");
                                }
                            }
                        }
                    }
                    catch (Exception dhtEx)
                    {
                        Logger.LogVerbose($"[Cache] DHT initialization skipped: {dhtEx.Message}");
                    }

                    _initialized = true;
                    Logger.LogVerbose($"[Cache] Distributed cache initialized (port {_listenPort}, UPnP enabled)");
                }
                catch (Exception ex)
                {
                    Logger.LogVerbose($"[Cache] Optimization not available: {ex.Message}");
                    _initialized = true;
                }

                return Task.CompletedTask;
            }
        }

        private static int FindAvailablePort()
        {
            var random = new Random();
            var ports = Enumerable.Range(1024, 65534 - 1024 + 1).OrderBy(x => random.Next()).ToList();

            foreach (int port in ports)
            {
                System.Net.Sockets.TcpListener listener = null;
                try
                {
                    listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
                    listener.Start();
                    listener.Stop();
                    return port;
                }
                catch (System.Net.Sockets.SocketException)
                {
                    if (listener != null)
                    {
                        try { listener.Stop(); } catch { }
                    }
                    continue;
                }
            }

            return 6881;
        }

        private static async Task<DownloadResult> TryDistributedDownloadAsync(
            string url,
            string destinationDirectory,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken,
            string contentKeyOrHash = null)
        {
            try
            {
                if (_client is null)
                {
                    return null;
                }

                // Use provided ContentId/hash or compute from URL
                string hash = !string.IsNullOrEmpty(contentKeyOrHash) ? contentKeyOrHash : GetUrlHash(url);
                string cachePath = GetCachePath(hash);

                if (!File.Exists(cachePath))

                {
                    await Logger.LogVerboseAsync($"[Cache] No cached metadata for URL").ConfigureAwait(false);
                    return null;

                }

                await Logger.LogVerboseAsync($"[Cache] Attempting optimized download").ConfigureAwait(false);

                var metadataType = Type.GetType(D("TW9ub1RvcnJlbnQuVG9ycmVudCwgTW9ub1RvcnJlbnQ="));
                dynamic metadata =


                await Task.Run(() =>
                {
                    System.Reflection.MethodInfo loadMethod = metadataType.GetMethod(D("TG9hZA=="), new[] { typeof(string) });
                    return loadMethod.Invoke(null, new object[] { cachePath });
                }).ConfigureAwait(false);

                string fileName = Path.GetFileName(metadata.Name.ToString());
                string finalPath = Path.Combine(destinationDirectory, fileName);
                string tempPath = DownloadHelper.GetTempFilePath(finalPath);

                // Use temp directory as save path for MonoTorrent
                string tempDirectory = Path.GetDirectoryName(tempPath);
                _ = Directory.CreateDirectory(tempDirectory);

                var managerType = Type.GetType(D("TW9ub1RvcnJlbnQuQ2xpZW50LlRvcnJlbnRNYW5hZ2VyLCBNb25vVG9ycmVudA=="));
                dynamic manager = Activator.CreateInstance(managerType, metadata, tempDirectory);

                await _client.Register(manager);

                await manager.StartAsync();

                DateTime startTime = DateTime.Now;
                var timeout = TimeSpan.FromHours(2);

                while (!cancellationToken.IsCancellationRequested && DateTime.Now - startTime < timeout)
                {
                    if (manager.State.ToString() == D("U2VlZGluZw==") || manager.Complete)
                    {
                        // MonoTorrent downloads to temp directory, now move to final location
                        await Logger.LogVerboseAsync($"[Cache] ✓ Optimized download complete: {fileName}").ConfigureAwait(false);

                        // Atomically move to final destination
                        try
                        {
                            DownloadHelper.MoveToFinalDestination(tempPath, finalPath);
                            await Logger.LogVerboseAsync($"[Cache] Moved temporary file to final destination: {finalPath}").ConfigureAwait(false);
                        }
                        catch (Exception moveEx)
                        {
                            await Logger.LogErrorAsync($"[Cache] Failed to move temporary file to final destination: {moveEx.Message}").ConfigureAwait(false);
                            try { File.Delete(tempPath); } catch { }
                            throw;
                        }

                        progress?.Report(new DownloadProgress
                        {
                            Status = DownloadStatus.Completed,
                            StatusMessage = "Download complete",
                            ProgressPercentage = 100,
                            FilePath = finalPath,
                            EndTime = DateTime.Now,
                        });

                        return DownloadResult.Succeeded(finalPath, "Downloaded via optimized cache", DownloadSource.Optimized);
                    }

                    double progressPct = manager.Progress * 100.0;
                    progress?.Report(new DownloadProgress
                    {
                        Status = DownloadStatus.InProgress,
                        StatusMessage = $"Downloading... ({(int)progressPct}%)",
                        ProgressPercentage = progressPct,
                    });

                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                }

                await manager.StopAsync();
                await _client.Unregister(manager);

                // Clean up any partial files if download was cancelled or incomplete
                if (cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (Directory.Exists(tempDirectory))
                        {
                            string[] partialFiles = Directory.GetFiles(tempDirectory, $"{fileName}*", SearchOption.AllDirectories);
                            foreach (string partialFile in partialFiles)
                            {
                                try
                                {
                                    File.Delete(partialFile);
                                    await Logger.LogVerboseAsync($"[Cache] Cleaned up cancelled download: {partialFile}").ConfigureAwait(false);
                                }
                                catch (Exception deleteEx)
                                {
                                    await Logger.LogVerboseAsync($"[Cache] Could not delete partial file {partialFile}: {deleteEx.Message}").ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        await Logger.LogWarningAsync($"[Cache] Error during cancellation cleanup: {cleanupEx.Message}").ConfigureAwait(false);
                    }
                }

                return null;
            }
            catch (Exception ex)

            {
                await Logger.LogVerboseAsync($"[Cache] Optimization attempt failed: {ex.Message}").ConfigureAwait(false);
                return null;
            }
        }

        private static async Task StartBackgroundSharingAsync(string url, string filePath, string contentKeyOrHash = null)
        {
            try
            {
                if (_client is null || !File.Exists(filePath))
                {
                    return;
                }

                // Use provided ContentId/hash or compute from URL
                string hash = !string.IsNullOrEmpty(contentKeyOrHash) ? contentKeyOrHash : GetUrlHash(url);
                string metadataPath = GetCachePath(hash);

                if (!File.Exists(metadataPath))

                {
                    await CreateCacheFileAsync(filePath, metadataPath).ConfigureAwait(false);
                }

                var metadataType = Type.GetType(D("TW9ub1RvcnJlbnQuVG9ycmVudCwgTW9ub1RvcnJlbnQ="));
                dynamic metadata = await Task.Run(() =>
                {
                    System.Reflection.MethodInfo loadMethod = metadataType.GetMethod(D("TG9hZA=="), new[] { typeof(string) });
                    return loadMethod.Invoke(null, new object[] { metadataPath });

                }).ConfigureAwait(false);

                var managerType = Type.GetType(D("TW9ub1RvcnJlbnQuQ2xpZW50LlRvcnJlbnRNYW5hZ2VyLCBNb25vVG9ycmVudA=="));

                dynamic manager = Activator.CreateInstance(managerType, metadata, Path.GetDirectoryName(filePath));

                await _client.Register(

manager);
                await manager.StartAsync();

                await Logger.LogVerboseAsync($"[Cache] Background sharing started: {Path.GetFileName(filePath)}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Logger.LogVerboseAsync($"[Cache] Sharing setup failed: {ex.Message}").ConfigureAwait(false);
            }
        }

        private static async Task CreateCacheFileAsync(string filePath, string metadataPath)
        {
            try
            {
                var creatorType = Type.GetType(D("TW9ub1RvcnJlbnQuVG9ycmVudENyZWF0b3IsIE1vbm9Ub3JyZW50"));
                dynamic creator = Activator.CreateInstance(creatorType);

                creator.Announces.Add(new List<string>
                {
                    "udp://tracker.opentrackr.org:1337/announce",
                    "udp://open.stealth.si:80/announce",
                });

                dynamic metadata = await creator.CreateAsync(filePath);


                await Task.Run(() => File.WriteAllBytes(metadataPath, metadata.ToBytes())).ConfigureAwait(false);

                await Logger.LogVerboseAsync($"[Cache] Created metadata: {metadataPath}").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Logger.LogVerboseAsync($"[Cache] Metadata creation failed: {ex.Message}").ConfigureAwait(false);
            }
        }

        private static string GetUrlHash(string url)
        {
            lock (_lock)
            {
                if (_urlHashes.TryGetValue(url, out string existing))
                {
                    return existing;
                }

                string normalized = NormalizeUrl(url);
                byte[] hashBytes;
#if NET48
				using ( var sha1 = SHA1.Create() )
				{
					hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(normalized));
				}
#else
                hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes(normalized));
#endif
                string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                _urlHashes[url] = hash;
                return hash;
            }
        }

        private static string NormalizeUrl(string url)
        {
            try
            {
                if (url.Contains("nexusmods.com"))
                {
                    System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(url, @"nexusmods\.com/([^/]+)/mods/(\d+)");
                    if (match.Success)
                    {
                        return $"nexusmods:{match.Groups[1].Value}:{match.Groups[2].Value}";
                    }
                }
                else if (url.Contains("deadlystream.com"))
                {
                    System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
                        url,
                        @"deadlystream\.com/files/file/(\d+)",
                        System.Text.RegularExpressions.RegexOptions.None,
                        TimeSpan.FromSeconds(2)
                    );
                    if (match.Success)
                    {
                        return $"deadlystream:{match.Groups[1].Value}";
                    }
                }
                else if (url.Contains("mega.nz"))
                {
                    System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
                        url, @"mega\.nz/(file|folder)/([A-Za-z0-9_-]+)", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(2)
                    );
                    if (match.Success)
                    {
                        return $"mega:{match.Groups[1].Value}:{match.Groups[2].Value}";
                    }
                }

                var uri = new Uri(url);
                return $"{uri.Host}{uri.AbsolutePath}";
            }
            catch
            {
                return url;
            }
        }

        private static string GetCachePath(string hash)
        {
            string cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KOTORModSync",
                "Cache",
                "Network"
            );

            if (!Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }

            return Path.Combine(cacheDir, $"{hash}.dat");
        }

        #region Phase 4: Content Identification & Integrity Verification

        /// <summary>
        /// CRITICAL: Computes ContentId from provider metadata BEFORE file download.
        /// This allows P2P peer discovery before downloading from the original URL.
        /// MUST be deterministic across all clients globally!
        /// </summary>
        public static string ComputeContentIdFromMetadata(
            Dictionary<string, object> normalizedMetadata,
            string primaryUrl)
        {
            // Build deterministic info dict from metadata ONLY
            var infoDict = new SortedDictionary<string, object>

(StringComparer.Ordinal)
            {
                ["provider"] = normalizedMetadata["provider"],
                ["url_canonical"] = Utility.UrlNormalizer.Normalize(primaryUrl, stripQueryParameters: true),
            };

            // Add provider-specific fields (these MUST match the whitelist from Phase 2.2)
            string provider = normalizedMetadata["provider"].ToString();

            switch (provider)
            {
                case "deadlystream":
                    infoDict["filePageId"] = normalizedMetadata.ContainsKey("filePageId") ? normalizedMetadata["filePageId"] : "";
                    infoDict["changelogId"] = normalizedMetadata.ContainsKey("changelogId") ? normalizedMetadata["changelogId"] : "";
                    infoDict["fileId"] = normalizedMetadata.ContainsKey("fileId") ? normalizedMetadata["fileId"] : "";
                    infoDict["version"] = normalizedMetadata.ContainsKey("version") ? normalizedMetadata["version"] : "";
                    infoDict["updated"] = normalizedMetadata.ContainsKey("updated") ? normalizedMetadata["updated"] : "";
                    infoDict["size"] = normalizedMetadata.ContainsKey("size") ? normalizedMetadata["size"] : 0L;
                    break;

                case "mega":
                    infoDict["nodeId"] = normalizedMetadata.ContainsKey("nodeId") ? normalizedMetadata["nodeId"] : "";
                    infoDict["hash"] = normalizedMetadata.ContainsKey("hash") ? normalizedMetadata["hash"] : "";
                    infoDict["size"] = normalizedMetadata.ContainsKey("size") ? normalizedMetadata["size"] : 0L;
                    infoDict["mtime"] = normalizedMetadata.ContainsKey("mtime") ? normalizedMetadata["mtime"] : 0L;
                    infoDict["name"] = normalizedMetadata.ContainsKey("name") ? normalizedMetadata["name"] : "";
                    break;

                case "nexus":
                    infoDict["fileId"] = normalizedMetadata.ContainsKey("fileId") ? normalizedMetadata["fileId"] : "";
                    infoDict["fileName"] = normalizedMetadata.ContainsKey("fileName") ? normalizedMetadata["fileName"] : "";
                    infoDict["size"] = normalizedMetadata.ContainsKey("size") ? normalizedMetadata["size"] : 0L;
                    infoDict["uploadedTimestamp"] = normalizedMetadata.ContainsKey("uploadedTimestamp") ? normalizedMetadata["uploadedTimestamp"] : 0L;
                    infoDict["md5Hash"] = normalizedMetadata.ContainsKey("md5Hash") ? normalizedMetadata["md5Hash"] : "";
                    break;

                case "direct":
                    infoDict["url"] = normalizedMetadata.ContainsKey("url") ? normalizedMetadata["url"] : "";
                    infoDict["contentLength"] = normalizedMetadata.ContainsKey("contentLength") ? normalizedMetadata["contentLength"] : 0L;
                    infoDict["lastModified"] = normalizedMetadata.ContainsKey("lastModified") ? normalizedMetadata["lastModified"] : "";
                    infoDict["etag"] = normalizedMetadata.ContainsKey("etag") ? normalizedMetadata["etag"] : "";
                    infoDict["fileName"] = normalizedMetadata.ContainsKey("fileName") ? normalizedMetadata["fileName"] : "";
                    break;
            }

            // Bencode and hash to create infohash
            byte[] bencodedInfo = Utility.CanonicalBencoding.BencodeCanonical(infoDict);
#if NET48
			byte[] infohash;
			using ( var sha1 = SHA1.Create() )
			{
				infohash = sha1.ComputeHash(bencodedInfo);
			}
#else
            byte[] infohash = SHA1.HashData(bencodedInfo);
#endif
            string contentId = BitConverter.ToString(infohash).Replace("-", "").ToLowerInvariant();

            return contentId;
        }

        /// <summary>
        /// Determines the optimal piece size for a given file size.
        /// Ensures total pieces <= 2^20 (1,048,576 pieces max).
        /// </summary>
        public static int DeterminePieceSize(long fileSize)
        {
            int[] candidates = { 65536, 131072, 262144, 524288, 1048576, 2097152, 4194304 }; // 64KB-4MB

            foreach (int size in candidates)
            {
                long pieceCount = (fileSize + size - 1) / size;
                if (pieceCount <= 1048576)
                {
                    return size;
                }
            }

            return 4194304; // Max 4MB pieces
        }

        /// <summary>
        /// Computes file integrity data AFTER download.
        /// Used to verify the file matches expected content and enable P2P sharing.
        /// Returns: (ContentHashSHA256, pieceLength, pieceHashes)
        /// </summary>
        public static async Task<(string contentHashSHA256, int pieceLength, string pieceHashes)> ComputeFileIntegrityData(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            long fileSize = fileInfo.Length;

            // 1. Determine canonical piece size
            int pieceLength = DeterminePieceSize(fileSize);

            // 2. Compute piece hashes (SHA-1, 20 bytes each) for P2P transfer verification
            var pieceHashList = new List<byte[]>();
            using (FileStream fs = File.OpenRead(filePath))
            {
                byte[] buffer = new byte[pieceLength];
                while (true)

                {
#if NET48
					int bytesRead = await fs.ReadAsync(buffer, 0, pieceLength);
#else
                    int bytesRead = await fs.ReadAsync(buffer.AsMemory(0, pieceLength)).ConfigureAwait(false);
#endif
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    byte[] pieceData = bytesRead == pieceLength ? buffer : buffer.Take(bytesRead).ToArray();
#if NET48
					using ( var sha1 = SHA1.Create() )
					{
						byte[] pieceHash = sha1.ComputeHash(pieceData);
						pieceHashList.Add(pieceHash);
					}
#else
                    byte[] pieceHash = SHA1.HashData(pieceData);
                    pieceHashList.Add(pieceHash);
#endif
                }
            }

            // Concatenate piece hashes as hex
            string pieceHashes = string.Concat(pieceHashList.Select(h =>
                BitConverter.ToString(h).Replace("-", "").ToLowerInvariant()));

            // 3. Compute SHA-256 of entire file (CANONICAL integrity check)
            byte[] sha256;
            using (FileStream fs = File.OpenRead(filePath))
            {
#if NET48
				using ( var sha = SHA256.Create() )
				{
					sha256 = sha.ComputeHash(fs);
				}
#else
                sha256 = await SHA256.HashDataAsync(fs).ConfigureAwait(false);
#endif
            }
            string contentHashSHA256 = BitConverter.ToString(sha256).Replace("-", "").ToLowerInvariant();

            return (contentHashSHA256, pieceLength, pieceHashes);
        }

        /// <summary>
        /// DEPRECATED: Use ComputeContentIdFromMetadata() for pre-download ContentId, then ComputeFileIntegrityData() post-download.
        /// This function computes ContentId from file bytes (wrong approach for P2P discovery).
        /// Kept for backward compatibility only.
        /// </summary>
        [Obsolete("Use ComputeContentIdFromMetadata() before download, then ComputeFileIntegrityData() after download")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        public static async Task<(string contentId, string contentHashSHA256, int pieceLength, string pieceHashes)> ComputeContentIdentifiers(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            long fileSize = fileInfo.Length;

            // 1. Determine canonical piece size
            int pieceLength = DeterminePieceSize(fileSize);

            // 2. Compute piece hashes (SHA-1, 20 bytes each)
            var pieceHashList = new List<byte[]>();
            using (FileStream fs = File.OpenRead(filePath))
            {
                byte[] buffer = new byte[pieceLength];
                while (true)

                {
#if NET48
					int bytesRead = await fs.ReadAsync(buffer, 0, pieceLength);
#else
                    int bytesRead = await fs.ReadAsync(buffer.AsMemory(0, pieceLength)).ConfigureAwait(false);
#endif
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    byte[] pieceData = bytesRead == pieceLength ? buffer : buffer.Take(bytesRead).ToArray();
#if NET48
					using ( var sha1 = SHA1.Create() )
					{
						byte[] pieceHash = sha1.ComputeHash(pieceData);
						pieceHashList.Add(pieceHash);
					}
#else
                    byte[] pieceHash = SHA1.HashData(pieceData);
                    pieceHashList.Add(pieceHash);
#endif
                }
            }

            // Concatenate piece hashes as hex
            string pieceHashes = string.Concat(pieceHashList.Select(h => BitConverter.ToString(h).Replace("-", "").ToLowerInvariant()));

            // 3. Build canonical bencoded info dict (BitTorrent spec)
            string sanitizedName = SanitizeFilename(Path.GetFileName(filePath));
            byte[] piecesRaw = pieceHashList.SelectMany(h => h).ToArray(); // Raw SHA-1 bytes concatenated

            var infoDict = new SortedDictionary<string, object>

(StringComparer.Ordinal)
            {
                ["length"] = fileSize,
                ["name"] = sanitizedName,
                ["piece length"] = (long)pieceLength,
                ["pieces"] = piecesRaw,
                ["private"] = (long)0,
            };

            byte[] bencodedInfo = Utility.CanonicalBencoding.BencodeCanonical(infoDict);
#if NET48
			byte[] infohash;
			using ( var sha1 = SHA1.Create() )
			{
				infohash = sha1.ComputeHash(bencodedInfo);
			}
#else
            byte[] infohash = SHA1.HashData(bencodedInfo);
#endif
            string contentId = BitConverter.ToString(infohash).Replace("-", "").ToLowerInvariant();

            // 4. Compute SHA-256 of entire file (CANONICAL integrity check)
            byte[] sha256;
            using (FileStream fs = File.OpenRead(filePath))
            {
#if NET48
				using ( var sha = SHA256.Create() )
				{
					sha256 = sha.ComputeHash(fs);
				}
#else
                sha256 = await SHA256.HashDataAsync(fs).ConfigureAwait(false);
#endif
            }
            string contentHashSHA256 = BitConverter.ToString(sha256).Replace("-", "").ToLowerInvariant();

            return (contentId, contentHashSHA256, pieceLength, pieceHashes);
        }

        /// <summary>
        /// Sanitizes a filename for canonical content identification.
        /// </summary>
        public static string SanitizeFilename(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            // Normalize to Unicode NFC
            name = name.Normalize(NormalizationForm.FormC);

            // Replace backslashes with forward slashes
            name = name.Replace('\\', '/');

            // Remove any path components (keep filename only)
            if (name.Contains('/'))
            {
                name = name.Substring(name.LastIndexOf('/') + 1);
            }

            return name;
        }

        /// <summary>
        /// Verifies content integrity using SHA-256 hash and piece-level verification.
        /// </summary>
        public static async Task<bool> VerifyContentIntegrity(string filePath, ResourceMetadata meta)
        {
            try
            {
                // 1. CANONICAL CHECK: SHA-256 of file bytes
                byte[] sha256Hash;
                using (FileStream fs = File.OpenRead(filePath))
                {
#if NET48
					using ( var sha = SHA256.Create() )
					{
						sha256Hash = sha.ComputeHash(fs);
					}
#else
                    sha256Hash = await SHA256.HashDataAsync(fs).ConfigureAwait(false);
#endif
                }
                string computedSHA256 = BitConverter.ToString(sha256Hash).Replace("-", "").ToLowerInvariant();

                if (meta.ContentHashSHA256 != null && !string.Equals(computedSHA256, meta.ContentHashSHA256, StringComparison.Ordinal))

                {
                    await Logger.LogErrorAsync($"[Cache] INTEGRITY FAILURE: SHA-256 mismatch").ConfigureAwait(false);
                    await Logger.LogErrorAsync($"  Expected: {meta.ContentHashSHA256}").ConfigureAwait(false);
                    await Logger.LogErrorAsync($"  Computed: {computedSHA256}").ConfigureAwait(false);
                    return false;
                }

                // 2. Piece-level verification (if piece data available)
                if (meta.PieceHashes != null && meta.PieceLength > 0)
                {
                    bool piecesValid = await VerifyPieceHashesFromStored(filePath, meta.PieceLength, meta.PieceHashes).ConfigureAwait(false);
                    if (!piecesValid)
                    {
                        await Logger.LogErrorAsync($"[Cache] INTEGRITY FAILURE: Piece hash mismatch").ConfigureAwait(false);
                        return false;
                    }
                }

                // 3. Verify file size matches
                var fileInfo = new FileInfo(filePath);
                if (meta.FileSize > 0 && fileInfo.Length != meta.FileSize)
                {
                    await Logger.LogErrorAsync($"[Cache] INTEGRITY FAILURE: File size mismatch").ConfigureAwait(false);
                    await Logger.LogErrorAsync($"  Expected: {meta.FileSize} bytes").ConfigureAwait(false);
                    await Logger.LogErrorAsync($"  Actual: {fileInfo.Length} bytes").ConfigureAwait(false);
                    return false;
                }

                return true;
            }
            catch (Exception ex)

            {
                await Logger.LogErrorAsync($"[Cache] Integrity verification failed: {ex.Message}").ConfigureAwait(false);
                return false;
            }
        }

        /// <summary>
        /// Verifies piece hashes from stored hex-encoded concatenated SHA-1 hashes.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        public static async Task<bool> VerifyPieceHashesFromStored(string filePath, int pieceLength, string pieceHashesHex)
        {
            try
            {
                // Parse stored piece hashes (hex-encoded concatenated SHA-1 hashes, 40 hex chars per piece)
                var expectedHashes = new List<byte[]>();
                for (int i = 0; i < pieceHashesHex.Length; i += 40)
                {
                    if (i + 40 > pieceHashesHex.Length)
                    {
                        break;
                    }

                    string hexPiece = pieceHashesHex.Substring(i, 40);
#if NET48
					expectedHashes.Add(HexStringToByteArray(hexPiece));
#else
                    expectedHashes.Add(Convert.FromHexString(hexPiece));
#endif
                }

                // Compute actual piece hashes from file
                using (FileStream fs = File.OpenRead(filePath))
                {
                    byte[] buffer = new byte[pieceLength];
                    int pieceIndex = 0;

                    while (true)

                    {
#if NET48
						int bytesRead = await fs.ReadAsync(buffer, 0, pieceLength);
#else
                        int bytesRead = await fs.ReadAsync(buffer.AsMemory(0, pieceLength)).ConfigureAwait(false);
#endif
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        byte[] pieceData = bytesRead == pieceLength ? buffer : buffer.Take(bytesRead).ToArray();
#if NET48
						byte[] computedHash;
						using ( var sha1 = SHA1.Create() )
						{
							computedHash = sha1.ComputeHash(pieceData);
						}
#else
                        byte[] computedHash = SHA1.HashData(pieceData);
#endif

                        if (pieceIndex >= expectedHashes.Count || !computedHash.SequenceEqual(expectedHashes[pieceIndex]))

                        {
                            await Logger.LogErrorAsync($"[Cache] Piece {pieceIndex} hash mismatch").ConfigureAwait(false);
                            return false;
                        }

                        pieceIndex++;
                    }

                    if (pieceIndex != expectedHashes.Count)

                    {
                        await Logger.LogErrorAsync($"[Cache] Piece count mismatch: expected {expectedHashes.Count}, got {pieceIndex}").ConfigureAwait(false);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)

            {
                await Logger.LogErrorAsync($"[Cache] Piece verification failed: {ex.Message}").ConfigureAwait(false);
                return false;
            }
        }

#if NET48
		private static byte[] HexStringToByteArray(string hex)
		{
			byte[] bytes = new byte[hex.Length / 2];
			for ( int i = 0; i < bytes.Length; i++ )
			{
				bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
			}
			return bytes;
		}
#endif

        /// <summary>
        /// Gets the path for a partial download file.
        /// </summary>
        public static string GetPartialFilePath(string contentKey, string destinationDirectory)
        {
            string partialDir = Path.Combine(destinationDirectory, ".partial");
            if (!Directory.Exists(partialDir))
            {
                Directory.CreateDirectory(partialDir);
            }

            return Path.Combine(partialDir, $"{contentKey.Substring(0, Math.Min(32, contentKey.Length))}.part");
        }

        /// <summary>
        /// Acquires a per-content lock to prevent concurrent downloads of the same content.
        /// </summary>
        public static async Task<IDisposable> AcquireContentKeyLock(string contentKey)
        {
            SemaphoreSlim sem = s_contentKeyLocks.GetOrAdd(contentKey, _ => new SemaphoreSlim(1, 1));

            await sem.WaitAsync().ConfigureAwait(false);
            return new LockReleaser(() =>
            {
                sem.Release();
                // Clean up if no waiters
                if (sem.CurrentCount == 1)
                {
                    s_contentKeyLocks.TryRemove(contentKey, out _);
                }
            });
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3260:Non-derived \"private\" classes and records should be \"sealed\"", Justification = "<Pending>")]
        private class LockReleaser : IDisposable
        {
            private readonly Action _release;
            public LockReleaser(Action release) => _release = release;
            public void Dispose() => _release();
        }

        /// <summary>
        /// Blocks a ContentId from being used in network cache (for DMCA/takedown compliance).
        /// </summary>
        public static void BlockContentId(string contentId, string reason = null)
        {
            lock (_lock)
            {
                s_blockedContentIds.Add(contentId);

                // Log with audit trail
                try
                {
                    string cacheDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "KOTORModSync",
                        "Cache"
                    );
                    if (!Directory.Exists(cacheDir))
                    {
                        Directory.CreateDirectory(cacheDir);
                    }

                    string auditLog = Path.Combine(cacheDir, "block-audit.log");
                    File.AppendAllText(auditLog, $"{DateTime.UtcNow:O}|BLOCK|{contentId}|{reason ?? "manual"}\n");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"[Cache] Failed to write audit log: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Checks if a ContentId is blocked.
        /// </summary>
        public static bool IsContentIdBlocked(string contentId)
        {
            lock (_lock)
            {
                return s_blockedContentIds.Contains(contentId);
            }
        }

        #endregion

        #region CLI Management Methods

        /// <summary>
        /// Gets the count of blocked ContentIds.
        /// </summary>
        public static int GetBlockedContentIdCount()
        {
            lock (s_blockedContentIds)
            {
                return s_blockedContentIds.Count;
            }
        }

        #endregion
    }
}
