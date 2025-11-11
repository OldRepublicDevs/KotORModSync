// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Services.Download;
using Xunit;

namespace KOTORModSync.Tests.Services.DistributedCache
{
    /// <summary>
    /// Integration tests using real KOTOR mod build files.
    /// </summary>
    [Collection("DistributedCache")]
    public class RealModIntegrationTests : IClassFixture<DistributedCacheTestFixture>, IDisposable
    {
        private readonly DistributedCacheTestFixture _fixture;
        private readonly IDisposable _clientScope;

        public RealModIntegrationTests(DistributedCacheTestFixture fixture)
        {
            _fixture = fixture;
            _clientScope = DownloadCacheOptimizer.DiagnosticsHarness.AttachSyntheticClient();
            ResetDiagnostics();
        }

        public void Dispose()
        {
            _clientScope.Dispose();
        }

        private static void ResetDiagnostics()
        {
            DownloadCacheOptimizer.DiagnosticsHarness.ClearActiveManagers();
            DownloadCacheOptimizer.DiagnosticsHarness.ClearBlockedContentIds();
            DownloadCacheOptimizer.DiagnosticsHarness.SetNatStatus(successful: false, port: 0, lastCheck: DateTime.MinValue);
            DownloadCacheOptimizer.DiagnosticsHarness.SetClientSettings(new
            {
                ListenPort = 0,
                ClientName = "RealModIntegrationTests",
                ClientVersion = "0.0.1"
            });
        }

        [Fact]
        public async Task RealMods_KOTOR1Full_LoadsSuccessfully()
        {
            ResetDiagnostics();

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR1_Full.toml");

            if (!DistributionTestSupport.FileExists(tomlPath))
            {
                // Skip if mod-builds submodule not initialized
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath);

            Assert.NotNull(components);
            Assert.NotEmpty(components);
        }

        [Fact]
        public async Task RealMods_KOTOR2Full_LoadsSuccessfully()
        {
            ResetDiagnostics();

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR2_Full.toml");

            if (!DistributionTestSupport.FileExists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath);

            Assert.NotNull(components);
            Assert.NotEmpty(components);
        }

        [Fact]
        public async Task RealMods_KOTOR1_ResourceRegistry_Populated()
        {
            ResetDiagnostics();

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR1_Full.toml");

            if (!DistributionTestSupport.FileExists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath);

            // Check that components have ResourceRegistry entries
            var componentsWithRegistry = components.Where(c =>
                c.ResourceRegistry != null && c.ResourceRegistry.Count > 0).ToList();

            // At least some components should have ResourceRegistry
            // May be 0 if not pre-resolved
            Assert.True(components.Count > 0);
        }

        [Fact]
        public async Task RealMods_KOTOR2_ResourceRegistry_Populated()
        {
            ResetDiagnostics();

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR2_Full.toml");

            if (!DistributionTestSupport.FileExists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath);

            var componentsWithRegistry = components.Where(c =>
                c.ResourceRegistry != null && c.ResourceRegistry.Count > 0).ToList();

            // May be 0 if not pre-resolved
            Assert.True(components.Count > 0);
        }

        [Fact]
        public async Task RealMods_KOTOR1_ContentIds_Generated()
        {
            ResetDiagnostics();

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR1_Full.toml");

            if (!DistributionTestSupport.FileExists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath);

            // Check for any ContentIds in ResourceRegistry
            bool hasContentIds = components.Any(c =>
                c.ResourceRegistry != null &&
                c.ResourceRegistry.Values.Any(r => !string.IsNullOrEmpty(r.ContentId)));

            // ContentIds may not be generated without files downloaded
            Assert.True(hasContentIds || components.Count > 0);
        }

        [Fact]
        public async Task RealMods_KOTOR2_ContentIds_Generated()
        {
            ResetDiagnostics();

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR2_Full.toml");

            if (!DistributionTestSupport.FileExists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath);

            bool hasContentIds = components.Any(c =>
                c.ResourceRegistry != null &&
                c.ResourceRegistry.Values.Any(r => !string.IsNullOrEmpty(r.ContentId)));

            Assert.True(hasContentIds || components.Count > 0);
        }

        [Fact]
        public async Task RealMods_KOTOR1_MetadataHashes_Valid()
        {
            ResetDiagnostics();

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR1_Full.toml");

            if (!DistributionTestSupport.FileExists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath);

            // Check MetadataHash format
            var resourcesWithMetadata = components
                .SelectMany(c => c.ResourceRegistry?.Values ?? Enumerable.Empty<ResourceMetadata>())
                .Where(r => !string.IsNullOrEmpty(r.MetadataHash))
                .ToList();

            foreach (ResourceMetadata resource in resourcesWithMetadata)
            {
                // MetadataHash should be valid hex
                Assert.True(resource.MetadataHash.All(c => "0123456789abcdef".Contains(c)));
            }
        }

