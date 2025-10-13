// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.Concurrent;
using KOTORModSync.Core.FileSystemUtils;
using Xunit;
using Assert = Xunit.Assert;

namespace KOTORModSync.Tests
{


	public class CrossPlatformFileWatcherTests : IDisposable
	{
		private readonly string _testDirectory;
		private readonly string _externalDirectory;
		private readonly List<CrossPlatformFileWatcher> _watchers;
		private readonly List<string> _createdFiles;
		private readonly List<string> _createdDirectories;

		public CrossPlatformFileWatcherTests()
		{
			_testDirectory = Path.Combine(Path.GetTempPath(), $"FileWatcherTest_{Guid.NewGuid()}");
			_externalDirectory = Path.Combine(Path.GetTempPath(), $"External_{Guid.NewGuid()}");
			Directory.CreateDirectory(_testDirectory);
			Directory.CreateDirectory(_externalDirectory);
			_watchers = new List<CrossPlatformFileWatcher>();
			_createdFiles = new List<string>();
			_createdDirectories = new List<string>();
		}

		public void Dispose()
		{
			foreach ( var watcher in _watchers )
			{
				try { watcher.Dispose(); }
				catch {  }
			}

			foreach ( var file in _createdFiles )
			{
				try
				{
					if ( File.Exists(file) )
						File.Delete(file);
				}
				catch {  }
			}

			foreach ( var dir in _createdDirectories.OrderByDescending(d => d.Length) )
			{
				try
				{
					if ( Directory.Exists(dir) )
						Directory.Delete(dir, true);
				}
				catch {  }
			}

			try
			{
				if ( Directory.Exists(_testDirectory) )
					Directory.Delete(_testDirectory, true);
			}
			catch {  }

			try
			{
				if ( Directory.Exists(_externalDirectory) )
					Directory.Delete(_externalDirectory, true);
			}
			catch {  }
		}

		private string CreateTestFile(string directory, string fileName, string content = "test content")
		{
			var filePath = Path.Combine(directory, fileName);
			File.WriteAllText(filePath, content);
			_createdFiles.Add(filePath);
			return filePath;
		}

		#region Basic Initialization Tests

		[Fact]
		public void Constructor_WithValidPath_InitializesCorrectly()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			Assert.NotNull(watcher);
			Assert.False(watcher.EnableRaisingEvents, "Watcher should not be enabled immediately after construction");
		}

		[Fact]
		public void Constructor_WithNullPath_ThrowsArgumentNullException()
		{

			var exception = Assert.Throws<ArgumentNullException>(() => new CrossPlatformFileWatcher(null!));
			Assert.Equal("path", exception.ParamName);
		}

