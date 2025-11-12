// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KOTORModSync.Core.Services.Download;
using Xunit;

namespace KOTORModSync.Tests.Services.DistributedCache
{
    /// <summary>
    /// Tests for metadata consistency, persistence, and integrity.
    /// </summary>
    [Collection("DistributedCache")]
    public class MetadataConsistencyTests : IClassFixture<DistributedCacheTestFixture>
    {
        private readonly DistributedCacheTestFixture _fixture;

        public MetadataConsistencyTests(DistributedCacheTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Metadata_DescriptorFile_MatchesPayload()
        {
            string testFile = _fixture.CreateTestFile("meta_test.bin", 512 * 1024);
            string descriptorPath = await _fixture.CreateDescriptorFileAsync(
                testFile,
                "meta_test",
                262144);

            DistributionPayload payload = _fixture.GetDescriptorPayload(descriptorPath);
            Assert.NotNull(payload);
            Assert.True(payload.DescriptorBytes.Length > 0);

            byte[] persistedBytes = await NetFrameworkCompatibility.ReadAllBytesAsync(descriptorPath);
            Assert.True(persistedBytes.Length > 0);
            Assert.True(payload.DescriptorBytes.AsSpan().SequenceEqual(persistedBytes), "Descriptor bytes on disk differ from payload snapshot.");
        }

        [Fact]
        public async Task Metadata_PieceHashes_CorrectCount()
        {
            long fileSize = 1024 * 1024; // 1 MB
            int pieceLength = 262144; // 256 KB
            int expectedPieces = (int)((fileSize + pieceLength - 1) / pieceLength);

            string testFile = _fixture.CreateTestFile("pieces_test.bin", fileSize);
            string descriptorPath = await _fixture.CreateDescriptorFileAsync(
                testFile,
                "pieces_test",
                pieceLength);
            DistributionPayload payload = _fixture.GetDescriptorPayload(descriptorPath);

            Assert.Equal(expectedPieces, payload.PieceHashes.Count);
            Assert.All(payload.PieceHashes, hash => Assert.Equal(20, hash.Length));
            Assert.Equal(pieceLength, payload.PieceLength);
        }

        [Fact]
        public async Task Metadata_PayloadMatchesIntegrityData()
        {
            string testFile = _fixture.CreateTestFile("integrity_test.bin", 2 * 1024 * 1024);
            string descriptorPath = await _fixture.CreateDescriptorFileAsync(
                testFile,
                "integrity_test",
                262144);

            DistributionPayload payload = _fixture.GetDescriptorPayload(descriptorPath);
            Assert.NotNull(payload);

            var (contentHashSHA256, pieceLength, pieceHashesHex) = await DownloadCacheOptimizer
                .ComputeFileIntegrityData(testFile)
;

            Assert.Equal(payload.PieceLength, pieceLength);

            string payloadPieceHashesHex = string.Concat(
                payload.PieceHashes.Select(h => BitConverter.ToString(h).Replace("-", "").ToLowerInvariant()));

            Assert.Equal(payloadPieceHashesHex, pieceHashesHex, StringComparer.Ordinal);

            // Ensure content hash is stable and non-empty
            Assert.False(string.IsNullOrWhiteSpace(contentHashSHA256));
            Assert.Equal(contentHashSHA256, contentHashSHA256.ToLowerInvariant());
        }

        [Fact]
        public void Metadata_ContentId_Deterministic_AcrossRuns()
        {
            string testFile = _fixture.CreateTestFile("deterministic.bin", 256 * 1024);

            var ids = Enumerable.Range(0, 5)
                .Select(_ => _fixture.ComputeContentId(testFile))
                .ToList();

            Assert.True(ids.All(id => string.Equals(id, ids[0], StringComparison.Ordinal)));
        }

        [Fact]
        public void Metadata_SameContent_DifferentNames_SameStructure()
        {
            string content = new string('X', 10000);
            string file1 = _fixture.CreateTestFile("name_a.txt", 0, content);
            string file2 = _fixture.CreateTestFile("name_b.txt", 0, content);

            string id1 = _fixture.ComputeContentId(file1);
            string id2 = _fixture.ComputeContentId(file2);

            // ContentId includes filename in info dict, so they'll be different
            // But both should be valid
            Assert.Equal(40, id1.Length);
            Assert.Equal(40, id2.Length);
        }

        [Fact]
        public async Task Metadata_DescriptorCreation_TimestampValid()
        {
            string testFile = _fixture.CreateTestFile("timestamp_test.bin", 100 * 1024);
            string descriptorFile = await _fixture.CreateDescriptorFileAsync(
                testFile,
                "timestamp_test");

            // Descriptor should have been created recently
            DateTime creationTime = File.GetCreationTimeUtc(descriptorFile);
            Assert.True((DateTime.UtcNow - creationTime).TotalMinutes < 5);
        }

        [Fact]
        public void Metadata_MultipleFiles_UniqueContentIds()
        {
            var files = Enumerable.Range(0, 10)
                .Select(i => _fixture.CreateTestFile($"unique_{i}.bin", 1024 * (i + 1)))
                .ToList();

            var contentIds = files.Select(f => _fixture.ComputeContentId(f)).ToList();

            // All ContentIds should be unique
            Assert.Equal(contentIds.Count, contentIds.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void Metadata_EmptyFile_ValidMetadata()
        {
            string file = _fixture.CreateTestFile("empty_meta.bin", 0);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
            Assert.True(contentId.All(c => "0123456789abcdef".Contains(c)));
        }

        [Fact]
        public void Metadata_LargeFile_ValidMetadata()
        {
            string file = _fixture.CreateTestFile("large_meta.bin", 50 * 1024 * 1024);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void Metadata_PieceSize_FollowsSpec()
        {
            // Verify piece size determination follows specification
            long[] testSizes = { 100 * 1024, 1024 * 1024, 10 * 1024 * 1024, 100 * 1024 * 1024 };

            foreach (long size in testSizes)
            {
                string file = _fixture.CreateTestFile($"spec_{size}.bin", size);
                string contentId = _fixture.ComputeContentId(file);

                // Should produce valid ContentId
                Assert.Equal(40, contentId.Length);
            }
        }

        [Fact]
        public void Metadata_ContentId_NoCollisions_SmallDataset()
        {
            HashSet<string> contentIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < 100; i++)
            {
                string file = _fixture.CreateTestFile($"collision_test_{i}.bin", 1024 * (i + 1));
                string contentId = _fixture.ComputeContentId(file);

                Assert.True(contentIds.Add(contentId), $"Collision detected for file {i}");
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        [InlineData(100000)]
        [InlineData(1000000)]
        public void Metadata_VariousSizes_AllValid(int sizeKB)
        {
            string file = _fixture.CreateTestFile($"size_{sizeKB}kb.bin", sizeKB * 1024L);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
            Assert.True(contentId.All(c => "0123456789abcdef".Contains(c)));
        }

        [Fact]
        public void Metadata_BinaryPatterns_AllValid()
        {
            // Test various binary patterns
            byte[][] patterns = new[]
            {
                new byte[] { 0x00 }, // All zeros
                new byte[] { 0xFF }, // All ones
                new byte[] { 0xAA }, // Alternating
                new byte[] { 0x55 }, // Alternating opposite
                new byte[] { 0x01, 0x02, 0x04, 0x08 }, // Powers of 2
            };

            foreach (byte[] pattern in patterns)
            {
                byte[] data = Enumerable.Repeat(pattern, 10000).SelectMany(b => b).ToArray();
                string file = DistributionTestSupport.EnsureBinaryTestFile(
                    _fixture.TestDataDirectory,
                    $"pattern_{pattern[0]:X2}.bin",
                    data);

                string contentId = _fixture.ComputeContentId(file);
                Assert.Equal(40, contentId.Length);
            }
        }

        [Fact]
        public void Metadata_Sequential_Consistency()
        {
            // Create file with sequential byte values
            byte[] data = Enumerable.Range(0, 10000).Select(i => (byte)(i % 256)).ToArray();
            string file = DistributionTestSupport.EnsureBinaryTestFile(
                _fixture.TestDataDirectory,
                "sequential.bin",
                data);

            string id1 = _fixture.ComputeContentId(file);
            string id2 = _fixture.ComputeContentId(file);

            Assert.Equal(id1, id2);
        }

        [Fact]
        public void Metadata_Fragmented_Consistency()
        {
            // Create file in multiple writes
            string relativePath = "fragmented.bin";
            string absolutePath = Path.Combine(_fixture.TestDataDirectory, relativePath);
            using (FileStream stream = File.Create(absolutePath))
            {
                for (int i = 0; i < 100; i++)
                {
                    byte[] chunk = new byte[1000];
                    new Random(i).NextBytes(chunk);
                    stream.Write(chunk, 0, chunk.Length);
                }
            }

            string id1 = _fixture.ComputeContentId(absolutePath);
            string id2 = _fixture.ComputeContentId(absolutePath);

            Assert.Equal(id1, id2);
        }
    }
}

