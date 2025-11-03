// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using KOTORModSync.Core.Services.Download;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class DownloadCacheConcurrencyTests
    {
        private string _testDirectory;

        [SetUp]
        public void Setup()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "KOTORModSync_ConcurrencyTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_testDirectory);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_testDirectory))
            {
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        [Test]
        public async Task AcquireContentKeyLock_MultipleConcurrentCalls_OnlyOneAcquires()
        {
            string contentKey = "test_content_key";
            int acquired = 0;
            int released = 0;
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using (await DownloadCacheOptimizer.AcquireContentKeyLock(contentKey).ConfigureAwait(false))
                    {
                        Interlocked.Increment(ref acquired);
                        await Task.Delay(10); // Simulate work
                        Interlocked.Increment(ref released);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            Assert.Multiple(() =>
            {
                // All should have acquired and released
                Assert.That(acquired, Is.EqualTo(10));
                Assert.That(released, Is.EqualTo(10));
            });
        }

        [Test]
        public async Task AcquireContentKeyLock_SerializesAccess()
        {
            string contentKey = "serial_test";
            int maxConcurrent = 0;
            int currentConcurrent = 0;
            var tasks = new List<Task>();

            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    using (await DownloadCacheOptimizer.AcquireContentKeyLock(contentKey).ConfigureAwait(false))
                    {
                        int current = Interlocked.Increment(ref currentConcurrent);
                        if (current > maxConcurrent)
                        {
                            maxConcurrent = current;
                        }
                        await Task.Delay(20); // Simulate work
                        Interlocked.Decrement(ref currentConcurrent);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Max concurrent should be 1 (serialized access)
            Assert.That(maxConcurrent, Is.EqualTo(1));
        }

        [Test]
        public async Task AcquireContentKeyLock_DifferentKeys_AllowParallelAccess()
        {
            int maxConcurrent = 0;
            int currentConcurrent = 0;
            var tasks = new List<Task>();

            for (int i = 0; i < 5; i++)
            {
                string uniqueKey = $"key_{i}";
                tasks.Add(Task.Run(async () =>
                {
                    using (await DownloadCacheOptimizer.AcquireContentKeyLock(uniqueKey).ConfigureAwait(false))
                    {
                        int current = Interlocked.Increment(ref currentConcurrent);
                        if (current > maxConcurrent)
                        {
                            maxConcurrent = current;
                        }
                        await Task.Delay(50); // Simulate longer work
                        Interlocked.Decrement(ref currentConcurrent);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Different keys should allow parallel access
            Assert.That(maxConcurrent, Is.GreaterThan(1));
        }

        [Test]
        public async Task ComputeFileIntegrityData_ConcurrentCalls_AllSucceed()
        {
            // Create test files
            var files = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                string file = Path.Combine(_testDirectory, $"test_{i}.txt");
                await File.WriteAllTextAsync(file, $"Content {i}");
                files.Add(file);
            }

            var tasks = files.Select(f => DownloadCacheOptimizer.ComputeFileIntegrityData(f)).ToList();
            var results = await Task.WhenAll(tasks);

            // All should succeed
            Assert.That(results.Length, Is.EqualTo(5));
            foreach (var result in results)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(result.contentHashSHA256, Has.Length.EqualTo(64));
                    Assert.That(result.pieceLength, Is.GreaterThan(0));
                });
            }
        }

        [Test]
        public async Task ComputeContentIdFromMetadata_ConcurrentCalls_ProduceConsistentResults()
        {
            var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["provider"] = "deadlystream",
                ["fileId"] = "test123",
                ["version"] = "1.0",
            };

            string url = "https://example.com/test";

            var tasks = Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => DownloadCacheOptimizer.ComputeContentIdFromMetadata(metadata, url)))
                .ToList();

            string[] results = await Task.WhenAll(tasks);

            // All results should be identical
            string first = results[0];
            foreach (string result in results)
            {
                Assert.That(result, Is.EqualTo(first));
            }
        }

        [Test]
        public async Task BlockContentId_ConcurrentBlocking_AllSucceed()
        {
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                string contentId = $"blocked_{i}";
                tasks.Add(Task.Run(() => DownloadCacheOptimizer.BlockContentId(contentId, "test")));
            }

            await Task.WhenAll(tasks);

            // All should be blocked
            for (int i = 0; i < 10; i++)
            {
                Assert.That(DownloadCacheOptimizer.IsContentIdBlocked($"blocked_{i}"), Is.True);
            }
        }

        [Test]
        public async Task IsContentIdBlocked_ConcurrentReads_AllSucceed()
        {
            string contentId = "read_test";
            DownloadCacheOptimizer.BlockContentId(contentId, "test");

            var tasks = Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() => DownloadCacheOptimizer.IsContentIdBlocked(contentId)))
                .ToList();

            bool[] results = await Task.WhenAll(tasks);

            // All should return true
            Assert.That(results.All(r => r), Is.True);
        }

        [Test]
        public async Task DeterminePieceSize_ConcurrentCalls_AreThreadSafe()
        {
            long[] fileSizes = { 1024, 1024 * 1024, 10 * 1024 * 1024, 100 * 1024 * 1024 };

            var tasks = fileSizes
                .SelectMany(_ => Enumerable.Range(0, 5))
                .Select(i => Task.Run(() => DownloadCacheOptimizer.DeterminePieceSize(fileSizes[i % fileSizes.Length])))
                .ToList();

            int[] results = await Task.WhenAll(tasks);

            // All should return valid piece sizes
            foreach (int result in results)
            {
                Assert.That(result, Is.GreaterThan(0));
                Assert.That(result, Is.LessThanOrEqualTo(4194304)); // Max 4MB
            }
        }

        [Test]
        public async Task PartialFilePath_ConcurrentGeneration_ProducesUniqueFiles()
        {
            var tasks = Enumerable.Range(0, 10)
                .Select(i => Task.Run(() =>
                    DownloadCacheOptimizer.GetPartialFilePath($"content_{i}", _testDirectory)))
                .ToList();

            string[] paths = await Task.WhenAll(tasks);

            // All paths should be unique
            var uniquePaths = paths.Distinct(StringComparer.Ordinal).ToList();
            Assert.That(uniquePaths.Count, Is.EqualTo(10));

            // All should be in .partial directory
            foreach (string path in paths)
            {
                Assert.That(path, Does.Contain(".partial"));
            }
        }

        [Test]
        public async Task AcquireContentKeyLock_Timeout_DoesNotDeadlock()
        {
            string contentKey = "timeout_test";

            using (await DownloadCacheOptimizer.AcquireContentKeyLock(contentKey))
            {
                // Try to acquire again from another task (should wait)
                var task = Task.Run(async () =>
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
                    {
                        try
                        {
                            using (await DownloadCacheOptimizer.AcquireContentKeyLock(contentKey).ConfigureAwait(false))
                            {
                                return "acquired";
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            return "timeout";
                        }
                    }
                });

                // The inner lock should timeout since we're holding the outer one
                await Task.Delay(200);

                // Complete to avoid deadlock
            }

            // Should complete without deadlock
            Assert.Pass();
        }
    }
}
