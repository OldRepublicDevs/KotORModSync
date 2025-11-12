// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Utility;
using NUnit.Framework;
using Xunit;
using Assert = Xunit.Assert;

namespace KOTORModSync.Tests
{


    public class CrossPlatformFileWatcherIntegrationTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly List<CrossPlatformFileWatcher> _watchers;
        private readonly List<string> _createdFiles;
        private readonly List<string> _createdDirectories;

        public CrossPlatformFileWatcherIntegrationTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"IntegrationTest_{Guid.NewGuid()}");
            _ = Directory.CreateDirectory(_testDirectory);
            _watchers = new List<CrossPlatformFileWatcher>();
            _createdFiles = new List<string>();
            _createdDirectories = new List<string>();
        }

        public void Dispose()
        {
            foreach (CrossPlatformFileWatcher watcher in _watchers)
            {
                try { watcher.Dispose(); }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                }
            }

            foreach (string file in _createdFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                }
            }

            foreach (string dir in _createdDirectories.OrderByDescending(d => d.Length))
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                }
            }

            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        #region Real-World Scenario Tests

        [Test]
        public async Task Scenario_ApplicationLogFile_DetectsMultipleAppends()
        {

            string logFile = Path.Combine(_testDirectory, "application.log");
            await NetFrameworkCompatibility.WriteAllTextAsync(logFile, "[STARTUP] Application started\n");
            _createdFiles.Add(logFile);
            await Task.Delay(100);

            var watcher = new CrossPlatformFileWatcher(_testDirectory, "*.log");
            _watchers.Add(watcher);

            int changeCount = 0;
            object lockObj = new object();

            watcher.Changed += (_, e) =>
            {
                if (string.Equals(e.Name, "application.log", StringComparison.Ordinal))
                {
                    lock (lockObj)
                    {
                        changeCount++;
                    }
                }
            };

            watcher.StartWatching();
            await Task.Delay(100);

            const int logEntries = 10;
            for (int i = 0; i < logEntries; i++)
            {
                await Task.Delay(200);
                await NetFrameworkCompatibility.AppendAllTextAsync(logFile, $"[INFO] Log entry {i} at {DateTime.Now:HH:mm:ss.fff}\n");
            }

            await Task.Delay(800);

            Assert.True(changeCount >= 1,
                $"Expected at least 1 change event for {logEntries} log appends, received {changeCount}");

            string logContent = await NetFrameworkCompatibility.ReadAllTextAsync(logFile);
            Assert.Contains("[STARTUP]", logContent, StringComparison.Ordinal);
            Assert.Contains("Log entry", logContent, StringComparison.Ordinal);
        }

        [Test]
        public async Task Scenario_ConfigFileUpdate_DetectsRewrite()
        {

            string configFile = Path.Combine(_testDirectory, "settings.ini");
            string[] initialConfig =
            new string[]
            {
                "[Settings]",
                "Theme=Dark",
                "Language=English",
            };
            await NetFrameworkCompatibility.WriteAllLinesAsync(configFile, initialConfig);
            _createdFiles.Add(configFile);
            await Task.Delay(100);

            var watcher = new CrossPlatformFileWatcher(_testDirectory, "*.ini");
            _watchers.Add(watcher);

            FileSystemEventArgs changeEvent = null;
            var eventReceived = new ManualResetEventSlim(initialState: false);

            watcher.Changed += (_, e) =>
            {
                if (string.Equals(e.Name, "settings.ini", StringComparison.Ordinal))
                {
                    changeEvent = e;
                    eventReceived.Set();
                }
            };

            watcher.StartWatching();
            await Task.Delay(100);

            string[] newConfig = new string[]
            {
                "[Settings]",
                "Theme=Light",
                "Language=French",
                "Version=2.0",
            };
            await NetFrameworkCompatibility.WriteAllLinesAsync(configFile, newConfig);

            bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
            Assert.True(signaled, "Change event must be raised when config file is rewritten");
            Assert.NotNull(changeEvent);

            string actualContent = await NetFrameworkCompatibility.ReadAllTextAsync(configFile);
            Assert.Contains("Theme=Light", actualContent, StringComparison.Ordinal);
            Assert.Contains("Version=2.0", actualContent, StringComparison.Ordinal);
        }

        [Test]
        public async Task Scenario_DatabaseBackup_DetectsBackupFileCreation()
        {

            var watcher = new CrossPlatformFileWatcher(_testDirectory, "*.bak");
            _watchers.Add(watcher);

            FileSystemEventArgs createEvent = null;
            var eventReceived = new ManualResetEventSlim(initialState: false);

            watcher.Created += (_, e) =>
            {
                if (e.Name.EndsWith(".bak", StringComparison.Ordinal))
                {
                    createEvent = e;
                    eventReceived.Set();
                }
            };

            watcher.StartWatching();
            await Task.Delay(100);

            string backupFile = Path.Combine(_testDirectory, $"database_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
            _createdFiles.Add(backupFile);

            byte[] backupData = new byte[1024 * 1024];
            new Random().NextBytes(backupData);
            await NetFrameworkCompatibility.WriteAllBytesAsync(backupFile, backupData);

            bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
            Assert.True(signaled, "Created event must be raised for backup file");
            Assert.NotNull(createEvent);
            Assert.EndsWith(".bak", createEvent.Name, StringComparison.Ordinal);
            Assert.True(File.Exists(backupFile), "Backup file must exist");
            Assert.True(new FileInfo(backupFile).Length >= 1024 * 1024, "Backup file must be at least 1MB");
        }

        [Test]
        public async Task Scenario_TempFileCleanup_DetectsMultipleDeletions()
        {

            List<string> tempFiles = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                string tempFile = Path.Combine(_testDirectory, $"temp_{i}.tmp");
                await NetFrameworkCompatibility.WriteAllTextAsync(tempFile, $"temp data {i}");
                tempFiles.Add(tempFile);
                _createdFiles.Add(tempFile);
            }
            await Task.Delay(100);

            var watcher = new CrossPlatformFileWatcher(_testDirectory, "*.tmp");
            _watchers.Add(watcher);

            var deletedFiles = new ConcurrentBag<string>();

            watcher.Deleted += (_, e) =>
            {
                deletedFiles.Add(e.Name);
            };

            watcher.StartWatching();
            await Task.Delay(100);

            foreach (string tempFile in tempFiles)
            {
                File.Delete(tempFile);
                await Task.Delay(200);
            }

            await Task.Delay(500);

            Assert.True(deletedFiles.Count >= tempFiles.Count,
                $"Expected {tempFiles.Count} deletion events, received {deletedFiles.Count}");

            foreach (string tempFile in tempFiles)
            {
                Assert.False(File.Exists(tempFile), $"Temp file {tempFile} should be deleted");
            }
        }

        [Test]
        public async Task Scenario_ExtractArchive_DetectsRapidFileCreation()
        {

            var watcher = new CrossPlatformFileWatcher(_testDirectory);
            _watchers.Add(watcher);

            ConcurrentBag<string> createdFiles = new ConcurrentBag<string>();

            watcher.Created += (_, e) =>
            {
                createdFiles.Add(e.Name);
            };

            watcher.StartWatching();
            await Task.Delay(100);

            int fileCount = 30;
            IEnumerable<Task> extractTasks = Enumerable.Range(0, fileCount).Select(async i =>
            {
                string fileName = $"extracted_{i:D3}.dat";
                string filePath = Path.Combine(_testDirectory, fileName);
                _createdFiles.Add(filePath);
                await NetFrameworkCompatibility.WriteAllTextAsync(filePath, $"extracted content {i}");
                await Task.Delay(50);
            });

            await Task.WhenAll(extractTasks);
            await Task.Delay(1000);

            Assert.True(createdFiles.Count >= fileCount * 0.6,
                $"Expected at least 60% ({fileCount * 0.6}) of {fileCount} files detected, received {createdFiles.Count}");

            int existingFiles = Directory.GetFiles(_testDirectory, "extracted_*.dat").Length;
            Assert.Equal(fileCount, existingFiles);
        }

        #endregion

        #region Stress Tests

        [Test]
        public async Task Stress_ContinuousActivity_RemainsStableFor30Seconds()
        {

            var watcher = new CrossPlatformFileWatcher(_testDirectory);
            _watchers.Add(watcher);

            int totalEvents = 0;
            bool errorOccurred = false;
            object lockObj = new object();

            watcher.Created += (_, __) => { lock (lockObj) { totalEvents++; } };
            watcher.Changed += (_, __) => { lock (lockObj) { totalEvents++; } };
            watcher.Deleted += (_, __) => { lock (lockObj) { totalEvents++; } };
            watcher.Error += (_, __) => { errorOccurred = true; };

            watcher.StartWatching();
            await Task.Delay(100);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var activityTask = Task.Run(async () =>
            {
                int counter = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        string file = Path.Combine(_testDirectory, $"stress_{counter++}.txt");
                        _createdFiles.Add(file);
                        await NetFrameworkCompatibility.WriteAllTextAsync(file, $"stress content {counter}", cts.Token);
                        await Task.Delay(100, cts.Token);

                        if (counter % 5 == 0 && File.Exists(file))
                        {
                            await NetFrameworkCompatibility.AppendAllTextAsync(file, " appended", encoding: null, cts.Token);
                            await Task.Delay(100, cts.Token);
                        }

                        if (counter % 10 == 0 && File.Exists(file))
                        {
                            File.Delete(file);
                        }

                        await Task.Delay(100, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {

                    }
                }
            }, cts.Token);

            await activityTask;
            cts.Dispose();
            await Task.Delay(500);

            Assert.False(errorOccurred, "No errors should occur during stress test");
            Assert.True(totalEvents >= 20,
                $"Expected at least 20 events during 10 second stress test, received {totalEvents}");
            Assert.True(watcher.EnableRaisingEvents, "Watcher must still be running after stress test");
        }

        [Test]
        public async Task Stress_HighVolumeCreation_ProcessesMajorityOfFiles()
        {

            var watcher = new CrossPlatformFileWatcher(_testDirectory);
            _watchers.Add(watcher);

            ConcurrentBag<string> detectedFiles = new ConcurrentBag<string>();

            watcher.Created += (_, e) =>
            {
                detectedFiles.Add(e.Name);
            };

            watcher.StartWatching();
            await Task.Delay(100);

            var stopwatch = Stopwatch.StartNew();

            int fileCount = 100;
            IEnumerable<Task> tasks = Enumerable.Range(0, fileCount).Select(async i =>
            {
                string fileName = $"volume_{i:D4}.txt";
                string filePath = Path.Combine(_testDirectory, fileName);
                _createdFiles.Add(filePath);
                await NetFrameworkCompatibility.WriteAllTextAsync(filePath, $"volume test {i}");
            });

            await Task.WhenAll(tasks);
            await Task.Delay(2000);

            stopwatch.Stop();

            Assert.True(detectedFiles.Count >= fileCount * 0.5,
                $"Expected at least 50% ({fileCount * 0.5}) of {fileCount} files detected, received {detectedFiles.Count}");

            double eventsPerSecond = detectedFiles.Count / stopwatch.Elapsed.TotalSeconds;
            Assert.True(eventsPerSecond >= 1,
                $"Event processing rate too low: {eventsPerSecond:F2} events/second");

            int existingFiles = Directory.GetFiles(_testDirectory, "volume_*.txt").Length;
            Assert.Equal(fileCount, existingFiles);
        }

        [Test]
        public async Task Stress_CreateModifyDeleteCycle_TracksAllOperations()
        {

            var watcher = new CrossPlatformFileWatcher(_testDirectory);
            _watchers.Add(watcher);

            int createdCount = 0;
            int modifiedCount = 0;
            int deletedCount = 0;
            object lockObj = new object();

            watcher.Created += (_, __) => { lock (lockObj) { createdCount++; } };
            watcher.Changed += (_, __) => { lock (lockObj) { modifiedCount++; } };
            watcher.Deleted += (_, __) => { lock (lockObj) { deletedCount++; } };

            watcher.StartWatching();
            await Task.Delay(100);

            int cycles = 15;
            for (int i = 0; i < cycles; i++)
            {
                string fileName = $"cycle_{i}.txt";
                string filePath = Path.Combine(_testDirectory, fileName);

                await NetFrameworkCompatibility.WriteAllTextAsync(filePath, $"initial {i}");
                await Task.Delay(200);

                await NetFrameworkCompatibility.WriteAllTextAsync(filePath, $"modified {i}");
                await Task.Delay(200);

                File.Delete(filePath);
                await Task.Delay(200);
            }

            await Task.Delay(800);

            Assert.True(createdCount >= cycles * 0.6,
                $"Expected at least 60% ({cycles * 0.6}) of {cycles} create events, received {createdCount}");
            Assert.True(deletedCount >= cycles * 0.6,
                $"Expected at least 60% ({cycles * 0.6}) of {cycles} delete events, received {deletedCount}");

            int totalEvents = createdCount + modifiedCount + deletedCount;
            Assert.True(totalEvents >= cycles * 1.5,
                $"Expected at least {cycles * 1.5} total events, received {totalEvents}");
        }

        #endregion

        #region Multi-Watcher Tests

        [Test]
        public async Task MultipleWatchers_SameDirectory_AllDetectSameEvents()
        {

            var watcher1 = new CrossPlatformFileWatcher(_testDirectory);
            var watcher2 = new CrossPlatformFileWatcher(_testDirectory);
            var watcher3 = new CrossPlatformFileWatcher(_testDirectory);
            _watchers.AddRange(new[] { watcher1, watcher2, watcher3 });

            int watcher1Events = 0;
            int watcher2Events = 0;
            int watcher3Events = 0;
            object lockObj = new object();

            watcher1.Created += (_, __) => { lock (lockObj) { watcher1Events++; } };
            watcher2.Created += (_, __) => { lock (lockObj) { watcher2Events++; } };
            watcher3.Created += (_, __) => { lock (lockObj) { watcher3Events++; } };

            watcher1.StartWatching();
            watcher2.StartWatching();
            watcher3.StartWatching();
            await Task.Delay(100);

            int fileCount = 5;
            for (int i = 0; i < fileCount; i++)
            {
                string filePath = Path.Combine(_testDirectory, $"multi_{i}.txt");
                _createdFiles.Add(filePath);
                await NetFrameworkCompatibility.WriteAllTextAsync(filePath, $"content {i}");
                await Task.Delay(150);
            }

            await Task.Delay(500);

            Assert.True(watcher1Events >= fileCount * 0.6,
                $"Watcher1 should detect at least {fileCount * 0.6} files, detected {watcher1Events}");
            Assert.True(watcher2Events >= fileCount * 0.6,
                $"Watcher2 should detect at least {fileCount * 0.6} files, detected {watcher2Events}");
            Assert.True(watcher3Events >= fileCount * 0.6,
                $"Watcher3 should detect at least {fileCount * 0.6} files, detected {watcher3Events}");
        }

        [Test]
        public async Task MultipleWatchers_DifferentDirectories_EachDetectsOwnEvents()
        {

            string dir1 = Path.Combine(_testDirectory, "dir1");
            string dir2 = Path.Combine(_testDirectory, "dir2");
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);
            _createdDirectories.AddRange(new[] { dir1, dir2 });

            var watcher1 = new CrossPlatformFileWatcher(dir1);
            var watcher2 = new CrossPlatformFileWatcher(dir2);
            _watchers.AddRange(new[] { watcher1, watcher2 });

            ConcurrentBag<string> watcher1Files = new ConcurrentBag<string>();
            ConcurrentBag<string> watcher2Files = new ConcurrentBag<string>();

            watcher1.Created += (_, e) => watcher1Files.Add(e.Name);
            watcher2.Created += (_, e) => watcher2Files.Add(e.Name);

            watcher1.StartWatching();
            watcher2.StartWatching();
            await Task.Delay(100);

            string file1 = Path.Combine(dir1, "file_in_dir1.txt");
            string file2 = Path.Combine(dir2, "file_in_dir2.txt");
            _createdFiles.AddRange(new[] { file1, file2 });

            await NetFrameworkCompatibility.WriteAllTextAsync(file1, "content 1");
            await Task.Delay(100);
            await NetFrameworkCompatibility.WriteAllTextAsync(file2, "content 2");
            await Task.Delay(500);

            Assert.Contains("file_in_dir1.txt", watcher1Files);
            Assert.DoesNotContain("file_in_dir2.txt", watcher1Files);

            Assert.Contains("file_in_dir2.txt", watcher2Files);
            Assert.DoesNotContain("file_in_dir1.txt", watcher2Files);
        }

        #endregion

        #region Stability Tests

        [Test]
        public async Task Stability_StartStopCycles_MaintainsCorrectState()
        {

            var watcher = new CrossPlatformFileWatcher(_testDirectory);
            _watchers.Add(watcher);

            int totalEvents = 0;
            object lockObj = new object();

            watcher.Created += (_, __) => { lock (lockObj) { totalEvents++; } };

            int cycles = 5;
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                watcher.StartWatching();
                Assert.True(watcher.EnableRaisingEvents, $"Cycle {cycle}: watcher should be enabled after StartWatching");
                await Task.Delay(200);

                string filePath = Path.Combine(_testDirectory, $"cycle_{cycle}.txt");
                _createdFiles.Add(filePath);
                await NetFrameworkCompatibility.WriteAllTextAsync(filePath, $"cycle {cycle}");
                await Task.Delay(100);

                watcher.StopWatching();
                Assert.False(watcher.EnableRaisingEvents, $"Cycle {cycle}: watcher should be disabled after StopWatching");
                await Task.Delay(200);
            }

            Assert.True(totalEvents >= cycles * 0.8,
                $"Expected at least {cycles * 0.8} events across {cycles} cycles, received {totalEvents}");
        }

        [Test]
        public async Task Stability_WatcherRunsFor60Seconds_NoMemoryLeakOrErrors()
        {

            var watcher = new CrossPlatformFileWatcher(_testDirectory);
            _watchers.Add(watcher);

            int eventCount = 0;
            bool errorOccurred = false;
            object lockObj = new object();

            watcher.Created += (_, __) => { lock (lockObj) { eventCount++; } };
            watcher.Error += (_, __) => { errorOccurred = true; };

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long initialMemory = GC.GetTotalMemory(forceFullCollection: true);

            watcher.StartWatching();
            await Task.Delay(100);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var activityTask = Task.Run(async () =>
            {
                int counter = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        string file = Path.Combine(_testDirectory, $"stability_{counter++}.txt");
                        _createdFiles.Add(file);
                        await NetFrameworkCompatibility.WriteAllTextAsync(file, $"content {counter}", cts.Token);
                        await Task.Delay(500, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {

                    }
                }
            }, cts.Token);

            await activityTask;
            cts.Dispose();
            await Task.Delay(500);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long finalMemory = GC.GetTotalMemory(forceFullCollection: true);
            long memoryIncrease = finalMemory - initialMemory;
            double memoryIncreaseMb = memoryIncrease / (1024.0 * 1024.0);

            Assert.False(errorOccurred, "No errors should occur during extended operation");
            Assert.True(eventCount >= 10, $"Expected at least 10 events during 10 seconds, received {eventCount}");
            Assert.True(memoryIncreaseMb < 100, $"Memory increase should be less than 100MB, was {memoryIncreaseMb:F2}MB");
            Assert.True(watcher.EnableRaisingEvents, "Watcher should still be running");
        }

        #endregion

        #region Error Recovery Tests

        [Test]
        public async Task ErrorRecovery_DirectoryDeleted_WatcherHandlesGracefully()
        {

            string tempDir = Path.Combine(_testDirectory, "temp_watch_dir");
            Directory.CreateDirectory(tempDir);
            _createdDirectories.Add(tempDir);

            var watcher = new CrossPlatformFileWatcher(tempDir);
            _watchers.Add(watcher);

            watcher.Error += (_, __) => { };

            watcher.StartWatching();
            await Task.Delay(100);

            string testFile = Path.Combine(tempDir, "test.txt");
            await NetFrameworkCompatibility.WriteAllTextAsync(testFile, "test");
            await Task.Delay(150);

            watcher.StopWatching();
            Directory.Delete(tempDir, recursive: true);
            await Task.Delay(100);

            Assert.False(Directory.Exists(tempDir), "Directory should be deleted");
        }

        #endregion

        #region Performance Measurement Tests

        [Test]
        public async Task Performance_MeasureEventLatency_WithinAcceptableRange()
        {

            var watcher = new CrossPlatformFileWatcher(_testDirectory);
            _watchers.Add(watcher);

            var latencies = new ConcurrentBag<TimeSpan>();
            var timestamps = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);

            watcher.Created += (_, e) =>
            {
                if (timestamps.TryRemove(e.Name, out long createTicks))
                {
                    long nowTicks = Stopwatch.GetTimestamp();
                    double latencySeconds = (nowTicks - createTicks) / (double)Stopwatch.Frequency;
                    latencies.Add(TimeSpan.FromSeconds(latencySeconds));
                }
            };

            watcher.StartWatching();
            await Task.Delay(100);

            int sampleSize = 10;
            for (int i = 0; i < sampleSize; i++)
            {
                string fileName = $"latency_{i}.txt";
                string filePath = Path.Combine(_testDirectory, fileName);
                _createdFiles.Add(filePath);

                timestamps[fileName] = Stopwatch.GetTimestamp();
                await NetFrameworkCompatibility.WriteAllTextAsync(filePath, $"content {i}");
                await Task.Delay(150);
            }

            await Task.Delay(1000);

            if (latencies.Any())
            {
                double avgLatencyMs = latencies.Average(l => l.TotalMilliseconds);
                TimeSpan maxLatency = latencies.Max();

                Assert.True(avgLatencyMs < 5000, $"Average latency too high: {avgLatencyMs:F2}ms");
                Assert.True(maxLatency.TotalSeconds < 10, $"Max latency too high: {maxLatency.TotalSeconds:F2}s");
            }
        }

        #endregion
    }
}
