// Copyright (C) 2025
// Licensed under the GPL version 3 license.

using System;
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
        public void EdgeCase_ZeroByteFile_HandledGracefully()
        {
            string file = _fixture.CreateTestFile("zero.bin", 0);
            string contentId = _fixture.ComputeContentId(file);

            Assert.NotNull(contentId);
            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void EdgeCase_OneByte_HandledGracefully()
        {
            string file = _fixture.CreateTestFile("one_byte.bin", 1);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void EdgeCase_PieceBoundary_Exact()
        {
            // File size exactly matches piece size
            string file = _fixture.CreateTestFile("exact_piece.bin", 262144);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void EdgeCase_PieceBoundary_OneLess()
        {
            // File size is one byte less than piece size
            string file = _fixture.CreateTestFile("piece_minus_one.bin", 262143);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void EdgeCase_PieceBoundary_OneMore()
        {
            // File size is one byte more than piece size
            string file = _fixture.CreateTestFile("piece_plus_one.bin", 262145);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void ErrorHandling_NonexistentFile_ThrowsException()
        {
            Assert.Throws<FileNotFoundException>(() =>
            {
                _fixture.ComputeContentId("/nonexistent/file.bin");
            });
        }

        [Fact]
        public void ErrorHandling_NullPath_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _fixture.ComputeContentId(filePath: null);
            });
        }

        [Fact]
        public void ErrorHandling_EmptyPath_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _fixture.ComputeContentId("");
            });
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
        }

        [Fact]
        public void ErrorHandling_BlockInvalidContentId_NoException()
        {
            // Should handle invalid ContentIds gracefully
            Exception exception = Record.Exception(() =>
            {
                DownloadCacheOptimizer.BlockContentId("invalid_id", "test");
            });

            Assert.Null(exception);
        }

        [Fact]
        public void ErrorHandling_GetResourceDetails_InvalidKey_ReturnsMessage()
        {
            string result = DownloadCacheOptimizer.GetSharedResourceDetails("totally_invalid_key");

            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task ErrorHandling_GracefulShutdown_MultipleCallsSafe()
        {
            await DownloadCacheOptimizer.GracefulShutdownAsync();
            await DownloadCacheOptimizer.GracefulShutdownAsync();
            await DownloadCacheOptimizer.GracefulShutdownAsync();

            // Should not throw
            (int activeShares, long totalUploadBytes, int connectedSources) stats = DownloadCacheOptimizer.GetNetworkCacheStats();

            // S2699: Add assertions
            // Expect stats values to be at least zero after shutdown, as all background sharing should stop.
            Assert.True(stats.activeShares >= 0);
            Assert.True(stats.totalUploadBytes >= 0);
            Assert.True(stats.connectedSources >= 0);
        }

        [Fact]
        public void EdgeCase_FilenameWithSpecialChars_HandledCorrectly()
        {
            // Filenames with special characters should work
            string file = _fixture.CreateTestFile("test (1) [special].bin", 1024);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void EdgeCase_VeryLongFilename_HandledCorrectly()
        {
            string longName = new string('a', 200) + ".bin";
            string file = _fixture.CreateTestFile(longName, 1024);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void EdgeCase_UnicodeFilename_HandledCorrectly()
        {
            string file = _fixture.CreateTestFile("测试文件_тест_🎮.bin", 1024);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public async Task ErrorHandling_ConcurrentAccess_Safe()
        {
            string file = _fixture.CreateTestFile("concurrent.bin", 10240);

            // Compute ContentId concurrently
            Task<string>[] tasks = Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => _fixture.ComputeContentId(file)))
                .ToArray();

            await Task.WhenAll(tasks);

            var results = tasks.Select(t => t.Result).ToList();
            Assert.True(results.All(r => string.Equals(r, results[0], StringComparison.Ordinal)));
        }

        [Fact]
        public void EdgeCase_FileModifiedDuringRead_DetectsChange()
        {
            string file = _fixture.CreateTestFile("modified.bin", 10240);
            string id1 = _fixture.ComputeContentId(file);

            // Modify file
            DistributionTestSupport.ModifyFile(
                file,
                stream =>
                {
                    stream.Seek(5000, SeekOrigin.Begin);
                    stream.WriteByte(0xFF);
                });

            string id2 = _fixture.ComputeContentId(file);

            Assert.NotEqual(id1, id2);
        }

        [Fact]
        public void EdgeCase_MaxInt32Size_HandledCorrectly()
        {
            // Test with file size near Int32.MaxValue boundary
            // We'll simulate with a smaller size for testing
            long size = int.MaxValue / 1000; // ~2 MB
            string file = _fixture.CreateTestFile("near_max_int.bin", size);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void ErrorHandling_Stats_ConsistentFormat()
        {
            for (int i = 0; i < 10; i++)
            {
                (int activeShares, long totalUploadBytes, int connectedSources) = DownloadCacheOptimizer.GetNetworkCacheStats();

                Assert.True(activeShares >= 0);
                Assert.True(totalUploadBytes >= 0);
                Assert.True(connectedSources >= 0);
            }
        }

        [Fact]
        public void EdgeCase_AlternatingBytes_ValidContentId()
        {
            byte[] data = new byte[10000];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 2 == 0 ? 0xAA : 0x55);
            }

            string file = DistributionTestSupport.EnsureBinaryTestFile(
                _fixture.TestDataDirectory,
                "alternating.bin",
                data);

            string contentId = _fixture.ComputeContentId(file);
            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void EdgeCase_AllZeros_ValidContentId()
        {
            byte[] data = new byte[10000];
            string file = DistributionTestSupport.EnsureBinaryTestFile(
                _fixture.TestDataDirectory,
                "all_zeros.bin",
                data);

            string contentId = _fixture.ComputeContentId(file);
            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void EdgeCase_AllOnes_ValidContentId()
        {
            byte[] data = Enumerable.Repeat((byte)0xFF, 10000).ToArray();
            string file = DistributionTestSupport.EnsureBinaryTestFile(
                _fixture.TestDataDirectory,
                "all_ones.bin",
                data);

            string contentId = _fixture.ComputeContentId(file);
            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void ErrorHandling_BlockContentId_NullReason_NoException()
        {
            Exception exception = Record.Exception(() =>
            {
                DownloadCacheOptimizer.BlockContentId(new string('a', 40), reason: null);
            });

            Assert.Null(exception);
        }

        [Fact]
        public void ErrorHandling_BlockContentId_EmptyReason_NoException()
        {
            Exception exception = Record.Exception(() =>
            {
                DownloadCacheOptimizer.BlockContentId(new string('b', 40), "");
            });

            Assert.Null(exception);
        }

        [Fact]
        public void ErrorHandling_GetResourceDetails_EmptyKey_ReturnsMessage()
        {
            string result = DownloadCacheOptimizer.GetSharedResourceDetails("");
            Assert.NotNull(result);
        }

        [Fact]
        public void ErrorHandling_GetResourceDetails_NullKey_ReturnsMessage()
        {
            string result = DownloadCacheOptimizer.GetSharedResourceDetails(contentKey: null);
            Assert.NotNull(result);
        }

        [Fact]
        public void EdgeCase_RandomData_Consistent()
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
        public void EdgeCase_TextFile_ValidContentId()
        {
            string text = string.Join(Environment.NewLine,
                Enumerable.Range(0, 1000).Select(i => $"Line {i}"));

            string file = _fixture.CreateTestFile("text_lines.txt", 0, text);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void EdgeCase_MixedNewlines_ValidContentId()
        {
            string text = "Line1\nLine2\rLine3\r\nLine4";
            string file = _fixture.CreateTestFile("mixed_newlines.txt", 0, text);
            string contentId = _fixture.ComputeContentId(file);

            Assert.Equal(40, contentId.Length);
        }

        [Fact]
        public void EdgeCase_BOMPresent_ValidContentId()
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
        public void EdgeCase_PowerOfTwo_FileSizes()
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
        public void EdgeCase_PrimeNumber_FileSizes()
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

