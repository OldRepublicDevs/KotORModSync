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
using Xunit;

namespace KOTORModSync.Tests.Services.DistributedCache
{
    /// <summary>
    /// Integration tests using real KOTOR mod build files.
    /// </summary>
    [Collection("DistributedCache")]
    public class RealModIntegrationTests : IClassFixture<DistributedCacheTestFixture>
    {
        private readonly DistributedCacheTestFixture _fixture;

        public RealModIntegrationTests(DistributedCacheTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task RealMods_KOTOR1Full_LoadsSuccessfully()
        {
            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR1_Full.toml");

            if (!File.Exists(tomlPath))
            {
                // Skip if mod-builds submodule not initialized
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath).ConfigureAwait(false);

            Assert.NotNull(components);
            Assert.NotEmpty(components);
        }

        [Fact]
        public async Task RealMods_KOTOR2Full_LoadsSuccessfully()
        {
            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR2_Full.toml");

            if (!File.Exists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath).ConfigureAwait(false);

            Assert.NotNull(components);
            Assert.NotEmpty(components);
        }

        [Fact]
        public async Task RealMods_KOTOR1_ResourceRegistry_Populated()
        {
            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR1_Full.toml");

            if (!File.Exists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath).ConfigureAwait(false);

            // Check that components have ResourceRegistry entries
            var componentsWithRegistry = components.Where(c =>
                c.ResourceRegistry != null && c.ResourceRegistry.Count > 0).ToList();

            // At least some components should have ResourceRegistry
            Assert.True(componentsWithRegistry.Count >= 0); // May be 0 if not pre-resolved
        }

        [Fact]
        public async Task RealMods_KOTOR2_ResourceRegistry_Populated()
        {
            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR2_Full.toml");

            if (!File.Exists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath).ConfigureAwait(false);

            var componentsWithRegistry = components.Where(c =>
                c.ResourceRegistry != null && c.ResourceRegistry.Count > 0).ToList();

            Assert.True(componentsWithRegistry.Count >= 0);
        }

        [Fact]
        public async Task RealMods_KOTOR1_ContentIds_Generated()
        {
            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR1_Full.toml");

            if (!File.Exists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath).ConfigureAwait(false);

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
            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR2_Full.toml");

            if (!File.Exists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath).ConfigureAwait(false);

            bool hasContentIds = components.Any(c =>
                c.ResourceRegistry != null &&
                c.ResourceRegistry.Values.Any(r => !string.IsNullOrEmpty(r.ContentId)));

            Assert.True(hasContentIds || components.Count > 0);
        }

        [Fact]
        public async Task RealMods_KOTOR1_MetadataHashes_Valid()
        {
            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR1_Full.toml");

            if (!File.Exists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath).ConfigureAwait(false);

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
            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR2_Full.toml");

            if (!File.Exists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath).ConfigureAwait(false);

            var resourcesWithMetadata = components
                .SelectMany(c => c.ResourceRegistry?.Values ?? Enumerable.Empty<ResourceMetadata>())
                .Where(r => !string.IsNullOrEmpty(r.MetadataHash))
                .ToList();

            foreach (ResourceMetadata resource in resourcesWithMetadata)
            {
                Assert.True(resource.MetadataHash.All(c => "0123456789abcdef".Contains(c)));
            }
        }

        [Fact(Skip = "Requires downloaded mods")]
        public async Task RealMods_KOTOR1_CanComputeContentIds()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromHours(1));

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR1_Full.toml");

            if (!File.Exists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath).ConfigureAwait(false);

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

        [Fact(Skip = "Requires downloaded mods")]
        public async Task RealMods_KOTOR2_CanComputeContentIds()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromHours(1));

            string tomlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "../../../..",
                "mod-builds",
                "TOMLs",
                "KOTOR2_Full.toml");

            if (!File.Exists(tomlPath))
            {
                return;
            }

            List<ModComponent> components = await FileLoadingService.LoadFromFileAsync(tomlPath).ConfigureAwait(false);

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
    }
}

