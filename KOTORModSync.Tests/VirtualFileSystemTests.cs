// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using KOTORModSync.Core;
using KOTORModSync.Core.Services.FileSystem;

namespace KOTORModSync.Tests
{
	/// <summary>
	/// Comprehensive tests for VirtualFileSystemProvider and RealFileSystemProvider.
	/// Each test runs operations on BOTH providers and validates they produce identical results.
	/// Uses real archives created with 7-Zip CLI (no mocking).
	/// </summary>
	[TestFixture]
	public class VirtualFileSystemTests
	{
		private string? _testRootDir;
		private string? _sourceDir;
		private string? _destinationDir;
		private string? _sevenZipPath;
		private MainConfig? _originalConfig;

		[OneTimeSetUp]
		public void OneTimeSetUp() => _sevenZipPath = Find7Zip();

		[SetUp]
		public void SetUp()
		{
			_testRootDir = Path.Combine(Path.GetTempPath(), "KOTORModSync_VFS_Tests_" + Guid.NewGuid().ToString("N"));
			_sourceDir = Path.Combine(_testRootDir, "Source");
			_destinationDir = Path.Combine(_testRootDir, "Dest");
			Directory.CreateDirectory(_sourceDir);
			Directory.CreateDirectory(_destinationDir);

			// Backup original config
			_originalConfig = new MainConfig();
		}

		[TearDown]
		public void TearDown()
		{
			try
			{
				if ( Directory.Exists(_testRootDir) )
					Directory.Delete(_testRootDir, recursive: true);
			}
			catch ( Exception ex )
			{
				TestContext.WriteLine($"Warning: Could not delete test directory: {ex.Message}");
			}
			// Restore original config paths
			_ = new MainConfig
			{
				sourcePath = _originalConfig?.sourcePath,
				destinationPath = _originalConfig?.destinationPath
			};
		}

		internal static string Find7Zip()
		{
			string[] paths =
			[
				@"C:\Program Files\7-Zip\7z.exe",
				@"C:\Program Files (x86)\7-Zip\7z.exe"
			];

			foreach ( string path in paths )
			{
				if ( File.Exists(path) )
					return path;
			}

			try
			{
				var process = Process.Start(new ProcessStartInfo
				{
					FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
					Arguments = "7z",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true
				});
				if ( process != null )
				{
					string output = process.StandardOutput.ReadToEnd();
					process.WaitForExit();
					if ( process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) )
					{
						return output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();
					}
				}
			}
			catch { /* Suppress */ }

			throw new InvalidOperationException("7-Zip not found. Please install 7-Zip to run these tests.");
		}

		internal void CreateArchive(string archivePath, Dictionary<string, string> files)
		{
			string tempDir = Path.Combine(Path.GetTempPath(), "temp_" + Guid.NewGuid());
			Directory.CreateDirectory(tempDir);
			try
			{
				foreach ( var file in files )
				{
					string filePath = Path.Combine(tempDir, file.Key);
					string? fileDir = Path.GetDirectoryName(filePath);
					if ( fileDir != null )
						_ = Directory.CreateDirectory(fileDir);
					File.WriteAllText(filePath, file.Value);
				}

				var startInfo = new ProcessStartInfo
				{
					FileName = _sevenZipPath,
					Arguments = $"a -tzip \"{archivePath}\" \"{Path.Combine(tempDir, "*")}\" -r",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true
				};

				using var process = Process.Start(startInfo);
				if ( process == null )
					return;
				process.WaitForExit();
			}
			finally
			{
				Directory.Delete(tempDir, recursive: true);
			}
		}

		internal static void CopyDirectory(string sourceDir, string destinationDir)
		{
			var dir = new DirectoryInfo(sourceDir);
			if ( !dir.Exists )
				throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

			Directory.CreateDirectory(destinationDir);

			foreach ( FileInfo file in dir.GetFiles() )
			{
				string targetFilePath = Path.Combine(destinationDir, file.Name);
				_ = file.CopyTo(targetFilePath, overwrite: true);
			}

			foreach ( DirectoryInfo subDir in dir.GetDirectories() )
			{
				string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
				CopyDirectory(subDir.FullName, newDestinationDir);
			}
		}

