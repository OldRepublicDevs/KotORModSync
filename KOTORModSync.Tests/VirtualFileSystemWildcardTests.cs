// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Services.FileSystem;
using NUnit.Framework;

namespace KOTORModSync.Tests
{
	/// <summary>
	/// Focused tests for wildcard operations to ensure VirtualFileSystemProvider
	/// matches RealFileSystemProvider behavior EXACTLY (1:1).
	/// Tests PathHelper.WildcardPathMatch integration.
	/// </summary>
	[TestFixture]
	public class VirtualFileSystemWildcardTests
	{
		private string _testRootDir = null!;
		private string _sourceDir = null!;
		private string _destinationDir = null!;
		private string _virtualTestDir = null!;
		private string _realTestDir = null!;
		private string _sevenZipPath = null!;

		[SetUp]
		public void SetUp()
		{
			// Create isolated test directories
			_testRootDir = Path.Combine(Path.GetTempPath(), "KOTORModSync_Wildcard_Tests_" + Guid.NewGuid().ToString("N"));
			_virtualTestDir = Path.Combine(_testRootDir, "Virtual");
			_realTestDir = Path.Combine(_testRootDir, "Real");
			_sourceDir = Path.Combine(_testRootDir, "Source");
			_destinationDir = Path.Combine(_testRootDir, "Destination");

			_ = Directory.CreateDirectory(_testRootDir);
			_ = Directory.CreateDirectory(_virtualTestDir);
			_ = Directory.CreateDirectory(_realTestDir);
			_ = Directory.CreateDirectory(_sourceDir);
			_ = Directory.CreateDirectory(_destinationDir);

			_sevenZipPath = Find7Zip();

			// Ensure placeholders resolve for each wildcard test
			var __ = new MainConfig
			{
				sourcePath = new DirectoryInfo(_sourceDir),
				destinationPath = new DirectoryInfo(_destinationDir),
				caseInsensitivePathing = true,
				useMultiThreadedIO = false
			};
		}

		[TearDown]
		public void Dispose()
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
		}

