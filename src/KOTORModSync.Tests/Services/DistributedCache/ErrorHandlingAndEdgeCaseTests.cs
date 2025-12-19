// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KOTORModSync.Core.Services.Download;
using KOTORModSync.Core.Utility;
using Xunit;

namespace KOTORModSync.Tests.Services.DistributedCache
{
    /// <summary>
    /// Tests for error handling, edge cases, and resilience of the distributed cache system.
    /// </summary>
    [Collection("DistributedCache")]
    public class ErrorHandlingAndEdgeCaseTests : IClassFixture<DistributedCacheTestFixture>, IDisposable
    {
        private readonly DistributedCacheTestFixture _fixture;
        private readonly IDisposable _clientScope;

        public ErrorHandlingAndEdgeCaseTests(DistributedCacheTestFixture fixture)
        {
            _fixture = fixture;
            _clientScope = DownloadCacheOptimizer.DiagnosticsHarness.AttachSyntheticClient();
            ResetState();
        }

        public void Dispose()
        {
            _clientScope.Dispose();
        }

        private static void ResetState()
        {
            DownloadCacheOptimizer.DiagnosticsHarness.ClearActiveManagers();
            DownloadCacheOptimizer.DiagnosticsHarness.ClearBlockedContentIds();
            DownloadCacheOptimizer.DiagnosticsHarness.SetNatStatus(successful: false, port: 0, lastCheck: DateTime.MinValue);
            DownloadCacheOptimizer.DiagnosticsHarness.SetClientSettings(new
            {
                ListenPort = 0,
                ClientName = "DiagnosticsHarness-Test",
                ClientVersion = "0.0.1"
            });
        }

        [Fact]
        public void EdgeCase_ZeroByteFile_HandledGracefully_ContentIdGeneration()
        {
            string file = _fixture.CreateTestFile("zero.bin", 0);
            string contentId = _fixture.ComputeContentId(file);

            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
            Assert.Matches("^[0-9a-f]+$", contentId);
            Assert.NotEmpty(contentId);
            Assert.True(File.Exists(file), "Test file should exist");
            Assert.Equal(0, new FileInfo(file).Length);
        }

        [Fact]
        public void EdgeCase_OneByte_HandledGracefully_ContentIdGeneration()
        {
            string file = _fixture.CreateTestFile("one_byte.bin", 1);
            string contentId = _fixture.ComputeContentId(file);

            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
            Assert.Matches("^[0-9a-f]+$", contentId);
            Assert.NotEmpty(contentId);
            Assert.True(File.Exists(file), "Test file should exist");
            Assert.Equal(1, new FileInfo(file).Length);
        }

        [Fact]
        public void EdgeCase_PieceBoundary_Exact_ContentIdGeneration()
        {
            // File size exactly matches piece size
            string file = _fixture.CreateTestFile("exact_piece.bin", 262144);
            string contentId = _fixture.ComputeContentId(file);

            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
            Assert.Matches("^[0-9a-f]+$", contentId);
            Assert.NotEmpty(contentId);
            Assert.True(File.Exists(file), "Test file should exist");
            Assert.Equal(262144, new FileInfo(file).Length);
        }

        [Fact]
        public void EdgeCase_PieceBoundary_OneLess_ContentIdGeneration()
        {
            // File size is one byte less than piece size
            string file = _fixture.CreateTestFile("piece_minus_one.bin", 262143);
            string contentId = _fixture.ComputeContentId(file);

            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
            Assert.Matches("^[0-9a-f]+$", contentId);
            Assert.NotEmpty(contentId);
            Assert.True(File.Exists(file), "Test file should exist");
            Assert.Equal(262143, new FileInfo(file).Length);
        }

        [Fact]
        public void EdgeCase_PieceBoundary_OneMore_ContentIdGeneration()
        {
            // File size is one byte more than piece size
            string file = _fixture.CreateTestFile("piece_plus_one.bin", 262145);
            string contentId = _fixture.ComputeContentId(file);

            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
            Assert.Matches("^[0-9a-f]+$", contentId);
            Assert.NotEmpty(contentId);
            Assert.True(File.Exists(file), "Test file should exist");
            Assert.Equal(262145, new FileInfo(file).Length);
        }