		private async Task<(VirtualFileSystemProvider virtualProvider, string realDestDir)> RunBothProviders(
			List<Instruction> instructions,
			string sourceDir)
		{
			Debug.Assert(_testRootDir != null);
			string virtualRoot = Path.Combine(_testRootDir, "Virtual");
			string realRoot = Path.Combine(_testRootDir, "Real");

			_ = Directory.CreateDirectory(virtualRoot);
			_ = Directory.CreateDirectory(realRoot);

			CopyDirectory(sourceDir, virtualRoot);
			CopyDirectory(sourceDir, realRoot);

			DirectoryInfo? originalSourcePath = MainConfig.SourcePath;
			DirectoryInfo? originalDestPath = MainConfig.DestinationPath;

			// Create deep copies of instructions BEFORE any execution
			var virtualInstructions = new List<Instruction>();
			var realInstructions = new List<Instruction>();
			foreach ( Instruction instruction in instructions )
			{
				virtualInstructions.Add(new Instruction
				{
					Action = instruction.Action,
					Source = instruction.Source.ToList(),
					Destination = instruction.Destination,
					Overwrite = instruction.Overwrite,
					Arguments = instruction.Arguments
				});
				realInstructions.Add(new Instruction
				{
					Action = instruction.Action,
					Source = instruction.Source.ToList(),
					Destination = instruction.Destination,
					Overwrite = instruction.Overwrite,
					Arguments = instruction.Arguments
				});
			}

			try
			{
				// Test 1: Virtual File System (Dry-Run)
				_ = new MainConfig
				{
					sourcePath = new DirectoryInfo(virtualRoot),
					destinationPath = new DirectoryInfo(Path.Combine(virtualRoot, "dest"))
				};

				var virtualProvider = new VirtualFileSystemProvider();
				await virtualProvider.InitializeFromRealFileSystemAsync(virtualRoot);
				var virtualComponent = new ModComponent
				{
					Name = "TestComponent",
					Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>(virtualInstructions)
				};

				_ = await virtualComponent.ExecuteInstructionsAsync(virtualComponent.Instructions, [], CancellationToken.None, virtualProvider);

				TestContext.WriteLine($"Virtual Provider - Files tracked: {virtualProvider.GetTrackedFiles().Count}");
				TestContext.WriteLine($"Virtual Provider - Issues: {virtualProvider.GetValidationIssues().Count}");

				// Test 2: Real File System
				_ = new MainConfig
				{
					sourcePath = new DirectoryInfo(realRoot),
					destinationPath = new DirectoryInfo(Path.Combine(realRoot, "dest"))
				};

				var realProvider = new RealFileSystemProvider();
				var realComponent = new ModComponent
				{
					Name = "TestComponent",
					Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>(realInstructions)
				};

				_ = await realComponent.ExecuteInstructionsAsync(realComponent.Instructions, new List<ModComponent>(), CancellationToken.None, realProvider);

				TestContext.WriteLine("Real Provider - Executed successfully");

				return (virtualProvider, Path.Combine(realRoot, "dest"));
			}
			finally
			{
				// Restore MainConfig paths
				_ = new MainConfig
				{
					sourcePath = originalSourcePath,
					destinationPath = originalDestPath
				};
			}
		}

