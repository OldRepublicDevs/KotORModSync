// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Generic;

using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Utility;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class ContentIdGenerationTests
    {
        [Test]
        public void ComputeContentIdFromMetadata_WithDeadlyStream_RealData_ProducesDeterministicId()
        {
            // Real data from KOTOR Dialogue Fixes on DeadlyStream
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1313",
                ["changelogId"] = "0", // Current version
                ["fileId"] = "1313",
                ["version"] = "5.2",
                ["updated"] = "2024-06-13",
                ["size"] = 15728640L, // ~15MB estimated
            };

            string url = "https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/";

            string contentId1 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);
            string contentId2 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            // Same metadata produces same ContentId
            Assert.That(contentId1, Is.EqualTo(contentId2));

            // ContentId should be 40 hex chars
            Assert.That(contentId1, Has.Length.EqualTo(40));
            Assert.That(contentId1, Does.Match("^[0-9a-f]+$"));
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithMega_RealData_UsesMerkleHash()
        {
            // Real data from Character Startup Changes on MEGA
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "mega",
                ["nodeId"] = "sRw1GBIK",
                ["hash"] = "27f3940b1aeed6c2fa76699409b15218", // MEGA's merkle tree hash
                ["size"] = 51200L, // ~50KB
                ["mtime"] = 1427500800L, // Approximate timestamp
                ["name"] = "Character_Startup_Changes_Patch.rar",
            };

            string url = "https://mega.nz/file/sRw1GBIK#J8znLBwR6t7ZvZnpQbsUBYcUNfPCWA7wYNW3qU6gZSg";

            string contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            // ContentId should be 40 hex characters (SHA-1)
            Assert.That(contentId, Has.Length.EqualTo(40));
            Assert.That(contentId, Does.Match("^[0-9a-f]+$"));

            // Different calls with same metadata should produce same ID
            string contentId2 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);
            Assert.That(contentId, Is.EqualTo(contentId2));
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithNexus_UsesApiFields()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "nexus",
                ["fileId"] = "12345",
                ["fileName"] = "mod_v1.0.zip",
                ["size"] = 4096L,
                ["uploadedTimestamp"] = 1704067200L,
                ["md5Hash"] = "abc123def456",
            };

            string url = "https://nexusmods.com/kotor/mods/123?tab=files";

            string contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            Assert.That(contentId, Has.Length.EqualTo(40));
            Assert.That(contentId, Does.Match("^[0-9a-f]+$"));
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithDirect_UsesHttpHeaders()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "direct",
                ["url"] = "https://example.com/file.zip",
                ["contentLength"] = 8192L,
                ["lastModified"] = "Mon, 01 Jan 2025 00:00:00 GMT",
                ["etag"] = "\"abc123\"",
                ["fileName"] = "file.zip",
            };

            string url = "https://example.com/file.zip";

            string contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            Assert.That(contentId, Has.Length.EqualTo(40));
            Assert.That(contentId, Does.Match("^[0-9a-f]+$"));
        }

        [Test]
        public void ComputeContentIdFromMetadata_DifferentVersions_ProducesDifferentId()
        {
            // Version 5.1 of KOTOR Dialogue Fixes
            var metadataV51 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1313",
                ["changelogId"] = "3614",
                ["fileId"] = "1313",
                ["version"] = "5.1",
                ["updated"] = "2024-05-31",
                ["size"] = 15700000L,
            };

            // Version 5.2 of KOTOR Dialogue Fixes
            var metadataV52 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1313",
                ["changelogId"] = "0",
                ["fileId"] = "1313",
                ["version"] = "5.2",
                ["updated"] = "2024-06-13",
                ["size"] = 15728640L,
            };

            string url = "https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/";

            string contentId1 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadataV51, url);
            string contentId2 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadataV52, url);

            // Different versions should produce different ContentIds
            Assert.That(contentId1, Is.Not.EqualTo(contentId2));
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithMissingOptionalFields_StillWorks()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1234",
                // Other fields missing
            };

            string url = "https://deadlystream.com/files/file/1234";

            string contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            Assert.That(contentId, Has.Length.EqualTo(40));
            Assert.That(contentId, Does.Match("^[0-9a-f]+$"));
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithUrlVariations_ProducesSameId()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1333",
                ["changelogId"] = "0",
                ["fileId"] = "1333",
                ["version"] = "1.9",
                ["updated"] = "2024-03-28",
                ["size"] = 5242880L,
            };

            // Real DeadlyStream URLs with different query params - JC's Minor Fixes
            string url1 = "https://deadlystream.com/files/file/1333-jcs-minor-fixes-for-k1/";
            string url2 = "https://deadlystream.com/files/file/1333-jcs-minor-fixes-for-k1/?tab=files";
            string url3 = "https://deadlystream.com/files/file/1333-jcs-minor-fixes-for-k1/?tab=reviews";

            string normalizedUrl1 = UrlNormalizer.Normalize(url1);
            string normalizedUrl2 = UrlNormalizer.Normalize(url2);
            string normalizedUrl3 = UrlNormalizer.Normalize(url3);

            // Simple test to verify normalization
            string testUrl = "https://example.com/path?query=1#fragment";
            string testNormalized = UrlNormalizer.Normalize(testUrl);
            TestContext.WriteLine($"Test normalization: {testUrl} -> {testNormalized}");

            TestContext.WriteLine($"URL1: {url1} -> {normalizedUrl1}");
            TestContext.WriteLine($"URL2: {url2} -> {normalizedUrl2}");
            TestContext.WriteLine($"URL3: {url3} -> {normalizedUrl3}");

            string contentId1 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url1);
            string contentId2 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url2);
            string contentId3 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url3);

            TestContext.WriteLine($"ContentId1: {contentId1}");
            TestContext.WriteLine($"ContentId2: {contentId2}");
            TestContext.WriteLine($"ContentId3: {contentId3}");

            // All should produce same ContentId (normalized URL base is same, query params removed)
            Assert.That(contentId1, Is.EqualTo(contentId2));
            Assert.That(contentId2, Is.EqualTo(contentId3));
            Assert.That(contentId1, Has.Length.EqualTo(40));
        }

        [Test]
        public void ComputeContentIdFromMetadata_IsIdempotent()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "nexus",
                ["fileId"] = "99999",
                ["fileName"] = "test.zip",
                ["size"] = 16384L,
                ["uploadedTimestamp"] = 1704067200L,
                ["md5Hash"] = "test123",
            };

            string url = "https://nexusmods.com/kotor/mods/999";

            // Call multiple times
            string id1 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);
            string id2 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);
            string id3 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            Assert.That(id1, Is.EqualTo(id2));
            Assert.That(id2, Is.EqualTo(id3));
        }
    }
}