		private static string Find7Zip()
		{
			string[] paths = new[]
			{
				@"C:\Program Files\7-Zip\7z.exe",
				@"C:\Program Files (x86)\7-Zip\7z.exe"
			};

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
						return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
					}
				}
			}
			catch { }

			throw new InvalidOperationException("7-Zip not found. Please install 7-Zip to run these tests.");
		}

		private void CreateArchive(string archivePath, Dictionary<string, string> files)
		{
			string tempDir = Path.Combine(_testRootDir, "temp_" + Guid.NewGuid().ToString("N"));
			_ = Directory.CreateDirectory(tempDir);

			try
			{
				foreach ( KeyValuePair<string, string> kvp in files )
				{
					string filePath = Path.Combine(tempDir, kvp.Key);
					Assert.That(filePath, Is.Not.Null);
					string? fileDir = Path.GetDirectoryName(filePath);
					Assert.That(fileDir, Is.Not.Null);
					if ( !string.IsNullOrEmpty(fileDir) )
						_ = Directory.CreateDirectory(fileDir);
					File.WriteAllText(filePath, kvp.Value);
				}

				var startInfo = new ProcessStartInfo
				{
					FileName = _sevenZipPath,
					Arguments = $"a -tzip \"{archivePath}\" \"{tempDir}\\*\" -r",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true
				};

				using ( var process = Process.Start(startInfo) )
				{
					Assert.That(process, Is.Not.Null);
					process.WaitForExit();
					if ( process.ExitCode != 0 )
						throw new InvalidOperationException($"7-Zip failed: {process.StandardError.ReadToEnd()}");
				}
			}
			finally
			{
				if ( Directory.Exists(tempDir) )
					Directory.Delete(tempDir, recursive: true);
			}
		}

		private async Task<(VirtualFileSystemProvider virtualProvider, RealFileSystemProvider realProvider)> RunBothProviders(
			List<Instruction> instructions,
			string realSourcePath,
			string realDestPath)
		{
			// Create copies for both tests
			string virtualSourceCopy = Path.Combine(_virtualTestDir, "source");
			string realSourceCopy = Path.Combine(_realTestDir, "source");
			string virtualDestCopy = Path.Combine(_virtualTestDir, "dest");
			string realDestCopy = Path.Combine(_realTestDir, "dest");

			_ = Directory.CreateDirectory(virtualSourceCopy);
			_ = Directory.CreateDirectory(realSourceCopy);
			_ = Directory.CreateDirectory(virtualDestCopy);
			_ = Directory.CreateDirectory(realDestCopy);

			// Copy source files to both locations
			CopyDirectory(realSourcePath, virtualSourceCopy);
			CopyDirectory(realSourcePath, realSourceCopy);
			CopyDirectory(realDestPath, virtualDestCopy);
			CopyDirectory(realDestPath, realDestCopy);

			// Update MainConfig for virtual test
			DirectoryInfo originalSourcePath = MainConfig.SourcePath;
			DirectoryInfo originalDestPath = MainConfig.DestinationPath;

			try
			{
				// Test 1: Virtual File System (Dry-Run)
				var config1 = new MainConfig
				{
					sourcePath = new DirectoryInfo(virtualSourceCopy),
					destinationPath = new DirectoryInfo(virtualDestCopy)
				};

				var virtualProvider = new VirtualFileSystemProvider();
				var virtualComponent = new Component { Name = "TestComponent" };

				var virtualInstructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>(instructions);
				_ = await virtualComponent.ExecuteInstructionsAsync(
					virtualInstructions,
					new List<Component>(),
					CancellationToken.None,
					virtualProvider
				);

				TestContext.WriteLine($"Virtual Provider - Files tracked: {virtualProvider.GetTrackedFiles().Count}");
				TestContext.WriteLine($"Virtual Provider - Issues: {virtualProvider.GetValidationIssues().Count}");

				// Test 2: Real File System
				var config2 = new MainConfig
				{
					sourcePath = new DirectoryInfo(realSourceCopy),
					destinationPath = new DirectoryInfo(realDestCopy)
				};

				var realProvider = new RealFileSystemProvider();
				var realComponent = new Component { Name = "TestComponent" };

				// Re-create instructions (they were mutated by the first run)
				var realInstructions = new List<Instruction>();
				foreach ( Instruction instruction in instructions )
				{
					var newInstruction = new Instruction
					{
						Action = instruction.Action,
						Source = instruction.Source.ToList(),
						Destination = instruction.Destination,
						Overwrite = instruction.Overwrite,
						Arguments = instruction.Arguments
					};
					realInstructions.Add(newInstruction);
				}

				var realInstructionsObservable = new System.Collections.ObjectModel.ObservableCollection<Instruction>(realInstructions);
				_ = await realComponent.ExecuteInstructionsAsync(
					realInstructionsObservable,
					new List<Component>(),
					CancellationToken.None,
					realProvider
				);

				TestContext.WriteLine($"Real Provider - Executed successfully");

				return (virtualProvider, realProvider);
			}
			finally
			{
				// Restore MainConfig
				var config3 = new MainConfig
				{
					sourcePath = originalSourcePath,
					destinationPath = originalDestPath
				};
			}
		}

		private static void CopyDirectory(string sourceDir, string destDir)
		{
			if ( !Directory.Exists(sourceDir) )
				return;

			foreach ( string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories) )
			{
				string relativePath = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				string destFile = Path.Combine(destDir, relativePath);
				string? destFileDir = Path.GetDirectoryName(destFile);

				if ( !string.IsNullOrEmpty(destFileDir) )
					Directory.CreateDirectory(destFileDir);

				File.Copy(file, destFile, overwrite: true);
			}
		}

		private void AssertFileSystemsMatch(VirtualFileSystemProvider virtualProvider, string realDir, string subfolder = "dest")
		{
			// Find the virtual dest directory path by looking for files that contain "Virtual\dest" or "Real\dest"
			// Get the dest directory from the virtual provider's tracked files
			string virtBasePath = Path.GetDirectoryName(Path.GetDirectoryName(realDir))!;
			string virtualPath = Path.Combine(virtBasePath, "Virtual", subfolder);

			HashSet<string> virtualFiles = virtualProvider.GetTrackedFiles()
				.Where(f => f.StartsWith(virtualPath, StringComparison.OrdinalIgnoreCase))
				.Select(f => f.Substring(virtualPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
				.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			HashSet<string> realFiles = Directory.Exists(realDir)
				? Directory.GetFiles(realDir, "*", SearchOption.AllDirectories)
					.Select(f => f.Substring(realDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
					.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
					.ToHashSet(StringComparer.OrdinalIgnoreCase)
				: new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			TestContext.WriteLine($"\n=== Virtual Files ({virtualFiles.Count}) in '{subfolder}' ===");
			foreach ( string file in virtualFiles.OrderBy(f => f) )
				TestContext.WriteLine($"  {file}");

			TestContext.WriteLine($"\n=== Real Files ({realFiles.Count}) in '{subfolder}' ===");
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

		[Test]
		public async Task Test_WildcardMove_StarPattern()
		{
			// Arrange - Create archive with multiple .txt files
			string archivePath = Path.Combine(_sourceDir, "files.zip");
			CreateArchive(archivePath, new()
			{
				{ "file1.txt", "Content 1" },
				{ "file2.txt", "Content 2" },
				{ "file3.txt", "Content 3" },
				{ "readme.md", "Readme" },
				{ "data.dat", "Data" }
			});

			var instructions = new List<Instruction>
			{
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\files.zip"], Destination = "<<modDirectory>>" },
				new() { Action = Instruction.ActionType.Move, Source = ["<<modDirectory>>\\files\\*.txt"], Destination = "<<kotorDirectory>>", Overwrite = true }
			};

			// Act
			(VirtualFileSystemProvider v, _) = await RunBothProviders(instructions, _sourceDir, _destinationDir);

			// Assert
			Assert.That(v.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(v, Path.Combine(_realTestDir, "dest"));
		}

		[Test]
		public async Task Test_WildcardCopy_QuestionMarkPattern()
		{
			// Arrange - Files with single character differences
			string archivePath = Path.Combine(_sourceDir, "similar.zip");
			CreateArchive(archivePath, new()
			{
				{ "file1.txt", "1" },
				{ "file2.txt", "2" },
				{ "file3.txt", "3" },
				{ "fileA.txt", "A" },
				{ "fileAB.txt", "AB" } // Should NOT match file?.txt
			});

			var instructions = new List<Instruction>
			{
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\similar.zip"], Destination = "<<modDirectory>>" },
				new() { Action = Instruction.ActionType.Copy, Source = ["<<modDirectory>>\\similar\\file?.txt"], Destination = "<<kotorDirectory>>", Overwrite = true }
			};

			// Act
			(VirtualFileSystemProvider v, _) = await RunBothProviders(instructions, _sourceDir, _destinationDir);

			// Assert
			Assert.That(v.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(v, Path.Combine(_realTestDir, "dest"));
		}

		[Test]
		public async Task Test_WildcardDelete_ComplexPattern()
		{
			// Arrange
			string archivePath = Path.Combine(_sourceDir, "mixed.zip");
			CreateArchive(archivePath, new()
			{
				{ "data_backup_2023.txt", "Old backup" },
				{ "data_backup_2024.txt", "New backup" },
				{ "data_current.txt", "Current" },
				{ "logs_backup_2023.log", "Log backup" },
				{ "config.txt", "Config" }
			});

			var instructions = new List<Instruction>
			{
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\mixed.zip"], Destination = "<<modDirectory>>" },
				new() { Action = Instruction.ActionType.Delete, Source = ["<<modDirectory>>\\mixed\\data_backup_*.txt"] },
				new() { Action = Instruction.ActionType.Move, Source = ["<<modDirectory>>\\mixed\\*"], Destination = "<<kotorDirectory>>", Overwrite = true }
			};

			// Act
			(VirtualFileSystemProvider v, _) = await RunBothProviders(instructions, _sourceDir, _destinationDir);

			// Assert
			Assert.That(v.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(v, Path.Combine(_realTestDir, "dest"));
		}

		[Test]
		public async Task Test_WildcardInArchiveName()
		{
			// Arrange - Multiple archives matching pattern
			string archive1 = Path.Combine(_sourceDir, "mod_v1.0.zip");
			string archive2 = Path.Combine(_sourceDir, "mod_v2.0.zip");
			string archive3 = Path.Combine(_sourceDir, "other.zip");

			CreateArchive(archive1, new()
			{
				{ "version.txt", "1.0" }
			});

			CreateArchive(archive2, new()
			{
				{ "version.txt", "2.0" }
			});

			CreateArchive(archive3, new()
			{
				{ "data.txt", "other" }
			});

			var instructions = new List<Instruction>
			{
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\mod_*.zip"], Destination = "<<modDirectory>>" }
			};

			// Act
			(VirtualFileSystemProvider v, _) = await RunBothProviders(instructions, _sourceDir, _destinationDir);

			// Assert
			Assert.That(v.GetValidationIssues(), Is.Empty);

			// For this test, files are extracted to the source directory, so we match against that.
			AssertFileSystemsMatch(v, Path.Combine(_realTestDir, "source"), "source");

			string virtualSourcePath = Path.Combine(_virtualTestDir, "source");
			var extractedFiles = v.GetTrackedFiles()
				.Where(f => f.StartsWith(virtualSourcePath, StringComparison.OrdinalIgnoreCase))
				.Select(f => f.Replace(virtualSourcePath + Path.DirectorySeparatorChar, ""))
				.ToList();

			Assert.That(extractedFiles.Count(x => x.EndsWith("version.txt")), Is.EqualTo(2));
			Assert.That(extractedFiles, Has.Some.EqualTo(@"mod_v1.0\version.txt"));
			Assert.That(extractedFiles, Has.Some.EqualTo(@"mod_v2.0\version.txt"));
			Assert.That(extractedFiles.Any(x => x.Contains("data.txt")), Is.False, "File from non-matching archive should not be extracted.");
		}

		[Test]
		public async Task Test_WildcardMultiplePatterns()
		{
			// Arrange
			string archivePath = Path.Combine(_sourceDir, "files.zip");
			CreateArchive(archivePath, new()
			{
				{ "script1.ncs", "Script 1" },
				{ "script2.ncs", "Script 2" },
				{ "dialog1.dlg", "Dialog 1" },
				{ "dialog2.dlg", "Dialog 2" },
				{ "appearance.2da", "Appearance" },
				{ "portraits.2da", "Portraits" },
				{ "readme.txt", "Readme" }
			});

			var instructions = new List<Instruction>
			{
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\files.zip"], Destination = "<<modDirectory>>" },
				new()
				{
					Action = Instruction.ActionType.Copy,
					Source =
					[
						"<<modDirectory>>\\files\\*.ncs",
						"<<modDirectory>>\\files\\*.dlg",
						"<<modDirectory>>\\files\\*.2da"
					],
					Destination = "<<kotorDirectory>>\\override",
					Overwrite = true
				}
			};

			(VirtualFileSystemProvider v, _) = await RunBothProviders(instructions, _sourceDir, _destinationDir);

			// Assert
			Assert.That(v.GetValidationIssues(), Is.Empty);
			AssertFileSystemsMatch(v, Path.Combine(_realTestDir, "dest"));

			string virtualDestPath = Path.Combine(_virtualTestDir, "dest", "override");
			var copiedFiles = v.GetTrackedFiles()
				.Where(f => f.StartsWith(virtualDestPath, StringComparison.OrdinalIgnoreCase))
				.Select(Path.GetFileName)
				.ToList();

			Assert.That(copiedFiles, Has.Count.EqualTo(6));
			Assert.That(copiedFiles, Does.Contain("script1.ncs"));
			Assert.That(copiedFiles, Does.Contain("dialog1.dlg"));
			Assert.That(copiedFiles, Does.Contain("appearance.2da"));
			Assert.That(copiedFiles, Does.Not.Contain("readme.txt"));
		}

		[Test]
		public async Task Test_WildcardNoMatches_ShouldProduceValidationError()
		{
			// Arrange
			string archivePath = Path.Combine(_sourceDir, "empty.zip");
			CreateArchive(archivePath, new()
			{
				{ "file1.txt", "1" },
				{ "file2.txt", "2" }
			});

			var instructions = new List<Instruction>
			{
				new() { Action = Instruction.ActionType.Extract, Source = ["<<modDirectory>>\\empty.zip"], Destination = "<<modDirectory>>" },
				new() { Action = Instruction.ActionType.Move, Source = ["<<modDirectory>>\\empty\\*.dat"], Destination = "<<kotorDirectory>>", Overwrite = true }
			};

			// Act & Assert
			Assert.ThrowsAsync<FileNotFoundException>(async () => await RunBothProviders(instructions, _sourceDir, _destinationDir));
		}
	}
}

