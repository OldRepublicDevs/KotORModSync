// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Utility;
using Xunit;

namespace KOTORModSync.Tests.Services.DistributedCache
{
    /// <summary>
    /// Test fixture providing infrastructure for distributed cache integration tests.
    /// Manages Docker containers, test files, and cleanup.
    /// </summary>
    public class DistributedCacheTestFixture : IAsyncLifetime
    {
        private readonly List<DockerBitTorrentClient> _containers = new List<DockerBitTorrentClient>();
        private readonly List<string> _tempDirectories = new List<string>();
        private readonly List<string> _tempFiles = new List<string>();

        public string TestDataDirectory { get; private set; }
        public string TorrentsDirectory { get; private set; }
        public string DownloadsDirectory { get; private set; }

        public async Task InitializeAsync()
        {
            // Create test directories
            TestDataDirectory = Path.Combine(Path.GetTempPath(), $"KMSTest_{Guid.NewGuid():N}");
            TorrentsDirectory = Path.Combine(TestDataDirectory, "torrents");
            DownloadsDirectory = Path.Combine(TestDataDirectory, "downloads");

            Directory.CreateDirectory(TestDataDirectory);
            Directory.CreateDirectory(TorrentsDirectory);
            Directory.CreateDirectory(DownloadsDirectory);

            _tempDirectories.Add(TestDataDirectory);

            // Initialize MainConfig for tests
            var config = new MainConfig
            {
                sourcePath = new DirectoryInfo(TestDataDirectory),
                destinationPath = new DirectoryInfo(DownloadsDirectory),
                debugLogging = true
            };

            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task DisposeAsync()
        {
            // Stop all containers
            foreach (DockerBitTorrentClient container in _containers)
            {
                try
                {
                    await container.StopAsync().ConfigureAwait(false);
                    container.Dispose();
                }
                catch
                {
                    // Best effort
                }
            }
            _containers.Clear();

            // Clean up temp files
            foreach (string file in _tempFiles.Where(File.Exists))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Best effort
                }
            }

            // Clean up temp directories
            foreach (string dir in _tempDirectories.Where(Directory.Exists))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch
                {
                    // Best effort
                }
            }
        }

        public async Task<DockerBitTorrentClient> StartContainerAsync(
            DockerBitTorrentClient.BitTorrentClientType clientType,
            CancellationToken cancellationToken = default)
        {
            var client = new DockerBitTorrentClient(clientType);
            await client.StartAsync(cancellationToken).ConfigureAwait(false);
            _containers.Add(client);
            return client;
        }

        public string CreateTestFile(string filename, long sizeBytes, string content = null)
        {
            string path = Path.Combine(TestDataDirectory, filename);

            if (content != null)
            {
                File.WriteAllText(path, content);
            }
            else
            {
                // Create file with random data
                using FileStream stream = File.Create(path);
                var random = new Random(42); // Deterministic for tests
                byte[] buffer = new byte[8192];
                long remaining = sizeBytes;

                while (remaining > 0)
                {
                    int toWrite = (int)Math.Min(remaining, buffer.Length);
                    random.NextBytes(buffer);
                    stream.Write(buffer, 0, toWrite);
                    remaining -= toWrite;
                }
            }

            _tempFiles.Add(path);
            return path;
        }

        public async Task<string> CreateTorrentFileAsync(
            string sourceFile,
            string torrentName,
            int pieceLength = 262144,
            List<string> trackers = null)
        {
            string torrentPath = Path.Combine(TorrentsDirectory, $"{torrentName}.torrent");

            // Build canonical info dict
            var infoDict = new System.Collections.Generic.SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["name"] = Path.GetFileName(sourceFile),
                ["piece length"] = pieceLength,
                ["length"] = new FileInfo(sourceFile).Length,
                ["private"] = 0
            };

            // Compute piece hashes
            using (FileStream fileStream = File.OpenRead(sourceFile))
            using (var sha1 = SHA1.Create())
            {
                var pieceHashes = new List<byte>();
                byte[] buffer = new byte[pieceLength];
                int bytesRead;

                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    byte[] hash = sha1.ComputeHash(buffer, 0, bytesRead);
                    pieceHashes.AddRange(hash);
                }

                infoDict["pieces"] = pieceHashes.ToArray();
            }

            // Encode info dict
            byte[] encodedInfo = CanonicalBencoding.BencodeCanonical(infoDict);

            // Compute ContentId (SHA-1 of bencoded info dict)
            byte[] contentId;
            using (var sha1Info = SHA1.Create())
            {
                contentId = sha1Info.ComputeHash(encodedInfo);
            }

            // Build full torrent dict
            var torrentDict = new System.Collections.Generic.SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["info"] = infoDict,
                ["creation date"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            if (trackers != null && trackers.Count > 0)
            {
                torrentDict["announce"] = trackers[0];
                if (trackers.Count > 1)
                {
                    var announceList = new List<object>();
                    foreach (string tracker in trackers)
                    {
                        announceList.Add(new List<object> { tracker });
                    }
                    torrentDict["announce-list"] = announceList;
                }
            }

            // Encode and save
            byte[] encodedTorrent = CanonicalBencoding.BencodeCanonical(torrentDict);
            await File.WriteAllBytesAsync(torrentPath, encodedTorrent).ConfigureAwait(false);

            _tempFiles.Add(torrentPath);
            return torrentPath;
        }

        public string ComputeContentId(string filePath)
        {
            // Read file and compute ContentId same way DownloadCacheOptimizer does
            long fileSize = new FileInfo(filePath).Length;
            int pieceLength = DeterminePieceSize(fileSize);

            var infoDict = new System.Collections.Generic.SortedDictionary<string, object>(StringComparer.Ordinal)
            {
                ["name"] = Path.GetFileName(filePath),
                ["piece length"] = pieceLength,
                ["length"] = fileSize,
                ["private"] = 0
            };

            // Compute piece hashes
            using (FileStream fileStream = File.OpenRead(filePath))
            using (var sha1Piece = SHA1.Create())
            {
                var pieceHashes = new List<byte>();
                byte[] buffer = new byte[pieceLength];
                int bytesRead;

                while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    byte[] hash = sha1Piece.ComputeHash(buffer, 0, bytesRead);
                    pieceHashes.AddRange(hash);
                }

                infoDict["pieces"] = pieceHashes.ToArray();
            }

            // Encode and hash
            byte[] encoded = CanonicalBencoding.BencodeCanonical(infoDict);
            byte[] contentIdBytes;
            using (var sha1 = SHA1.Create())
            {
                contentIdBytes = sha1.ComputeHash(encoded);
            }

            return BitConverter.ToString(contentIdBytes).Replace("-", "").ToLowerInvariant();
        }

        private static int DeterminePieceSize(long fileSize)
        {
            int[] candidates = { 65536, 131072, 262144, 524288, 1048576, 2097152, 4194304 };
            foreach (int size in candidates)
            {
                if ((fileSize + size - 1) / size <= 1048576)
                {
                    return size;
                }
            }
            return 4194304;
        }

        public string CreateTempDirectory(string name = null)
        {
            string path = Path.Combine(TestDataDirectory, name ?? Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            _tempDirectories.Add(path);
            return path;
        }

        public async Task WaitForConditionAsync(
            Func<Task<bool>> condition,
            TimeSpan timeout,
            string errorMessage,
            CancellationToken cancellationToken = default)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                if (await condition().ConfigureAwait(false))
                {
                    return;
                }
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
            throw new TimeoutException(errorMessage);
        }
    }
}

