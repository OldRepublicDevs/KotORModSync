// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private readonly List<DockerCacheClient> _containers = new List<DockerCacheClient>();
        private readonly List<string> _tempDirectories = new List<string>();
        private readonly List<string> _tempFiles = new List<string>();
        private readonly Dictionary<string, DistributionPayload> _descriptorPayloads = new Dictionary<string, DistributionPayload>(StringComparer.OrdinalIgnoreCase);

        public string TestDataDirectory { get; private set; }
        public string DescriptorsDirectory { get; private set; }
        public string DownloadsDirectory { get; private set; }

        public async Task InitializeAsync()
        {
            await DockerCacheClient.CleanupResidualContainersAsync().ConfigureAwait(false);

            // Create test directories
            TestDataDirectory = Path.Combine(Path.GetTempPath(), $"KMSTest_{Guid.NewGuid():N}");
            DescriptorsDirectory = Path.Combine(TestDataDirectory, "descriptors");
            DownloadsDirectory = Path.Combine(TestDataDirectory, "downloads");

            Directory.CreateDirectory(TestDataDirectory);
            Directory.CreateDirectory(DescriptorsDirectory);
            Directory.CreateDirectory(DownloadsDirectory);

            _tempDirectories.Add(TestDataDirectory);

            // Initialize MainConfig for tests
            var config = new MainConfig
            {
                sourcePath = new DirectoryInfo(TestDataDirectory),
                destinationPath = new DirectoryInfo(DownloadsDirectory),
                debugLogging = true,
            };

            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            // Stop all containers
            foreach (DockerCacheClient container in _containers)
            {
                try
                {
                    await container.StopAsync();
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
                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // Best effort
                }
            }

            await DockerCacheClient.CleanupResidualContainersAsync().ConfigureAwait(false);
        }

        public async Task<DockerCacheClient> StartContainerAsync(
            DockerCacheClient.CacheClientFlavor clientType,
            CancellationToken cancellationToken = default)
        {
            var client = new DockerCacheClient(clientType);
            await client.StartAsync(cancellationToken);
            _containers.Add(client);
            return client;
        }

        public string CreateTestFile(string filename, long sizeBytes, string content = null)
        {
            string relativePath = filename.Replace('\\', Path.DirectorySeparatorChar);
            string path = DistributionTestSupport.EnsureTestFile(TestDataDirectory, relativePath, sizeBytes, content);
            _tempFiles.Add(path);
            return path;
        }

        public async Task<string> CreateDescriptorFileAsync(
            string sourceFile,
            string descriptorName,
            int pieceLength = 262144,
            List<string> trackers = null,
            CancellationToken cancellationToken = default)
        {
            (string descriptorPath, DistributionPayload payload) = await DistributionTestSupport.CreateDescriptorAsync(
                sourceFile,
                DescriptorsDirectory,
                descriptorName,
                trackers,
                pieceLength,
                cancellationToken).ConfigureAwait(false);

            _tempFiles.Add(descriptorPath);
            _descriptorPayloads[descriptorPath] = payload;
            return descriptorPath;
        }

        public string ComputeContentId(string filePath)
        {
            return DownloadCacheOptimizer.ComputeContentIdForFileAsync(filePath)
                .GetAwaiter()
                .GetResult();
        }

        public DistributionPayload GetDescriptorPayload(string descriptorPath)
        {
            if (!_descriptorPayloads.TryGetValue(descriptorPath, out DistributionPayload payload))
            {
                throw new InvalidOperationException($"Descriptor payload not found for path '{descriptorPath}'. Ensure CreateDescriptorFileAsync was used to build it.");
            }

            return payload;
        }

        public string CreateTempDirectory(string name = null)
        {
            string path = Path.Combine(TestDataDirectory, name ?? Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            _tempDirectories.Add(path);
            return path;
        }

        public Task WaitForConditionAsync(
            Func<Task<bool>> condition,
            TimeSpan timeout,
            string errorMessage,
            CancellationToken cancellationToken = default)
        {
            return AsyncWaitHelper.WaitUntilAsync(
                condition,
                timeout,
                pollInterval: TimeSpan.FromSeconds(1),
                errorFactory: () => new TimeoutException(errorMessage),
                cancellationToken: cancellationToken);
        }
    }
}

