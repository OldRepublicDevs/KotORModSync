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
using KOTORModSync.Core.Services.FileSystem;
using KOTORModSync.Core.Services.Validation;
using NUnit.Framework;

namespace KOTORModSync.Tests
{
	/// <summary>
	/// Integration tests for the full dry-run validation pipeline.
	/// Tests DryRunValidator end-to-end with real components and instructions.
	/// </summary>
	[TestFixture]
	public class DryRunValidationIntegrationTests
	{
		private string _testRootDir = null!;
		private string _sourceDir = null!;
		private string _destDir = null!;
		private string _sevenZipPath = null!;

		[SetUp]
		public void SetUp()
		{
			_testRootDir = Path.Combine(Path.GetTempPath(), "KOTORModSync_Integration_Tests_" + Guid.NewGuid().ToString("N"));
			_sourceDir = Path.Combine(_testRootDir, "Source");
			_destDir = Path.Combine(_testRootDir, "Dest");

			_ = Directory.CreateDirectory(_testRootDir);
			_ = Directory.CreateDirectory(_sourceDir);
			_ = Directory.CreateDirectory(_destDir);

			_sevenZipPath = Find7Zip();

			var config = new MainConfig
			{
				sourcePath = new DirectoryInfo(_sourceDir),
				destinationPath = new DirectoryInfo(_destDir),
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

		private string Find7Zip()
		{
			var paths = new[]
			{
				@"C:\Program Files\7-Zip\7z.exe",
				@"C:\Program Files (x86)\7-Zip\7z.exe"
			};

			foreach ( var path in paths )
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
			Directory.CreateDirectory(tempDir);

			try
			{
				foreach ( var kvp in files )
				{
					string filePath = Path.Combine(tempDir, kvp.Key);
					string? fileDir = Path.GetDirectoryName(filePath);
					if ( !string.IsNullOrEmpty(fileDir) )
						Directory.CreateDirectory(fileDir);
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

				using var process = Process.Start(startInfo);
				if ( process == null )
					return;
				process.WaitForExit();
				if ( process.ExitCode != 0 )
					throw new InvalidOperationException($"7-Zip failed: {process.StandardError.ReadToEnd()}");
			}
			finally
			{
				if ( Directory.Exists(tempDir) )
					Directory.Delete(tempDir, recursive: true);
			}
		}

		[Test]
		public async Task Test_DryRunValidator_ValidInstallation_Passes()
		{
			// Arrange - Create valid mod installation
			string mod1Archive = Path.Combine(_sourceDir, "mod1.zip");
			CreateArchive(mod1Archive, new Dictionary<string, string>
			{
				{ "override/appearance.2da", "Appearance data" },
				{ "override/dialog.dlg", "Dialog data" }
			});

			var component = new Component
			{
				Name = "Test Mod",
				Guid = Guid.NewGuid(),
				IsSelected = true
			};

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Extract,
				Source = ["<<modDirectory>>\\mod1.zip"],
				Destination = "<<kotorDirectory>>"
			});

			// Using static DryRunValidator

			// Act
			var result = await DryRunValidator.ValidateInstallationAsync(
				new List<Component> { component },
				CancellationToken.None
			);

			// Assert
			TestContext.WriteLine($"Validation result: {result.IsValid}");
			TestContext.WriteLine($"Issues: {result.Issues.Count}");
			foreach ( var issue in result.Issues )
			{
				TestContext.WriteLine($"  [{issue.Severity}] {issue.Category}: {issue.Message}");
			}

			Assert.That(result.IsValid, Is.True);
			Assert.That(result.Issues.Where(i => i.Severity == ValidationSeverity.Error), Is.Empty);
		}

		[Test]
		public async Task Test_DryRunValidator_InvalidOperationOrder_Fails()
		{
			// Arrange - Try to move a file that was just deleted
			string archivePath = Path.Combine(_sourceDir, "test.zip");
			CreateArchive(archivePath, new Dictionary<string, string>
			{
				{ "file.txt", "Content" }
			});

			var component = new Component
			{
				Name = "Invalid Mod",
				Guid = Guid.NewGuid(),
				IsSelected = true
			};

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Extract,
				Source = ["<<modDirectory>>\\test.zip"],
				Destination = "<<modDirectory>>"
			});

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Delete,
				Source = ["<<modDirectory>>\\test\\file.txt"]
			});

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Move,
				Source = ["<<modDirectory>>\\test\\file.txt"], // Already deleted!
				Destination = "<<kotorDirectory>>\\moved.txt",
				Overwrite = true
			});

			// Using static DryRunValidator

			// Act
			var result = await DryRunValidator.ValidateInstallationAsync(
				new List<Component> { component },
				CancellationToken.None
			);

			// Assert
			TestContext.WriteLine($"Validation result: {result.IsValid}");
			TestContext.WriteLine($"Errors: {result.Issues.Count(i => i.Severity == ValidationSeverity.Error)}");
			foreach ( var issue in result.Issues.Where(i => i.Severity == ValidationSeverity.Error) )
			{
				TestContext.WriteLine($"  {issue.Category}: {issue.Message}");
			}

			Assert.That(result.IsValid, Is.False);
			Assert.That(result.Issues, Has.Some.Matches<ValidationIssue>(i =>
				i.Severity == ValidationSeverity.Error &&
				i.Message.Contains("Could not find any files matching the pattern")));
		}

		[Test]
		public async Task Test_DryRunValidator_MissingArchiveFile_Fails()
		{
			// Arrange - Reference non-existent archive
			var component = new Component
			{
				Name = "Missing Archive Mod",
				Guid = Guid.NewGuid(),
				IsSelected = true
			};

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Extract,
				Source = ["<<modDirectory>>\\doesnotexist.zip"],
				Destination = "<<kotorDirectory>>"
			});

			// Using static DryRunValidator

			// Act
			var result = await DryRunValidator.ValidateInstallationAsync(
				new List<Component> { component },
				CancellationToken.None
			);

			// Assert - Should have validation issues for missing file
			Assert.That(result.IsValid, Is.False);
			Assert.That(result.Issues, Has.Some.Matches<ValidationIssue>(i => i.Severity == ValidationSeverity.Error && i.Message.Contains("Could not find any files matching the pattern")));
		}

		[Test]
		public async Task Test_DryRunValidator_FileInArchiveNotFound_Fails()
		{
			// Arrange - Extract archive, then try to move a file that doesn't exist in it
			string archivePath = Path.Combine(_sourceDir, "limited.zip");
			CreateArchive(archivePath, new Dictionary<string, string>
			{
				{ "exists.txt", "This file exists" }
			});

			var component = new Component
			{
				Name = "Archive Content Mismatch",
				Guid = Guid.NewGuid(),
				IsSelected = true
			};

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Extract,
				Source = ["<<modDirectory>>\\limited.zip"],
				Destination = "<<modDirectory>>"
			});

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Move,
				Source = ["<<modDirectory>>\\limited\\doesnotexist.txt"], // Not in archive!
				Destination = "<<kotorDirectory>>\\moved.txt",
				Overwrite = true
			});

			// Using static DryRunValidator

			// Act
			var result = await DryRunValidator.ValidateInstallationAsync(
				new List<Component> { component },
				CancellationToken.None
			);

			// Assert
			TestContext.WriteLine($"Validation result: {result.IsValid}");
			foreach ( var issue in result.Issues )
			{
				TestContext.WriteLine($"  [{issue.Severity}] {issue.Category}: {issue.Message}");
			}

			Assert.That(result.IsValid, Is.False);
			Assert.That(result.Issues, Has.Some.Matches<ValidationIssue>(i => i.Severity == ValidationSeverity.Error && i.Message.Contains("Could not find any files matching the pattern")));
		}

		[Test]
		public async Task Test_DryRunValidator_MultipleComponents_WithDependencies()
		{
			// Arrange - Create interdependent components
			string baseModArchive = Path.Combine(_sourceDir, "base.zip");
			string patchArchive = Path.Combine(_sourceDir, "patch.zip");

			CreateArchive(baseModArchive, new Dictionary<string, string>
			{
				{ "override/base.2da", "Base data" }
			});

			CreateArchive(patchArchive, new Dictionary<string, string>
			{
				{ "patch.2da", "Patch data" }
			});

			var baseMod = new Component
			{
				Name = "Base Mod",
				Guid = Guid.NewGuid(),
				IsSelected = true
			};
			baseMod.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Extract,
				Source = ["<<modDirectory>>\\base.zip"],
				Destination = "<<modDirectory>>"
			});
			baseMod.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Move,
				Source = ["<<modDirectory>>\\base\\override\\*"],
				Destination = "<<kotorDirectory>>\\override",
				Overwrite = true
			});

			var patchMod = new Component
			{
				Name = "Patch Mod",
				Guid = Guid.NewGuid(),
				IsSelected = true
			};
			patchMod.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Extract,
				Source = ["<<modDirectory>>\\patch.zip"],
				Destination = "<<modDirectory>>"
			});
			patchMod.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Move,
				Source = ["<<modDirectory>>\\patch\\patch.2da"],
				Destination = "<<kotorDirectory>>\\override", // Overwrites base mod file
				Overwrite = true
			});

			// Patch depends on base
			patchMod.Dependencies.Add(baseMod.Guid);

			// Using static DryRunValidator

			// Act
			var result = await DryRunValidator.ValidateInstallationAsync(
				new List<Component> { baseMod, patchMod },
				CancellationToken.None
			);

			// Assert
			TestContext.WriteLine($"Validation result: {result.IsValid}");
			foreach ( var issue in result.Issues )
			{
				TestContext.WriteLine($"  [{issue.Severity}] {issue.Category}: {issue.Message}");
			}

			Assert.That(result.IsValid, Is.True);
			Assert.That(result.Issues.Where(i => i.Severity != ValidationSeverity.Error), Is.Empty);
		}

		[Test]
		public async Task Test_DryRunValidator_OverwriteConflict_Warning()
		{
			// Arrange - Two mods trying to write to the same file
			string mod1Archive = Path.Combine(_sourceDir, "mod1.zip");
			string mod2Archive = Path.Combine(_sourceDir, "mod2.zip");

			CreateArchive(mod1Archive, new Dictionary<string, string>
			{
				{ "shared.txt", "Mod 1 version" }
			});

			CreateArchive(mod2Archive, new Dictionary<string, string>
			{
				{ "shared.txt", "Mod 2 version" }
			});

			var component = new Component
			{
				Name = "Conflicting Mods",
				Guid = Guid.NewGuid(),
				IsSelected = true
			};

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Extract,
				Source = ["<<modDirectory>>\\mod1.zip"],
				Destination = "<<modDirectory>>"
			});

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Move,
				Source = ["<<modDirectory>>\\mod1\\shared.txt"],
				Destination = "<<kotorDirectory>>\\override\\shared.txt",
				Overwrite = true
			});

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Extract,
				Source = ["<<modDirectory>>\\mod2.zip"],
				Destination = "<<modDirectory>>"
			});

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Move,
				Source = ["<<modDirectory>>\\mod2\\shared.txt"],
				Destination = "<<kotorDirectory>>\\override\\shared.txt", // Overwrites mod1's file
				Overwrite = true
			});

			// Using static DryRunValidator

			// Act
			var result = await DryRunValidator.ValidateInstallationAsync(
				new List<Component> { component },
				CancellationToken.None
			);

			// Assert
			TestContext.WriteLine($"Validation result: {result.IsValid}");
			TestContext.WriteLine($"Warnings: {result.Issues.Count(i => i.Severity == ValidationSeverity.Warning)}");
			foreach ( var issue in result.Issues )
			{
				TestContext.WriteLine($"  [{issue.Severity}] {issue.Category}: {issue.Message}");
			}

			// Should pass but with warnings
			Assert.That(result.IsValid || result.Issues.All(i => i.Severity != ValidationSeverity.Error), Is.True);
		}

		[Test]
		public async Task Test_DryRunValidator_ComplexWorkflow_AllOperationTypes()
		{
			// Arrange - Complex workflow with all operation types
			string mainArchive = Path.Combine(_sourceDir, "main.zip");
			string patchArchive = Path.Combine(_sourceDir, "patch.zip");

			CreateArchive(mainArchive, new Dictionary<string, string>
			{
				{ "data/file1.txt", "File 1" },
				{ "data/file2.txt", "File 2" },
				{ "data/file3.txt", "File 3" },
				{ "config/old_config.ini", "Old config" },
				{ "backup/backup1.bak", "Backup 1" },
				{ "backup/backup2.bak", "Backup 2" }
			});

			CreateArchive(patchArchive, new Dictionary<string, string>
			{
				{ "new_config.ini", "New config" }
			});

			var component = new Component
			{
				Name = "Complex Workflow",
				Guid = Guid.NewGuid(),
				IsSelected = true
			};

			// 1. Extract main archive
			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Extract,
				Source = ["<<modDirectory>>\\main.zip"],
				Destination = "<<modDirectory>>"
			});

			// 2. Copy files
			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Copy,
				Source = ["<<modDirectory>>\\main\\data\\file1.txt"],
				Destination = "<<kotorDirectory>>\\final",
				Overwrite = true
			});

			// 3. Move files
			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Move,
				Source = ["<<modDirectory>>\\main\\data\\file2.txt"],
				Destination = "<<kotorDirectory>>\\final",
				Overwrite = true
			});

			// 4. Rename file
			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Rename,
				Source = ["<<modDirectory>>\\main\\data\\file3.txt"],
				Destination = "file3_renamed.txt",
				Overwrite = true
			});

			// 5. Delete backup files
			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Delete,
				Source = ["<<modDirectory>>\\main\\backup\\backup1.bak"]
			});

			// 6. Extract patch
			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Extract,
				Source = ["<<modDirectory>>\\patch.zip"],
				Destination = "<<modDirectory>>"
			});

			// 7. Move patch file to replace old config
			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Move,
				Source = ["<<modDirectory>>\\patch\\new_config.ini"],
				Destination = "<<kotorDirectory>>\\config",
				Overwrite = true
			});

			// Act
			var result = await DryRunValidator.ValidateInstallationAsync(
				new List<Component> { component },
				CancellationToken.None
			);

			// Assert
			TestContext.WriteLine($"Validation result: {result.IsValid}");
			TestContext.WriteLine($"Total issues: {result.Issues.Count}");
			foreach ( var issue in result.Issues )
			{
				TestContext.WriteLine($"  [{issue.Severity}] {issue.Category}: {issue.Message}");
			}

			Assert.That(result.IsValid, Is.True);
			Assert.That(result.Issues.Where(i => i.Severity == ValidationSeverity.Error), Is.Empty);
		}

		[Test]
		public async Task Test_DryRunValidator_WildcardOperations()
		{
			// Arrange
			string archivePath = Path.Combine(_sourceDir, "wildcards.zip");
			CreateArchive(archivePath, new Dictionary<string, string>
			{
				{ "scripts/script1.ncs", "Script 1" },
				{ "scripts/script2.ncs", "Script 2" },
				{ "scripts/script3.ncs", "Script 3" },
				{ "dialogs/dialog1.dlg", "Dialog 1" },
				{ "dialogs/dialog2.dlg", "Dialog 2" },
				{ "readme.txt", "Readme" }
			});

			var component = new Component
			{
				Name = "Wildcard Test",
				Guid = Guid.NewGuid(),
				IsSelected = true
			};

			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Extract,
				Source = ["<<modDirectory>>\\wildcards.zip"],
				Destination = "<<modDirectory>>"
			});

			// Move all .ncs files
			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Move,
				Source = ["<<modDirectory>>\\wildcards\\scripts\\*.ncs"],
				Destination = "<<kotorDirectory>>\\override",
				Overwrite = true
			});

			// Copy all .dlg files
			component.Instructions.Add(new Instruction
			{
				Action = Instruction.ActionType.Copy,
				Source = ["<<modDirectory>>\\wildcards\\dialogs\\*.dlg"],
				Destination = "<<kotorDirectory>>\\override",
				Overwrite = true
			});

			// Using static DryRunValidator

			// Act
			var result = await DryRunValidator.ValidateInstallationAsync(
				new List<Component> { component },
				CancellationToken.None
			);

			// Assert
			TestContext.WriteLine($"Validation result: {result.IsValid}");
			foreach ( var issue in result.Issues )
			{
				TestContext.WriteLine($"  [{issue.Severity}] {issue.Category}: {issue.Message}");
			}

			Assert.That(result.IsValid, Is.True);
			Assert.That(result.Issues.Where(i => i.Severity == ValidationSeverity.Error), Is.Empty);
		}
	}
}

