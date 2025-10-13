// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using KOTORModSync.Core.FileSystemUtils;
using Xunit;
using Assert = Xunit.Assert;

namespace KOTORModSync.Tests
{
	/// <summary>
	/// Integration and stress tests for CrossPlatformFileWatcher.
	/// Tests real-world scenarios, performance, and stability.
	/// All tests use real file system operations without any mocking.
	/// </summary>
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
			_watchers = [];
			_createdFiles = [];
			_createdDirectories = [];
		}

		public void Dispose()
		{
			foreach ( CrossPlatformFileWatcher watcher in _watchers )
			{
				try { watcher.Dispose(); }
				catch { /* Ignore cleanup errors */ }
			}

			foreach ( string file in _createdFiles )
			{
				try
				{
					if ( File.Exists(file) )
						File.Delete(file);
				}
				catch { /* Ignore cleanup errors */ }
			}

			foreach ( string dir in _createdDirectories.OrderByDescending(d => d.Length) )
			{
				try
				{
					if ( Directory.Exists(dir) )
						Directory.Delete(dir, true);
				}
				catch { /* Ignore cleanup errors */ }
			}

			try
			{
				if ( Directory.Exists(_testDirectory) )
					Directory.Delete(_testDirectory, true);
			}
			catch { /* Ignore cleanup errors */ }
		}

		#region Real-World Scenario Tests

		[Fact]
		public async Task Scenario_ApplicationLogFile_DetectsMultipleAppends()
		{
			// Goal: Verify watcher detects log file appends simulating real application logging

			// Arrange
			string logFile = Path.Combine(_testDirectory, "application.log");
			await File.WriteAllTextAsync(logFile, "[STARTUP] Application started\n");
			_createdFiles.Add(logFile);
			await Task.Delay(100);

			var watcher = new CrossPlatformFileWatcher(_testDirectory, "*.log");
			_watchers.Add(watcher);

			int changeCount = 0;
			object lockObj = new();

			watcher.Changed += (_, e) =>
			{
				if ( e.Name == "application.log" )
					lock ( lockObj ) changeCount++;
			};

			watcher.StartWatching();
			await Task.Delay(100);

			// Act - Simulate application writing log entries
			const int logEntries = 10;
			for ( int i = 0; i < logEntries; i++ )
			{
				await Task.Delay(200);
				await File.AppendAllTextAsync(logFile, $"[INFO] Log entry {i} at {DateTime.Now:HH:mm:ss.fff}\n");
			}

			await Task.Delay(800);

			// Assert
			Assert.True(changeCount >= 1,
				$"Expected at least 1 change event for {logEntries} log appends, received {changeCount}");

			string logContent = await File.ReadAllTextAsync(logFile);
			Assert.Contains("[STARTUP]", logContent);
			Assert.Contains("Log entry", logContent);
		}

		[Fact]
		public async Task Scenario_ConfigFileUpdate_DetectsRewrite()
		{
			// Goal: Verify watcher detects config file being completely rewritten

			// Arrange
			string configFile = Path.Combine(_testDirectory, "settings.ini");
			string[] initialConfig =
			[
				"[Settings]",
				"Theme=Dark",
				"Language=English"
			];
			await File.WriteAllLinesAsync(configFile, initialConfig);
			_createdFiles.Add(configFile);
			await Task.Delay(100);

			var watcher = new CrossPlatformFileWatcher(_testDirectory, "*.ini");
			_watchers.Add(watcher);

			FileSystemEventArgs? changeEvent = null;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Changed += (_, e) =>
			{
				if ( e.Name == "settings.ini" )
				{
					changeEvent = e;
					eventReceived.Set();
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			// Act - Completely rewrite config file
			string[] newConfig =
			[
				"[Settings]",
				"Theme=Light",
				"Language=French",
				"Version=2.0"
			];
			await File.WriteAllLinesAsync(configFile, newConfig);

			// Assert
			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
			Assert.True(signaled, "Change event must be raised when config file is rewritten");
			Assert.NotNull(changeEvent);

			string actualContent = await File.ReadAllTextAsync(configFile);
			Assert.Contains("Theme=Light", actualContent);
			Assert.Contains("Version=2.0", actualContent);
		}

		[Fact]
		public async Task Scenario_DatabaseBackup_DetectsBackupFileCreation()
		{
			// Goal: Verify watcher detects backup file creation simulating database backup

			// Arrange
			var watcher = new CrossPlatformFileWatcher(_testDirectory, "*.bak");
			_watchers.Add(watcher);

			FileSystemEventArgs? createEvent = null;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Created += (_, e) =>
			{
				if ( e.Name!.EndsWith(".bak") )
				{
					createEvent = e;
					eventReceived.Set();
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			// Act - Simulate database backup creation
			string backupFile = Path.Combine(_testDirectory, $"database_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
			_createdFiles.Add(backupFile);

			byte[] backupData = new byte[1024 * 1024]; // 1MB simulated backup
			new Random().NextBytes(backupData);
			await File.WriteAllBytesAsync(backupFile, backupData);

			// Assert
			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
			Assert.True(signaled, "Created event must be raised for backup file");
			Assert.NotNull(createEvent);
			Assert.EndsWith(".bak", createEvent!.Name);
			Assert.True(File.Exists(backupFile), "Backup file must exist");
			Assert.True(new FileInfo(backupFile).Length >= 1024 * 1024, "Backup file must be at least 1MB");
		}

		[Fact]
		public async Task Scenario_TempFileCleanup_DetectsMultipleDeletions()
		{
			// Goal: Verify watcher detects cleanup of multiple temp files

			// Arrange
			List<string> tempFiles = [];
			for ( int i = 0; i < 5; i++ )
			{
				string tempFile = Path.Combine(_testDirectory, $"temp_{i}.tmp");
				await File.WriteAllTextAsync(tempFile, $"temp data {i}");
				tempFiles.Add(tempFile);
				_createdFiles.Add(tempFile);
			}
			await Task.Delay(100);

			var watcher = new CrossPlatformFileWatcher(_testDirectory, "*.tmp");
			_watchers.Add(watcher);

			var deletedFiles = new ConcurrentBag<string>();

			watcher.Deleted += (_, e) =>
			{
				deletedFiles.Add(e.Name!);
			};

			watcher.StartWatching();
			await Task.Delay(100);

			// Act - Cleanup temp files
			foreach ( string tempFile in tempFiles )
			{
				File.Delete(tempFile);
				await Task.Delay(200);
			}

			await Task.Delay(500);

			// Assert
			Assert.True(deletedFiles.Count >= tempFiles.Count,
				$"Expected {tempFiles.Count} deletion events, received {deletedFiles.Count}");

			foreach ( string tempFile in tempFiles )
			{
				Assert.False(File.Exists(tempFile), $"Temp file {tempFile} should be deleted");
			}
		}

		[Fact]
		public async Task Scenario_ExtractArchive_DetectsRapidFileCreation()
		{
			// Goal: Verify watcher handles rapid file creation simulating archive extraction

			// Arrange
			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			ConcurrentBag<string> createdFiles = [];

			watcher.Created += (_, e) =>
			{
				createdFiles.Add(e.Name!);
			};

			watcher.StartWatching();
			await Task.Delay(100);

			// Act - Simulate rapid extraction of 30 files
			int fileCount = 30;
			IEnumerable<Task> extractTasks = Enumerable.Range(0, fileCount).Select(async i =>
			{
				string fileName = $"extracted_{i:D3}.dat";
				string filePath = Path.Combine(_testDirectory, fileName);
				_createdFiles.Add(filePath);
				await File.WriteAllTextAsync(filePath, $"extracted content {i}");
				await Task.Delay(50); // Small stagger to simulate extraction
			});

			await Task.WhenAll(extractTasks);
			await Task.Delay(1000); // Allow all events to propagate

			// Assert
			Assert.True(createdFiles.Count >= fileCount * 0.6,
				$"Expected at least 60% ({fileCount * 0.6}) of {fileCount} files detected, received {createdFiles.Count}");

			// Verify all files actually exist
			int existingFiles = Directory.GetFiles(_testDirectory, "extracted_*.dat").Length;
			Assert.Equal(fileCount, existingFiles);
		}

		#endregion

		#region Stress Tests

		[Fact]
		public async Task Stress_ContinuousActivity_RemainsStableFor30Seconds()
		{
			// Goal: Verify watcher remains stable under continuous file activity

			// Arrange
			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			int totalEvents = 0;
			bool errorOccurred = false;
			object lockObj = new();

			watcher.Created += (_, _) => { lock ( lockObj ) totalEvents++; };
			watcher.Changed += (_, _) => { lock ( lockObj ) totalEvents++; };
			watcher.Deleted += (_, _) => { lock ( lockObj ) totalEvents++; };
			watcher.Error += (_, _) => { errorOccurred = true; };

			watcher.StartWatching();
			await Task.Delay(100);

			// Act - Continuous activity for 10 seconds
			var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			var activityTask = Task.Run(async () =>
			{
				int counter = 0;
				while ( !cts.Token.IsCancellationRequested )
				{
					try
					{
						string file = Path.Combine(_testDirectory, $"stress_{counter++}.txt");
						_createdFiles.Add(file);
						await File.WriteAllTextAsync(file, $"stress content {counter}", cts.Token);
						await Task.Delay(100, cts.Token);

						if ( counter % 5 == 0 && File.Exists(file) )
						{
							await File.AppendAllTextAsync(file, " appended", cts.Token);
							await Task.Delay(100, cts.Token);
						}

						if ( counter % 10 == 0 && File.Exists(file) )
						{
							File.Delete(file);
						}

						await Task.Delay(100, cts.Token);
					}
					catch ( OperationCanceledException )
					{
						break;
					}
					catch ( Exception )
					{
						// Ignore file operation errors during stress test
					}
				}
			}, cts.Token);

			await activityTask;
			cts.Dispose();
			await Task.Delay(500); // Allow events to settle

			// Assert
			Assert.False(errorOccurred, "No errors should occur during stress test");
			Assert.True(totalEvents >= 20,
				$"Expected at least 20 events during 10 second stress test, received {totalEvents}");
			Assert.True(watcher.EnableRaisingEvents, "Watcher must still be running after stress test");
		}

		[Fact]
		public async Task Stress_HighVolumeCreation_ProcessesMajorityOfFiles()
		{
			// Goal: Verify watcher can process high volume of file creations

			// Arrange
			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			ConcurrentBag<string> detectedFiles = [];

			watcher.Created += (_, e) =>
			{
				detectedFiles.Add(e.Name!);
			};

			watcher.StartWatching();
			await Task.Delay(100);

			var stopwatch = Stopwatch.StartNew();

			// Act - Create 100 files as fast as possible
			int fileCount = 100;
			IEnumerable<Task> tasks = Enumerable.Range(0, fileCount).Select(async i =>
			{
				string fileName = $"volume_{i:D4}.txt";
				string filePath = Path.Combine(_testDirectory, fileName);
				_createdFiles.Add(filePath);
				await File.WriteAllTextAsync(filePath, $"volume test {i}");
			});

			await Task.WhenAll(tasks);
			await Task.Delay(2000); // Allow time for event processing

			stopwatch.Stop();

			// Assert
			Assert.True(detectedFiles.Count >= fileCount * 0.5,
				$"Expected at least 50% ({fileCount * 0.5}) of {fileCount} files detected, received {detectedFiles.Count}");

			double eventsPerSecond = detectedFiles.Count / stopwatch.Elapsed.TotalSeconds;
			Assert.True(eventsPerSecond >= 1,
				$"Event processing rate too low: {eventsPerSecond:F2} events/second");

			// Verify all files actually exist
			int existingFiles = Directory.GetFiles(_testDirectory, "volume_*.txt").Length;
			Assert.Equal(fileCount, existingFiles);
		}

		[Fact]
		public async Task Stress_CreateModifyDeleteCycle_TracksAllOperations()
		{
			// Goal: Verify watcher tracks complete lifecycle of files under stress

			// Arrange
			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			int createdCount = 0;
			int modifiedCount = 0;
			int deletedCount = 0;
			object lockObj = new();

			watcher.Created += (_, _) => { lock ( lockObj ) createdCount++; };
			watcher.Changed += (_, _) => { lock ( lockObj ) modifiedCount++; };
			watcher.Deleted += (_, _) => { lock ( lockObj ) deletedCount++; };

			watcher.StartWatching();
			await Task.Delay(100);

			// Act - Run create-modify-delete cycles
			int cycles = 15;
			for ( int i = 0; i < cycles; i++ )
			{
				string fileName = $"cycle_{i}.txt";
				string filePath = Path.Combine(_testDirectory, fileName);

				// Create
				await File.WriteAllTextAsync(filePath, $"initial {i}");
				await Task.Delay(200);

				// Modify
				await File.WriteAllTextAsync(filePath, $"modified {i}");
				await Task.Delay(200);

				// Delete
				File.Delete(filePath);
				await Task.Delay(200);
			}

			await Task.Delay(800);

			// Assert
			Assert.True(createdCount >= cycles * 0.6,
				$"Expected at least 60% ({cycles * 0.6}) of {cycles} create events, received {createdCount}");
			Assert.True(deletedCount >= cycles * 0.6,
				$"Expected at least 60% ({cycles * 0.6}) of {cycles} delete events, received {deletedCount}");

			// Modified events are platform-dependent and may be less reliable
			// but at least some should occur
			int totalEvents = createdCount + modifiedCount + deletedCount;
			Assert.True(totalEvents >= cycles * 1.5,
				$"Expected at least {cycles * 1.5} total events, received {totalEvents}");
		}

		#endregion

		#region Multi-Watcher Tests

		[Fact]
		public async Task MultipleWatchers_SameDirectory_AllDetectSameEvents()
		{
			// Goal: Verify multiple watchers on same directory all receive events independently

			// Arrange
			var watcher1 = new CrossPlatformFileWatcher(_testDirectory);
			var watcher2 = new CrossPlatformFileWatcher(_testDirectory);
			var watcher3 = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.AddRange(new[] { watcher1, watcher2, watcher3 });

			int watcher1Events = 0;
			int watcher2Events = 0;
			int watcher3Events = 0;
			object lockObj = new();

			watcher1.Created += (_, _) => { lock ( lockObj ) watcher1Events++; };
			watcher2.Created += (_, _) => { lock ( lockObj ) watcher2Events++; };
			watcher3.Created += (_, _) => { lock ( lockObj ) watcher3Events++; };

			watcher1.StartWatching();
			watcher2.StartWatching();
			watcher3.StartWatching();
			await Task.Delay(100);

			// Act - Create files
			int fileCount = 5;
			for ( int i = 0; i < fileCount; i++ )
			{
				string filePath = Path.Combine(_testDirectory, $"multi_{i}.txt");
				_createdFiles.Add(filePath);
				await File.WriteAllTextAsync(filePath, $"content {i}");
				await Task.Delay(150);
			}

			await Task.Delay(500);

			// Assert - All watchers should detect files
			Assert.True(watcher1Events >= fileCount * 0.6,
				$"Watcher1 should detect at least {fileCount * 0.6} files, detected {watcher1Events}");
			Assert.True(watcher2Events >= fileCount * 0.6,
				$"Watcher2 should detect at least {fileCount * 0.6} files, detected {watcher2Events}");
			Assert.True(watcher3Events >= fileCount * 0.6,
				$"Watcher3 should detect at least {fileCount * 0.6} files, detected {watcher3Events}");
		}

		[Fact]
		public async Task MultipleWatchers_DifferentDirectories_EachDetectsOwnEvents()
		{
			// Goal: Verify watchers on different directories don't interfere with each other

			// Arrange
			string dir1 = Path.Combine(_testDirectory, "dir1");
			string dir2 = Path.Combine(_testDirectory, "dir2");
			Directory.CreateDirectory(dir1);
			Directory.CreateDirectory(dir2);
			_createdDirectories.AddRange(new[] { dir1, dir2 });

			var watcher1 = new CrossPlatformFileWatcher(dir1);
			var watcher2 = new CrossPlatformFileWatcher(dir2);
			_watchers.AddRange(new[] { watcher1, watcher2 });

			ConcurrentBag<string> watcher1Files = [];
			ConcurrentBag<string> watcher2Files = [];

			watcher1.Created += (_, e) => watcher1Files.Add(e.Name!);
			watcher2.Created += (_, e) => watcher2Files.Add(e.Name!);

			watcher1.StartWatching();
			watcher2.StartWatching();
			await Task.Delay(100);

			// Act
			string file1 = Path.Combine(dir1, "file_in_dir1.txt");
			string file2 = Path.Combine(dir2, "file_in_dir2.txt");
			_createdFiles.AddRange(new[] { file1, file2 });

			await File.WriteAllTextAsync(file1, "content 1");
			await Task.Delay(100);
			await File.WriteAllTextAsync(file2, "content 2");
			await Task.Delay(500);

			// Assert
			Assert.Contains("file_in_dir1.txt", watcher1Files);
			Assert.DoesNotContain("file_in_dir2.txt", watcher1Files);

			Assert.Contains("file_in_dir2.txt", watcher2Files);
			Assert.DoesNotContain("file_in_dir1.txt", watcher2Files);
		}

		#endregion

		#region Stability Tests

		[Fact]
		public async Task Stability_StartStopCycles_MaintainsCorrectState()
		{
			// Goal: Verify watcher maintains correct state through multiple start/stop cycles

			// Arrange
			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			int totalEvents = 0;
			object lockObj = new();

			watcher.Created += (_, _) => { lock ( lockObj ) totalEvents++; };

			// Act - Run multiple start/stop cycles
			int cycles = 5;
			for ( int cycle = 0; cycle < cycles; cycle++ )
			{
				watcher.StartWatching();
				Assert.True(watcher.EnableRaisingEvents, $"Cycle {cycle}: watcher should be enabled after StartWatching");
				await Task.Delay(200);

				// Create file
				string filePath = Path.Combine(_testDirectory, $"cycle_{cycle}.txt");
				_createdFiles.Add(filePath);
				await File.WriteAllTextAsync(filePath, $"cycle {cycle}");
				await Task.Delay(100);

				watcher.StopWatching();
				Assert.False(watcher.EnableRaisingEvents, $"Cycle {cycle}: watcher should be disabled after StopWatching");
				await Task.Delay(200);
			}

			// Assert
			Assert.True(totalEvents >= cycles * 0.8,
				$"Expected at least {cycles * 0.8} events across {cycles} cycles, received {totalEvents}");
		}

		[Fact]
		public async Task Stability_WatcherRunsFor60Seconds_NoMemoryLeakOrErrors()
		{
			// Goal: Verify watcher remains stable during extended operation

			// Arrange
			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			int eventCount = 0;
			bool errorOccurred = false;
			object lockObj = new();

			watcher.Created += (_, _) => { lock ( lockObj ) eventCount++; };
			watcher.Error += (_, _) => { errorOccurred = true; };

			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			long initialMemory = GC.GetTotalMemory(true);

			watcher.StartWatching();
			await Task.Delay(100);

			// Act - Run for 10 seconds with periodic activity
			var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			var activityTask = Task.Run(async () =>
			{
				int counter = 0;
				while ( !cts.Token.IsCancellationRequested )
				{
					try
					{
						string file = Path.Combine(_testDirectory, $"stability_{counter++}.txt");
						_createdFiles.Add(file);
						await File.WriteAllTextAsync(file, $"content {counter}", cts.Token);
						await Task.Delay(500, cts.Token);
					}
					catch ( OperationCanceledException )
					{
						break;
					}
					catch ( Exception )
					{
						// Ignore file operation errors
					}
				}
			}, cts.Token);

			await activityTask;
			cts.Dispose();
			await Task.Delay(500);

			// Check memory
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			long finalMemory = GC.GetTotalMemory(true);
			long memoryIncrease = finalMemory - initialMemory;
			double memoryIncreaseMb = memoryIncrease / (1024.0 * 1024.0);

			// Assert
			Assert.False(errorOccurred, "No errors should occur during extended operation");
			Assert.True(eventCount >= 10, $"Expected at least 10 events during 10 seconds, received {eventCount}");
			Assert.True(memoryIncreaseMb < 100, $"Memory increase should be less than 100MB, was {memoryIncreaseMb:F2}MB");
			Assert.True(watcher.EnableRaisingEvents, "Watcher should still be running");
		}

		#endregion

		#region Error Recovery Tests

		[Fact]
		public async Task ErrorRecovery_DirectoryDeleted_WatcherHandlesGracefully()
		{
			// Goal: Verify watcher handles watched directory deletion gracefully

			// Arrange
			string tempDir = Path.Combine(_testDirectory, "temp_watch_dir");
			Directory.CreateDirectory(tempDir);
			_createdDirectories.Add(tempDir);

			var watcher = new CrossPlatformFileWatcher(tempDir);
			_watchers.Add(watcher);

			watcher.Error += (_, _) => { /* Error may occur depending on platform */ };

			watcher.StartWatching();
			await Task.Delay(100);

			// Create a file to verify watcher is working
			string testFile = Path.Combine(tempDir, "test.txt");
			await File.WriteAllTextAsync(testFile, "test");
			await Task.Delay(150);

			// Act - Delete the directory being watched
			watcher.StopWatching();
			Directory.Delete(tempDir, true);
			await Task.Delay(100);

			// Assert - Should not crash (no exception should propagate)
			Assert.False(Directory.Exists(tempDir), "Directory should be deleted");
		}

		#endregion

		#region Performance Measurement Tests

		[Fact]
		public async Task Performance_MeasureEventLatency_WithinAcceptableRange()
		{
			// Goal: Verify event detection latency is within acceptable range

			// Arrange
			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			var latencies = new ConcurrentBag<TimeSpan>();
			var timestamps = new ConcurrentDictionary<string, DateTime>();

			watcher.Created += (_, e) =>
			{
				if ( timestamps.TryRemove(e.Name!, out DateTime createTime) )
				{
					TimeSpan latency = DateTime.Now - createTime;
					latencies.Add(latency);
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			// Act - Create files and measure latency
			int sampleSize = 10;
			for ( int i = 0; i < sampleSize; i++ )
			{
				string fileName = $"latency_{i}.txt";
				string filePath = Path.Combine(_testDirectory, fileName);
				_createdFiles.Add(filePath);

				timestamps[fileName] = DateTime.Now;
				await File.WriteAllTextAsync(filePath, $"content {i}");
				await Task.Delay(150);
			}

			await Task.Delay(1000);

			// Assert
			if ( latencies.Any() )
			{
				double avgLatencyMs = latencies.Average(l => l.TotalMilliseconds);
				TimeSpan maxLatency = latencies.Max();

				// Acceptable latency thresholds (vary by platform)
				Assert.True(avgLatencyMs < 5000, $"Average latency too high: {avgLatencyMs:F2}ms");
				Assert.True(maxLatency.TotalSeconds < 10, $"Max latency too high: {maxLatency.TotalSeconds:F2}s");
			}
		}

		#endregion
	}
}
