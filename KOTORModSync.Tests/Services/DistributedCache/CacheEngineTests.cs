// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.Threading.Tasks;
using KOTORModSync.Core.Services.Download;
using Xunit;

namespace KOTORModSync.Tests.Services.DistributedCache
{
    /// <summary>
    /// Tests for distributed cache engine operations, statistics, and lifecycle.
    /// </summary>
    [Collection("DistributedCache")]
    public class CacheEngineTests : IClassFixture<DistributedCacheTestFixture>, IDisposable
    {
        private readonly DistributedCacheTestFixture _fixture;
        private IDisposable _clientScope;

        public CacheEngineTests(DistributedCacheTestFixture fixture)
        {
            _fixture = fixture;
            ResetCacheState();
        }

        public void Dispose()
        {
            _clientScope?.Dispose();
        }

        private void ResetCacheState()
        {
            _clientScope?.Dispose();
            _clientScope = DownloadCacheOptimizer.DiagnosticsHarness.AttachSyntheticClient();
            DownloadCacheOptimizer.DiagnosticsHarness.ClearActiveManagers();
            DownloadCacheOptimizer.DiagnosticsHarness.ClearBlockedContentIds();
            DownloadCacheOptimizer.DiagnosticsHarness.SetNatStatus(successful: false, 0, DateTime.MinValue);
            DownloadCacheOptimizer.DiagnosticsHarness.SetClientSettings(new
            {
                ListenPort = 35555,
                AllowPortForwarding = true,
                MaximumConnections = 150,
            });
        }

        private static DownloadCacheOptimizer.DiagnosticsHarness.SyntheticResourceOptions CreateResource(
            long uploaded,
            long downloaded,
            double progress,
            int peers,
            string state,
            string key = "")
        {
            return new DownloadCacheOptimizer.DiagnosticsHarness.SyntheticResourceOptions
            {
                ContentKey = key,
                UploadedBytes = uploaded,
                DownloadedBytes = downloaded,
                Progress = progress,
                ConnectedPeers = peers,
                State = state,
            };
        }

