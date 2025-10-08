// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Diagnostics;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace KOTORModSync.Tests
{
	/// <summary>
	/// Integration tests for AutoInstructionGenerator.
	/// Tests real archive creation and instruction generation without mocking.
	/// </summary>
	[TestFixture]
	public class AutoInstructionGeneratorTests
	{
		private string? _testDirectory;
		private List<string>? _createdArchives;

		[SetUp]
		public void SetUp()
		{
			_testDirectory = Path.Combine(Path.GetTempPath(), "KOTORModSync_AutoInstructionTests_" + Guid.NewGuid());
			Directory.CreateDirectory(_testDirectory);
			_createdArchives = new List<string>();

			// Initialize MainConfig for the tests
			var mainConfig = new MainConfig();
			mainConfig.sourcePath = new DirectoryInfo(_testDirectory);
			mainConfig.destinationPath = new DirectoryInfo(Path.Combine(_testDirectory, "KOTOR"));
			Directory.CreateDirectory(mainConfig.destinationPath.FullName);
		}

		[TearDown]
		public void TearDown()
		{
			// Cleanup test files
			try
			{
				if ( Directory.Exists(_testDirectory) )
					Directory.Delete(_testDirectory, recursive: true);
			}
			catch
			{
				// Ignore cleanup errors
			}
		}

		#region Test Archive Creation Helpers

		private string CreateFlatArchive(string archiveName, params string[] fileNames)
		{
			Debug.Assert(_testDirectory != null);
			string archivePath = Path.Combine(_testDirectory, archiveName);
			using ( var archive = ZipArchive.Create() )
			{
				foreach ( string fileName in fileNames )
				{
					var memStream = new MemoryStream();
					var writer = new StreamWriter(memStream);
					writer.WriteLine($"Test content for {fileName}");
					writer.Flush();
					memStream.Position = 0;
					archive.AddEntry(fileName, memStream);
				}

				using ( var fileStream = File.Create(archivePath) )
				{
					archive.SaveTo(fileStream, new WriterOptions(CompressionType.Deflate));
				}
			}

			_createdArchives?.Add(archivePath);
			return archivePath;
		}

		private string CreateSingleFolderArchive(string archiveName, string folderName, params string[] fileNames)
		{
			Debug.Assert(_testDirectory != null);
			string archivePath = Path.Combine(_testDirectory, archiveName);
			using ( var archive = ZipArchive.Create() )
			{
				foreach ( string fileName in fileNames )
				{
					string entryPath = $"{folderName}/{fileName}";
					var memStream = new MemoryStream();
					var writer = new StreamWriter(memStream);
					writer.WriteLine($"Test content for {fileName}");
					writer.Flush();
					memStream.Position = 0;
					archive.AddEntry(entryPath, memStream);
				}

				using ( var fileStream = File.Create(archivePath) )
				{
					archive.SaveTo(fileStream, new WriterOptions(CompressionType.Deflate));
				}
			}

			_createdArchives?.Add(archivePath);
			return archivePath;
		}

		private string CreateMultiFolderArchive(string archiveName, Dictionary<string, string[]> foldersWithFiles)
		{
			Debug.Assert(_testDirectory != null);
			string archivePath = Path.Combine(_testDirectory, archiveName);
			using ( var archive = ZipArchive.Create() )
			{
				foreach ( var kvp in foldersWithFiles )
				{
					string folderName = kvp.Key;
					string[] fileNames = kvp.Value;

					foreach ( string fileName in fileNames )
					{
						string entryPath = $"{folderName}/{fileName}";
						var memStream = new MemoryStream();
						var writer = new StreamWriter(memStream);
						writer.WriteLine($"Test content for {fileName} in {folderName}");
						writer.Flush();
						memStream.Position = 0;
						archive.AddEntry(entryPath, memStream);
					}
				}

				using ( var fileStream = File.Create(archivePath) )
				{
					archive.SaveTo(fileStream, new WriterOptions(CompressionType.Deflate));
				}
			}

			_createdArchives?.Add(archivePath);
			return archivePath;
		}

		private string CreateTslPatcherArchive(string archiveName, bool includeNamespacesIni, bool includeChangesIni)
		{
			Debug.Assert(_testDirectory != null);
			string archivePath = Path.Combine(_testDirectory, archiveName);
			using ( var archive = ZipArchive.Create() )
			{
				// Add TSLPatcher.exe
				string exePath = "TSLPatcher.exe";
				var exeStream = new MemoryStream();
				var exeWriter = new BinaryWriter(exeStream);
				exeWriter.Write(new byte[] { 0x4D, 0x5A }); // PE header
				exeWriter.Flush();
				exeStream.Position = 0;
				archive.AddEntry(exePath, exeStream);

				// Add tslpatchdata folder with changes.ini
				if ( includeChangesIni )
				{
					string changesIniPath = "tslpatchdata/changes.ini";
					var changesStream = new MemoryStream();
					var changesWriter = new StreamWriter(changesStream);
					changesWriter.WriteLine("[Settings]");
					changesWriter.WriteLine("Version=1.0");
					changesWriter.Flush();
					changesStream.Position = 0;
					archive.AddEntry(changesIniPath, changesStream);
				}

				// Add namespaces.ini
				if ( includeNamespacesIni )
				{
					string namespacesIniPath = "tslpatchdata/namespaces.ini";
					var namespacesStream = new MemoryStream();
					var namespacesWriter = new StreamWriter(namespacesStream);
					namespacesWriter.WriteLine("[Namespaces]");
					namespacesWriter.WriteLine("0=Option1");
					namespacesWriter.WriteLine("1=Option2");
					namespacesWriter.WriteLine();
					namespacesWriter.WriteLine("[Option1]");
					namespacesWriter.WriteLine("Name=First Option");
					namespacesWriter.WriteLine("Description=This is the first option");
					namespacesWriter.WriteLine("IniName=changes_option1.ini");
					namespacesWriter.WriteLine();
					namespacesWriter.WriteLine("[Option2]");
					namespacesWriter.WriteLine("Name=Second Option");
					namespacesWriter.WriteLine("Description=This is the second option");
					namespacesWriter.WriteLine("IniName=changes_option2.ini");
					namespacesWriter.Flush();
					namespacesStream.Position = 0;
					archive.AddEntry(namespacesIniPath, namespacesStream);
				}

				using ( var fileStream = File.Create(archivePath) )
				{
					archive.SaveTo(fileStream, new WriterOptions(CompressionType.Deflate));
				}
			}

			_createdArchives?.Add(archivePath);
			return archivePath;
		}

		#endregion

		#region Flat Archive Tests

		[Test]
		public void GenerateInstructions_FlatArchive_CreatesExtractAndMove()
		{
			// Arrange
			string archivePath = CreateFlatArchive("FlatMod.zip", "file1.2da", "file2.tga", "file3.tpc");
			var component = new ModComponent { Name = "FlatMod", Guid = Guid.NewGuid() };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True, "Generation should succeed");
			Assert.That(component.Instructions, Has.Count.EqualTo(2));
			Assert.That(component.InstallationMethod, Is.EqualTo("Loose-File Mod"));

			// Verify Extract instruction
			Instruction extractInstruction = component.Instructions[0];
			Assert.That(extractInstruction.Action, Is.EqualTo(Instruction.ActionType.Extract));
			Assert.That(extractInstruction.Source, Has.Count.EqualTo(1));
			Assert.That(extractInstruction.Source[0], Does.Contain("FlatMod.zip"));
			Assert.That(extractInstruction.Source[0], Does.Contain("<<modDirectory>>"));

			// Verify Move instruction
			Instruction moveInstruction = component.Instructions[1];
			Assert.That(moveInstruction.Action, Is.EqualTo(Instruction.ActionType.Move));
			Assert.That(moveInstruction.Source, Has.Count.EqualTo(1));
			Assert.That(moveInstruction.Source[0], Does.Contain("FlatMod\\*"));
			Assert.That(moveInstruction.Destination, Is.EqualTo(@"<<kotorDirectory>>\Override"));
		}

		[Test]
		public void GenerateInstructions_FlatArchiveNoGameFiles_ReturnsFalse()
		{
			// Arrange
			string archivePath = CreateFlatArchive("NoGameFiles.zip", "readme.txt", "install.bat");
			var component = new ModComponent { Name = "NoGameFiles", Guid = Guid.NewGuid() };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.False, "Generation should fail for archives without game files");
			Assert.That(component.Instructions, Is.Empty);
		}

		#endregion

		#region Single Folder Archive Tests

		[Test]
		public void GenerateInstructions_SingleFolderArchive_CreatesExtractAndMove()
		{
			// Arrange
			string archivePath = CreateSingleFolderArchive("SingleFolder.zip", "Override", "file1.2da", "file2.tga");
			var component = new ModComponent { Name = "SingleFolder", Guid = Guid.NewGuid() };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Instructions, Has.Count.EqualTo(2));

			// Verify Move instruction targets the specific folder
			Instruction moveInstruction = component.Instructions[1];
			Assert.That(moveInstruction.Action, Is.EqualTo(Instruction.ActionType.Move));
			Assert.That(moveInstruction.Source[0], Does.Contain("SingleFolder\\Override\\*"));
		}

		#endregion

		#region Multi-Folder Archive Tests

		[Test]
		public void GenerateInstructions_MultiFolderArchive_CreatesExtractAndChoose()
		{
			// Arrange
			var folders = new Dictionary<string, string[]>
			{
				{ "Option_A", new[] { "file1.2da", "file2.tga" } },
				{ "Option_B", new[] { "file1.2da", "file2.tga" } }
			};
			string archivePath = CreateMultiFolderArchive("MultiOption.zip", folders);
			var component = new ModComponent { Name = "MultiOption", Guid = Guid.NewGuid() };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Instructions, Has.Count.EqualTo(2));

			// Verify Extract instruction
			Assert.That(component.Instructions[0].Action, Is.EqualTo(Instruction.ActionType.Extract));

			// Verify Choose instruction
			Instruction chooseInstruction = component.Instructions[1];
			Assert.That(chooseInstruction.Action, Is.EqualTo(Instruction.ActionType.Choose));
			Assert.That(chooseInstruction.Source, Has.Count.EqualTo(2));

			// Verify Options were created
			Assert.That(component.Options, Has.Count.EqualTo(2));
			Assert.That(component.Options.Any(o => o.Name == "Option_A"), Is.True);
			Assert.That(component.Options.Any(o => o.Name == "Option_B"), Is.True);

			// Verify each option has a Move instruction
			foreach ( Option option in component.Options )
			{
				Assert.That(option.Instructions, Has.Count.EqualTo(1));
				Instruction moveInstruction = option.Instructions[0];
				Assert.That(moveInstruction.Action, Is.EqualTo(Instruction.ActionType.Move));
				Assert.That(moveInstruction.Source[0], Does.Contain(option.Name));
				Assert.That(moveInstruction.Destination, Is.EqualTo(@"<<kotorDirectory>>\Override"));
			}
		}

		[Test]
		public void GenerateInstructions_ThreeFolderArchive_CreatesAllOptions()
		{
			// Arrange
			var folders = new Dictionary<string, string[]>
			{
				{ "Version1", new[] { "appearance.2da" } },
				{ "Version2", new[] { "appearance.2da" } },
				{ "Version3", new[] { "appearance.2da" } }
			};
			string archivePath = CreateMultiFolderArchive("ThreeVersions.zip", folders);
			var component = new ModComponent { Name = "ThreeVersions", Guid = Guid.NewGuid() };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Options, Has.Count.EqualTo(3));
			foreach ( Option option in component.Options )
			{
				Assert.That(option.Instructions, Has.Count.EqualTo(1));
			}
		}

		[Test]
		public void GenerateInstructions_MultiFolderWithNonGameFiles_IgnoresEmptyFolders()
		{
			// Arrange
			var folders = new Dictionary<string, string[]>
			{
				{ "WithGameFiles", new[] { "file1.2da", "readme.txt" } },
				{ "OnlyDocs", new[] { "readme.txt", "license.txt" } },
				{ "AlsoGameFiles", new[] { "file2.tga" } }
			};
			string archivePath = CreateMultiFolderArchive("MixedFolders.zip", folders);
			var component = new ModComponent { Name = "MixedFolders", Guid = Guid.NewGuid() };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Options, Has.Count.EqualTo(2)); // Only folders with game files
			Assert.That(component.Options.Any(o => o.Name == "WithGameFiles"), Is.True);
			Assert.That(component.Options.Any(o => o.Name == "AlsoGameFiles"), Is.True);
			Assert.That(component.Options.Any(o => o.Name == "OnlyDocs"), Is.False);
		}

		#endregion

		#region TSLPatcher Tests

		[Test]
		public void GenerateInstructions_SimpleTslPatcher_CreatesExtractAndPatcher()
		{
			// Arrange
			string archivePath = CreateTslPatcherArchive("SimplePatcher.zip", includeNamespacesIni: false, includeChangesIni: true);
			var component = new ModComponent { Name = "SimplePatcher", Guid = Guid.NewGuid() };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Instructions, Has.Count.EqualTo(2));
			Assert.That(component.InstallationMethod, Is.EqualTo("TSLPatcher"));

			// Verify Extract instruction
			Assert.That(component.Instructions[0].Action, Is.EqualTo(Instruction.ActionType.Extract));

			// Verify Patcher instruction
			Instruction patcherInstruction = component.Instructions[1];
			Assert.That(patcherInstruction.Action, Is.EqualTo(Instruction.ActionType.Patcher));
			Assert.That(patcherInstruction.Source[0], Does.Contain("TSLPatcher.exe"));
			Assert.That(patcherInstruction.Destination, Is.EqualTo("<<kotorDirectory>>"));
		}

		[Test]
		public void GenerateInstructions_TslPatcherWithNamespaces_CreatesExtractAndChoose()
		{
			// Arrange
			string archivePath = CreateTslPatcherArchive("NamespacesPatcher.zip", includeNamespacesIni: true, includeChangesIni: true);
			var component = new ModComponent { Name = "NamespacesPatcher", Guid = Guid.NewGuid() };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Instructions, Has.Count.EqualTo(2));
			Assert.That(component.InstallationMethod, Is.EqualTo("TSLPatcher"));

			// Verify Extract instruction
			Assert.That(component.Instructions[0].Action, Is.EqualTo(Instruction.ActionType.Extract));

			// Verify Choose instruction
			Instruction chooseInstruction = component.Instructions[1];
			Assert.That(chooseInstruction.Action, Is.EqualTo(Instruction.ActionType.Choose));
			Assert.That(chooseInstruction.Source, Has.Count.EqualTo(2));

			// Verify Options from namespaces.ini
			Assert.That(component.Options, Has.Count.EqualTo(2));

			Option firstOption = component.Options[0];
			Assert.That(firstOption.Name, Is.EqualTo("First Option"));
			Assert.That(firstOption.Description, Is.EqualTo("This is the first option"));
			Assert.That(firstOption.Instructions, Has.Count.EqualTo(1));

			Instruction firstPatcher = firstOption.Instructions[0];
			Assert.That(firstPatcher.Action, Is.EqualTo(Instruction.ActionType.Patcher));
			Assert.That(firstPatcher.Arguments, Is.EqualTo("changes_option1.ini"));

			Option secondOption = component.Options[1];
			Assert.That(secondOption.Name, Is.EqualTo("Second Option"));
			Assert.That(secondOption.Description, Is.EqualTo("This is the second option"));

			Instruction secondPatcher = secondOption.Instructions[0];
			Assert.That(secondPatcher.Arguments, Is.EqualTo("changes_option2.ini"));
		}

		#endregion

		#region Edge Cases and Error Handling

		[Test]
		public void GenerateInstructions_NonExistentArchive_ReturnsFalse()
		{
			// Arrange
			Debug.Assert(_testDirectory != null);
			string archivePath = Path.Combine(_testDirectory, "NonExistent.zip");
			var component = new ModComponent { Name = "NonExistent", Guid = Guid.NewGuid() };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.False);
			Assert.That(component.Instructions, Is.Empty);
		}

		[Test]
		public void GenerateInstructions_NullComponent_ThrowsArgumentNullException()
		{
			// Arrange
			string archivePath = CreateFlatArchive("Test.zip", "file.2da");

			// Act & Assert
			Assert.Throws<ArgumentNullException>(() =>
				AutoInstructionGenerator.GenerateInstructions(null, archivePath));
		}

		[Test]
		public void GenerateInstructions_NullArchivePath_ThrowsArgumentException()
		{
			// Arrange
			var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };

			// Act & Assert
			Assert.Throws<ArgumentException>(() =>
				AutoInstructionGenerator.GenerateInstructions(component, null));
		}

		[Test]
		public void GenerateInstructions_ComponentWithExistingInstructions_ReplacesInstructions()
		{
			// Arrange
			string archivePath = CreateFlatArchive("ReplaceTest.zip", "file.2da");
			var component = new ModComponent { Name = "ReplaceTest", Guid = Guid.NewGuid() };

			// Add existing instruction
			var existingInstruction = new Instruction { Action = Instruction.ActionType.Move };
			existingInstruction.SetParentComponent(component);
			component.Instructions.Add(existingInstruction);

			Assert.That(component.Instructions, Has.Count.EqualTo(1));

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Instructions, Has.Count.EqualTo(2)); // Extract + Move
			Assert.That(component.Instructions.Contains(existingInstruction), Is.False);
		}

		#endregion

		#region File Extension Detection Tests

		[TestCase("appearance.2da")]
		[TestCase("texture.tga")]
		[TestCase("texture.tpc")]
		[TestCase("model.mdl")]
		[TestCase("script.ncs")]
		[TestCase("dialog.dlg")]
		[TestCase("item.uti")]
		public void GenerateInstructions_RecognizesGameFileExtension(string fileName)
		{
			// Arrange
			string archivePath = CreateFlatArchive($"Test_{Path.GetExtension(fileName)}.zip", fileName);
			var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True, $"Should recognize {Path.GetExtension(fileName)} as a game file");
			Assert.That(component.Instructions, Has.Count.EqualTo(2));
		}

		#endregion

		#region Instruction Validation Tests

		[Test]
		public void GenerateInstructions_AllInstructionsHaveValidGuids()
		{
			// Arrange
			var folders = new Dictionary<string, string[]>
			{
				{ "OptionA", new[] { "file1.2da" } },
				{ "OptionB", new[] { "file2.2da" } }
			};
			string archivePath = CreateMultiFolderArchive("GuidTest.zip", folders);
			var component = new ModComponent { Name = "GuidTest", Guid = Guid.NewGuid() };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);

			// Check all instruction GUIDs
			foreach ( Instruction instruction in component.Instructions )
			{
				Assert.That(instruction.Guid, Is.Not.EqualTo(Guid.Empty));
			}

			// Check all option GUIDs and their instruction GUIDs
			foreach ( Option option in component.Options )
			{
				Assert.That(option.Guid, Is.Not.EqualTo(Guid.Empty));
				foreach ( Instruction instruction in option.Instructions )
				{
					Assert.That(instruction.Guid, Is.Not.EqualTo(Guid.Empty));
				}
			}
		}

		[Test]
		public void GenerateInstructions_PathsUseCorrectPlaceholders()
		{
			// Arrange
			string archivePath = CreateFlatArchive("PathTest.zip", "file.2da");
			var component = new ModComponent { Name = "PathTest", Guid = Guid.NewGuid() };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);

			// Check Extract instruction
			Instruction extractInstruction = component.Instructions[0];
			Assert.That(extractInstruction.Source[0], Does.Contain("<<modDirectory>>"));
			Assert.That(extractInstruction.Source[0], Does.Not.Contain(_testDirectory));

			// Check Move instruction
			Instruction moveInstruction = component.Instructions[1];
			Assert.That(moveInstruction.Source[0], Does.Contain("<<modDirectory>>"));
			Assert.That(moveInstruction.Destination, Does.Contain("<<kotorDirectory>>"));
		}

		#endregion

		#region Hybrid Scenario Tests

		[Test]
		public void GenerateInstructions_TslPatcherWithSingleOverrideFolder_CreatesHybridInstructions()
		{
			// Arrange: Archive with TSLPatcher + one Override folder
			string archivePath = CreateHybridArchive("hybrid_patcher_single.zip",
				hasTslPatcher: true,
				hasNamespacesIni: false,
				overrideFolders: new Dictionary<string, string[]>
				{
					{ "Textures", new[] { "test.tga", "test2.tpc" } }
				});

			var component = new ModComponent { Name = "HybridMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True, "Should successfully generate instructions");
			Assert.That(component.Instructions.Count, Is.EqualTo(3), "Should have Extract + Patcher + Move");
			Assert.That(component.InstallationMethod, Is.EqualTo("Hybrid (TSLPatcher + Loose Files)"));

			// Verify Extract
			Instruction extractInstruction = component.Instructions[0];
			Assert.That(extractInstruction.Action, Is.EqualTo(Instruction.ActionType.Extract));
			Assert.That(extractInstruction.Source[0], Does.Contain("hybrid_patcher_single.zip"));

			// Verify Patcher
			Instruction patcherInstruction = component.Instructions[1];
			Assert.That(patcherInstruction.Action, Is.EqualTo(Instruction.ActionType.Patcher));
			Assert.That(patcherInstruction.Source[0], Does.Contain("TSLPatcher.exe"));
			Assert.That(patcherInstruction.Destination, Does.Contain("<<kotorDirectory>>"));

			// Verify Move
			Instruction moveInstruction = component.Instructions[2];
			Assert.That(moveInstruction.Action, Is.EqualTo(Instruction.ActionType.Move));
			Assert.That(moveInstruction.Source[0], Does.Contain("Textures"));
			Assert.That(moveInstruction.Destination, Does.Contain("Override"));
		}

		[Test]
		public void GenerateInstructions_TslPatcherWithMultipleOverrideFolders_CreatesChooseForBoth()
		{
			// Arrange: Archive with TSLPatcher + multiple Override folders
			string archivePath = CreateHybridArchive("hybrid_patcher_multi.zip",
				hasTslPatcher: true,
				hasNamespacesIni: false,
				overrideFolders: new Dictionary<string, string[]>
				{
					{ "Textures_HD", new[] { "test.tga" } },
					{ "Textures_SD", new[] { "test.tga" } }
				});

			var component = new ModComponent { Name = "HybridMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Instructions.Count, Is.EqualTo(3), "Should have Extract + Patcher + Choose");
			Assert.That(component.Options.Count, Is.EqualTo(2), "Should have 2 Override folder options");
			Assert.That(component.InstallationMethod, Is.EqualTo("Hybrid (TSLPatcher + Loose Files)"));

			// Verify Extract
			Assert.That(component.Instructions[0].Action, Is.EqualTo(Instruction.ActionType.Extract));

			// Verify Patcher
			Assert.That(component.Instructions[1].Action, Is.EqualTo(Instruction.ActionType.Patcher));

			// Verify Choose for Override folders
			Instruction chooseInstruction = component.Instructions[2];
			Assert.That(chooseInstruction.Action, Is.EqualTo(Instruction.ActionType.Choose));
			Assert.That(chooseInstruction.Source.Count, Is.EqualTo(2));

			// Verify Options
			Assert.That(component.Options[0].Name, Is.EqualTo("Textures_HD"));
			Assert.That(component.Options[1].Name, Is.EqualTo("Textures_SD"));
			Assert.That(component.Options[0].Instructions[0].Action, Is.EqualTo(Instruction.ActionType.Move));
			Assert.That(component.Options[1].Instructions[0].Action, Is.EqualTo(Instruction.ActionType.Move));
		}

		[Test]
		public void GenerateInstructions_NamespacesWithOverrideFolder_CreatesTwoChooseInstructions()
		{
			// Arrange: Archive with namespaces.ini TSLPatcher + Override folder
			string archivePath = CreateHybridArchive("hybrid_namespaces.zip",
				hasTslPatcher: true,
				hasNamespacesIni: true,
				overrideFolders: new Dictionary<string, string[]>
				{
					{ "Bonus", new[] { "bonus.2da" } }
				});

			var component = new ModComponent { Name = "ComplexHybridMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Instructions.Count, Is.EqualTo(3), "Extract + Choose(Namespaces) + Move");
			Assert.That(component.Options.Count, Is.AtLeast(1), "Should have namespace options");
			Assert.That(component.InstallationMethod, Is.EqualTo("Hybrid (TSLPatcher + Loose Files)"));

			// Verify Extract
			Assert.That(component.Instructions[0].Action, Is.EqualTo(Instruction.ActionType.Extract));

			// Verify first Choose (for namespaces)
			Assert.That(component.Instructions[1].Action, Is.EqualTo(Instruction.ActionType.Choose));

			// Verify Move for Override folder
			Assert.That(component.Instructions[2].Action, Is.EqualTo(Instruction.ActionType.Move));
			Assert.That(component.Instructions[2].Source[0], Does.Contain("Bonus"));
		}

		[Test]
		public void GenerateInstructions_TslPatcherWithFlatFiles_CreatesPatcherAndMove()
		{
			// Arrange: Archive with TSLPatcher + flat files in root
			string archivePath = CreateHybridArchiveWithFlatFiles("hybrid_flat.zip",
				hasTslPatcher: true,
				flatFiles: new[] { "appearance.2da", "dialog.tlk" });

			var component = new ModComponent { Name = "HybridFlatMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Instructions.Count, Is.EqualTo(3), "Extract + Patcher + Move");
			Assert.That(component.InstallationMethod, Is.EqualTo("Hybrid (TSLPatcher + Loose Files)"));

			// Verify Move moves from root (not from a subfolder)
			Instruction moveInstruction = component.Instructions[2];
			Assert.That(moveInstruction.Source[0], Does.Match(@"<<modDirectory>>\\[^\\]+\\\*$"),
				"Should move from extracted root, not a subfolder");
		}

		[Test]
		public void GenerateInstructions_TslPatcherFolderExcludedFromOverride_DoesNotCreateMoveForPatcherFolder()
		{
			// Arrange: Archive with TSLPatcher in subfolder + another folder with files
			string archivePath = CreateHybridArchive("hybrid_nested_patcher.zip",
				hasTslPatcher: true,
				hasNamespacesIni: false,
				patcherInSubfolder: "PatcherFolder",
				overrideFolders: new Dictionary<string, string[]>
				{
					{ "Override", new[] { "test.2da" } }
				});

			var component = new ModComponent { Name = "NestedPatcherMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Instructions.Count, Is.EqualTo(3), "Extract + Patcher + Move");

			// Verify only Override folder gets a Move, not PatcherFolder
			Instruction moveInstruction = component.Instructions[2];
			Assert.That(moveInstruction.Source[0], Does.Contain("Override"));
			Assert.That(moveInstruction.Source[0], Does.Not.Contain("PatcherFolder"));
		}

		[Test]
		public void GenerateInstructions_TslPatchDataFolderNotMovedToOverride_OnlyGameFoldersMove()
		{
			// Arrange: Archive with tslpatchdata folder (with game files) and Override folder (with game files)
			string archivePath = CreateComplexHybridArchive("hybrid_complex.zip");

			var component = new ModComponent { Name = "ComplexMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Instructions.Count, Is.EqualTo(3), "Should have Extract + Patcher + Move");

			// Find the Move instruction
			var moveInstructions = component.Instructions.Where(i => i.Action == Instruction.ActionType.Move).ToList();
			Assert.That(moveInstructions.Count, Is.EqualTo(1), "Should have exactly one Move instruction");

			// Ensure Move instruction is for Override folder, NOT tslpatchdata
			Instruction moveInstruction = moveInstructions[0];
			Assert.That(moveInstruction.Source[0], Does.Contain("Override"),
				"Should move from Override folder");
			Assert.That(moveInstruction.Source[0], Does.Not.Contain("tslpatchdata"),
				"Should NOT move from tslpatchdata folder");
		}

		#endregion

		#region Edge Case Tests

		[Test]
		public void GenerateInstructions_EmptyArchive_ReturnsFalse()
		{
			// Arrange: Archive with no files
			Debug.Assert(_testDirectory != null);
			string archivePath = Path.Combine(_testDirectory, "empty.zip");
			using ( var archive = ZipArchive.Create() )
			{
				using ( var fileStream = File.Create(archivePath) )
				{
					archive.SaveTo(fileStream, new WriterOptions(CompressionType.Deflate));
				}
			}

			var component = new ModComponent { Name = "EmptyMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.False, "Empty archive should return false");
			Assert.That(component.Instructions.Count, Is.EqualTo(0));
		}

		[Test]
		public void GenerateInstructions_OnlyNonGameFiles_ReturnsFalse()
		{
			// Arrange: Archive with only non-game files (txt, pdf, etc)
			string archivePath = CreateFlatArchive("non_game_files.zip",
				"readme.txt", "license.pdf", "info.doc");

			var component = new ModComponent { Name = "NonGameMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.False, "Should return false for non-game files only");
			Assert.That(component.Instructions.Count, Is.EqualTo(0));
		}

		[Test]
		public void GenerateInstructions_MixedGameAndNonGameFiles_OnlyProcessesGameFiles()
		{
			// Arrange: Archive with mixed file types
			string archivePath = CreateFlatArchive("mixed_files.zip",
				"appearance.2da", "readme.txt", "dialog.tlk", "license.pdf");

			var component = new ModComponent { Name = "MixedMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True, "Should process game files");
			Assert.That(component.Instructions.Count, Is.EqualTo(2), "Extract + Move");
			Assert.That(component.InstallationMethod, Is.EqualTo("Loose-File Mod"));
		}

		[Test]
		public void GenerateInstructions_DeeplyNestedFiles_UsesTopLevelFolder()
		{
			// Arrange: Archive with deeply nested structure
			Debug.Assert(_testDirectory != null);
			string archivePath = Path.Combine(_testDirectory, "nested.zip");
			using ( var archive = ZipArchive.Create() )
			{
				var memStream = new MemoryStream();
				var writer = new StreamWriter(memStream);
				writer.WriteLine("Test content");
				writer.Flush();
				memStream.Position = 0;
				archive.AddEntry("TopFolder/Sub1/Sub2/Sub3/file.2da", memStream);

				using ( var fileStream = File.Create(archivePath) )
				{
					archive.SaveTo(fileStream, new WriterOptions(CompressionType.Deflate));
				}
			}

			var component = new ModComponent { Name = "NestedMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);

			// Should track TopFolder, not the deeply nested path
			Instruction moveInstruction = component.Instructions[1];
			Assert.That(moveInstruction.Source[0], Does.Contain("TopFolder"));
		}

		[Test]
		public void GenerateInstructions_FourFolderArchive_CreatesAllFourOptions()
		{
			// Arrange: Archive with 4 folders (edge case for multiple choices)
			var folders = new Dictionary<string, string[]>
			{
				{ "Version_A", new[] { "file.2da" } },
				{ "Version_B", new[] { "file.2da" } },
				{ "Version_C", new[] { "file.2da" } },
				{ "Version_D", new[] { "file.2da" } }
			};
			string archivePath = CreateMultiFolderArchive("four_folders.zip", folders);

			var component = new ModComponent { Name = "FourVersionMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Options.Count, Is.EqualTo(4), "Should create 4 options");
			Assert.That(component.Instructions[1].Action, Is.EqualTo(Instruction.ActionType.Choose));
			Assert.That(component.Instructions[1].Source.Count, Is.EqualTo(4));

			// Verify all option names
			var optionNames = component.Options.Select(o => o.Name).ToList();
			Assert.That(optionNames, Contains.Item("Version_A"));
			Assert.That(optionNames, Contains.Item("Version_B"));
			Assert.That(optionNames, Contains.Item("Version_C"));
			Assert.That(optionNames, Contains.Item("Version_D"));
		}

		[Test]
		public void GenerateInstructions_CaseInsensitiveTslPatchData_DetectsTslPatcher()
		{
			// Arrange: Archive with case variations (TSLPatchData, tslpatchdata, TsLpAtChDaTa)
			Debug.Assert(_testDirectory != null);
			string archivePath = Path.Combine(_testDirectory, "case_test.zip");
			using ( var archive = ZipArchive.Create() )
			{
				// Use uppercase TSLPATCHDATA
				var changesStream = new MemoryStream();
				var changesWriter = new StreamWriter(changesStream);
				changesWriter.WriteLine("[Settings]");
				changesWriter.Flush();
				changesStream.Position = 0;
				archive.AddEntry("TSLPATCHDATA/changes.ini", changesStream);

				var exeStream = new MemoryStream();
				var exeWriter = new BinaryWriter(exeStream);
				exeWriter.Write(new byte[] { 0x4D, 0x5A });
				exeWriter.Flush();
				exeStream.Position = 0;
				archive.AddEntry("TSLPatcher.exe", exeStream);

				using ( var fileStream = File.Create(archivePath) )
				{
					archive.SaveTo(fileStream, new WriterOptions(CompressionType.Deflate));
				}
			}

			var component = new ModComponent { Name = "CaseTestMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.InstallationMethod, Is.EqualTo("TSLPatcher"));
			Assert.That(component.Instructions.Any(i => i.Action == Instruction.ActionType.Patcher), Is.True);
		}

		[Test]
		public void GenerateInstructions_AllSupportedGameFileExtensions_ProcessedCorrectly()
		{
			// Arrange: Archive with all supported game file extensions
			var extensions = new[]
			{
				".2da", ".are", ".bik", ".dds", ".dlg", ".git", ".gui", ".ifo",
				".jrl", ".lip", ".lyt", ".mdl", ".mdx", ".mp3", ".ncs", ".pth",
				".ssf", ".tga", ".tlk", ".txi", ".tpc", ".utc", ".utd", ".ute",
				".uti", ".utm", ".utp", ".uts", ".utw", ".vis", ".wav"
			};

			var fileNames = extensions.Select(ext => $"testfile{ext}").ToArray();
			string archivePath = CreateFlatArchive("all_extensions.zip", fileNames);

			var component = new ModComponent { Name = "AllExtensionsMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True, "All game file extensions should be recognized");
			Assert.That(component.Instructions.Count, Is.EqualTo(2), "Extract + Move");
		}

		[Test]
		public void GenerateInstructions_FoldersWithOnlyNonGameFiles_NotIncludedInChoose()
		{
			// Arrange: Archive with some folders having only non-game files
			var folders = new Dictionary<string, string[]>
			{
				{ "ValidFolder", new[] { "test.2da" } },
				{ "DocsFolder", new[] { "readme.txt", "manual.pdf" } },
				{ "AnotherValid", new[] { "dialog.tlk" } }
			};
			string archivePath = CreateMultiFolderArchive("mixed_folder_types.zip", folders);

			var component = new ModComponent { Name = "MixedFolderMod" };

			// Act
			bool result = AutoInstructionGenerator.GenerateInstructions(component, archivePath);

			// Assert
			Assert.That(result, Is.True);
			Assert.That(component.Options.Count, Is.EqualTo(2), "Only folders with game files should be options");

			var optionNames = component.Options.Select(o => o.Name).ToList();
			Assert.That(optionNames, Contains.Item("ValidFolder"));
			Assert.That(optionNames, Contains.Item("AnotherValid"));
			Assert.That(optionNames, Does.Not.Contain("DocsFolder"));
		}

		#endregion

		#region Helper Methods for Hybrid Tests

		private string CreateHybridArchive(string archiveName, bool hasTslPatcher, bool hasNamespacesIni,
			Dictionary<string, string[]>? overrideFolders = null, string? patcherInSubfolder = null)
		{
			Debug.Assert(_testDirectory != null);
			string archivePath = Path.Combine(_testDirectory, archiveName);
			using ( var archive = ZipArchive.Create() )
			{
				// Add TSLPatcher files if requested
				if ( hasTslPatcher )
				{
					string basePath = string.IsNullOrEmpty(patcherInSubfolder) ? "" : patcherInSubfolder + "/";

					// Add TSLPatcher.exe
					var exeStream = new MemoryStream();
					var exeWriter = new BinaryWriter(exeStream);
					exeWriter.Write(new byte[] { 0x4D, 0x5A });
					exeWriter.Flush();
					exeStream.Position = 0;
					archive.AddEntry(basePath + "TSLPatcher.exe", exeStream);

					// Add tslpatchdata folder
					if ( hasNamespacesIni )
					{
						var namespaceStream = new MemoryStream();
						var namespaceWriter = new StreamWriter(namespaceStream);
						namespaceWriter.WriteLine("[Namespaces]");
						namespaceWriter.WriteLine("Namespace0=Option1");
						namespaceWriter.WriteLine("[Option1]");
						namespaceWriter.WriteLine("Name=Test Option 1");
						namespaceWriter.WriteLine("IniName=changes_option1.ini");
						namespaceWriter.Flush();
						namespaceStream.Position = 0;
						archive.AddEntry(basePath + "tslpatchdata/namespaces.ini", namespaceStream);
					}
					else
					{
						var changesStream = new MemoryStream();
						var changesWriter = new StreamWriter(changesStream);
						changesWriter.WriteLine("[Settings]");
						changesWriter.Flush();
						changesStream.Position = 0;
						archive.AddEntry(basePath + "tslpatchdata/changes.ini", changesStream);
					}
				}

				// Add override folders if requested
				if ( overrideFolders != null )
				{
					foreach ( var kvp in overrideFolders )
					{
						foreach ( string fileName in kvp.Value )
						{
							var fileStream = new MemoryStream();
							var fileWriter = new StreamWriter(fileStream);
							fileWriter.WriteLine($"Content for {fileName}");
							fileWriter.Flush();
							fileStream.Position = 0;
							archive.AddEntry($"{kvp.Key}/{fileName}", fileStream);
						}
					}
				}

				using ( var fileStream = File.Create(archivePath) )
				{
					archive.SaveTo(fileStream, new WriterOptions(CompressionType.Deflate));
				}
			}
			Debug.Assert(_testDirectory != null);
			_createdArchives?.Add(archivePath);
			return archivePath;
		}

		private string CreateHybridArchiveWithFlatFiles(string archiveName, bool hasTslPatcher, string[] flatFiles)
		{
			Debug.Assert(_testDirectory != null);
			string archivePath = Path.Combine(_testDirectory, archiveName);
			using ( var archive = ZipArchive.Create() )
			{
				// Add TSLPatcher if requested
				if ( hasTslPatcher )
				{
					var exeStream = new MemoryStream();
					var exeWriter = new BinaryWriter(exeStream);
					exeWriter.Write(new byte[] { 0x4D, 0x5A });
					exeWriter.Flush();
					exeStream.Position = 0;
					archive.AddEntry("TSLPatcher.exe", exeStream);

					var changesStream = new MemoryStream();
					var changesWriter = new StreamWriter(changesStream);
					changesWriter.WriteLine("[Settings]");
					changesWriter.Flush();
					changesStream.Position = 0;
					archive.AddEntry("tslpatchdata/changes.ini", changesStream);
				}

				// Add flat files in root
				foreach ( string fileName in flatFiles )
				{
					var fileStream = new MemoryStream();
					var fileWriter = new StreamWriter(fileStream);
					fileWriter.WriteLine($"Content for {fileName}");
					fileWriter.Flush();
					fileStream.Position = 0;
					archive.AddEntry(fileName, fileStream);
				}

				using ( var fileStream = File.Create(archivePath) )
				{
					archive.SaveTo(fileStream, new WriterOptions(CompressionType.Deflate));
				}
			}

			_createdArchives?.Add(archivePath);
			return archivePath;
		}

		private string CreateComplexHybridArchive(string archiveName)
		{
			Debug.Assert(_testDirectory != null);
			string archivePath = Path.Combine(_testDirectory, archiveName);
			using ( var archive = ZipArchive.Create() )
			{
				// Add TSLPatcher in root
				var exeStream = new MemoryStream();
				var exeWriter = new BinaryWriter(exeStream);
				exeWriter.Write(new byte[] { 0x4D, 0x5A });
				exeWriter.Flush();
				exeStream.Position = 0;
				archive.AddEntry("TSLPatcher.exe", exeStream);

				var changesStream = new MemoryStream();
				var changesWriter = new StreamWriter(changesStream);
				changesWriter.WriteLine("[Settings]");
				changesWriter.Flush();
				changesStream.Position = 0;
				archive.AddEntry("tslpatchdata/changes.ini", changesStream);

				// Add a game file in tslpatchdata (should be ignored for Override)
				var dataFileStream = new MemoryStream();
				var dataFileWriter = new StreamWriter(dataFileStream);
				dataFileWriter.WriteLine("Data");
				dataFileWriter.Flush();
				dataFileStream.Position = 0;
				archive.AddEntry("tslpatchdata/appearance.2da", dataFileStream);

				// Add a separate Override folder
				var overrideFileStream = new MemoryStream();
				var overrideFileWriter = new StreamWriter(overrideFileStream);
				overrideFileWriter.WriteLine("Override content");
				overrideFileWriter.Flush();
				overrideFileStream.Position = 0;
				archive.AddEntry("Override/custom.2da", overrideFileStream);

				using ( var fileStream = File.Create(archivePath) )
				{
					archive.SaveTo(fileStream, new WriterOptions(CompressionType.Deflate));
				}
			}

			_createdArchives?.Add(archivePath);
			return archivePath;
		}

		#endregion
	}
}