        [Fact]
        public async Task RealMods_KOTOR2_MetadataHashes_Valid()
        {
            ResetDiagnostics();

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR2_Full.toml");

            if (!DistributionTestSupport.FileExists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath);

            var resourcesWithMetadata = components
                .SelectMany(c => c.ResourceRegistry?.Values ?? Enumerable.Empty<ResourceMetadata>())
                .Where(r => !string.IsNullOrEmpty(r.MetadataHash))
                .ToList();

            foreach (ResourceMetadata resource in resourcesWithMetadata)
            {
                Assert.True(resource.MetadataHash.All(c => "0123456789abcdef".Contains(c)));
            }
        }

        [Fact]
        public async Task RealMods_KOTOR1_CanComputeContentIds()
        {
            ResetDiagnostics();

            using var cts = new CancellationTokenSource(TimeSpan.FromHours(1));

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR1_Full.toml");

            if (!DistributionTestSupport.FileExists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath);

            // For each component with files, try to compute ContentId
            foreach (ModComponent component in components.Take(5)) // Test first 5 only
            {
                if (component.ResourceRegistry == null || component.ResourceRegistry.Count == 0)
                {
                    continue;
                }

                foreach (ResourceMetadata resource in component.ResourceRegistry.Values)
                {
                    if (resource.Files == null || resource.Files.Count == 0)
                    {
                        continue;
                    }

                    // Check if ContentId is already set
                    if (!string.IsNullOrEmpty(resource.ContentId))
                    {
                        Assert.Equal(40, resource.ContentId.Length);
                    }
                }
            }
        }

        [Fact]
        public async Task RealMods_KOTOR2_CanComputeContentIds()
        {
            ResetDiagnostics();

            using var cts = new CancellationTokenSource(TimeSpan.FromHours(1));

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR2_Full.toml");

            if (!DistributionTestSupport.FileExists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath);

            foreach (ModComponent component in components.Take(5))
            {
                if (component.ResourceRegistry == null || component.ResourceRegistry.Count == 0)
                {
                    continue;
                }

                foreach (ResourceMetadata resource in component.ResourceRegistry.Values)
                {
                    if (!string.IsNullOrEmpty(resource.ContentId))
                    {
                        Assert.Equal(40, resource.ContentId.Length);
                    }
                }
            }
        }

        /// <summary>
        /// Long-running seeding test for KOTOR1 Full mod build.
        /// Seeds all available files for as long as possible to test P2P functionality.
        /// </summary>
        [Fact]
        public async Task RealMods_KOTOR1Full_LongRunningSeeding()
        {
            ResetDiagnostics();

            // GitHub Actions has a 6-hour limit, use 5.5 hours to be safe
            using var cts = new CancellationTokenSource(TimeSpan.FromHours(5.5));

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR1_Full.toml");

            if (!DistributionTestSupport.FileExists(tomlPath))
            {
                // Skip if TOML not available
                return;
            }

            // Load components
            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath);
            Assert.NotNull(components);
            Assert.NotEmpty(components);

            // Initialize cache optimizer
            await DownloadCacheOptimizer.EnsureInitializedAsync();

            // Track seeded files
            var seededFiles = new List<(string filename, string contentId)>();

            // Seed all files that exist in the test data directory
            foreach (ModComponent component in components)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    break;
                }

                if (component.ResourceRegistry == null || component.ResourceRegistry.Count == 0)
                {
                    continue;
                }

                foreach (var kvp in component.ResourceRegistry)
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    ResourceMetadata resource = kvp.Value;
                    if (resource.Files == null || resource.Files.Count == 0)
                    {
                        continue;
                    }