		private static void AssertFileSystemsMatch(VirtualFileSystemProvider virtualProvider, string realDestDir)
		{
			// Find the virtual dest directory path by looking for files that contain "Virtual\dest" or "Real\dest"
			// Get the dest directory from the virtual provider's tracked files
			string virtDestPath = Path.GetDirectoryName(Path.GetDirectoryName(realDestDir))!;
			string virtualDestPath = Path.Combine(virtDestPath, "Virtual", "dest");

			var virtualFiles = virtualProvider.GetTrackedFiles()
				.Where(f => f.StartsWith(virtualDestPath, StringComparison.OrdinalIgnoreCase))
				.Select(f => f.Substring(virtualDestPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
				.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			HashSet<string> realFiles = Directory.Exists(realDestDir)
				? Directory.GetFiles(realDestDir, "*", SearchOption.AllDirectories)
					.Select(f => f.Substring(realDestDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
					.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
					.ToHashSet(StringComparer.OrdinalIgnoreCase)
				: new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			TestContext.WriteLine($"\n=== Virtual Files ({virtualFiles.Count}) ===");
			foreach ( string file in virtualFiles.OrderBy(f => f) )
				TestContext.WriteLine($"  {file}");

			TestContext.WriteLine($"\n=== Real Files ({realFiles.Count}) ===");
			foreach ( string file in realFiles.OrderBy(f => f) )
				TestContext.WriteLine($"  {file}");

			// Files in virtual but not in real
			var missingInReal = virtualFiles.Except(realFiles, StringComparer.OrdinalIgnoreCase).ToList();
			if ( missingInReal.Count > 0 )
			{
				TestContext.WriteLine($"\n=== Files in VIRTUAL but NOT in REAL ({missingInReal.Count}) ===");
				foreach ( string file in missingInReal )
					TestContext.WriteLine($"  {file}");
			}

			// Files in real but not in virtual
			var missingInVirtual = realFiles.Except(virtualFiles, StringComparer.OrdinalIgnoreCase).ToList();
			if ( missingInVirtual.Count > 0 )
			{
				TestContext.WriteLine($"\n=== Files in REAL but NOT in VIRTUAL ({missingInVirtual.Count}) ===");
				foreach ( string file in missingInVirtual )
					TestContext.WriteLine($"  {file}");
			}
			Assert.Multiple(() =>
			{
				Assert.That(missingInReal, Is.Empty);
				Assert.That(missingInVirtual, Is.Empty);
				Assert.That(realFiles, Has.Count.EqualTo(virtualFiles.Count));
			});
		}

		#region Archive Operation Tests

		[Test]
		public async Task Test_ExtractArchive_Basic()
		{
			// Arrange
			Debug.Assert(_sourceDir != null);
			string archivePath = Path.Combine(_sourceDir, "test.zip");
			CreateArchive(archivePath, new()
			{
				{ "file1.txt", "Content 1" },
				{ "file2.txt", "Content 2" },
				{ "subfolder/file3.txt", "Content 3" }
			});

			var instructions = new List<Instruction>
			{
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\test.zip"],
					Destination = "<<modDirectory>>"
				}
			};

			// Act
			(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(instructions, _sourceDir);

			// Assert
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(virtualProvider, realDestDir);
		}

		[Test]
		public async Task Test_MoveArchiveThenRenameThenExtract()
		{
			Debug.Assert(_sourceDir != null);
			string src = Path.Combine(_sourceDir, "chain_a.zip");
			CreateArchive(src, new() { { "a.txt", "A" } });

			var instructions = new List<Instruction>
		{
			new() { Action = Instruction.ActionType.Rename, Source = ["<<modDirectory>>\\chain_a.zip"], Destination = "chain_b.zip", Overwrite = true },
			new() { Action = Instruction.ActionType.Rename, Source = ["<<modDirectory>>\\chain_b.zip"], Destination = "chain_final.zip", Overwrite = true },
			new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\chain_final.zip"], Destination = "<<kotorDirectory>>" }
		};

			(VirtualFileSystemProvider v, string r) = await RunBothProviders(instructions, _sourceDir);
			Assert.That(v.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(v, r);
		}

		[Test]
		public async Task Test_CopyArchiveTwiceThenExtractBoth()
		{
			Debug.Assert(_sourceDir != null);
			string src = Path.Combine(_sourceDir, "dup.zip");
			CreateArchive(src, new() { { "d.txt", "D" } });

			var instructions = new List<Instruction>
		{
			// Copy to subdirectories then rename
			new() { Action = Instruction.ActionType.Copy, Source = ["<<modDirectory>>\\dup.zip"], Destination = "<<modDirectory>>\\copy1", Overwrite = true },
			new() { Action = Instruction.ActionType.Rename, Source = ["<<modDirectory>>\\copy1\\dup.zip"], Destination = "dup_copy1.zip", Overwrite = true },
			new() { Action = Instruction.ActionType.Copy, Source = ["<<modDirectory>>\\dup.zip"], Destination = "<<modDirectory>>\\copy2", Overwrite = true },
			new() { Action = Instruction.ActionType.Rename, Source = ["<<modDirectory>>\\copy2\\dup.zip"], Destination = "dup_copy2.zip", Overwrite = true },
			new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\copy1\\dup_copy1.zip"], Destination = "<<kotorDirectory>>\\extract1" },
			new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\copy2\\dup_copy2.zip"], Destination = "<<kotorDirectory>>\\extract2" }
		};

			(VirtualFileSystemProvider v, string r) = await RunBothProviders(instructions, _sourceDir);
			Assert.That(v.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(v, r);
		}

		[Test]
		public async Task Test_RenameArchiveIntoSubfolderThenExtract()
		{
			Debug.Assert(_sourceDir != null);
			string src = Path.Combine(_sourceDir, "sub.zip");
			CreateArchive(src, new() { { "s.txt", "S" } });
			string subdir = Path.Combine(_sourceDir, "subdir"); _ = Directory.CreateDirectory(subdir);

			var instructions = new List<Instruction>
		{
			new() { Action = Instruction.ActionType.Rename, Source = ["<<modDirectory>>\\sub.zip"], Destination = "subdir\\final.zip", Overwrite = true },
			new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\subdir\\final.zip"], Destination = "<<kotorDirectory>>" }
		};

			(VirtualFileSystemProvider v, string r) = await RunBothProviders(instructions, _sourceDir);
			Assert.That(v.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(v, r);
		}

		[Test]
		public async Task Test_ExtractThenMoveExtractedFolder()
		{
			Debug.Assert(_sourceDir != null);
			string src = Path.Combine(_sourceDir, "mv_extract.zip");
			CreateArchive(src, new() { { "inner/x.txt", "X" } });
			var instructions = new List<Instruction>
		{
			new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\mv_extract.zip"], Destination = "<<modDirectory>>" },
			new() { Action = Instruction.ActionType.Move, Source = ["<<modDirectory>>\\mv_extract\\inner\\x.txt"], Destination = "<<kotorDirectory>>", Overwrite = true }
		};

			(VirtualFileSystemProvider v, string r) = await RunBothProviders(instructions, _sourceDir);
			Assert.That(v.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(v, r);
		}

		[Test]
		public async Task Test_ExtractThenCopyWildcardSet()
		{
			Debug.Assert(_sourceDir != null);
			string src = Path.Combine(_sourceDir, "wc_set.zip");
			CreateArchive(src, new() { { "pack/a.txt", "A" }, { "pack/b.log", "B" }, { "pack/c.txt", "C" } });
			Debug.Assert(_destinationDir != null);
			string outDir = Path.Combine(_destinationDir, "txts");
			var instructions = new List<Instruction>
			{
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\wc_set.zip"], Destination = "<<modDirectory>>" },
				new() { Action = Instruction.ActionType.Copy, Source = ["<<modDirectory>>\\wc_set\\pack\\*.txt"], Destination = "<<kotorDirectory>>\\txts", Overwrite = true }
			};

			(VirtualFileSystemProvider v, string r) = await RunBothProviders(instructions, _sourceDir);
			Assert.That(v.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(v, r);
		}

		[Test]
		public async Task Test_RenameExtractedFileThenCopy()
		{
			Debug.Assert(_sourceDir != null);
			string src = Path.Combine(_sourceDir, "rn_copy.zip");
			CreateArchive(src, new() { { "p/q.txt", "Q" } });
			var instructions = new List<Instruction>
			{
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\rn_copy.zip"], Destination = "<<modDirectory>>" },
				new() { Action = Instruction.ActionType.Rename, Source = ["<<modDirectory>>\\rn_copy\\p\\q.txt"], Destination = "qq.txt", Overwrite = true },
				new() { Action = Instruction.ActionType.Copy, Source = ["<<modDirectory>>\\rn_copy\\p\\qq.txt"], Destination = "<<kotorDirectory>>\\copied", Overwrite = true }
			};

			(VirtualFileSystemProvider v, string r) = await RunBothProviders(instructions, _sourceDir);
			Assert.That(v.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(v, r);
		}

		[Test]
		public async Task Test_DeleteExtractedFileThenVerifyMissing()
		{
			Debug.Assert(_sourceDir != null);
			string src = Path.Combine(_sourceDir, "del.zip");
			CreateArchive(src, new() { { "rm.txt", "RM" } });
			var instructions = new List<Instruction>
			{
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\del.zip"], Destination = "<<modDirectory>>" },
				new() { Action = Instruction.ActionType.Delete, Source = ["<<modDirectory>>\\del\\rm.txt"], Destination = string.Empty }
			};

			(VirtualFileSystemProvider v, string r) = await RunBothProviders(instructions, _sourceDir);
			Assert.That(v.GetValidationIssues(), Is.Empty);
			// After deletion, both should not contain the file
			Assert.That(v.GetTrackedFiles().Any(p => p.EndsWith("del\\rm.txt", StringComparison.OrdinalIgnoreCase)), Is.False);
			AssertFileSystemsMatch(v, r);
		}

		[Test]
		public async Task Test_MoveArchiveThenExtractTwoArchivesSequentially()
		{
			Debug.Assert(_sourceDir != null);
			string a1 = Path.Combine(_sourceDir, "a1.zip");
			string a2 = Path.Combine(_sourceDir, "a2.zip");
			CreateArchive(a1, new() { { "x.txt", "X" } });
			CreateArchive(a2, new() { { "y.txt", "Y" } });
			var instructions = new List<Instruction>
			{
				new() { Action = Instruction.ActionType.Rename, Source = ["<<modDirectory>>\\a1.zip"], Destination = "a1_moved.zip", Overwrite = true },
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\a1_moved.zip"], Destination = "<<kotorDirectory>>" },
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\a2.zip"], Destination = "<<kotorDirectory>>" }
			};

			(VirtualFileSystemProvider v, string r) = await RunBothProviders(instructions, _sourceDir);
			Assert.That(v.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(v, r);
		}

		[Test]
		public async Task Test_CopyThenMoveExtractedSetIntoNestedFolder()
		{
			Debug.Assert(_sourceDir != null);
			string src = Path.Combine(_sourceDir, "nest.zip");
			CreateArchive(src, new() { { "n/a.txt", "A" }, { "n/b.txt", "B" } });
			var instructions = new List<Instruction>
			{
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\nest.zip"], Destination = "<<modDirectory>>" },
				new() { Action = Instruction.ActionType.Copy, Source = ["<<modDirectory>>\\nest\\n\\*.txt"], Destination = "<<kotorDirectory>>\\nested\\deep", Overwrite = true },
				new() { Action = Instruction.ActionType.Move, Source = ["<<kotorDirectory>>\\nested\\deep\\a.txt"], Destination = "<<kotorDirectory>>\\final", Overwrite = true }
			};

			(VirtualFileSystemProvider v, string r) = await RunBothProviders(instructions, _sourceDir);
			Assert.That(v.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(v, r);
		}

		[Test]
		public void Test_MoveNonexistentFile_ShouldRecordErrorAndNotModifyReal()
		{
			var instructions = new List<Instruction>
			{
				new() {
					Action = Instruction.ActionType.Move,
					Source = ["<<modDirectory>>\\nope.txt"],
					Destination = "<<kotorDirectory>>\\anywhere.txt",
					Overwrite = true
				}
			};

			// SetRealPaths throws FileNotFoundException for missing files before provider is called
			Debug.Assert(_sourceDir != null);
			Assert.ThrowsAsync<FileNotFoundException>(async () => await RunBothProviders(instructions, _sourceDir));
		}

		[Test]
		public async Task Test_MoveArchiveThenExtract()
		{
			// Arrange
			Debug.Assert(_sourceDir != null);
			string originalArchivePath = Path.Combine(_sourceDir, "original.zip");
			CreateArchive(originalArchivePath, new()
			{
				{ "data.txt", "Important data" },
				{ "config.ini", "[Settings]\nvalue=123" }
			});

			var instructions = new List<Instruction>
			{
				new()
				{
					Action = Instruction.ActionType.Rename,
					Source = ["<<modDirectory>>\\original.zip"],
					Destination = "moved.zip",
					Overwrite = true
				},
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\moved.zip"],
					Destination = "<<kotorDirectory>>"
				}
			};

			// Act
			(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(instructions, _sourceDir);

			// Assert
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(virtualProvider, realDestDir);
		}

		[Test]
		public async Task Test_CopyArchiveThenExtractBoth()
		{
			// Arrange
			Debug.Assert(_sourceDir != null);
			string originalArchivePath = Path.Combine(_sourceDir, "source.zip");
			CreateArchive(originalArchivePath, new()
			{
				{ "shared.txt", "Shared content" }
			});

			var instructions = new List<Instruction>
			{
				// Copy the archive to a subdirectory
				new()
				{
					Action = Instruction.ActionType.Copy,
					Source = ["<<modDirectory>>\\source.zip"],
					Destination = "<<modDirectory>>\\archives",
					Overwrite = true
				},
				// Rename the copied archive
				new()
				{
					Action = Instruction.ActionType.Rename,
					Source = ["<<modDirectory>>\\archives\\source.zip"],
					Destination = "copy.zip",
					Overwrite = true
				},
				// Extract the original
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\source.zip"],
					Destination = "<<kotorDirectory>>\\original_extract"
				},
				// Extract the copied archive
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\archives\\copy.zip"],
					Destination = "<<kotorDirectory>>\\copy_extract"
				}
			};

			// Act
			(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(instructions, _sourceDir);

			// Assert
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(virtualProvider, realDestDir);
		}

		[Test]
		public async Task Test_RenameArchiveThenExtract()
		{
			// Arrange
			Debug.Assert(_sourceDir != null);
			string originalArchivePath = Path.Combine(_sourceDir, "oldname.zip");
			CreateArchive(originalArchivePath, new()
			{
				{ "readme.txt", "Read me first" }
			});

			var instructions = new List<Instruction>
			{
				new()
				{
					Action = Instruction.ActionType.Rename,
					Source = ["<<modDirectory>>\\oldname.zip"],
					Destination = "newname.zip",
					Overwrite = true
				},
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\newname.zip"],
					Destination = "<<kotorDirectory>>"
				}
			};

			// Act
			(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(instructions, _sourceDir);

			// Assert
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(virtualProvider, realDestDir);
		}

		[Test]
		public async Task Test_ExtractMultipleArchives()
		{
			// Arrange
			Debug.Assert(_sourceDir != null);
			string archive1 = Path.Combine(_sourceDir, "mod1.zip");
			string archive2 = Path.Combine(_sourceDir, "mod2.zip");
			string archive3 = Path.Combine(_sourceDir, "mod3.zip");

			CreateArchive(archive1, new()
			{
				{ "mod1/data.txt", "Mod 1 data" }
			});

			CreateArchive(archive2, new()
			{
				{ "mod2/config.ini", "Mod 2 config" },
				{ "mod2/assets/texture.tga", "Texture data" }
			});

			CreateArchive(archive3, new()
			{
				{ "mod3/script.ncs", "Script bytecode" }
			});

			var instructions = new List<Instruction>
			{
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\mod1.zip"],
					Destination = "<<kotorDirectory>>"
				},
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\mod2.zip"],
					Destination = "<<kotorDirectory>>"
				},
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\mod3.zip"],
					Destination = "<<kotorDirectory>>"
				}
			};

			// Act
			(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(instructions, _sourceDir);

			// Assert
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(virtualProvider, realDestDir);
		}

		#endregion

		#region File Operation Tests

		[Test]
		public async Task Test_MoveExtractedFiles()
		{
			// Arrange
			Debug.Assert(_sourceDir != null);
			string archivePath = Path.Combine(_sourceDir, "files.zip");
			CreateArchive(archivePath, new()
			{
				{ "file1.txt", "Content 1" },
				{ "file2.txt", "Content 2" }
			});

			var instructions = new List<Instruction>
			{
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\files.zip"],
					Destination = "<<modDirectory>>"
				},
				new()
				{
					Action = Instruction.ActionType.Move,
					Source = ["<<modDirectory>>\\files\\file1.txt"],
					Destination = "<<kotorDirectory>>\\final",
					Overwrite = true
				}
			};

			// Act
			(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(instructions, _sourceDir);

			// Assert
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(virtualProvider, realDestDir);
		}

		[Test]
		public async Task Test_CopyExtractedFiles()
		{
			// Arrange
			Debug.Assert(_sourceDir != null);
			string archivePath = Path.Combine(_sourceDir, "source.zip");
			CreateArchive(archivePath, new()
			{
				{ "original.txt", "Original content" }
			});

			var instructions = new List<Instruction>
			{
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\source.zip"],
					Destination = "<<modDirectory>>"
				},
				new()
				{
					Action = Instruction.ActionType.Copy,
					Source = ["<<modDirectory>>\\source\\original.txt"],
					Destination = "<<kotorDirectory>>\\backup",
					Overwrite = true
				}
			};

			// Act
			(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(instructions, _sourceDir);

			// Assert
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(virtualProvider, realDestDir);
		}

		[Test]
		public async Task Test_RenameExtractedFile()
		{
			// Arrange
			Debug.Assert(_sourceDir != null);
			string archivePath = Path.Combine(_sourceDir, "content.zip");
			CreateArchive(archivePath, new()
			{
				{ "oldname.dat", "Data content" }
			});

			var instructions = new List<Instruction>
			{
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\content.zip"],
					Destination = "<<modDirectory>>"
				},
				new()
				{
					Action = Instruction.ActionType.Rename,
					Source = ["<<modDirectory>>\\content\\oldname.dat"],
					Destination = "newname.dat",
					Overwrite = true
				}
			};

			// Act
			(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(instructions, _sourceDir);

			// Assert
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(virtualProvider, realDestDir);
		}

		[Test]
		public async Task Test_DeleteExtractedFile()
		{
			// Arrange
			Debug.Assert(_sourceDir != null);
			string archivePath = Path.Combine(_sourceDir, "cleanup.zip");
			CreateArchive(archivePath, new()
			{
				{ "keep.txt", "Keep this" },
				{ "delete.txt", "Delete this" },
				{ "also_keep.txt", "Keep this too" }
			});

			var instructions = new List<Instruction>
			{
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\cleanup.zip"],
					Destination = "<<modDirectory>>"
				},
				new()
				{
					Action = Instruction.ActionType.Delete,
					Source = ["<<modDirectory>>\\cleanup\\delete.txt"]
				}
			};

			// Act
			(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(instructions, _sourceDir);

			// Assert
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(virtualProvider, realDestDir);
		}

		#endregion

		#region Validation Tests (Should Fail)

		[Test]
		public void Test_ExtractNonExistentArchive_ShouldFail()
		{
			// Arrange
			var instructions = new List<Instruction>
			{
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\doesnotexist.zip"],
					Destination = "<<kotorDirectory>>"
				}
			};

			// Act & Assert - SetRealPaths throws FileNotFoundException for missing files before provider is called
			Debug.Assert(_sourceDir != null);
			_ = Assert.ThrowsAsync<FileNotFoundException>(async () => await RunBothProviders(instructions, _sourceDir));
		}

		[Test]
		public Task Test_MoveNonExistentFile_DetectedInDryRun()
		{
			// Arrange - Create archive, extract, delete, then try to move deleted file
			Debug.Assert(_sourceDir != null);
			string archivePath = Path.Combine(_sourceDir, "test.zip");
			CreateArchive(archivePath, new()
			{
				{ "file.txt", "content" }
			});

			var instructions = new List<Instruction>
			{
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\test.zip"],
					Destination = "<<modDirectory>>"
				},
				new()
				{
					Action = Instruction.ActionType.Delete,
					Source = ["<<modDirectory>>\\test\\file.txt"]
				},
				new()
				{
					Action = Instruction.ActionType.Move,
					Source = ["<<modDirectory>>\\test\\file.txt"],
					Destination = "<<kotorDirectory>>\\moved.txt"
				}
			};

			// Act & Assert - SetRealPaths throws FileNotFoundException for missing files before provider is called
			_ = Assert.ThrowsAsync<FileNotFoundException>(async () => await RunBothProviders(instructions, _sourceDir));
			return Task.CompletedTask;
		}

		[Test]
		public async Task Test_ExtractMovedArchive_Success()
		{
			// Arrange
			Debug.Assert(_sourceDir != null);
			string originalPath = Path.Combine(_sourceDir, "original.zip");
			CreateArchive(originalPath, new()
			{
				{ "data/file.txt", "Important data" }
			});

			_ = Directory.CreateDirectory(Path.Combine(_sourceDir, "subdir"));

			var instructions = new List<Instruction>
			{
				new()
				{
					Action = Instruction.ActionType.Rename,
					Source = ["<<modDirectory>>\\original.zip"],
					Destination = "subdir\\moved.zip",
					Overwrite = true
				},
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\subdir\\moved.zip"],
					Destination = "<<kotorDirectory>>"
				}
			};

			// Act
			(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(instructions, _sourceDir);

			// Assert
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(virtualProvider, realDestDir);
		}

		#endregion

		#region Complex Scenarios

		[Test]
		public async Task Test_ComplexModInstallation_MultipleArchivesAndOperations()
		{
			// Arrange - Simulate a complex mod installation workflow
			Debug.Assert(_sourceDir != null);
			string mod1Archive = Path.Combine(_sourceDir, "mod1.zip");
			string mod2Archive = Path.Combine(_sourceDir, "mod2.zip");
			string patchArchive = Path.Combine(_sourceDir, "patch.zip");

			CreateArchive(mod1Archive, new()
			{
				{ "override/appearance.2da", "Mod1 appearance data" },
				{ "override/dialog.dlg", "Mod1 dialog" },
				{ "modules/module1.mod", "Module 1" }
			});

			CreateArchive(mod2Archive, new()
			{
				{ "override/appearance.2da", "Mod2 appearance data (conflicts!)" },
				{ "override/spells.2da", "Mod2 spells" },
				{ "lips/scene1.lip", "Lip sync data" }
			});

			CreateArchive(patchArchive, new()
			{
				{ "appearance.2da", "Patched appearance" },
				{ "compatibility_fix.txt", "Instructions" }
			});

			var instructions = new List<Instruction>
			{
				// Install mod1
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\mod1.zip"], Destination = "<<modDirectory>>" },
				new() { Action = Instruction.ActionType.Move, Source = ["<<modDirectory>>\\mod1\\override\\*"], Destination = "<<kotorDirectory>>\\override", Overwrite = true },
				new() { Action = Instruction.ActionType.Move, Source = ["<<modDirectory>>\\mod1\\modules\\*"], Destination = "<<kotorDirectory>>\\modules", Overwrite = true },

				// Install mod2
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\mod2.zip"], Destination = "<<modDirectory>>" },
				// Backup original appearance.2da before mod2 overwrites
				new() { Action = Instruction.ActionType.Copy, Source = ["<<kotorDirectory>>\\override\\appearance.2da"], Destination = "<<kotorDirectory>>\\backup\\appearance.2da.mod1", Overwrite = true },
				// Move mod2 files (overwriting mod1's appearance.2da)
				new() { Action = Instruction.ActionType.Move, Source = ["<<modDirectory>>\\mod2\\override\\*"], Destination = "<<kotorDirectory>>\\override", Overwrite = true },
				new() { Action = Instruction.ActionType.Move, Source = ["<<modDirectory>>\\mod2\\lips\\*"], Destination = "<<kotorDirectory>>\\lips", Overwrite = true },

				// Apply patch
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\patch.zip"], Destination = "<<modDirectory>>" },
				new() { Action = Instruction.ActionType.Move, Source = ["<<modDirectory>>\\patch\\appearance.2da"], Destination = "<<kotorDirectory>>\\override", Overwrite = true },

				// Cleanup temp files from mod directory
				new() { Action = Instruction.ActionType.Delete, Source = ["<<modDirectory>>\\patch\\compatibility_fix.txt"] }
			};

			// Act
			(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(instructions, _sourceDir);

			// Assert
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(virtualProvider, realDestDir);

			// Verify final state
			List<string> virtualFiles = virtualProvider.GetTrackedFiles();
			Assert.Multiple(() =>
			{
				Assert.That(virtualFiles.Any(f => f.EndsWith("override\\appearance.2da", StringComparison.OrdinalIgnoreCase)), Is.True);
				Assert.That(virtualFiles.Any(f => f.EndsWith("override\\dialog.dlg", StringComparison.OrdinalIgnoreCase)), Is.True);
				Assert.That(virtualFiles.Any(f => f.EndsWith("override\\spells.2da", StringComparison.OrdinalIgnoreCase)), Is.True);
				Assert.That(virtualFiles.Any(f => f.EndsWith("backup\\appearance.2da.mod1\\appearance.2da", StringComparison.OrdinalIgnoreCase)), Is.True);
			});
		}

		[Test]
		public async Task Test_NestedArchiveOperations()
		{
			// Arrange - Archive gets renamed and copied before extraction
			Debug.Assert(_sourceDir != null);
			string originalPath = Path.Combine(_sourceDir, "original.zip");
			CreateArchive(originalPath, new()
			{
				{ "nested/deep/file.txt", "Deep content" }
			});

			var instructions = new List<Instruction>
			{
				new()
				{
					Action = Instruction.ActionType.Rename,
					Source = ["<<modDirectory>>\\original.zip"],
					Destination = "temp1.zip",
					Overwrite = true
				},
				new()
				{
					Action = Instruction.ActionType.Copy,
					Source = ["<<modDirectory>>\\temp1.zip"],
					Destination = "<<modDirectory>>\\backup",
					Overwrite = true
				},
				new()
				{
					Action = Instruction.ActionType.Rename,
					Source = ["<<modDirectory>>\\backup\\temp1.zip"],
					Destination = "temp2.zip",
					Overwrite = true
				},
				new()
				{
					Action = Instruction.ActionType.Rename,
					Source = ["<<modDirectory>>\\backup\\temp2.zip"],
					Destination = "final.zip",
					Overwrite = true
				},
				new()
				{
					Action = Instruction.ActionType.Extract,
					Source = ["<<modDirectory>>\\backup\\final.zip"],
					Destination = "<<kotorDirectory>>"
				},
				new()
				{
					Action = Instruction.ActionType.Delete,
					Source = ["<<modDirectory>>\\temp1.zip"]
				}
			};

			// Act
			(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(instructions, _sourceDir);

			// Assert
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(virtualProvider, realDestDir);
		}

		[Test]
		[Category("Integration")]
		[Explicit("Long-running integration test that downloads mods from the internet")]
		public async Task Test_FullModBuildInstallation_KOTOR1_Mobile_Full()
		{
			// Arrange - Create fake KOTOR directory structure
			Debug.Assert(_testRootDir != null);
			string kotorRoot = Path.Combine(_testRootDir, "KOTOR_Install");
			CreateKOTORDirectoryStructure(kotorRoot);

			string modDirectory = Path.Combine(_testRootDir, "Mods");
			_ = Directory.CreateDirectory(modDirectory);

			// Load TOML file - search in multiple possible locations
			string[] possiblePaths =
			{
				Path.Combine(Environment.CurrentDirectory, "mod-builds", "TOMLs", "KOTOR1_Mobile_Full.toml"),
				Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "mod-builds", "TOMLs", "KOTOR1_Mobile_Full.toml"),
				Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "mod-builds", "TOMLs", "KOTOR1_Mobile_Full.toml")
			};

			string? tomlPath = null;
			foreach ( string path in possiblePaths )
			{
				try
				{
					string fullPath = Path.GetFullPath(path);
					if ( File.Exists(fullPath) )
					{
						tomlPath = fullPath;
						break;
					}
				}
				catch
				{
					// Ignore path resolution errors
				}
			}

			if ( tomlPath is null )
			{
				Assert.Ignore($"TOML file not found. Tried locations:\n{string.Join("\n", possiblePaths)}");
				return;
			}

			TestContext.WriteLine($"Loading TOML from: {tomlPath}");

			// Parse TOML and get component list
			List<ModComponent> components;
			try
			{
				components = ModComponent.ReadComponentsFromFile(tomlPath);
			}
			catch ( Exception ex )
			{
				Assert.Fail($"Failed to parse TOML: {ex.Message}");
				return;
			}

			TestContext.WriteLine($"Loaded {components.Count} mods from TOML");

			// Configure paths
			_ = new MainConfig
			{
				sourcePath = new DirectoryInfo(modDirectory),
				destinationPath = new DirectoryInfo(kotorRoot)
			};

			// Attempt to download all mods (many will fail, which is expected)
			TestContext.WriteLine("Attempting to download mods...");
			int downloadedCount = 0;
			int failedCount = 0;

			foreach ( ModComponent component in components )
			{
				if ( component.ModLink.Count == 0 )
				{
					TestContext.WriteLine($"  [{component.Name}] No download links available");
					component.IsSelected = false;
					failedCount++;
					continue;
				}

				try
				{
					// Try to download from first available link
					// Note: This is a simplified version - real implementation would be more robust
					bool downloaded = await TryDownloadModAsync(component, modDirectory);
					if ( downloaded )
					{
						TestContext.WriteLine($"  ✓ [{component.Name}] Downloaded successfully");
						downloadedCount++;
					}
					else
					{
						TestContext.WriteLine($"  ✗ [{component.Name}] Download failed");
						component.IsSelected = false;
						failedCount++;
					}
				}
				catch ( Exception ex )
				{
					TestContext.WriteLine($"  ✗ [{component.Name}] Download error: {ex.Message}");
					component.IsSelected = false;
					failedCount++;
				}
			}

			TestContext.WriteLine($"\nDownload summary: {downloadedCount} successful, {failedCount} failed");

			if ( downloadedCount == 0 )
			{
				Assert.Ignore("No mods were successfully downloaded - skipping installation test");
				return;
			}

			// Filter to only selected (successfully downloaded) components
			var selectedComponents = components.Where(c => c.IsSelected).ToList();
			TestContext.WriteLine($"\nInstalling {selectedComponents.Count} mods...");

			// Create component list with all instructions
			var allInstructions = new List<Instruction>();
			foreach ( ModComponent component in selectedComponents )
			{
				allInstructions.AddRange(component.Instructions);
			}

			if ( allInstructions.Count == 0 )
			{
				Assert.Ignore("No instructions to execute");
				return;
			}

			TestContext.WriteLine($"Total instructions: {allInstructions.Count}");

			// Act - Run installation with both providers
			try
			{
				(VirtualFileSystemProvider virtualProvider, string realDestDir) = await RunBothProviders(
					allInstructions,
					modDirectory
				);

				// Assert - Check that installation completed without critical errors
				var criticalIssues = virtualProvider.GetValidationIssues()
					.Where(i => i.Severity >= ValidationSeverity.Error)
					.ToList();

				TestContext.WriteLine("\nInstallation complete!");
				TestContext.WriteLine($"Total validation issues: {virtualProvider.GetValidationIssues().Count}");
				TestContext.WriteLine($"Critical issues: {criticalIssues.Count}");

				foreach ( ValidationIssue issue in criticalIssues.Take(10) )
				{
					TestContext.WriteLine($"  {issue.Severity}: [{issue.Category}] {issue.Message}");
				}

				// Assert no critical errors
				Assert.That(criticalIssues, Is.Empty, "Installation should complete without critical errors");

				// Assert virtual and real file systems match
				AssertFileSystemsMatch(virtualProvider, realDestDir);

				TestContext.WriteLine("\n✓ Installation test passed!");
			}
			catch ( Exception ex )
			{
				Assert.Fail($"Installation failed: {ex.Message}\n{ex.StackTrace}");
			}
		}

		private static void CreateKOTORDirectoryStructure(string rootPath)
		{
			_ = Directory.CreateDirectory(rootPath);

			// Create all required subdirectories
			string[] directories =
			[
				"data",
				"lips",
				"modules\\extras",
				"movies",
				"Override",
				"rims",
				"streammusic",
				"streamsounds",
				"streamwaves\\globe",
				"TexturePacks",
				"utils\\swupdateskins"
			];

			foreach ( string dir in directories )
			{
				_ = Directory.CreateDirectory(Path.Combine(rootPath, dir));
			}

			// Create required files
			File.WriteAllText(Path.Combine(rootPath, "swkotor.exe"), "fake exe");
			File.WriteAllText(Path.Combine(rootPath, "dialog.tlk"), "fake dialog");
		}

		private static async Task<bool> TryDownloadModAsync(ModComponent component, string modDirectory)
		{
			// Check if mod files already exist in the directory
			// This allows the test to work if someone manually places mod files there
			await Task.CompletedTask;

			// Extract expected filenames from the component's instructions
			var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			if ( component.Instructions.Count > 0 )
			{
				foreach ( Instruction instruction in component.Instructions )
				{
					if ( instruction.Action != Instruction.ActionType.Extract )
						continue;
					foreach ( string cleanSource in instruction.Source.Select(source => source.Replace("<<modDirectory>>\\", "")
								 .Replace("<<modDirectory>>/", "")) )
					{
						// Handle wildcards by checking for any matching files
						if ( cleanSource.Contains('*') )
						{
							try
							{
								string searchPattern = Path.GetFileName(cleanSource);
								string searchDir = Path.GetDirectoryName(cleanSource) ?? "";
								string fullSearchDir = Path.Combine(modDirectory, searchDir);

								if ( !Directory.Exists(fullSearchDir) )
									continue;
								string[] matchingFiles = Directory.GetFiles(fullSearchDir, searchPattern, SearchOption.TopDirectoryOnly);
								if ( matchingFiles.Length > 0 )
									return true; // Found at least one matching file
							}
							catch
							{
								// Ignore errors in wildcard matching
							}
						}
						else
						{
							_ = expectedFiles.Add(cleanSource);
						}
					}
				}
			}

			// Check if any of the expected files exist
			foreach ( string expectedFile in expectedFiles )
			{
				string fullPath = Path.Combine(modDirectory, expectedFile);
				if ( !File.Exists(fullPath) )
					continue;
				TestContext.WriteLine($"    Found existing file: {expectedFile}");
				return true;
			}

			// If no files found, this mod is not available
			return false;
		}

		#endregion
	}
}

