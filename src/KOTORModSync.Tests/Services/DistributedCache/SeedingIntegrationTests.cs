// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.Services.Download;
using Xunit;

namespace KOTORModSync.Tests.Services.DistributedCache
{
    /// <summary>
    /// Integration tests for seeding functionality using Docker containers (Relay / Cascade).
    /// </summary>
    [Collection("DistributedCache")]
    public class SeedingIntegrationTests : IClassFixture<DistributedCacheTestFixture>
    {
        private readonly DistributedCacheTestFixture _fixture;

        public SeedingIntegrationTests(DistributedCacheTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Seeding_Relay_StartsSuccessfully()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            DockerCacheClient client = await _fixture.StartContainerAsync(
                DockerCacheClient.CacheClientFlavor.Relay,
                cts.Token);

            Assert.NotNull(client.ContainerId);
            Assert.True(client.WebPort > 0);
            Assert.True(client.DistributionPort > 0);
        }

        [Fact]
        public async Task Seeding_Cascade_StartsSuccessfully()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            DockerCacheClient client = await _fixture.StartContainerAsync(
                DockerCacheClient.CacheClientFlavor.Cascade,
                cts.Token);

            Assert.NotNull(client.ContainerId);
            Assert.True(client.WebPort > 0);
            Assert.True(client.DistributionPort > 0);
        }

        [Fact]
        public async Task Seeding_AddDescriptor_Success()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            DockerCacheClient client = await _fixture.StartContainerAsync(
                DockerCacheClient.CacheClientFlavor.Relay,
                cts.Token);

            string testFile = _fixture.CreateTestFile("seed_test.bin", 1024 * 1024);
            string descriptorPath = await _fixture.CreateDescriptorFileAsync(
                testFile,
                "seed_test",
                262144);
            DistributionPayload payload = _fixture.GetDescriptorPayload(descriptorPath);

            string downloadPath = _fixture.CreateTempDirectory("downloads");
            string contentKey = await client.AddResourceAsync(descriptorPath, downloadPath, payload.ContentId, cts.Token);

            Assert.Equal(payload.ContentId, contentKey, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void ContainerEngine_DetectionWorks()
        {
            var client = new DockerCacheClient(DockerCacheClient.CacheClientFlavor.Relay);
            Assert.NotNull(client);
        }
    }
}