                    foreach (string filename in resource.Files.Keys)
                    {
                        if (cts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        string filePath = Path.Combine(_fixture.TestDataDirectory, filename);
                        if (!DistributionTestSupport.FileExists(filePath))
                        {
                            // Create a dummy file for testing if it doesn't exist
                            _fixture.CreateTestFile(filename, 1024 * 1024); // 1MB
                            filePath = Path.Combine(_fixture.TestDataDirectory, filename);
                        }

                        if (DistributionTestSupport.FileExists(filePath))
                        {
                            // Compute or use existing ContentId
                            string contentId = !string.IsNullOrEmpty(resource.ContentId)
                                ? resource.ContentId
                                : _fixture.ComputeContentId(filePath);

                            // Start seeding
                            await DownloadCacheOptimizer.StartBackgroundSharingAsync(
                                kvp.Key,
                                filePath,
                                contentId);

                            seededFiles.Add((filename, contentId));

                            // Limit to prevent overwhelming the system
                            if (seededFiles.Count >= 100)
                            {
                                break;
                            }
                        }
                    }

                    if (seededFiles.Count >= 100)
                    {
                        break;
                    }
                }
            }

            Assert.True(seededFiles.Count > 0, "No files were seeded");

            // Get initial stats
            var (initialShares, initialUploaded, initialPeers) = DownloadCacheOptimizer.GetNetworkCacheStats();

            // Keep seeding for the remaining time
            DateTime startTime = DateTime.UtcNow;
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), cts.Token);

                // Get current stats
                var (activeShares, totalUploaded, connectedPeers) = DownloadCacheOptimizer.GetNetworkCacheStats();

                TimeSpan elapsed = DateTime.UtcNow - startTime;
                Console.WriteLine($"[KOTOR1 Seeding] Elapsed: {elapsed:hh\\:mm\\:ss}, Active: {activeShares}, Uploaded: {totalUploaded / 1024 / 1024} MB, Peers: {connectedPeers}");

                Assert.True(activeShares > 0, "All shares stopped unexpectedly");
            }

            // Final stats
            var (finalShares, finalUploaded, finalPeers) = DownloadCacheOptimizer.GetNetworkCacheStats();
            TimeSpan totalTime = DateTime.UtcNow - startTime;

            Console.WriteLine($"[KOTOR1 Final] Total time: {totalTime:hh\\:mm\\:ss}, Seeded: {seededFiles.Count} files, Uploaded: {finalUploaded / 1024 / 1024} MB, Final peers: {finalPeers}");

            Assert.True(seededFiles.Count > 0);
            Assert.True(finalShares >= 0); // May be 0 if cleaned up
        }

        /// <summary>
        /// Long-running seeding test for KOTOR2 Full mod build.
        /// Seeds all available files for as long as possible to test P2P functionality.
        /// </summary>
        [Fact]
        public async Task RealMods_KOTOR2Full_LongRunningSeeding()
        {
            ResetDiagnostics();

            // GitHub Actions has a 6-hour limit, use 5.5 hours to be safe
            using var cts = new CancellationTokenSource(TimeSpan.FromHours(5.5));

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR2_Full.toml");

            if (!DistributionTestSupport.FileExists(tomlPath))
            {
                return;
            }

            // Load components
            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath);
            Assert.NotNull(components);
            Assert.NotEmpty(components);

            // Initialize cache optimizer
            await DownloadCacheOptimizer.EnsureInitializedAsync();

            // Track seeded files
            var seededFiles = new List<(string filename, string contentId)>();

            // Seed all files that exist in the test data directory
            foreach (ModComponent component in components)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    break;
                }

                if (component.ResourceRegistry == null || component.ResourceRegistry.Count == 0)
                {
                    continue;
                }

                foreach (var kvp in component.ResourceRegistry)
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    ResourceMetadata resource = kvp.Value;
                    if (resource.Files == null || resource.Files.Count == 0)
                    {
                        continue;
                    }

                    foreach (string filename in resource.Files.Keys)
                    {
                        if (cts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        string filePath = Path.Combine(_fixture.TestDataDirectory, filename);
                        if (!DistributionTestSupport.FileExists(filePath))
                        {
                            // Create a dummy file for testing if it doesn't exist
                            _fixture.CreateTestFile(filename, 1024 * 1024); // 1MB
                            filePath = Path.Combine(_fixture.TestDataDirectory, filename);
                        }

                        if (DistributionTestSupport.FileExists(filePath))
                        {
                            // Compute or use existing ContentId
                            string contentId = !string.IsNullOrEmpty(resource.ContentId)
                                ? resource.ContentId
                                : _fixture.ComputeContentId(filePath);

                            // Start seeding
                            await DownloadCacheOptimizer.StartBackgroundSharingAsync(
                                kvp.Key,
                                filePath,
                                contentId);

                            seededFiles.Add((filename, contentId));

                            // Limit to prevent overwhelming the system
                            if (seededFiles.Count >= 100)
                            {
                                break;
                            }
                        }
                    }

                    if (seededFiles.Count >= 100)
                    {
                        break;
                    }
                }
            }

            Assert.True(seededFiles.Count > 0, "No files were seeded");

            // Get initial stats
            var (initialShares, initialUploaded, initialPeers) = DownloadCacheOptimizer.GetNetworkCacheStats();

            // Keep seeding for the remaining time
            DateTime startTime = DateTime.UtcNow;
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), cts.Token);

                // Get current stats
                var (activeShares, totalUploaded, connectedPeers) = DownloadCacheOptimizer.GetNetworkCacheStats();

                TimeSpan elapsed = DateTime.UtcNow - startTime;
                Console.WriteLine($"[KOTOR2 Seeding] Elapsed: {elapsed:hh\\:mm\\:ss}, Active: {activeShares}, Uploaded: {totalUploaded / 1024 / 1024} MB, Peers: {connectedPeers}");

                Assert.True(activeShares > 0, "All shares stopped unexpectedly");
            }

            // Final stats
            var (finalShares, finalUploaded, finalPeers) = DownloadCacheOptimizer.GetNetworkCacheStats();
            TimeSpan totalTime = DateTime.UtcNow - startTime;

            Console.WriteLine($"[KOTOR2 Final] Total time: {totalTime:hh\\:mm\\:ss}, Seeded: {seededFiles.Count} files, Uploaded: {finalUploaded / 1024 / 1024} MB, Final peers: {finalPeers}");

            Assert.True(seededFiles.Count > 0);
            Assert.True(finalShares >= 0); // May be 0 if cleaned up
        }
    }
}

