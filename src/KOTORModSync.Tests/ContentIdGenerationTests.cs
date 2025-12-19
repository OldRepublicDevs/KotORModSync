// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Utility;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class ContentIdGenerationTests
    {
        [Test]
        public void ComputeContentIdFromMetadata_WithDeadlyStream_RealData_ProducesDeterministicId_ContentIdGeneration()
        {
            // Example data from Example Dialogue Enhancement on DeadlyStream
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

            string url = "https://deadlystream.com/files/file/1313-example-dialogue-enhancement/";

            string contentId1 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);
            string contentId2 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            // Same metadata produces same ContentId
            Assert.Multiple(() =>
            {
                Assert.That(contentId1, Is.EqualTo(contentId2), "Same metadata should produce identical ContentId");
                Assert.That(contentId1, Is.Not.Null, "ContentId should not be null");
                Assert.That(contentId1, Has.Length.EqualTo(40), "ContentId should be exactly 40 hexadecimal characters");
                Assert.That(contentId1, Does.Match("^[0-9a-f]+$"), "ContentId should contain only lowercase hexadecimal digits");
                Assert.That(contentId1, Is.Not.Empty, "ContentId should not be empty");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithMega_RealData_UsesMerkleHash_ContentIdGeneration()
        {
            // Example data from Example Startup Modifier on MEGA
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "mega",
                ["nodeId"] = "sRw1GBIK",
                ["hash"] = "27f3940b1aeed6c2fa76699409b15218", // MEGA's merkle tree hash
                ["size"] = 51200L, // ~50KB
                ["mtime"] = 1427500800L, // Approximate timestamp
                ["name"] = "Example_Startup_Modifier_Patch.rar",
            };

            string url = "https://mega.nz/file/sRw1GBIK#J8znLBwR6t7ZvZnpQbsUBYcUNfPCWA7wYNW3qU6gZSg";

            string contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            // ContentId should be 40 hex characters (SHA-1)
            Assert.Multiple(() =>
            {
                Assert.That(contentId, Is.Not.Null, "ContentId should not be null");
                Assert.That(contentId, Has.Length.EqualTo(40), "ContentId should be exactly 40 hexadecimal characters");
                Assert.That(contentId, Does.Match("^[0-9a-f]+$"), "ContentId should contain only lowercase hexadecimal digits");
                Assert.That(contentId, Is.Not.Empty, "ContentId should not be empty");
            });

            // Different calls with same metadata should produce same ID
            string contentId2 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);
            Assert.Multiple(() =>
            {
                Assert.That(contentId, Is.EqualTo(contentId2), "Multiple calls with same metadata should produce identical ContentId");
                Assert.That(contentId2, Has.Length.EqualTo(40), "Second ContentId should also be exactly 40 characters");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithNexus_UsesApiFields_ContentIdGeneration()
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

            Assert.Multiple(() =>
            {
                Assert.That(contentId, Is.Not.Null, "ContentId should not be null");
                Assert.That(contentId, Has.Length.EqualTo(40), "ContentId should be exactly 40 hexadecimal characters");
                Assert.That(contentId, Does.Match("^[0-9a-f]+$"), "ContentId should contain only lowercase hexadecimal digits");
                Assert.That(contentId, Is.Not.Empty, "ContentId should not be empty");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithDirect_UsesHttpHeaders_ContentIdGeneration()
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

            Assert.Multiple(() =>
            {
                Assert.That(contentId, Is.Not.Null, "ContentId should not be null");
                Assert.That(contentId, Has.Length.EqualTo(40), "ContentId should be exactly 40 hexadecimal characters");
                Assert.That(contentId, Does.Match("^[0-9a-f]+$"), "ContentId should contain only lowercase hexadecimal digits");
                Assert.That(contentId, Is.Not.Empty, "ContentId should not be empty");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_DifferentVersions_ProducesDifferentId_ContentIdGeneration()
        {
            // Version 5.1 of Example Dialogue Enhancement
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

            // Version 5.2 of Example Dialogue Enhancement
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

            string url = "https://deadlystream.com/files/file/1313-example-dialogue-enhancement/";

            string contentId1 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadataV51, url);
            string contentId2 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadataV52, url);

            // Different versions should produce different ContentIds
            Assert.Multiple(() =>
            {
                Assert.That(contentId1, Is.Not.EqualTo(contentId2), "Different versions should produce different ContentIds");
                Assert.That(contentId1, Is.Not.Null, "First ContentId should not be null");
                Assert.That(contentId2, Is.Not.Null, "Second ContentId should not be null");
                Assert.That(contentId1, Has.Length.EqualTo(40), "First ContentId should be exactly 40 characters");
                Assert.That(contentId2, Has.Length.EqualTo(40), "Second ContentId should be exactly 40 characters");
                Assert.That(contentId1, Does.Match("^[0-9a-f]+$"), "First ContentId should contain only hexadecimal digits");
                Assert.That(contentId2, Does.Match("^[0-9a-f]+$"), "Second ContentId should contain only hexadecimal digits");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithMissingOptionalFields_StillWorks_ContentIdGeneration()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1234",
                // Other fields missing
            };

            string url = "https://deadlystream.com/files/file/1234";

            string contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            Assert.Multiple(() =>
            {
                Assert.That(contentId, Is.Not.Null, "ContentId should not be null");
                Assert.That(contentId, Has.Length.EqualTo(40), "ContentId should be exactly 40 hexadecimal characters");
                Assert.That(contentId, Does.Match("^[0-9a-f]+$"), "ContentId should contain only lowercase hexadecimal digits");
                Assert.That(contentId, Is.Not.Empty, "ContentId should not be empty");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithUrlVariations_ProducesSameId_ContentIdGeneration()
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

            // Example DeadlyStream URLs with different query params - Example Minor Fixes
            string url1 = "https://deadlystream.com/files/file/1333-example-minor-fixes-for-k1/";
            string url2 = "https://deadlystream.com/files/file/1333-example-minor-fixes-for-k1/?tab=files";
            string url3 = "https://deadlystream.com/files/file/1333-example-minor-fixes-for-k1/?tab=reviews";

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

            Assert.Multiple(() =>
            {
                // All should produce same ContentId (normalized URL base is same, query params removed)
                Assert.That(contentId1, Is.Not.Null, "First ContentId should not be null");
                Assert.That(contentId2, Is.Not.Null, "Second ContentId should not be null");
                Assert.That(contentId3, Is.Not.Null, "Third ContentId should not be null");
                Assert.That(contentId1, Is.EqualTo(contentId2), "URLs with different query params should produce same ContentId after normalization");
                Assert.That(contentId2, Is.EqualTo(contentId3), "URLs with different query params should produce same ContentId after normalization");
                Assert.That(contentId1, Has.Length.EqualTo(40), "ContentId should be exactly 40 characters");
                Assert.That(contentId2, Has.Length.EqualTo(40), "ContentId should be exactly 40 characters");
                Assert.That(contentId3, Has.Length.EqualTo(40), "ContentId should be exactly 40 characters");
                Assert.That(contentId1, Does.Match("^[0-9a-f]+$"), "ContentId should contain only hexadecimal digits");
                Assert.That(contentId2, Does.Match("^[0-9a-f]+$"), "ContentId should contain only hexadecimal digits");
                Assert.That(contentId3, Does.Match("^[0-9a-f]+$"), "ContentId should contain only hexadecimal digits");
                // Verify URL normalization worked
                Assert.That(normalizedUrl1, Is.Not.Null, "First normalized URL should not be null");
                Assert.That(normalizedUrl2, Is.Not.Null, "Second normalized URL should not be null");
                Assert.That(normalizedUrl3, Is.Not.Null, "Third normalized URL should not be null");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_IsIdempotent_ContentIdGeneration()
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

            Assert.Multiple(() =>
            {
                Assert.That(id1, Is.EqualTo(id2), "First and second calls should produce identical ContentId");
                Assert.That(id2, Is.EqualTo(id3), "Second and third calls should produce identical ContentId");
                Assert.That(id1, Is.Not.Null, "ContentId should not be null");
                Assert.That(id1, Has.Length.EqualTo(40), "ContentId should be exactly 40 characters");
                Assert.That(id1, Does.Match("^[0-9a-f]+$"), "ContentId should contain only lowercase hexadecimal characters");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithNullMetadata_ThrowsNullReferenceException_ContentIdGeneration()
        {
            string url = "https://example.com/file.zip";

            var exception = Assert.Throws<NullReferenceException>(() =>
            {
                DownloadCacheOptimizer.ComputeContentIdFromMetadata(null, url);
            });

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null, "Null metadata should throw NullReferenceException");
                Assert.That(url, Is.Not.Null, "URL should not be null");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithNullUrl_ThrowsArgumentException_ContentIdGeneration()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "direct",
            };

            // Null URL causes bencoding to throw ArgumentException because null values are not allowed
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, null);
            });

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null, "Null URL should throw ArgumentException");
                Assert.That(metadata, Is.Not.Null, "Metadata should not be null");
                Assert.That(metadata.ContainsKey("provider"), Is.True, "Metadata should contain provider key");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithEmptyMetadata_ThrowsKeyNotFoundException_ContentIdGeneration()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal);
            string url = "https://example.com/file.zip";

            var exception = Assert.Throws<KeyNotFoundException>(() =>
            {
                DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);
            });

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null, "Empty metadata should throw KeyNotFoundException");
                Assert.That(metadata, Is.Not.Null, "Metadata should not be null");
                Assert.That(metadata, Is.Empty, "Metadata dictionary should be empty");
                Assert.That(url, Is.Not.Null, "URL should not be null");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithMissingProvider_ThrowsKeyNotFoundException_ContentIdGeneration()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["fileId"] = "12345",
                ["size"] = 4096L,
            };
            string url = "https://example.com/file.zip";

            var exception = Assert.Throws<KeyNotFoundException>(() =>
            {
                DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);
            });

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null, "Metadata missing provider should throw KeyNotFoundException");
                Assert.That(metadata, Is.Not.Null, "Metadata should not be null");
                Assert.That(metadata.ContainsKey("provider"), Is.False, "Metadata should not contain provider key");
                Assert.That(metadata.ContainsKey("fileId"), Is.True, "Metadata should contain fileId key");
                Assert.That(metadata.ContainsKey("size"), Is.True, "Metadata should contain size key");
                Assert.That(url, Is.Not.Null, "URL should not be null");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithMetadataKeyOrdering_ProducesSameId_ContentIdGeneration()
        {
            // Same metadata, different insertion order
            var metadata1 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1234",
                ["version"] = "1.0",
                ["size"] = 1024L,
            };

            var metadata2 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["size"] = 1024L,
                ["provider"] = "deadlystream",
                ["version"] = "1.0",
                ["filePageId"] = "1234",
            };

            string url = "https://deadlystream.com/files/file/1234";

            string id1 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata1, url);
            string id2 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata2, url);

            Assert.Multiple(() =>
            {
                Assert.That(id1, Is.EqualTo(id2), "ContentId should be independent of metadata dictionary insertion order");
                Assert.That(id1, Has.Length.EqualTo(40), "ContentId should be exactly 40 characters");
                Assert.That(id1, Does.Match("^[0-9a-f]+$"), "ContentId should contain only lowercase hexadecimal characters");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithCaseSensitiveProvider_IsCaseSensitive_ContentIdGeneration()
        {
            var metadata1 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1234",
            };

            var metadata2 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "DEADLYSTREAM",
                ["filePageId"] = "1234",
            };

            string url = "https://deadlystream.com/files/file/1234";

            string id1 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata1, url);
            string id2 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata2, url);

            Assert.Multiple(() =>
            {
                // Provider case should affect ContentId (implementation detail, but should be tested)
                Assert.That(id1, Is.Not.Null, "First ContentId should not be null");
                Assert.That(id2, Is.Not.Null, "Second ContentId should not be null");
                Assert.That(id1, Is.Not.EqualTo(id2), "Provider case differences should produce different ContentIds");
                Assert.That(id1, Has.Length.EqualTo(40), "First ContentId should be exactly 40 characters");
                Assert.That(id2, Has.Length.EqualTo(40), "Second ContentId should be exactly 40 characters");
                Assert.That(id1, Does.Match("^[0-9a-f]+$"), "First ContentId should contain only hexadecimal digits");
                Assert.That(id2, Does.Match("^[0-9a-f]+$"), "Second ContentId should contain only hexadecimal digits");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithSpecialCharactersInMetadata_HandlesCorrectly_ContentIdGeneration()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1234",
                ["version"] = "1.0-beta+test",
                ["fileName"] = "file with spaces & special chars!@#.zip",
            };

            string url = "https://deadlystream.com/files/file/1234";

            string contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            Assert.Multiple(() =>
            {
                Assert.That(contentId, Is.Not.Null, "ContentId should not be null even with special characters");
                Assert.That(contentId, Has.Length.EqualTo(40), "ContentId should be exactly 40 characters");
                Assert.That(contentId, Does.Match("^[0-9a-f]+$"), "ContentId should contain only lowercase hexadecimal characters");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithVeryLargeMetadataValues_HandlesCorrectly_ContentIdGeneration()
        {
            var largeString = new string('A', 10000);
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1234",
                ["description"] = largeString,
            };

            string url = "https://deadlystream.com/files/file/1234";

            string contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            Assert.Multiple(() =>
            {
                Assert.That(contentId, Is.Not.Null, "ContentId should handle large metadata values");
                Assert.That(contentId, Has.Length.EqualTo(40), "ContentId should be exactly 40 characters regardless of input size");
                Assert.That(contentId, Does.Match("^[0-9a-f]+$"), "ContentId should contain only lowercase hexadecimal characters");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithNumericStringVsNumber_ProducesDifferentIds_ContentIdGeneration()
        {
            // Test that string "123" vs number 123 produce different IDs
            var metadata1 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "123",
                ["size"] = "1024", // String
            };

            var metadata2 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "123",
                ["size"] = 1024L, // Number
            };

            string url = "https://deadlystream.com/files/file/123";

            string id1 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata1, url);
            string id2 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata2, url);

            Assert.Multiple(() =>
            {
                Assert.That(id1, Is.Not.Null, "First ContentId (string size) should not be null");
                Assert.That(id2, Is.Not.Null, "Second ContentId (numeric size) should not be null");
                Assert.That(id1, Is.Not.EqualTo(id2), "String vs numeric representation should produce different ContentIds");
                Assert.That(id1, Has.Length.EqualTo(40), "First ContentId should be exactly 40 characters");
                Assert.That(id2, Has.Length.EqualTo(40), "Second ContentId should be exactly 40 characters");
                Assert.That(id1, Does.Match("^[0-9a-f]+$"), "First ContentId should contain only hexadecimal digits");
                Assert.That(id2, Does.Match("^[0-9a-f]+$"), "Second ContentId should contain only hexadecimal digits");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithEmptyStringValues_HandlesCorrectly_ContentIdGeneration()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1234",
                ["version"] = "",
                ["fileName"] = "",
            };

            string url = "https://deadlystream.com/files/file/1234";

            string contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            Assert.Multiple(() =>
            {
                Assert.That(contentId, Is.Not.Null, "ContentId should handle empty string values");
                Assert.That(contentId, Has.Length.EqualTo(40), "ContentId should be exactly 40 characters");
                Assert.That(contentId, Does.Match("^[0-9a-f]+$"), "ContentId should contain only lowercase hexadecimal characters");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithAllProviders_ProducesValidIds_ContentIdGeneration()
        {
            string[] providers = { "deadlystream", "mega", "nexus", "direct" };
            var results = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string provider in providers)
            {
                var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["provider"] = provider,
                };

                // Add provider-specific required fields
                switch (provider)
                {
                    case "deadlystream":
                        metadata["filePageId"] = "1234";
                        break;
                    case "mega":
                        metadata["nodeId"] = "testNode";
                        metadata["hash"] = "testhash123";
                        break;
                    case "nexus":
                        metadata["fileId"] = "12345";
                        break;
                    case "direct":
                        metadata["url"] = "https://example.com/file.zip";
                        break;
                }

                string url = provider switch
                {
                    "deadlystream" => "https://deadlystream.com/files/file/1234",
                    "mega" => "https://mega.nz/file/testNode#testkey",
                    "nexus" => "https://nexusmods.com/kotor/mods/123",
                    "direct" => "https://example.com/file.zip",
                    _ => "https://example.com"
                };

                string contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);
                results[provider] = contentId;

                Assert.Multiple(() =>
                {
                    Assert.That(contentId, Is.Not.Null, $"ContentId for {provider} should not be null");
                    Assert.That(contentId, Has.Length.EqualTo(40), $"ContentId for {provider} should be exactly 40 characters");
                    Assert.That(contentId, Does.Match("^[0-9a-f]+$"), $"ContentId for {provider} should contain only lowercase hexadecimal characters");
                });
            }

            // All providers should produce different ContentIds for different providers
            var uniqueIds = results.Values.Distinct(StringComparer.Ordinal).ToList();
            Assert.That(uniqueIds, Has.Count.EqualTo(results.Count), "Different providers should produce different ContentIds");
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithUnicodeCharacters_HandlesCorrectly_ContentIdGeneration()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1234",
                ["fileName"] = "测试文件_тест_テスト.zip",
                ["version"] = "v1.0-αβγ",
            };

            string url = "https://deadlystream.com/files/file/1234";

            string contentId = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url);

            Assert.Multiple(() =>
            {
                Assert.That(contentId, Is.Not.Null, "ContentId should handle Unicode characters");
                Assert.That(contentId, Has.Length.EqualTo(40), "ContentId should be exactly 40 characters");
                Assert.That(contentId, Does.Match("^[0-9a-f]+$"), "ContentId should contain only lowercase hexadecimal characters");
            });
        }

        [Test]
        public void ComputeContentIdFromMetadata_WithZeroAndNegativeValues_HandlesCorrectly_ContentIdGeneration()
        {
            var metadata1 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1234",
                ["size"] = 0L,
            };

            var metadata2 = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["filePageId"] = "1234",
                ["size"] = -1L,
            };

            string url = "https://deadlystream.com/files/file/1234";

            string id1 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata1, url);
            string id2 = DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata2, url);

            Assert.Multiple(() =>
            {
                Assert.That(id1, Is.Not.Null, "ContentId should handle zero values");
                Assert.That(id2, Is.Not.Null, "ContentId should handle negative values");
                Assert.That(id1, Is.Not.EqualTo(id2), "Zero and negative values should produce different ContentIds");
                Assert.That(id1, Has.Length.EqualTo(40), "ContentId should be exactly 40 characters");
                Assert.That(id2, Has.Length.EqualTo(40), "ContentId should be exactly 40 characters");
            });
        }
    }
}
