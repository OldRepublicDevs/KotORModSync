// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Services.FileSystem;
using KOTORModSync.Core.Services.ImmutableCheckpoint;

namespace KOTORModSync.Tests
{
	/// <summary>
	/// Comprehensive integration tests for the bidirectional delta checkpoint system.
	/// These tests use real file operations and actual installations, no mocking.
	/// </summary>
	[TestFixture]
	public sealed class CheckpointSystemIntegrationTests
	{
	private DirectoryInfo _gameDirectory = null!;
	private DirectoryInfo _sourceDirectory = null!;
	private CheckpointService _checkpointService = null!;
	private MainConfig _mainConfig = null!;
	private List<ModComponent> _testComponents = null!;
	private string _currentSessionId = null!;

		[SetUp]
		public async Task SetUp()
		{
			// Create isolated test directories
			string tempRoot = Path.Combine(Path.GetTempPath(), "KOTORModSync_CheckpointTests", Guid.NewGuid().ToString("N"));
			_gameDirectory = Directory.CreateDirectory(Path.Combine(tempRoot, "GameDirectory"));
			_sourceDirectory = Directory.CreateDirectory(Path.Combine(tempRoot, "SourceDirectory"));

			// Initialize MainConfig
			_mainConfig = new MainConfig
			{
				destinationPath = _gameDirectory,
				sourcePath = _sourceDirectory
			};

			// Create initial game files (simulating a clean KOTOR installation)
			await CreateInitialGameFilesAsync();

			// Initialize checkpoint service
			_checkpointService = new CheckpointService(_gameDirectory.FullName);

			// Create test mod components
			_testComponents = new List<ModComponent>();
		}

		[TearDown]
		public void TearDown()
		{
			try
			{
				if ( _gameDirectory?.Parent != null && _gameDirectory.Parent.Exists )
				{
					Directory.Delete(_gameDirectory.Parent.FullName, recursive: true);
				}
			}
			catch
			{
				// Best effort cleanup
			}
		}

		#region Test Fixture Setup Helpers

		/// <summary>
		/// Creates initial game files to simulate a clean KOTOR installation
		/// </summary>
		private async Task CreateInitialGameFilesAsync()
		{
			// Create directory structure
			Directory.CreateDirectory(Path.Combine(_gameDirectory.FullName, "Override"));
			Directory.CreateDirectory(Path.Combine(_gameDirectory.FullName, "Modules"));
			Directory.CreateDirectory(Path.Combine(_gameDirectory.FullName, "StreamVoice"));

			// Create some baseline game files
			await CreateTestFileAsync(Path.Combine(_gameDirectory.FullName, "chitin.key"), "ORIGINAL_CHITIN_KEY", 1024);
			await CreateTestFileAsync(Path.Combine(_gameDirectory.FullName, "dialog.tlk"), "ORIGINAL_DIALOG_TLK", 5 * 1024 * 1024); // 5MB
			await CreateTestFileAsync(Path.Combine(_gameDirectory.FullName, "Override", "appearance.2da"), "ORIGINAL_APPEARANCE", 10 * 1024);
			await CreateTestFileAsync(Path.Combine(_gameDirectory.FullName, "Modules", "danm13.mod"), "ORIGINAL_DANM13_MODULE", 2 * 1024 * 1024); // 2MB

			// Create a large file to test binary diff performance
			await CreateTestFileAsync(Path.Combine(_gameDirectory.FullName, "StreamVoice", "large_audio.wav"), "ORIGINAL_LARGE_AUDIO", 50 * 1024 * 1024); // 50MB
		}

		/// <summary>
		/// Creates a test file with specified content pattern and size
		/// </summary>
		private async Task CreateTestFileAsync(string path, string contentPattern, int sizeBytes)
		{
			string? directoryName = Path.GetDirectoryName(path);
			if ( !string.IsNullOrEmpty(directoryName) )
			{
				Directory.CreateDirectory(directoryName);
			}

			using ( var stream = File.Create(path) )
			{
				byte[] pattern = Encoding.UTF8.GetBytes(contentPattern);
				int written = 0;

				while ( written < sizeBytes )
				{
					int toWrite = Math.Min(pattern.Length, sizeBytes - written);
					await stream.WriteAsync(pattern, 0, toWrite);
					written += toWrite;
				}
			}
		}

		/// <summary>
		/// Creates a test mod component that makes specific file changes
		/// </summary>
		private ModComponent CreateTestModComponent(string name, int modNumber)
		{
			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = name,
				IsSelected = true,
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>()
			};