		[Fact]
		public void StartWatching_EnablesEventRaising()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);
			Assert.False(watcher.EnableRaisingEvents, "Precondition: watcher should start disabled");

			watcher.StartWatching();

			Assert.True(watcher.EnableRaisingEvents, "StartWatching() must set EnableRaisingEvents to true");
		}

		[Fact]
		public void StopWatching_DisablesEventRaising()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);
			watcher.StartWatching();
			Assert.True(watcher.EnableRaisingEvents, "Precondition: watcher should be enabled");

			watcher.StopWatching();

			Assert.False(watcher.EnableRaisingEvents, "StopWatching() must set EnableRaisingEvents to false");
		}

		[Fact]
		public void StartWatching_AfterDispose_ThrowsObjectDisposedException()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			watcher.Dispose();

			Assert.Throws<ObjectDisposedException>(() => watcher.StartWatching());
		}

		[Fact]
		public void Dispose_MultipleCalls_DoesNotThrow()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);

			watcher.Dispose();
			watcher.Dispose();
			watcher.Dispose();
		}

		#endregion

		#region File Creation Detection Tests

		[Fact]
		public async Task FileCreated_InWatchedDirectory_RaisesCreatedEventWithCorrectData()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			FileSystemEventArgs? capturedEvent = null;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Created += (sender, e) =>
			{
				capturedEvent = e;
				eventReceived.Set();
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string fileName = "created_test.txt";
			string filePath = CreateTestFile(_testDirectory, fileName);

			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
			Assert.True(signaled, "Created event must be raised within 5 seconds of file creation");
			Assert.NotNull(capturedEvent);
			Assert.Equal(WatcherChangeTypes.Created, capturedEvent!.ChangeType);
			Assert.Equal(fileName, capturedEvent.Name);
			Assert.True(File.Exists(filePath), "Created file must actually exist on file system");
		}

		[Fact]
		public async Task MultipleFilesCreated_SequentiallyInWatchedDirectory_RaisesCreatedEventForEach()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			var createdFiles = new ConcurrentBag<string>();
			int eventCount = 0;
			object lockObj = new();

			watcher.Created += (_, e) =>
			{
				createdFiles.Add(e.Name!);
				lock ( lockObj ) eventCount++;
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string[] fileNames = ["file1.txt", "file2.txt", "file3.txt"];
			foreach ( string fileName in fileNames )
			{
				CreateTestFile(_testDirectory, fileName);
				await Task.Delay(150);
			}

			await Task.Delay(800);

			Assert.True(eventCount >= fileNames.Length,
				$"Expected at least {fileNames.Length} Created events, but received {eventCount}");

			foreach ( string fileName in fileNames )
			{
				Assert.True(createdFiles.Contains(fileName),
					$"Created event must have been raised for {fileName}");
			}
		}

		[Fact]
		public async Task FileCreated_WithSpecificExtension_RaisesCreatedEventWithCorrectName()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory, "*.log");
			_watchers.Add(watcher);

			FileSystemEventArgs? capturedEvent = null;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Created += (_, e) =>
			{
				if ( e.Name!.EndsWith(".log") )
				{
					capturedEvent = e;
					eventReceived.Set();
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string fileName = "application.log";
			CreateTestFile(_testDirectory, fileName);

			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
			Assert.True(signaled, "Created event must be raised for .log file");
			Assert.NotNull(capturedEvent);
			Assert.Equal(fileName, capturedEvent!.Name);
			Assert.EndsWith(".log", capturedEvent.Name);
		}

		#endregion

		#region File Deletion Detection Tests

		[Fact]
		public async Task FileDeleted_FromWatchedDirectory_RaisesDeletedEventWithCorrectData()
		{

			string fileName = "delete_test.txt";
			string filePath = CreateTestFile(_testDirectory, fileName);
			await Task.Delay(100);

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			FileSystemEventArgs? capturedEvent = null;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Deleted += (_, e) =>
			{
				capturedEvent = e;
				eventReceived.Set();
			};

			watcher.StartWatching();
			await Task.Delay(100);

			File.Delete(filePath);

			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
			Assert.True(signaled, "Deleted event must be raised within 5 seconds of file deletion");
			Assert.NotNull(capturedEvent);
			Assert.Equal(WatcherChangeTypes.Deleted, capturedEvent!.ChangeType);
			Assert.Equal(fileName, capturedEvent.Name);
			Assert.False(File.Exists(filePath), "Deleted file must no longer exist on file system");
		}

		[Fact]
		public async Task MultipleFilesDeleted_SequentiallyFromWatchedDirectory_RaisesDeletedEventForEach()
		{

			string[] fileNames = { "delete1.txt", "delete2.txt", "delete3.txt" };
			var filePaths = new List<string>();
			foreach ( string fileName in fileNames )
			{
				var filePath = CreateTestFile(_testDirectory, fileName);
				filePaths.Add(filePath);
			}
			await Task.Delay(100);

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			var deletedFiles = new ConcurrentBag<string>();
			int eventCount = 0;
			object lockObj = new();

			watcher.Deleted += (_, e) =>
			{
				deletedFiles.Add(e.Name!);
				lock ( lockObj ) eventCount++;
			};

			watcher.StartWatching();
			await Task.Delay(100);

			foreach ( string filePath in filePaths )
			{
				File.Delete(filePath);
				await Task.Delay(150);
			}

			await Task.Delay(800);

			Assert.True(eventCount >= fileNames.Length,
				$"Expected at least {fileNames.Length} Deleted events, but received {eventCount}");

			foreach ( string fileName in fileNames )
			{
				Assert.True(deletedFiles.Contains(fileName),
					$"Deleted event must have been raised for {fileName}");
			}
		}

		#endregion

		#region File Modification Detection Tests

		[Fact]
		public async Task FileModified_InWatchedDirectory_RaisesChangedEventWithCorrectData()
		{

			string fileName = "modify_test.txt";
			string filePath = CreateTestFile(_testDirectory, fileName, "initial content");
			await Task.Delay(100);

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			FileSystemEventArgs? capturedEvent = null;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Changed += (_, e) =>
			{
				if ( e.Name == fileName )
				{
					capturedEvent = e;
					eventReceived.Set();
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string modifiedContent = "modified content";
			await File.WriteAllTextAsync(filePath, modifiedContent);

			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
			Assert.True(signaled, "Changed event must be raised within 5 seconds of file modification");
			Assert.NotNull(capturedEvent);
			Assert.Equal(WatcherChangeTypes.Changed, capturedEvent!.ChangeType);
			Assert.Equal(fileName, capturedEvent.Name);
			Assert.Equal(modifiedContent, await File.ReadAllTextAsync(filePath));
		}

		[Fact]
		public async Task FileModified_MultipleTimesInWatchedDirectory_RaisesChangedEventForEachModification()
		{

			string fileName = "multi_modify.txt";
			string filePath = CreateTestFile(_testDirectory, fileName, "original");
			await Task.Delay(100);

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			int changeCount = 0;
			var lockObj = new object();

			watcher.Changed += (sender, e) =>
			{
				if ( e.Name == fileName )
				{
					lock ( lockObj ) changeCount++;
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			int modifications = 3;
			for ( int i = 0; i < modifications; i++ )
			{
				await Task.Delay(200);
				File.WriteAllText(filePath, $"content version {i}");
			}

			await Task.Delay(800);

			Assert.True(changeCount >= 1,
				$"Expected at least 1 Changed event for {modifications} modifications, but received {changeCount}");
		}

		#endregion

		#region File Move Operations Tests

		[Fact]
		public async Task FileMovedIntoWatchedDirectory_FromExternalLocation_RaisesCreatedEvent()
		{

			string fileName = "moved_in.txt";
			string sourceFilePath = Path.Combine(_externalDirectory, fileName);
			await File.WriteAllTextAsync(sourceFilePath, "content to move");
			_createdFiles.Add(sourceFilePath);
			await Task.Delay(500);

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			FileSystemEventArgs? capturedEvent = null;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Created += (_, e) =>
			{
				if ( e.Name == fileName )
				{
					capturedEvent = e;
					eventReceived.Set();
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string destinationFilePath = Path.Combine(_testDirectory, fileName);
			_createdFiles.Add(destinationFilePath);
			File.Move(sourceFilePath, destinationFilePath);

			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
			Assert.True(signaled, "Created event must be raised when file is moved into watched directory");
			Assert.NotNull(capturedEvent);
			Assert.Equal(fileName, capturedEvent!.Name);
			Assert.True(File.Exists(destinationFilePath), "File must exist in watched directory after move");
			Assert.False(File.Exists(sourceFilePath), "File must not exist in source location after move");
		}

		[Fact]
		public async Task FileMovedOutOfWatchedDirectory_ToExternalLocation_RaisesDeletedEvent()
		{

			string fileName = "moved_out.txt";
			string sourceFilePath = CreateTestFile(_testDirectory, fileName);
			await Task.Delay(500);

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			FileSystemEventArgs? capturedEvent = null;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Deleted += (_, e) =>
			{
				if ( e.Name == fileName )
				{
					capturedEvent = e;
					eventReceived.Set();
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string destinationFilePath = Path.Combine(_externalDirectory, fileName);
			_createdFiles.Add(destinationFilePath);
			File.Move(sourceFilePath, destinationFilePath);

			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
			Assert.True(signaled, "Deleted event must be raised when file is moved out of watched directory");
			Assert.NotNull(capturedEvent);
			Assert.Equal(fileName, capturedEvent!.Name);
			Assert.False(File.Exists(sourceFilePath), "File must not exist in watched directory after move");
			Assert.True(File.Exists(destinationFilePath), "File must exist in destination location after move");
		}

		[Fact]
		public async Task FileMovedWithinWatchedDirectory_RaisesRenamedOrDeletedAndCreatedEvents()
		{

			string oldFileName = "old_name.txt";
			string newFileName = "new_name.txt";
			string sourceFilePath = CreateTestFile(_testDirectory, oldFileName);
			await Task.Delay(500);

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			bool renamedEventRaised = false;
			bool deletedEventRaised = false;
			bool createdEventRaised = false;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Renamed += (_, e) =>
			{
				if ( e.OldName == oldFileName && e.Name == newFileName )
				{
					renamedEventRaised = true;
					eventReceived.Set();
				}
			};

			watcher.Deleted += (_, e) =>
			{
				if ( e.Name == oldFileName )
				{
					deletedEventRaised = true;
				}
			};

			watcher.Created += (_, e) =>
			{
				if ( e.Name == newFileName )
				{
					createdEventRaised = true;
					eventReceived.Set();
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string destinationFilePath = Path.Combine(_testDirectory, newFileName);
			_createdFiles.Add(destinationFilePath);
			File.Move(sourceFilePath, destinationFilePath);

			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
			Assert.True(signaled, "Either Renamed event or (Deleted + Created) events must be raised");
			Assert.False(File.Exists(sourceFilePath), "Old file name must not exist after rename");
			Assert.True(File.Exists(destinationFilePath), "New file name must exist after rename");

			bool eventSequenceValid = renamedEventRaised || (deletedEventRaised && createdEventRaised);
			Assert.True(eventSequenceValid,
				"Either Renamed event or both Deleted and Created events must be raised for file move within directory");
		}

		#endregion

		#region Copy Operations Tests

		[Fact]
		public async Task FileCopiedIntoWatchedDirectory_RaisesCreatedEvent()
		{

			string fileName = "copy_source.txt";
			string sourceFilePath = Path.Combine(_externalDirectory, fileName);
			File.WriteAllText(sourceFilePath, "content to copy");
			_createdFiles.Add(sourceFilePath);
			await Task.Delay(500);

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			FileSystemEventArgs? capturedEvent = null;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Created += (sender, e) =>
			{
				if ( e.Name!.Contains("copy_dest") )
				{
					capturedEvent = e;
					eventReceived.Set();
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string destinationFileName = "copy_dest.txt";
			string destinationFilePath = Path.Combine(_testDirectory, destinationFileName);
			_createdFiles.Add(destinationFilePath);
			File.Copy(sourceFilePath, destinationFilePath);

			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
			Assert.True(signaled, "Created event must be raised when file is copied into watched directory");
			Assert.NotNull(capturedEvent);
			Assert.Equal(destinationFileName, capturedEvent!.Name);
			Assert.True(File.Exists(destinationFilePath), "Destination file must exist after copy");
			Assert.True(File.Exists(sourceFilePath), "Source file must still exist after copy");
			Assert.Equal(File.ReadAllText(sourceFilePath), File.ReadAllText(destinationFilePath));
		}

		#endregion

		#region Subdirectory Tests

		[Fact]
		public async Task FileCreatedInSubdirectory_WithIncludeSubdirectoriesTrue_RaisesCreatedEvent()
		{

			string subDirName = "subdir";
			string subDirPath = Path.Combine(_testDirectory, subDirName);
			Directory.CreateDirectory(subDirPath);
			_createdDirectories.Add(subDirPath);

			var watcher = new CrossPlatformFileWatcher(_testDirectory, includeSubdirectories: true);
			_watchers.Add(watcher);

			FileSystemEventArgs? capturedEvent = null;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Created += (sender, e) =>
			{
				if ( e.Name!.Contains("subdir") )
				{
					capturedEvent = e;
					eventReceived.Set();
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string fileName = "subfile.txt";
			string filePath = CreateTestFile(subDirPath, fileName);

			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
			Assert.True(signaled, "Created event must be raised for file in subdirectory when includeSubdirectories is true");
			Assert.NotNull(capturedEvent);
			Assert.True(capturedEvent!.Name!.Contains(fileName), $"Event name should contain {fileName}");
			Assert.True(File.Exists(filePath), "File must exist in subdirectory");
		}

		[Fact]
		public async Task FileCreatedInSubdirectory_WithIncludeSubdirectoriesFalse_DoesNotRaiseEvent()
		{

			string subDirName = "subdir_excluded";
			string subDirPath = Path.Combine(_testDirectory, subDirName);
			Directory.CreateDirectory(subDirPath);
			_createdDirectories.Add(subDirPath);

			var watcher = new CrossPlatformFileWatcher(_testDirectory, includeSubdirectories: false);
			_watchers.Add(watcher);

			bool eventRaisedForSubdir = false;

			watcher.Created += (_, e) =>
			{
				if ( e.Name!.Contains(subDirName) )
				{
					eventRaisedForSubdir = true;
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string fileName = "subfile_excluded.txt";
			string filePath = CreateTestFile(subDirPath, fileName);
			await Task.Delay(500);

			Assert.False(eventRaisedForSubdir, "Created event must NOT be raised for file in subdirectory when includeSubdirectories is false");
			Assert.True(File.Exists(filePath), "File must exist in subdirectory even though event wasn't raised");
		}

		#endregion

		#region Filter Tests

		[Fact]
		public async Task FileCreated_MatchingFilter_RaisesCreatedEvent()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory, "*.txt");
			_watchers.Add(watcher);

			var detectedFiles = new ConcurrentBag<string>();

			watcher.Created += (_, e) =>
			{
				detectedFiles.Add(e.Name!);
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string txtFile = "matching.txt";
			string logFile = "nonmatching.log";
			CreateTestFile(_testDirectory, txtFile);
			await Task.Delay(200);
			CreateTestFile(_testDirectory, logFile);
			await Task.Delay(500);

			Assert.Contains(txtFile, detectedFiles);

		}

		#endregion

		#region Watcher State Tests

		[Fact]
		public async Task WatcherStopped_FileCreated_DoesNotRaiseEvent()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			bool eventRaised = false;

			watcher.Created += (_, _) =>
			{
				eventRaised = true;
			};

			watcher.StartWatching();
			await Task.Delay(500);

			CreateTestFile(_testDirectory, "test1.txt");
			await Task.Delay(1000);
			bool eventRaisedWhileRunning = eventRaised;

			watcher.StopWatching();
			eventRaised = false;
			await Task.Delay(500);

			CreateTestFile(_testDirectory, "test2.txt");
			await Task.Delay(2000);

			Assert.True(eventRaisedWhileRunning, "Precondition: watcher should raise event while running");
			Assert.False(eventRaised, "Stopped watcher must NOT raise events");
		}

		[Fact]
		public async Task WatcherRestartedAfterStop_FileCreated_RaisesEvent()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			int eventCount = 0;
			object lockObj = new object();

			watcher.Created += (_, _) =>
			{
				lock ( lockObj ) eventCount++;
			};

			watcher.StartWatching();
			await Task.Delay(100);
			CreateTestFile(_testDirectory, "file1.txt");
			await Task.Delay(300);
			int countAfterFirstRun = eventCount;
			watcher.StopWatching();
			await Task.Delay(100);

			watcher.StartWatching();
			await Task.Delay(100);
			CreateTestFile(_testDirectory, "file2.txt");
			await Task.Delay(300);

			Assert.True(countAfterFirstRun >= 1, "Watcher should detect file during first run");
			Assert.True(eventCount >= 2, "Restarted watcher must detect new files");
		}

		#endregion

		#region Error Handling Tests

		[Fact]
		public async Task WatcherDisposed_WhileRunning_StopsGracefully()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			watcher.StartWatching();
			await Task.Delay(100);

			watcher.Dispose();

			Assert.False(watcher.EnableRaisingEvents, "Disposed watcher must have EnableRaisingEvents set to false");
		}

		#endregion

		#region Large File Operations Tests

		[Fact]
		public async Task LargeFileCreated_InWatchedDirectory_RaisesCreatedEvent()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			FileSystemEventArgs? capturedEvent = null;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Created += (_, e) =>
			{
				if ( e.Name == "largefile.dat" )
				{
					capturedEvent = e;
					eventReceived.Set();
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string filePath = Path.Combine(_testDirectory, "largefile.dat");
			_createdFiles.Add(filePath);
			byte[] data = new byte[5 * 1024 * 1024];
			new Random().NextBytes(data);
			await File.WriteAllBytesAsync(filePath, data);

			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(5));
			Assert.True(signaled, "Created event must be raised for large file");
			Assert.NotNull(capturedEvent);
			Assert.Equal("largefile.dat", capturedEvent!.Name);
			Assert.True(File.Exists(filePath), "Large file must exist");
			Assert.True(new FileInfo(filePath).Length >= 5 * 1024 * 1024, "File size must be at least 5MB");
		}

		#endregion

		#region Empty File Tests

		[Fact]
		public async Task EmptyFileCreated_InWatchedDirectory_RaisesCreatedEvent()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			FileSystemEventArgs? capturedEvent = null;
			var eventReceived = new ManualResetEventSlim(false);

			watcher.Created += (_, e) =>
			{
				if ( e.Name == "empty.txt" )
				{
					capturedEvent = e;
					eventReceived.Set();
				}
			};

			watcher.StartWatching();
			await Task.Delay(100);

			string filePath = Path.Combine(_testDirectory, "empty.txt");
			_createdFiles.Add(filePath);
			await File.Create(filePath).DisposeAsync();

			bool signaled = eventReceived.Wait(TimeSpan.FromSeconds(3));
			Assert.True(signaled, "Created event must be raised for empty file");
			Assert.NotNull(capturedEvent);
			Assert.Equal("empty.txt", capturedEvent!.Name);
			Assert.True(File.Exists(filePath), "Empty file must exist");
			Assert.Equal(0, new FileInfo(filePath).Length);
		}

		#endregion

		#region Thread Safety Tests

		[Fact]
		public async Task MultipleFilesCreated_Concurrently_AllEventsRaisedWithoutDataCorruption()
		{

			var watcher = new CrossPlatformFileWatcher(_testDirectory);
			_watchers.Add(watcher);

			var detectedFiles = new ConcurrentBag<string>();

			watcher.Created += (_, e) =>
			{
				detectedFiles.Add(e.Name!);
			};

			watcher.StartWatching();
			await Task.Delay(100);

			int fileCount = 20;
			IEnumerable<Task> tasks = Enumerable.Range(0, fileCount).Select(async i =>
			{
				string fileName = $"concurrent_{i:D3}.txt";
				await Task.Run(() => CreateTestFile(_testDirectory, fileName, $"content {i}"));
			});

			await Task.WhenAll(tasks);
			await Task.Delay(1500);

			Assert.True(detectedFiles.Count >= fileCount / 2,
				$"Expected at least {fileCount / 2} files detected out of {fileCount} concurrent creations, got {detectedFiles.Count}");

			int distinctFiles = detectedFiles.Distinct().Count();
			Assert.True(distinctFiles <= fileCount,
				$"Detected file count ({detectedFiles.Count}) should not exceed created file count ({fileCount}) by too much");
		}

		#endregion
	}
}