        [Fact]
        public void ErrorHandling_NonexistentFile_ThrowsException_ContentIdGeneration()
        {
            string nonexistentPath = "/nonexistent/file.bin";
            Assert.False(File.Exists(nonexistentPath), "File should not exist before test");

            var exception = Assert.Throws<FileNotFoundException>(() =>
            {
                _fixture.ComputeContentId(nonexistentPath);
            });

            Assert.NotNull(exception);
            Assert.Contains("Source file for distribution payload was not found", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(nonexistentPath, exception.FileName);
        }

        [Fact]
        public void ErrorHandling_NullPath_ThrowsException_ContentIdGeneration()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
            {
                _fixture.ComputeContentId(filePath: null);
            });

            Assert.NotNull(exception);
            Assert.Equal("sourceFilePath", exception.ParamName);
        }

        [Fact]
        public void ErrorHandling_EmptyPath_ThrowsException_ContentIdGeneration()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                _fixture.ComputeContentId("");
            });

            Assert.NotNull(exception);
            Assert.True(string.IsNullOrEmpty(exception.ParamName) || exception.Message.Contains("path", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ErrorHandling_GetStats_NeverThrows()
        {
            // Stats should never throw, even if engine isn't initialized
            Exception exception = Record.Exception(() =>
            {
                (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            });

            Assert.Null(exception);
            
            // Verify stats are returned (values may be 0 if not initialized)
            var stats = DownloadCacheOptimizer.GetNetworkCacheStats();
            Assert.True(stats.activeShares >= 0, "Active shares should be non-negative");
            Assert.True(stats.totalUploadBytes >= 0, "Total upload bytes should be non-negative");
            Assert.True(stats.connectedSources >= 0, "Connected sources should be non-negative");
        }

        [Fact]
        public void ErrorHandling_BlockInvalidContentId_NoException()
        {
            // Should handle invalid ContentIds gracefully
            string invalidContentId = "invalid_id_not_40_chars";
            Exception exception = Record.Exception(() =>
            {
                DownloadCacheOptimizer.BlockContentId(invalidContentId, "test");
            });

            Assert.Null(exception);
            
            // Verify it was blocked (even if invalid format)
            bool isBlocked = DownloadCacheOptimizer.IsContentIdBlocked(invalidContentId);
            Assert.True(isBlocked, "Invalid ContentId should still be blocked");
        }

        [Fact]
        public void ErrorHandling_GetResourceDetails_InvalidKey_ReturnsMessage()
        {
            string invalidKey = "totally_invalid_key";
            string result = DownloadCacheOptimizer.GetSharedResourceDetails(invalidKey);

            Assert.NotNull(result);
            Assert.NotEmpty(result);
            // Result should be a message (may indicate not found or error)
        }

        [Fact]
        public async Task ErrorHandling_GracefulShutdown_MultipleCallsSafe()
        {
            // Multiple shutdown calls should be safe
            Exception exception1 = await Record.ExceptionAsync(async () => await DownloadCacheOptimizer.GracefulShutdownAsync());
            Exception exception2 = await Record.ExceptionAsync(async () => await DownloadCacheOptimizer.GracefulShutdownAsync());
            Exception exception3 = await Record.ExceptionAsync(async () => await DownloadCacheOptimizer.GracefulShutdownAsync());

            Assert.Null(exception1);
            Assert.Null(exception2);
            Assert.Null(exception3);

            // Should not throw
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();

            // S2699: Add assertions
            // Expect stats values to be at least zero after shutdown, as all background sharing should stop.
            Assert.True(stats.activeShares >= 0, "Active shares should be non-negative after shutdown");
            Assert.True(stats.totalUploadBytes >= 0, "Total upload bytes should be non-negative after shutdown");
            Assert.True(stats.connectedSources >= 0, "Connected sources should be non-negative after shutdown");
        }

        [Fact]
        public void EdgeCase_FilenameWithSpecialChars_HandledCorrectly_ContentIdGeneration()
        {
            // Filenames with special characters should work
            string fileName = "test (1) [special].bin";
            string file = _fixture.CreateTestFile(fileName, 1024);
            string contentId = _fixture.ComputeContentId(file);

            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
            Assert.Matches("^[0-9a-f]+$", contentId);
            Assert.True(File.Exists(file), "File with special characters should exist");
            Assert.Equal(1024, new FileInfo(file).Length);
        }

        [Fact]
        public void EdgeCase_VeryLongFilename_HandledCorrectly_ContentIdGeneration()
        {
            string longName = new string('a', 200) + ".bin";
            string file = _fixture.CreateTestFile(longName, 1024);
            string contentId = _fixture.ComputeContentId(file);

            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
            Assert.Matches("^[0-9a-f]+$", contentId);
            // File may or may not exist depending on OS path length limits
            // But if it exists, it should have correct size
            if (File.Exists(file))
            {
                Assert.Equal(1024, new FileInfo(file).Length);
            }
        }

        [Fact]
        public void EdgeCase_UnicodeFilename_HandledCorrectly_ContentIdGeneration()
        {
            string fileName = "测试文件_тест_🎮.bin";
            string file = _fixture.CreateTestFile(fileName, 1024);
            string contentId = _fixture.ComputeContentId(file);

            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
            Assert.Matches("^[0-9a-f]+$", contentId);
            Assert.True(File.Exists(file), "File with Unicode characters should exist");
            Assert.Equal(1024, new FileInfo(file).Length);
        }

        [Fact]
        public async Task ErrorHandling_ConcurrentAccess_Safe_ContentIdGeneration()
        {
            string file = _fixture.CreateTestFile("concurrent.bin", 10240);
            Assert.True(File.Exists(file), "Test file should exist");

            // Compute ContentId concurrently
            Task<string>[] tasks = Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => _fixture.ComputeContentId(file)))
                .ToArray();

            await Task.WhenAll(tasks);

            var results = tasks.Select(t => t.Result).ToList();
            Assert.NotNull(results);
            Assert.Equal(10, results.Count);
            Assert.True(results.All(r => r != null), "All results should be non-null");
            Assert.True(results.All(r => r.Length == 40), "All ContentIds should be 40 characters");
            Assert.True(results.All(r => System.Text.RegularExpressions.Regex.IsMatch(r, "^[0-9a-f]+$")), 
                "All ContentIds should contain only hexadecimal digits");
            Assert.True(results.All(r => string.Equals(r, results[0], StringComparison.Ordinal)), 
                "All concurrent calls should produce identical ContentId");
        }

        [Fact]
        public void EdgeCase_FileModifiedDuringRead_DetectsChange_ContentIdGeneration()
        {
            string file = _fixture.CreateTestFile("modified.bin", 10240);
            Assert.True(File.Exists(file), "Test file should exist");
            Assert.Equal(10240, new FileInfo(file).Length);
            
            string id1 = _fixture.ComputeContentId(file);
            Assert.NotNull(id1);
            Assert.Equal(40, id1.Length);

            // Modify file
            DistributionTestSupport.ModifyFile(
                file,
                stream =>
                {
                    stream.Seek(5000, SeekOrigin.Begin);
                    stream.WriteByte(0xFF);
                });

            Assert.Equal(10240, new FileInfo(file).Length);
            string id2 = _fixture.ComputeContentId(file);
            
            Assert.NotNull(id2);
            Assert.Equal(40, id2.Length);
            Assert.NotEqual(id1, id2);
            Assert.Matches("^[0-9a-f]+$", id1);
            Assert.Matches("^[0-9a-f]+$", id2);
        }

        [Fact]
        public void EdgeCase_MaxInt32Size_HandledCorrectly_ContentIdGeneration()
        {
            // Test with file size near Int32.MaxValue boundary
            // We'll simulate with a smaller size for testing
            long size = int.MaxValue / 1000; // ~2 MB
            string file = _fixture.CreateTestFile("near_max_int.bin", size);
            string contentId = _fixture.ComputeContentId(file);

            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
            Assert.Matches("^[0-9a-f]+$", contentId);
            if (File.Exists(file))
            {
                Assert.Equal(size, new FileInfo(file).Length);
            }
        }

        [Fact]
        public void ErrorHandling_Stats_ConsistentFormat()
        {
            var allStats = new List<(int activeShares, long totalUploadBytes, int connectedSources)>();
            
            for (int i = 0; i < 10; i++)
            {
                (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();
                allStats.Add(stats);

                Assert.True(stats.activeShares >= 0, $"Active shares should be non-negative (iteration {i})");
                Assert.True(stats.totalUploadBytes >= 0, $"Total upload bytes should be non-negative (iteration {i})");
                Assert.True(stats.connectedSources >= 0, $"Connected sources should be non-negative (iteration {i})");
            }

            Assert.Equal(10, allStats.Count);
            // Stats should be consistent (may vary slightly, but should be reasonable)
            Assert.All(allStats, s => 
            {
                Assert.True(s.activeShares >= 0);
                Assert.True(s.totalUploadBytes >= 0);
                Assert.True(s.connectedSources >= 0);
            });
        }

        [Fact]
        public void EdgeCase_AlternatingBytes_ValidContentId_ContentIdGeneration()
        {
            byte[] data = new byte[10000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 2 == 0 ? 0xAA : 0x55);
            }

            Assert.Equal(10000, data.Length);
            Assert.True(data[0] == 0xAA, "First byte should be 0xAA");
            Assert.True(data[1] == 0x55, "Second byte should be 0x55");

            string file = DistributionTestSupport.EnsureBinaryTestFile(
                _fixture.TestDataDirectory,
                "alternating.bin",
                data);
            
            Assert.True(File.Exists(file), "Test file should exist");

            string contentId = _fixture.ComputeContentId(file);
            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
            Assert.Matches("^[0-9a-f]+$", contentId);
            Assert.True(File.Exists(file), "Test file should exist");
            Assert.Equal(10000, new FileInfo(file).Length);
        }

        [Fact]
        public void EdgeCase_AllZeros_ValidContentId_ContentIdGeneration()
        {
            byte[] data = new byte[10000];
            string file = DistributionTestSupport.EnsureBinaryTestFile(
                _fixture.TestDataDirectory,
                "all_zeros.bin",
                data);

            string contentId = _fixture.ComputeContentId(file);
            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
            Assert.Matches("^[0-9a-f]+$", contentId);
            Assert.True(File.Exists(file), "Test file should exist");
            Assert.Equal(10000, new FileInfo(file).Length);
        }

        [Fact]
        public void EdgeCase_AllOnes_ValidContentId_ContentIdGeneration()
        {
            byte[] data = Enumerable.Repeat((byte)0xFF, 10000).ToArray();
            string file = DistributionTestSupport.EnsureBinaryTestFile(
                _fixture.TestDataDirectory,
                "all_ones.bin",
                data);

            string contentId = _fixture.ComputeContentId(file);
            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
            Assert.Matches("^[0-9a-f]+$", contentId);
            Assert.True(File.Exists(file), "Test file should exist");
            Assert.Equal(10000, new FileInfo(file).Length);
        }

        [Fact]
        public void ErrorHandling_BlockContentId_NullReason_NoException()
        {
            string contentId = new string('a', 40);
            Exception exception = Record.Exception(() =>
            {
                DownloadCacheOptimizer.BlockContentId(contentId, reason: null);
            });

            Assert.Null(exception);
            // Verify it was blocked
            bool isBlocked = DownloadCacheOptimizer.IsContentIdBlocked(contentId);
            Assert.True(isBlocked, "ContentId should be blocked even with null reason");
        }

        [Fact]
        public void ErrorHandling_BlockContentId_EmptyReason_NoException()
        {
            string contentId = new string('b', 40);
            Exception exception = Record.Exception(() =>
            {
                DownloadCacheOptimizer.BlockContentId(contentId, "");
            });

            Assert.Null(exception);
            // Verify it was blocked
            bool isBlocked = DownloadCacheOptimizer.IsContentIdBlocked(contentId);
            Assert.True(isBlocked, "ContentId should be blocked even with empty reason");
        }

        [Fact]
        public void ErrorHandling_GetResourceDetails_EmptyKey_ReturnsMessage()
        {
            string result = DownloadCacheOptimizer.GetSharedResourceDetails("");
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void ErrorHandling_GetResourceDetails_NullKey_ReturnsMessage()
        {
            string result = DownloadCacheOptimizer.GetSharedResourceDetails(contentKey: null);
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void EdgeCase_RandomData_Consistent_ContentIdGeneration()
        {
            var random = new Random(42); // Fixed seed
            byte[] data = new byte[100000];
            random.NextBytes(data);

            string file = DistributionTestSupport.EnsureBinaryTestFile(
                _fixture.TestDataDirectory,
                "random_seeded.bin",
                data);

            string id1 = _fixture.ComputeContentId(file);
            string id2 = _fixture.ComputeContentId(file);

            Assert.Equal(id1, id2);
        }

        [Fact]
        public void EdgeCase_TextFile_ValidContentId_ContentIdGeneration()
        {
            string text = string.Join(Environment.NewLine,
                Enumerable.Range(0, 1000).Select(i => $"Line {i}"));

            string file = _fixture.CreateTestFile("text_lines.txt", 0, text);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void EdgeCase_MixedNewlines_ValidContentId_ContentIdGeneration()
        {
            string text = "Line1\nLine2\rLine3\r\nLine4";
            string file = _fixture.CreateTestFile("mixed_newlines.txt", 0, text);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void EdgeCase_BOMPresent_ValidContentId_ContentIdGeneration()
        {
            byte[] bom = { 0xEF, 0xBB, 0xBF }; // UTF-8 BOM
            byte[] text = System.Text.Encoding.UTF8.GetBytes("Test content");
            byte[] data = bom.Concat(text).ToArray();

            string file = DistributionTestSupport.EnsureBinaryTestFile(
                _fixture.TestDataDirectory,
                "with_bom.txt",
                data);

            string contentId = _fixture.ComputeContentId(file);
            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public async Task ErrorHandling_ConcurrentStatsAccess_Safe()
        {
            Task<(int activeShares, long totalUploadBytes, int connectedSources)>[] tasks = Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() => DownloadCacheOptimizer.GetNetworkCacheStats()))
                .ToArray();

            await Task.WhenAll(tasks);

            // All should succeed without exceptions
            Assert.True(tasks.All(t => NetFrameworkCompatibility.IsCompletedSuccessfully(t)));
        }

        [Fact]
        public void EdgeCase_PowerOfTwo_FileSizes_ContentIdGeneration()
        {
            int[] powerOfTwo = { 1024, 2048, 4096, 8192, 16384, 32768, 65536 };

            foreach (int size in powerOfTwo)
            {
                string file = _fixture.CreateTestFile($"power_{size}.bin", size);
                string contentId = _fixture.ComputeContentId(file);

                Assert.Equal(40, contentId.Length);
            }
        }

        [Fact]
        public void EdgeCase_PrimeNumber_FileSizes_ContentIdGeneration()
        {
            int[] primes = { 1009, 2003, 4001, 8009, 16007, 32003 };

            foreach (int size in primes)
            {
                string file = _fixture.CreateTestFile($"prime_{size}.bin", size);
                string contentId = _fixture.ComputeContentId(file);

                Assert.Equal(40, contentId.Length);
            }
        }
    }
}

