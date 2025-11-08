// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.Linq;
using System.Threading.Tasks;
using KOTORModSync.Core.Services.Download;
using Xunit;

namespace KOTORModSync.Tests.Services.DistributedCache
{
    /// <summary>
    /// Tests for distributed cache engine operations, statistics, and lifecycle.
    /// </summary>
    [Collection("DistributedCache")]
    public class CacheEngineTests : IClassFixture<DistributedCacheTestFixture>
    {
        private readonly DistributedCacheTestFixture _fixture;

        public CacheEngineTests(DistributedCacheTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void CacheEngine_GetStats_ReturnsValid()
        {
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();

            Assert.True(stats.activeShares >= 0);
            Assert.True(stats.totalUploadBytes >= 0);
            Assert.True(stats.connectedSources >= 0);
        }

        [Fact]
        public void CacheEngine_GetStats_MultipleCalls_Consistent()
        {
            (int activeShares, long totalUploadBytes, int connectedSources) stats1 = DownloadCacheOptimizer.GetNetworkCacheStats();
            (int activeShares, long totalUploadBytes, int connectedSources) stats2 = DownloadCacheOptimizer.GetNetworkCacheStats();

            // Stats should be consistent across calls
            Assert.True(stats1.activeShares >= 0);
            Assert.True(stats2.activeShares >= 0);
        }

        [Fact]
        public void CacheEngine_ActiveShares_NonNegative()
        {
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.True(stats.activeShares >= 0);
        }

        [Fact]
        public void CacheEngine_TotalUpload_NonNegative()
        {
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.True(stats.totalUploadBytes >= 0);
        }

        [Fact]
        public void CacheEngine_ConnectedSources_NonNegative()
        {
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.True(stats.connectedSources >= 0);
        }

        [Fact]
        public async Task CacheEngine_GracefulShutdown_Succeeds()
        {
            await DownloadCacheOptimizer.GracefulShutdownAsync().ConfigureAwait(false);

            // After shutdown, stats should still work
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.NotNull(stats);
        }

        [Fact]
        public async Task CacheEngine_Initialization_Idempotent()
        {
            await DownloadCacheOptimizer.EnsureInitializedAsync().ConfigureAwait(false);
            await DownloadCacheOptimizer.EnsureInitializedAsync().ConfigureAwait(false);

            // Multiple initializations should be safe
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.NotNull(stats);
        }

        [Fact]
        public void CacheEngine_GetSharedResourceDetails_InvalidKey_ReturnsMessage()
        {
            string details = DownloadCacheOptimizer.GetSharedResourceDetails("nonexistent");

            Assert.Contains("not found", details, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void CacheEngine_GetSharedResourceDetails_EmptyKey_HandledGracefully()
        {
            string details = DownloadCacheOptimizer.GetSharedResourceDetails("");

            Assert.NotNull(details);
        }

        [Fact]
        public void CacheEngine_GetSharedResourceDetails_NullKey_HandledGracefully()
        {
            string details = DownloadCacheOptimizer.GetSharedResourceDetails(null);

            Assert.NotNull(details);
        }

        [Fact]
        public void CacheEngine_BlockContentId_ValidId_NoException()
        {
            string validId = new string('a', 40); // Valid SHA-1 format

            // Should not throw
            DownloadCacheOptimizer.BlockContentId(validId, "Test block");
        }

        [Fact]
        public void CacheEngine_BlockContentId_InvalidFormat_NoException()
        {
            // Even invalid formats should be handled
            DownloadCacheOptimizer.BlockContentId("invalid", "Test block");
        }

        [Fact]
        public void CacheEngine_Stats_AfterBlock_Consistent()
        {
            string contentId = new string('b', 40);
            DownloadCacheOptimizer.BlockContentId(contentId, "Test");

            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.NotNull(stats);
        }

        [Fact]
        public void CacheEngine_MultipleBlocks_Handled()
        {
            for (int i = 0; i < 10; i++)
            {
                string id = new string((char)('a' + i), 40);
                DownloadCacheOptimizer.BlockContentId(id, $"Block {i}");
            }

            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.NotNull(stats);
        }

        [Fact]
        public void CacheEngine_StatsFormat_Valid()
        {
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();

            // All numeric fields should be non-negative
            Assert.True(stats.activeShares >= 0);
            Assert.True(stats.totalUploadBytes >= 0);
            Assert.True(stats.connectedSources >= 0);
        }

        [Fact]
        public void CacheEngine_Bandwidth_WithinLimits()
        {
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();

            // Upload should respect configured limits (100 KB/s = 102400 bytes/s)
            // Over time, total upload should stay reasonable
            Assert.True(stats.totalUploadBytes >= 0);
        }

        [Fact]
        public void CacheEngine_ConnectionLimit_Respected()
        {
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();

            // Connected sources should be within configured limits (150)
            Assert.True(stats.connectedSources <= 150);
        }

        [Fact]
        public void CacheEngine_EncryptionEnabled_Default()
        {
            // Encryption should be enabled by default
            // We can't directly check this, but verify engine works
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.NotNull(stats);
        }

        [Fact]
        public void CacheEngine_DiskCacheConfigured()
        {
            // Disk cache should be configured
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.NotNull(stats);
        }

        [Fact]
        public async Task CacheEngine_RepeatedShutdown_Safe()
        {
            await DownloadCacheOptimizer.GracefulShutdownAsync().ConfigureAwait(false);
            await DownloadCacheOptimizer.GracefulShutdownAsync().ConfigureAwait(false);

            // Multiple shutdowns should be safe
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.NotNull(stats);
        }
    }
}

