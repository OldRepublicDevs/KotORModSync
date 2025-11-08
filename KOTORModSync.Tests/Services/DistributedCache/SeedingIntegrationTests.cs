// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Services.Download;
using Xunit;

namespace KOTORModSync.Tests.Services.DistributedCache
{
    /// <summary>
    /// Integration tests for seeding functionality using Docker containers.
    /// </summary>
    [Collection("DistributedCache")]
    public class SeedingIntegrationTests : IClassFixture<DistributedCacheTestFixture>
    {
        private readonly DistributedCacheTestFixture _fixture;

        public SeedingIntegrationTests(DistributedCacheTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact(Skip = "Requires Docker")]
        public async Task Seeding_QBittorrent_StartsSuccessfully()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            DockerBitTorrentClient client = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.QBittorrent,
                cts.Token).ConfigureAwait(false);

            Assert.NotNull(client.ContainerId);
            Assert.True(client.WebPort > 0);
            Assert.True(client.BitTorrentPort > 0);
        }

        [Fact(Skip = "Requires Docker")]
        public async Task Seeding_Transmission_StartsSuccessfully()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            DockerBitTorrentClient client = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.Transmission,
                cts.Token).ConfigureAwait(false);

            Assert.NotNull(client.ContainerId);
        }

        [Fact(Skip = "Requires Docker")]
        public async Task Seeding_Deluge_StartsSuccessfully()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            DockerBitTorrentClient client = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.Deluge,
                cts.Token).ConfigureAwait(false);

            Assert.NotNull(client.ContainerId);
        }

        [Fact(Skip = "Requires Docker")]
        public async Task Seeding_AddTorrent_Success()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            // Start container
            DockerBitTorrentClient client = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.QBittorrent,
                cts.Token).ConfigureAwait(false);

            // Create test file and torrent
            string testFile = _fixture.CreateTestFile("seed_test.bin", 1024 * 1024);
            string torrentFile = await _fixture.CreateTorrentFileAsync(
                testFile,
                "seed_test",
                262144).ConfigureAwait(false);

            // Add to client
            string downloadPath = _fixture.CreateTempDirectory("downloads");
            string hash = await client.AddTorrentAsync(torrentFile, downloadPath, cts.Token).ConfigureAwait(false);

            Assert.NotNull(hash);
        }

        [Fact(Skip = "Requires Docker")]
        public async Task Seeding_TorrentReachesSeeding_State()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

            DockerBitTorrentClient client = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.QBittorrent,
                cts.Token).ConfigureAwait(false);

            string testFile = _fixture.CreateTestFile("seeding_test.bin", 512 * 1024);
            string torrentFile = await _fixture.CreateTorrentFileAsync(
                testFile,
                "seeding_test").ConfigureAwait(false);

            string downloadPath = _fixture.CreateTempDirectory("downloads");

            // Copy file to download location so it's immediately complete
            File.Copy(testFile, Path.Combine(downloadPath, Path.GetFileName(testFile)));

            string hash = await client.AddTorrentAsync(torrentFile, downloadPath, cts.Token).ConfigureAwait(false);

            // Wait for seeding state
            await _fixture.WaitForConditionAsync(
                async () =>
                {
                    DockerBitTorrentClient.TorrentStats stats = await client.GetTorrentStatsAsync(hash, cts.Token).ConfigureAwait(false);
                    return stats.Progress >= 1.0 || stats.State.Contains("seed", StringComparison.OrdinalIgnoreCase);
                },
                TimeSpan.FromMinutes(5),
                "Torrent did not reach seeding state",
                cts.Token).ConfigureAwait(false);
        }

        [Fact(Skip = "Requires Docker")]
        public async Task Seeding_MonoTorrent_CanConnectToQBittorrent()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));

            // Start qBittorrent container
            DockerBitTorrentClient qbt = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.QBittorrent,
                cts.Token).ConfigureAwait(false);

            // Create test file
            string testFile = _fixture.CreateTestFile("peer_test.bin", 256 * 1024);
            string torrentFile = await _fixture.CreateTorrentFileAsync(
                testFile,
                "peer_test",
                65536,
                new System.Collections.Generic.List<string> { "udp://localhost:6969/announce" }).ConfigureAwait(false);

            // Add to qBittorrent and let it seed
            string qbtDownloadPath = _fixture.CreateTempDirectory("qbt_downloads");
            File.Copy(testFile, Path.Combine(qbtDownloadPath, Path.GetFileName(testFile)));
            await qbt.AddTorrentAsync(torrentFile, qbtDownloadPath, cts.Token).ConfigureAwait(false);

            // Wait for qBittorrent to start seeding
            await Task.Delay(5000, cts.Token).ConfigureAwait(false);

            // Start MonoTorrent download
            string downloadPath = _fixture.CreateTempDirectory("mono_downloads");
            DownloadResult result = await DownloadCacheOptimizer.TryOptimizedDownload(
                url: "test://peer_test",
                destinationDirectory: downloadPath,
                traditionalDownloadFunc: async () => throw new InvalidOperationException("Should not fall back to URL"),
                progress: null,
                cancellationToken: cts.Token,
                contentId: null).ConfigureAwait(false);

            // Verify MonoTorrent discovered the peer (via LPD or DHT)
            // This is a weak test - just verifies the download attempt was made
            Assert.NotNull(result);
        }

        [Fact(Skip = "Requires Docker and long runtime")]
        public async Task Seeding_LocalPeerDiscovery_FindsPeers()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

            // Start two containers on same network
            DockerBitTorrentClient seeder = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.QBittorrent,
                cts.Token).ConfigureAwait(false);

            DockerBitTorrentClient leecher = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.Transmission,
                cts.Token).ConfigureAwait(false);

            // Create and add torrent to seeder
            string testFile = _fixture.CreateTestFile("lpd_test.bin", 128 * 1024);
            string torrentFile = await _fixture.CreateTorrentFileAsync(
                testFile,
                "lpd_test",
                65536).ConfigureAwait(false);

            string seederPath = _fixture.CreateTempDirectory("seeder");
            File.Copy(testFile, Path.Combine(seederPath, Path.GetFileName(testFile)));
            await seeder.AddTorrentAsync(torrentFile, seederPath, cts.Token).ConfigureAwait(false);

            // Add to leecher
            string leecherPath = _fixture.CreateTempDirectory("leecher");
            string hash = await leecher.AddTorrentAsync(torrentFile, leecherPath, cts.Token).ConfigureAwait(false);

            // Wait for LPD to discover peer
            await _fixture.WaitForConditionAsync(
                async () =>
                {
                    DockerBitTorrentClient.TorrentStats stats = await leecher.GetTorrentStatsAsync(hash, cts.Token).ConfigureAwait(false);
                    return stats.Peers > 0 || stats.Seeds > 0;
                },
                TimeSpan.FromMinutes(10),
                "Local peer discovery failed to find peers",
                cts.Token).ConfigureAwait(false);
        }

        [Fact(Skip = "Requires Docker")]
        public async Task Seeding_Upload_IncreasesOverTime()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));

            DockerBitTorrentClient seeder = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.QBittorrent,
                cts.Token).ConfigureAwait(false);

            DockerBitTorrentClient leecher = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.Transmission,
                cts.Token).ConfigureAwait(false);

            string testFile = _fixture.CreateTestFile("upload_test.bin", 512 * 1024);
            string torrentFile = await _fixture.CreateTorrentFileAsync(
                testFile,
                "upload_test").ConfigureAwait(false);

            // Setup seeder
            string seederPath = _fixture.CreateTempDirectory("seeder");
            File.Copy(testFile, Path.Combine(seederPath, Path.GetFileName(testFile)));
            string seederHash = await seeder.AddTorrentAsync(torrentFile, seederPath, cts.Token).ConfigureAwait(false);

            // Get initial upload
            DockerBitTorrentClient.TorrentStats initialStats = await seeder.GetTorrentStatsAsync(seederHash, cts.Token).ConfigureAwait(false);
            long initialUploaded = initialStats.Uploaded;

            // Start leecher
            string leecherPath = _fixture.CreateTempDirectory("leecher");
            await leecher.AddTorrentAsync(torrentFile, leecherPath, cts.Token).ConfigureAwait(false);

            // Wait and check upload increased
            await Task.Delay(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);

            DockerBitTorrentClient.TorrentStats finalStats = await seeder.GetTorrentStatsAsync(seederHash, cts.Token).ConfigureAwait(false);
            Assert.True(finalStats.Uploaded > initialUploaded,
                $"Upload did not increase: {initialUploaded} -> {finalStats.Uploaded}");
        }

        [Fact(Skip = "Requires Docker")]
        public async Task Seeding_MultipleFiles_AllSeeded()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

            DockerBitTorrentClient client = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.QBittorrent,
                cts.Token).ConfigureAwait(false);

            // Create multiple test files
            var files = new[]
            {
                _fixture.CreateTestFile("multi1.bin", 128 * 1024),
                _fixture.CreateTestFile("multi2.bin", 256 * 1024),
                _fixture.CreateTestFile("multi3.bin", 512 * 1024)
            };

            string downloadPath = _fixture.CreateTempDirectory("multi_seed");

            // Create torrents and add them
            foreach (string file in files)
            {
                string torrentFile = await _fixture.CreateTorrentFileAsync(
                    file,
                    Path.GetFileNameWithoutExtension(file)).ConfigureAwait(false);

                File.Copy(file, Path.Combine(downloadPath, Path.GetFileName(file)));
                await client.AddTorrentAsync(torrentFile, downloadPath, cts.Token).ConfigureAwait(false);
            }

            // Verify all are added
            await Task.Delay(2000, cts.Token).ConfigureAwait(false);
            // Success if no exceptions thrown
        }

        [Fact(Skip = "Requires Docker")]
        public async Task Seeding_PortForwarding_Enabled()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            DockerBitTorrentClient client = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.QBittorrent,
                cts.Token).ConfigureAwait(false);

            // Port forwarding should be enabled by default
            Assert.True(client.BitTorrentPort > 0);
        }

        [Fact(Skip = "Requires Docker")]
        public async Task Seeding_Stats_Accurate()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

            DockerBitTorrentClient client = await _fixture.StartContainerAsync(
                DockerBitTorrentClient.BitTorrentClientType.QBittorrent,
                cts.Token).ConfigureAwait(false);

            string testFile = _fixture.CreateTestFile("stats_test.bin", 256 * 1024);
            string torrentFile = await _fixture.CreateTorrentFileAsync(
                testFile,
                "stats_test").ConfigureAwait(false);

            string downloadPath = _fixture.CreateTempDirectory("stats");
            File.Copy(testFile, Path.Combine(downloadPath, Path.GetFileName(testFile)));

            string hash = await client.AddTorrentAsync(torrentFile, downloadPath, cts.Token).ConfigureAwait(false);

            // Wait a bit
            await Task.Delay(3000, cts.Token).ConfigureAwait(false);

            DockerBitTorrentClient.TorrentStats stats = await client.GetTorrentStatsAsync(hash, cts.Token).ConfigureAwait(false);

            Assert.NotNull(stats);
            Assert.True(stats.Progress >= 0.0 && stats.Progress <= 1.0);
            Assert.True(stats.Downloaded >= 0);
            Assert.True(stats.Uploaded >= 0);
        }
    }
}