			// Each mod will add/modify different files to create realistic change patterns
			string testFilesDir = Path.Combine(_sourceDirectory.FullName, $"Mod_{modNumber}");
			Directory.CreateDirectory(testFilesDir);

			// Create test files for this mod
			string newTexture = Path.Combine(testFilesDir, $"texture_{modNumber}.tga");
			File.WriteAllText(newTexture, $"TEXTURE_CONTENT_FOR_MOD_{modNumber}");

			string modified2DA = Path.Combine(testFilesDir, "appearance.2da");
			File.WriteAllText(modified2DA, $"MODIFIED_APPEARANCE_BY_MOD_{modNumber}");

			// Add copy instructions
			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Copy,
				Source = new List<string> { newTexture },
				Destination = Path.Combine(_gameDirectory.FullName, "Override", Path.GetFileName(newTexture))
			});

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Copy,
				Source = new List<string> { modified2DA },
				Destination = Path.Combine(_gameDirectory.FullName, "Override", "appearance.2da")
			});

			return component;
		}

		/// <summary>
		/// Creates a mod that modifies a large binary file (simulates texture/audio mod)
		/// </summary>
		private async Task<ModComponent> CreateLargeFileModComponentAsync(string name)
		{
			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = name,
				IsSelected = true,
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>()
			};

			string testFilesDir = Path.Combine(_sourceDirectory.FullName, "LargeFileMod");
			Directory.CreateDirectory(testFilesDir);

			// Create modified version of large audio file (with small changes)
			string modifiedAudio = Path.Combine(testFilesDir, "large_audio.wav");
			string originalAudio = Path.Combine(_gameDirectory.FullName, "StreamVoice", "large_audio.wav");
			
			// Copy original and modify a small portion
			File.Copy(originalAudio, modifiedAudio, true);
			using ( var stream = File.OpenWrite(modifiedAudio) )
			{
				stream.Seek(1024 * 1024, SeekOrigin.Begin); // Seek to 1MB
				byte[] modification = Encoding.UTF8.GetBytes("MODIFIED_SECTION_OF_LARGE_FILE");
				await stream.WriteAsync(modification, 0, modification.Length);
			}

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Copy,
				Source = new List<string> { modifiedAudio },
				Destination = originalAudio
			});

			return component;
		}

		/// <summary>
		/// Computes SHA256 hash of a file
		/// </summary>
		private async Task<string> ComputeFileHashAsync(string filePath)
		{
			using ( var sha256 = SHA256.Create() )
			using ( var stream = File.OpenRead(filePath) )
			{
				byte[] hash = await sha256.ComputeHashAsync(stream);
				return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
			}
		}

		/// <summary>
		/// Verifies game directory matches expected checkpoint state
		/// </summary>
		private async Task<bool> VerifyGameStateAsync(Checkpoint checkpoint)
		{
			foreach ( var file in checkpoint.Files )
			{
				string fullPath = Path.Combine(_gameDirectory.FullName, file.Value.Path);
				
				if ( !File.Exists(fullPath) )
				{
					Console.WriteLine($"VERIFY FAILED: Missing file {file.Value.Path}");
					return false;
				}

				string currentHash = await ComputeFileHashAsync(fullPath);
				if ( currentHash != file.Value.Hash )
				{
					Console.WriteLine($"VERIFY FAILED: Hash mismatch for {file.Value.Path}");
					Console.WriteLine($"  Expected: {file.Value.Hash}");
					Console.WriteLine($"  Actual: {currentHash}");
					return false;
				}
			}

			return true;
		}

		#endregion

		#region Basic Checkpoint Tests

		[Test]
		public async Task CheckpointService_CreatesBaselineSnapshot()
		{
			// Act
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();

			// Assert
			Assert.That(_currentSessionId, Is.Not.Null.And.Not.Empty, "Session ID should be generated");

			var sessions = await _checkpointService.ListSessionsAsync();
			Assert.That(sessions, Has.Count.EqualTo(1), "Should have one session");

			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			Assert.That(checkpoints, Has.Count.EqualTo(1), "Should have baseline checkpoint");
			Assert.That(checkpoints[0].ComponentName, Is.EqualTo("Baseline"), "First checkpoint should be baseline");
		}

		[Test]
		public async Task CheckpointService_CreatesCheckpointAfterModInstallation()
		{
			// Arrange
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var mod = CreateTestModComponent("Test Mod 1", 1);
			var fileSystemProvider = new RealFileSystemProvider();

			// Act - Install mod
			await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
			
			// Create checkpoint
			string checkpointId = await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());

			// Assert
			Assert.That(checkpointId, Is.Not.Null.And.Not.Empty, "Checkpoint ID should be generated");

			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			Assert.That(checkpoints, Has.Count.EqualTo(2), "Should have baseline + 1 mod checkpoint");

			var checkpoint = checkpoints.FirstOrDefault(c => c.Id == checkpointId);
			Assert.That(checkpoint, Is.Not.Null, "Checkpoint should exist");
			Assert.That(checkpoint.ComponentName, Is.EqualTo(mod.Name), "Checkpoint name should match mod");
			Assert.That(checkpoint.Added, Is.Not.Empty, "Should track added files");
		}

		[Test]
		public async Task CheckpointService_TracksFileChanges()
		{
			// Arrange
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var mod = CreateTestModComponent("Change Tracker Test", 1);
			var fileSystemProvider = new RealFileSystemProvider();

			int initialFileCount = Directory.GetFiles(_gameDirectory.FullName, "*", SearchOption.AllDirectories).Length;

			// Act
			await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
			string checkpointId = await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());

			// Assert
			var checkpoint = (await _checkpointService.ListCheckpointsAsync(_currentSessionId))
				.First(c => c.Id == checkpointId);

			Assert.That(checkpoint.Added.Count, Is.GreaterThan(0), "Should have added files");
			Assert.That(checkpoint.Modified.Count, Is.GreaterThan(0), "Should have modified files");
			
			int currentFileCount = Directory.GetFiles(_gameDirectory.FullName, "*", SearchOption.AllDirectories).Length;
			Assert.That(currentFileCount, Is.GreaterThan(initialFileCount), "File count should increase");
		}

		#endregion

		#region Multiple Checkpoint Tests

		[Test]
		public async Task CheckpointService_CreatesMultipleCheckpoints()
		{
			// Arrange
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();
			int modCount = 5;

			// Act - Install multiple mods
			for ( int i = 1; i <= modCount; i++ )
			{
				var mod = CreateTestModComponent($"Test Mod {i}", i);
				await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
				await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());
			}

			// Assert
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			Assert.That(checkpoints, Has.Count.EqualTo(modCount + 1), $"Should have baseline + {modCount} mod checkpoints");

			// Verify sequence numbers
			var ordered = checkpoints.OrderBy(c => c.Sequence).ToList();
			for ( int i = 0; i < ordered.Count; i++ )
			{
				Assert.That(ordered[i].Sequence, Is.EqualTo(i), $"Checkpoint {i} should have sequence {i}");
			}
		}

		[Test]
		public async Task CheckpointService_IdentifiesAnchorCheckpoints()
		{
			// Arrange - Create 25 checkpoints to test anchor at 10, 20
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();
			int modCount = 25;

			// Act
			for ( int i = 1; i <= modCount; i++ )
			{
				var mod = CreateTestModComponent($"Mod {i}", i);
				await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
				await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());
			}

			// Assert
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			var anchors = checkpoints.Where(c => c.IsAnchor).ToList();

			Assert.That(anchors, Has.Count.EqualTo(3), "Should have 3 anchors (0, 10, 20)");
			Assert.That(anchors.Any(a => a.Sequence == 0), Is.True, "Sequence 0 (baseline) should be anchor");
			Assert.That(anchors.Any(a => a.Sequence == 10), Is.True, "Sequence 10 should be anchor");
			Assert.That(anchors.Any(a => a.Sequence == 20), Is.True, "Sequence 20 should be anchor");
		}

		#endregion

		#region Restoration Tests

		[Test]
		public async Task CheckpointService_RestoresNearbyCheckpoint()
		{
			// Arrange - Install 5 mods
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();
			var checkpointIds = new List<string>();

			for ( int i = 1; i <= 5; i++ )
			{
				var mod = CreateTestModComponent($"Mod {i}", i);
				await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
				string cpId = await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());
				checkpointIds.Add(cpId);
			}

			// Get checkpoint 3 state
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			var targetCheckpoint = checkpoints.First(c => c.Sequence == 3);

			// Act - Restore to checkpoint 3 (from checkpoint 5 = 2 steps back)
			await _checkpointService.RestoreCheckpointAsync(targetCheckpoint.Id);

			// Assert - Verify game state matches checkpoint 3
			var restoredCheckpoint = checkpoints.First(c => c.Id == targetCheckpoint.Id);
			bool stateMatches = await VerifyGameStateAsync(restoredCheckpoint);
			Assert.That(stateMatches, Is.True, "Game state should match checkpoint 3");

			// Verify files from mods 4 and 5 are removed
			string mod4File = Path.Combine(_gameDirectory.FullName, "Override", "texture_4.tga");
			string mod5File = Path.Combine(_gameDirectory.FullName, "Override", "texture_5.tga");
			Assert.That(File.Exists(mod4File), Is.False, "Mod 4 file should be removed");
			Assert.That(File.Exists(mod5File), Is.False, "Mod 5 file should be removed");
		}

		[Test]
		public async Task CheckpointService_RestoresDistantCheckpoint()
		{
			// Arrange - Install 25 mods (to test anchor navigation)
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();

			for ( int i = 1; i <= 25; i++ )
			{
				var mod = CreateTestModComponent($"Mod {i}", i);
				await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
				await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());
			}

			// Get checkpoint 5 state
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			var targetCheckpoint = checkpoints.First(c => c.Sequence == 5);

			// Act - Restore to checkpoint 5 (from checkpoint 25 = 20 steps back, uses anchors)
			await _checkpointService.RestoreCheckpointAsync(targetCheckpoint.Id);

			// Assert
			var restoredCheckpoint = checkpoints.First(c => c.Id == targetCheckpoint.Id);
			bool stateMatches = await VerifyGameStateAsync(restoredCheckpoint);
			Assert.That(stateMatches, Is.True, "Game state should match checkpoint 5");

			// Verify files from mods 6-25 are removed
			for ( int i = 6; i <= 25; i++ )
			{
				string modFile = Path.Combine(_gameDirectory.FullName, "Override", $"texture_{i}.tga");
				Assert.That(File.Exists(modFile), Is.False, $"Mod {i} file should be removed");
			}
		}

		[Test]
		public async Task CheckpointService_RestoresToBaseline()
		{
			// Arrange - Install 10 mods
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();
			
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			var baselineCheckpoint = checkpoints.First(c => c.Sequence == 0);

			for ( int i = 1; i <= 10; i++ )
			{
				var mod = CreateTestModComponent($"Mod {i}", i);
				await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
				await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());
			}

			// Act - Restore to baseline (undo all mods)
			await _checkpointService.RestoreCheckpointAsync(baselineCheckpoint.Id);

			// Assert - Verify original game state
			bool stateMatches = await VerifyGameStateAsync(baselineCheckpoint);
			Assert.That(stateMatches, Is.True, "Game state should match baseline");

			// Verify all mod files are removed
			string overrideDir = Path.Combine(_gameDirectory.FullName, "Override");
			var overrideFiles = Directory.GetFiles(overrideDir).Select(Path.GetFileName).ToList();
			
			Assert.That(overrideFiles, Does.Not.Contain("texture_1.tga"), "Mod files should be removed");
			Assert.That(overrideFiles, Contains.Item("appearance.2da"), "Original files should exist");
		}

		[Test]
		public async Task CheckpointService_RestoreToAnchorCheckpoint()
		{
			// Arrange - Install 15 mods (anchor at 10)
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();

			for ( int i = 1; i <= 15; i++ )
			{
				var mod = CreateTestModComponent($"Mod {i}", i);
				await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
				await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());
			}

			// Get anchor checkpoint (10)
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			var anchorCheckpoint = checkpoints.First(c => c.Sequence == 10 && c.IsAnchor);

			// Act - Restore to anchor
			await _checkpointService.RestoreCheckpointAsync(anchorCheckpoint.Id);

			// Assert
			bool stateMatches = await VerifyGameStateAsync(anchorCheckpoint);
			Assert.That(stateMatches, Is.True, "Game state should match anchor checkpoint 10");

			// Verify files from mods 11-15 are removed
			for ( int i = 11; i <= 15; i++ )
			{
				string modFile = Path.Combine(_gameDirectory.FullName, "Override", $"texture_{i}.tga");
				Assert.That(File.Exists(modFile), Is.False, $"Mod {i} file should be removed");
			}

			// Verify files from mods 1-10 still exist
			for ( int i = 1; i <= 10; i++ )
			{
				string modFile = Path.Combine(_gameDirectory.FullName, "Override", $"texture_{i}.tga");
				Assert.That(File.Exists(modFile), Is.True, $"Mod {i} file should exist");
			}
		}

		#endregion

	#region Large File Tests

	[Test]
	[CancelAfter(120000)] // 2 minutes timeout
	public async Task CheckpointService_HandlesLargeFilesWithBinaryDiff()
		{
			// Arrange
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var largeFileMod = await CreateLargeFileModComponentAsync("Large Audio Mod");
			var fileSystemProvider = new RealFileSystemProvider();

			string audioFile = Path.Combine(_gameDirectory.FullName, "StreamVoice", "large_audio.wav");
			string originalHash = await ComputeFileHashAsync(audioFile);

			// Act - Install mod that modifies 50MB file
			await largeFileMod.ExecuteInstructionsAsync(largeFileMod.Instructions, new List<ModComponent> { largeFileMod }, default, fileSystemProvider);
			string checkpointId = await _checkpointService.CreateCheckpointAsync(largeFileMod.Name, largeFileMod.Guid.ToString());

			string modifiedHash = await ComputeFileHashAsync(audioFile);

			// Assert - File should be modified
			Assert.That(modifiedHash, Is.Not.EqualTo(originalHash), "Large file should be modified");

			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			var checkpoint = checkpoints.First(c => c.Id == checkpointId);

			// Verify delta is much smaller than full file
			var modifiedFile = checkpoint.Modified.FirstOrDefault(m => m.Path.Contains("large_audio.wav"));
			Assert.That(modifiedFile, Is.Not.Null, "Should track large file modification");
			Assert.That(modifiedFile.ForwardDeltaSize, Is.LessThan(modifiedFile.TargetSize / 10), "Delta should be < 10% of file size");

			// Test restoration
			var baseline = checkpoints.First(c => c.Sequence == 0);
			await _checkpointService.RestoreCheckpointAsync(baseline.Id);

			string restoredHash = await ComputeFileHashAsync(audioFile);
			Assert.That(restoredHash, Is.EqualTo(originalHash), "Large file should be restored to original");
		}

		#endregion

		#region Storage Efficiency Tests

		[Test]
		public async Task CheckpointService_DeduplicatesIdenticalFiles()
		{
			// Arrange - Create two mods with identical file
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();

			string sharedFilePath = Path.Combine(_sourceDirectory.FullName, "shared_file.txt");
			File.WriteAllText(sharedFilePath, "SHARED_CONTENT");

			// Create mod 1
			var mod1 = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Mod 1",
				IsSelected = true,
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
				{
					new Instruction
					{
						Action = Instruction.ActionType.Copy,
						Source = new List<string> { sharedFilePath },
						Destination = Path.Combine(_gameDirectory.FullName, "Override", "file1.txt")
					}
				}
			};

			// Create mod 2 (different destination, same content)
			var mod2 = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Mod 2",
				IsSelected = true,
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
				{
					new Instruction
					{
						Action = Instruction.ActionType.Copy,
						Source = new List<string> { sharedFilePath },
						Destination = Path.Combine(_gameDirectory.FullName, "Override", "file2.txt")
					}
				}
			};

			// Act - Install both mods
			await mod1.ExecuteInstructionsAsync(mod1.Instructions, new List<ModComponent> { mod1 }, default, fileSystemProvider);
			await _checkpointService.CreateCheckpointAsync(mod1.Name, mod1.Guid.ToString());

			await mod2.ExecuteInstructionsAsync(mod2.Instructions, new List<ModComponent> { mod2 }, default, fileSystemProvider);
			await _checkpointService.CreateCheckpointAsync(mod2.Name, mod2.Guid.ToString());

			// Assert - Both files should reference same CAS object
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			var cp1 = checkpoints.First(c => c.ComponentName == "Mod 1");
			var cp2 = checkpoints.First(c => c.ComponentName == "Mod 2");

			string file1CASHash = cp1.Files.First(f => f.Key.Contains("file1.txt")).Value.CASHash;
			string file2CASHash = cp2.Files.First(f => f.Key.Contains("file2.txt")).Value.CASHash;

			Assert.That(file1CASHash, Is.EqualTo(file2CASHash), "Identical files should share CAS object");
		}

		[Test]
		public async Task CheckpointService_MeasuresStorageEfficiency()
		{
			// Arrange - Install 20 mods
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();

			for ( int i = 1; i <= 20; i++ )
			{
				var mod = CreateTestModComponent($"Mod {i}", i);
				await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
				await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());
			}

			// Act - Calculate storage metrics
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			long totalDeltaSize = checkpoints.Sum(c => c.DeltaSize);
			long totalFileSize = checkpoints.Sum(c => c.TotalSize);

			// Assert - Delta storage should be much smaller than full file storage
			Console.WriteLine($"Total file size: {totalFileSize:N0} bytes");
			Console.WriteLine($"Total delta size: {totalDeltaSize:N0} bytes");
			Console.WriteLine($"Storage efficiency: {(double)totalDeltaSize / totalFileSize * 100:F2}%");

			Assert.That(totalDeltaSize, Is.LessThan(totalFileSize), "Delta storage should be smaller than full storage");
		}

		#endregion

		#region Validation and Corruption Tests

		[Test]
		public async Task CheckpointService_ValidatesCheckpointIntegrity()
		{
			// Arrange
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var mod = CreateTestModComponent("Validation Test", 1);
			var fileSystemProvider = new RealFileSystemProvider();

			await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
			string checkpointId = await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());

			// Act - Validate checkpoint
			var (isValid, errors) = await _checkpointService.ValidateCheckpointAsync(checkpointId);

			// Assert
			Assert.That(isValid, Is.True, "Checkpoint should be valid");
			Assert.That(errors, Is.Empty, "Should have no errors");
		}

		[Test]
		public async Task CheckpointService_DetectsCorruptedCheckpoint()
		{
			// Arrange - Create checkpoint
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var mod = CreateTestModComponent("Corruption Test", 1);
			var fileSystemProvider = new RealFileSystemProvider();

			await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
			string checkpointId = await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());

			// Act - Corrupt CAS by deleting objects directory
			string casObjectsDir = Path.Combine(_gameDirectory.FullName, ".kotor_modsync", "checkpoints", "objects");
			if ( Directory.Exists(casObjectsDir) )
			{
				Directory.Delete(casObjectsDir, true);
			}

			// Validate corrupted checkpoint
			var (isValid, errors) = await _checkpointService.ValidateCheckpointAsync(checkpointId);

			// Assert
			Assert.That(isValid, Is.False, "Checkpoint should be invalid");
			Assert.That(errors, Is.Not.Empty, "Should have error messages");
			Assert.That(errors.Any(e => e.Contains("Missing CAS object")), Is.True, "Should detect missing CAS objects");
		}

		[Test]
		public async Task CheckpointService_ValidatesEntireSession()
		{
			// Arrange - Create multiple checkpoints
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();

			for ( int i = 1; i <= 5; i++ )
			{
				var mod = CreateTestModComponent($"Mod {i}", i);
				await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
				await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());
			}

			// Act - Validate entire session
			var (isValid, errorsByCheckpoint) = await _checkpointService.ValidateSessionAsync(_currentSessionId);

			// Assert
			Assert.That(isValid, Is.True, "Session should be valid");
			Assert.That(errorsByCheckpoint, Is.Empty, "Should have no checkpoint errors");
		}

		#endregion

		#region Garbage Collection Tests

		[Test]
		public async Task CheckpointService_GarbageCollectsOrphanedObjects()
		{
			// Arrange - Create session with checkpoints
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();

			for ( int i = 1; i <= 5; i++ )
			{
				var mod = CreateTestModComponent($"Mod {i}", i);
				await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
				await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());
			}

			// Act - Delete session (creates orphaned CAS objects)
			await _checkpointService.DeleteSessionAsync(_currentSessionId);

			// Run garbage collection
			int orphanedCount = await _checkpointService.GarbageCollectAsync();

			// Assert
			Assert.That(orphanedCount, Is.GreaterThan(0), "Should have collected orphaned objects");

			// Verify CAS objects directory is cleaned up
			string casDir = Path.Combine(_gameDirectory.FullName, ".kotor_modsync", "checkpoints", "objects");
			if ( Directory.Exists(casDir) )
			{
				int remainingObjects = Directory.GetFiles(casDir, "*", SearchOption.AllDirectories).Length;
				Assert.That(remainingObjects, Is.EqualTo(0), "All orphaned objects should be removed");
			}
		}

		#endregion

		#region Session Management Tests

		[Test]
		public async Task CheckpointService_ListsMultipleSessions()
		{
			// Arrange - Create multiple sessions
			var session1Id = await _checkpointService.StartInstallationSessionAsync();
			var mod1 = CreateTestModComponent("Session 1 Mod", 1);
			await mod1.ExecuteInstructionsAsync(mod1.Instructions, new List<ModComponent> { mod1 }, default, new RealFileSystemProvider());
			await _checkpointService.CreateCheckpointAsync(mod1.Name, mod1.Guid.ToString());
			await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);

			// Create second session
			var session2Id = await _checkpointService.StartInstallationSessionAsync();
			var mod2 = CreateTestModComponent("Session 2 Mod", 2);
			await mod2.ExecuteInstructionsAsync(mod2.Instructions, new List<ModComponent> { mod2 }, default, new RealFileSystemProvider());
			await _checkpointService.CreateCheckpointAsync(mod2.Name, mod2.Guid.ToString());

			// Act
			var sessions = await _checkpointService.ListSessionsAsync();

			// Assert
			Assert.That(sessions, Has.Count.EqualTo(2), "Should have two sessions");
			Assert.That(sessions.Any(s => s.Id == session1Id), Is.True, "Should contain session 1");
			Assert.That(sessions.Any(s => s.Id == session2Id), Is.True, "Should contain session 2");
		}

		[Test]
		public async Task CheckpointService_DeletesSpecificSession()
		{
			// Arrange - Create two sessions
			var session1Id = await _checkpointService.StartInstallationSessionAsync();
			var mod1 = CreateTestModComponent("Session 1 Mod", 1);
			await mod1.ExecuteInstructionsAsync(mod1.Instructions, new List<ModComponent> { mod1 }, default, new RealFileSystemProvider());
			await _checkpointService.CreateCheckpointAsync(mod1.Name, mod1.Guid.ToString());
			await _checkpointService.CompleteSessionAsync(keepCheckpoints: true);

			var session2Id = await _checkpointService.StartInstallationSessionAsync();
			var mod2 = CreateTestModComponent("Session 2 Mod", 2);
			await mod2.ExecuteInstructionsAsync(mod2.Instructions, new List<ModComponent> { mod2 }, default, new RealFileSystemProvider());
			await _checkpointService.CreateCheckpointAsync(mod2.Name, mod2.Guid.ToString());

			// Act - Delete session 1
			await _checkpointService.DeleteSessionAsync(session1Id);

			// Assert
			var sessions = await _checkpointService.ListSessionsAsync();
			Assert.That(sessions, Has.Count.EqualTo(1), "Should have one session remaining");
			Assert.That(sessions[0].Id, Is.EqualTo(session2Id), "Session 2 should remain");
		}

		#endregion

		#region Edge Case Tests

		[Test]
		public async Task CheckpointService_HandlesEmptyModComponent()
		{
			// Arrange - Mod with no instructions
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var emptyMod = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Empty Mod",
				IsSelected = true,
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>()
			};

			// Act
			await emptyMod.ExecuteInstructionsAsync(emptyMod.Instructions, new List<ModComponent> { emptyMod }, default, new RealFileSystemProvider());
			string checkpointId = await _checkpointService.CreateCheckpointAsync(emptyMod.Name, emptyMod.Guid.ToString());

			// Assert - Should create checkpoint with no changes
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			var checkpoint = checkpoints.First(c => c.Id == checkpointId);

			Assert.That(checkpoint, Is.Not.Null, "Should create checkpoint even for empty mod");
			Assert.That(checkpoint.Added, Is.Empty, "Should have no added files");
			Assert.That(checkpoint.Modified, Is.Empty, "Should have no modified files");
			Assert.That(checkpoint.Deleted, Is.Empty, "Should have no deleted files");
		}

		[Test]
		public async Task CheckpointService_HandlesFileDeletion()
		{
			// Arrange - Install mod, then delete a file
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();

			var mod1 = CreateTestModComponent("Add Files Mod", 1);
			await mod1.ExecuteInstructionsAsync(mod1.Instructions, new List<ModComponent> { mod1 }, default, fileSystemProvider);
			await _checkpointService.CreateCheckpointAsync(mod1.Name, mod1.Guid.ToString());

			// Create mod that deletes a file
			string fileToDelete = Path.Combine(_gameDirectory.FullName, "Override", "texture_1.tga");
			var deleteMod = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Delete Files Mod",
				IsSelected = true,
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
				{
					new Instruction
					{
						Action = Instruction.ActionType.Delete,
						Source = new List<string> { fileToDelete }
					}
				}
			};

			// Act
			await deleteMod.ExecuteInstructionsAsync(deleteMod.Instructions, new List<ModComponent> { deleteMod }, default, fileSystemProvider);
			string checkpointId = await _checkpointService.CreateCheckpointAsync(deleteMod.Name, deleteMod.Guid.ToString());

			// Assert
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			var checkpoint = checkpoints.First(c => c.Id == checkpointId);

			Assert.That(checkpoint.Deleted, Has.Count.EqualTo(1), "Should track deleted file");
			Assert.That(File.Exists(fileToDelete), Is.False, "File should be deleted");

			// Test restoration
			var mod1Checkpoint = checkpoints.First(c => c.ComponentName == "Add Files Mod");
			await _checkpointService.RestoreCheckpointAsync(mod1Checkpoint.Id);

			Assert.That(File.Exists(fileToDelete), Is.True, "File should be restored");
		}

		[Test]
		public async Task CheckpointService_HandlesConcurrentFileModifications()
		{
			// Arrange - Multiple mods modifying same file
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();

			// Act - Install 5 mods that all modify appearance.2da
			for ( int i = 1; i <= 5; i++ )
			{
				var mod = CreateTestModComponent($"Mod {i}", i);
				await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);
				await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());
			}

			// Assert - Each checkpoint should track the modification
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			var modCheckpoints = checkpoints.Where(c => c.Sequence > 0).ToList();

			foreach ( var checkpoint in modCheckpoints )
			{
				var appearance2daChange = checkpoint.Modified.FirstOrDefault(m => m.Path.Contains("appearance.2da"));
				Assert.That(appearance2daChange, Is.Not.Null, $"Checkpoint {checkpoint.Sequence} should track appearance.2da modification");
			}

			// Test restoration to each checkpoint
			for ( int i = modCheckpoints.Count - 1; i >= 1; i-- )
			{
				await _checkpointService.RestoreCheckpointAsync(modCheckpoints[i - 1].Id);
				bool stateMatches = await VerifyGameStateAsync(modCheckpoints[i - 1]);
				Assert.That(stateMatches, Is.True, $"Should correctly restore to checkpoint {i}");
			}
		}

		#endregion

	#region Performance Tests

	[Test]
	[Category("Performance")]
	[CancelAfter(300000)] // 5 minutes
	public async Task CheckpointService_PerformanceTest_50Mods()
		{
			// Arrange
			_currentSessionId = await _checkpointService.StartInstallationSessionAsync();
			var fileSystemProvider = new RealFileSystemProvider();
			int modCount = 50;
			var timings = new List<TimeSpan>();

			// Act - Install 50 mods and measure checkpoint creation time
			for ( int i = 1; i <= modCount; i++ )
			{
				var mod = CreateTestModComponent($"Performance Mod {i}", i);
				await mod.ExecuteInstructionsAsync(mod.Instructions, new List<ModComponent> { mod }, default, fileSystemProvider);

				var sw = System.Diagnostics.Stopwatch.StartNew();
				await _checkpointService.CreateCheckpointAsync(mod.Name, mod.Guid.ToString());
				sw.Stop();

				timings.Add(sw.Elapsed);
			}

			// Assert
			double avgSeconds = timings.Average(t => t.TotalSeconds);
			double maxSeconds = timings.Max(t => t.TotalSeconds);

			Console.WriteLine($"Average checkpoint creation time: {avgSeconds:F2} seconds");
			Console.WriteLine($"Max checkpoint creation time: {maxSeconds:F2} seconds");

			Assert.That(avgSeconds, Is.LessThan(15.0), "Average checkpoint time should be < 15 seconds");
			Assert.That(maxSeconds, Is.LessThan(60.0), "Max checkpoint time should be < 60 seconds");

			// Test restore performance
			var checkpoints = await _checkpointService.ListCheckpointsAsync(_currentSessionId);
			var targetCheckpoint = checkpoints.First(c => c.Sequence == 25);

			var restoreSw = System.Diagnostics.Stopwatch.StartNew();
			await _checkpointService.RestoreCheckpointAsync(targetCheckpoint.Id);
			restoreSw.Stop();

			Console.WriteLine($"Restore time (checkpoint 50 → 25): {restoreSw.Elapsed.TotalSeconds:F2} seconds");
			Assert.That(restoreSw.Elapsed.TotalSeconds, Is.LessThan(120.0), "Restore should complete within 2 minutes");
		}

		#endregion
	}
}