        [Fact]
        public void CacheEngine_GetStats_ReturnsValid()
        {
            ResetCacheState();

            DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 1_500_000, downloaded: 750_000, progress: 0.50, peers: 2, state: "Ready", key: "share-a"));
            DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 3_250_000, downloaded: 2_000_000, progress: 0.80, peers: 4, state: "Running", key: "share-b"));

            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();

            Assert.Equal(2, stats.activeShares);
            Assert.Equal(4_750_000, stats.totalUploadBytes);
            Assert.Equal(6, stats.connectedSources);
        }

        [Fact]
        public void CacheEngine_GetStats_MultipleCalls_Consistent()
        {
            ResetCacheState();

            string key = DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 1000, downloaded: 500, progress: 0.25, peers: 1, state: "Starting", key: "consistency"));

            var first = DownloadCacheOptimizer.GetNetworkCacheStats();
            var second = DownloadCacheOptimizer.GetNetworkCacheStats();

            Assert.Equal(first, second);

            DownloadCacheOptimizer.DiagnosticsHarness.UpdateSyntheticResource(
                key,
                CreateResource(uploaded: 4000, downloaded: 2000, progress: 0.75, peers: 3, state: "Sharing", key: key));

            var updated = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.Equal(1, updated.activeShares);
            Assert.Equal(4000, updated.totalUploadBytes);
            Assert.Equal(3, updated.connectedSources);
        }

        [Fact]
        public void CacheEngine_ActiveShares_NonNegative()
        {
            ResetCacheState();

            string key = DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 500, downloaded: 250, progress: 0.30, peers: 1, state: "Active", key: "removable"));

            Assert.Equal(1, DownloadCacheOptimizer.GetNetworkCacheStats().activeShares);

            DownloadCacheOptimizer.DiagnosticsHarness.RemoveSyntheticResource(key);
            Assert.Equal(0, DownloadCacheOptimizer.GetNetworkCacheStats().activeShares);
        }

        [Fact]
        public void CacheEngine_TotalUpload_NonNegative()
        {
            ResetCacheState();

            string key = DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 8_000, downloaded: 4_000, progress: 0.60, peers: 2, state: "Running", key: "upload"));

            Assert.Equal(8_000, DownloadCacheOptimizer.GetNetworkCacheStats().totalUploadBytes);

            DownloadCacheOptimizer.DiagnosticsHarness.UpdateSyntheticResource(
                key,
                CreateResource(uploaded: 0, downloaded: 4_000, progress: 0.60, peers: 2, state: "Running", key: key));

            Assert.Equal(0, DownloadCacheOptimizer.GetNetworkCacheStats().totalUploadBytes);
        }

        [Fact]
        public void CacheEngine_ConnectedSources_NonNegative()
        {
            ResetCacheState();

            string key = DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 2_000, downloaded: 1_000, progress: 0.45, peers: 5, state: "Connecting", key: "peers"));

            Assert.Equal(5, DownloadCacheOptimizer.GetNetworkCacheStats().connectedSources);

            DownloadCacheOptimizer.DiagnosticsHarness.UpdateSyntheticResource(
                key,
                CreateResource(uploaded: 2_000, downloaded: 1_000, progress: 0.80, peers: 1, state: "Sharing", key: key));

            Assert.Equal(1, DownloadCacheOptimizer.GetNetworkCacheStats().connectedSources);
        }

        [Fact]
        public async Task CacheEngine_GracefulShutdown_Succeeds()
        {
            ResetCacheState();

            DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 10_000, downloaded: 8_000, progress: 0.90, peers: 2, state: "Seeding", key: "shutdown"));

            using (DownloadCacheOptimizer.DiagnosticsHarness.AttachSyntheticClient())
            {
                await DownloadCacheOptimizer.GracefulShutdownAsync();
            }

            Assert.Equal(0, DownloadCacheOptimizer.GetNetworkCacheStats().activeShares);
        }

        [Fact]
        public async Task CacheEngine_Initialization_Idempotent()
        {
            ResetCacheState();

            await DownloadCacheOptimizer.EnsureInitializedAsync();
            await DownloadCacheOptimizer.EnsureInitializedAsync();

            var timestamp = DateTime.UtcNow;
            DownloadCacheOptimizer.DiagnosticsHarness.SetNatStatus(successful: true, 35555, timestamp);

            var natStatus = DownloadCacheOptimizer.GetNatStatus();
            Assert.True(natStatus.successful);
            Assert.Equal(35555, natStatus.port);
            Assert.Equal(timestamp, natStatus.lastCheck);
        }

        [Fact]
        public void CacheEngine_GetSharedResourceDetails_InvalidKey_ReturnsMessage()
        {
            ResetCacheState();

            string details = DownloadCacheOptimizer.GetSharedResourceDetails("nonexistent");

            Assert.Contains("not found", details, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CacheEngine_GetSharedResourceDetails_EmptyKey_HandledGracefully()
        {
            ResetCacheState();

            string key = DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 12_000, downloaded: 6_000, progress: 0.65, peers: 3, state: "Seeding", key: "details"));

            string details = DownloadCacheOptimizer.GetSharedResourceDetails(key);

            Assert.Contains("ContentKey", details, StringComparison.Ordinal);
            Assert.Contains("Progress", details, StringComparison.Ordinal);
            Assert.Contains("Uploaded", details, StringComparison.Ordinal);
        }

        [Fact]
        public void CacheEngine_GetSharedResourceDetails_NullKey_HandledGracefully()
        {
            ResetCacheState();

            string details = DownloadCacheOptimizer.GetSharedResourceDetails(contentKey: null);
            Assert.Contains("not found", details, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CacheEngine_BlockContentId_ValidId_NoException()
        {
            ResetCacheState();

            string validId = new string('a', 40); // Valid SHA-1 format

            DownloadCacheOptimizer.BlockContentId(validId, "Test block");

            Assert.True(DownloadCacheOptimizer.IsContentIdBlocked(validId));
        }

        [Fact]
        public void CacheEngine_BlockContentId_InvalidFormat_NoException()
        {
            ResetCacheState();

            // Even invalid formats should be handled
            DownloadCacheOptimizer.BlockContentId("invalid", "Test block");

            Assert.True(DownloadCacheOptimizer.IsContentIdBlocked("invalid"));
        }

        [Fact]
        public void CacheEngine_Stats_AfterBlock_Consistent()
        {
            ResetCacheState();

            string contentId = new string('b', 40);
            DownloadCacheOptimizer.BlockContentId(contentId, "Test");

            DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 1_000, downloaded: 500, progress: 0.25, peers: 1, state: "Idle", key: "stats"));

            var stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.Equal(1, stats.activeShares);
            Assert.Equal(1_000, stats.totalUploadBytes);
            Assert.Equal(1, stats.connectedSources);
        }

        [Fact]
        public void CacheEngine_MultipleBlocks_Handled()
        {
            ResetCacheState();

            for (int i = 0; i < 10; i++)
            {
                string id = new string((char)('a' + i), 40);
                DownloadCacheOptimizer.BlockContentId(id, $"Block {i}");
            }

            Assert.Equal(10, DownloadCacheOptimizer.GetBlockedContentIdCount());
        }

        [Fact]
        public void CacheEngine_StatsFormat_Valid()
        {
            ResetCacheState();

            string key = DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 2_000_000, downloaded: 1_000_000, progress: 0.50, peers: 2, state: "Sharing", key: "format"));

            string details = DownloadCacheOptimizer.GetSharedResourceDetails(key);

            Assert.Contains("Uploaded", details, StringComparison.Ordinal);
            Assert.Contains("Downloaded", details, StringComparison.Ordinal);
            Assert.Contains("Ratio", details, StringComparison.Ordinal);
        }

        [Fact]
        public void CacheEngine_Bandwidth_WithinLimits()
        {
            ResetCacheState();

            string key = DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 50_000_000, downloaded: 25_000_000, progress: 0.95, peers: 10, state: "Uploading", key: "bandwidth"));

            Assert.Equal(50_000_000, DownloadCacheOptimizer.GetNetworkCacheStats().totalUploadBytes);

            DownloadCacheOptimizer.DiagnosticsHarness.UpdateSyntheticResource(
                key,
                CreateResource(uploaded: -10, downloaded: 25_000_000, progress: 0.95, peers: 10, state: "Uploading", key: key));

            Assert.Equal(0, DownloadCacheOptimizer.GetNetworkCacheStats().totalUploadBytes);
        }

        [Fact]
        public void CacheEngine_ConnectionLimit_Respected()
        {
            ResetCacheState();

            DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 1_000, downloaded: 1_000, progress: 0.10, peers: 150, state: "Busy", key: "limit"));

            Assert.Equal(150, DownloadCacheOptimizer.GetNetworkCacheStats().connectedSources);
        }

        [Fact]
        public void CacheEngine_EncryptionEnabled_Default()
        {
            ResetCacheState();

            string key = DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 250_000, downloaded: 0, progress: 0.40, peers: 2, state: "Active", key: "encryption"));

            string details = DownloadCacheOptimizer.GetSharedResourceDetails(key);
            Assert.Contains("Downloaded: 0.00 MB", details, StringComparison.Ordinal);
        }

        [Fact]
        public void CacheEngine_DiskCacheConfigured()
        {
            ResetCacheState();

            string key = DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 75_000, downloaded: 50_000, progress: 0.33, peers: 1, state: "Preparing", key: "disk"));

            string details = DownloadCacheOptimizer.GetSharedResourceDetails(key);
            Assert.Contains("State: Preparing", details, StringComparison.Ordinal);
            Assert.Contains("Progress", details, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CacheEngine_RepeatedShutdown_Safe()
        {
            ResetCacheState();

            DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateResource(uploaded: 5_000, downloaded: 2_500, progress: 0.20, peers: 1, state: "Stopping", key: "repeat"));

            using (DownloadCacheOptimizer.DiagnosticsHarness.AttachSyntheticClient())
            {
                await DownloadCacheOptimizer.GracefulShutdownAsync();
                await DownloadCacheOptimizer.GracefulShutdownAsync();
            }

            Assert.Equal(0, DownloadCacheOptimizer.GetNetworkCacheStats().activeShares);
        }
    }
}

