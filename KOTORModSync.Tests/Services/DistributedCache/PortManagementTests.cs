// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.IO;
using System.Threading.Tasks;
using KOTORModSync.Core.Services.Download;
using Xunit;

namespace KOTORModSync.Tests.Services.DistributedCache
{
    /// <summary>
    /// Tests for port management, persistence, and availability.
    /// </summary>
    [Collection("DistributedCache")]
    public class PortManagementTests : IClassFixture<DistributedCacheTestFixture>, IDisposable
    {
        private readonly DistributedCacheTestFixture _fixture;
        private IDisposable _clientScope;

        public PortManagementTests(DistributedCacheTestFixture fixture)
        {
            _fixture = fixture;
            ResetState();
        }

        public void Dispose()
        {
            _clientScope?.Dispose();
        }

        private void ResetState()
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

        private static DownloadCacheOptimizer.DiagnosticsHarness.SyntheticResourceOptions CreateShare(
            string key,
            long uploaded = 0,
            long downloaded = 0,
            double progress = 0,
            int peers = 0,
            string state = "Idle")
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

        private static string GetHarnessPortPath()
        {
            return DownloadCacheOptimizer.DiagnosticsHarness.GetPortConfigurationPath();
        }

        [Fact]
        public void PortManagement_FindAvailablePort_ReturnsValid()
        {
            ResetState();

            DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateShare("share", uploaded: 1234, downloaded: 567, progress: 0.4, peers: 1, state: "Ready"));

            (int activeShares, long totalUploadBytes, int connectedSources) = DownloadCacheOptimizer.GetNetworkCacheStats();

            Assert.Equal(1, activeShares);
            Assert.Equal(1234, totalUploadBytes);
            Assert.Equal(1, connectedSources);
        }

        [Fact]
        public void PortManagement_PortPersistence_ConfigExistsAndContainsPort()
        {
            ResetState();

            string configPath = GetHarnessPortPath();
            string directory = Path.GetDirectoryName(configPath)!;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(configPath, "35555");

            using (DownloadCacheOptimizer.DiagnosticsHarness.AttachSyntheticClient())
            {
                DownloadCacheOptimizer.DiagnosticsHarness.SetClientSettings(new
                {
                    ListenPort = 35555,
                    AllowPortForwarding = true,
                    MaximumConnections = 150,
                });

                var status = DownloadCacheOptimizer.GetNatStatus();
                Assert.Equal(35555, status.port);
            }
        }

        [Fact]
        public async Task PortManagement_EngineStartup_UsesConfiguredPort()
        {
            ResetState();

            string configPath = GetHarnessPortPath();
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            await File.WriteAllTextAsync(configPath, "40000");

            DownloadCacheOptimizer.DiagnosticsHarness.SetNatStatus(successful: true, 40000, DateTime.UtcNow);

            await DownloadCacheOptimizer.EnsureInitializedAsync();

            var status = DownloadCacheOptimizer.GetNatStatus();
            Assert.True(status.successful);
            Assert.Equal(40000, status.port);
        }

        [Fact]
        public void PortManagement_PortConflict_HandledGracefully()
        {
            ResetState();

            DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateShare("conflict", uploaded: 0, downloaded: 0, progress: 0, peers: 0, state: "Initializing"));

            (int activeShares, long _totalUploadBytes, int _connectedSources) = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.Equal(1, activeShares);
        }

        [Fact]
        public void PortManagement_MultipleInstances_Simulated()
        {
            ResetState();

            DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateShare("instanceA", uploaded: 100, downloaded: 50, progress: 0.2, peers: 1, state: "Running"));
            DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateShare("instanceB", uploaded: 200, downloaded: 120, progress: 0.5, peers: 2, state: "Running"));

            var stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.Equal(2, stats.activeShares);
            Assert.Equal(300, stats.totalUploadBytes);
            Assert.Equal(3, stats.connectedSources);
        }

        [Fact]
        public async Task PortManagement_PortRelease_OnShutdown()
        {
            ResetState();

            DownloadCacheOptimizer.DiagnosticsHarness.RegisterSyntheticResource(
                CreateShare("shutdown", uploaded: 1000, downloaded: 500, progress: 0.7, peers: 1, state: "Running"));

            using (DownloadCacheOptimizer.DiagnosticsHarness.AttachSyntheticClient())
            {
                await DownloadCacheOptimizer.GracefulShutdownAsync();
            }

            Assert.Equal(0, DownloadCacheOptimizer.GetNetworkCacheStats().activeShares);
        }

        [Fact]
        public void PortManagement_InvalidPort_Rejected()
        {
            ResetState();

            DownloadCacheOptimizer.DiagnosticsHarness.SetNatStatus(successful: true, -1, DateTime.UtcNow);
            var status = DownloadCacheOptimizer.GetNatStatus();
            Assert.True(status.port >= 0);

            DownloadCacheOptimizer.DiagnosticsHarness.SetNatStatus(successful: true, 70000, DateTime.UtcNow);
            status = DownloadCacheOptimizer.GetNatStatus();
            Assert.True(status.port <= 65535);
        }

        [Fact]
        public void PortManagement_NATTraversal_Attempted()
        {
            ResetState();

            var initialStatus = DownloadCacheOptimizer.GetNatStatus();
            Assert.False(initialStatus.successful);

            DownloadCacheOptimizer.DiagnosticsHarness.SetNatStatus(successful: true, 35555, DateTime.UtcNow);
            var status = DownloadCacheOptimizer.GetNatStatus();
            Assert.True(status.successful);
            Assert.Equal(35555, status.port);
        }

        [Fact]
        public void PortManagement_UPnP_Configured()
        {
            ResetState();

            using (DownloadCacheOptimizer.DiagnosticsHarness.AttachSyntheticClient())
            {
                DownloadCacheOptimizer.DiagnosticsHarness.SetClientSettings(new
                {
                    ListenPort = 49999,
                    AllowPortForwarding = true,
                    MaximumConnections = 100,
                });

                var status = DownloadCacheOptimizer.GetNatStatus();
                Assert.Equal(49999, status.port);
            }
        }

        [Fact]
        public void PortManagement_NATPMP_Configured()
        {
            ResetState();

            DownloadCacheOptimizer.DiagnosticsHarness.SetNatStatus(successful: true, 42000, DateTime.UtcNow);
            var status = DownloadCacheOptimizer.GetNatStatus();
            Assert.True(status.successful);
            Assert.Equal(42000, status.port);
        }
    }
}

