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
	/// Tests the ModComponent.TryGenerateInstructionsFromArchive() method
	/// which is used by the real app to auto-generate instructions from ModLinks.
	/// </summary>
	[TestFixture]
	public class ModComponentInstructionGenerationTests
	{
		private string? _testDirectory;
		private MainConfig? _mainConfig;

		[SetUp]
		public void SetUp()
		{
			_testDirectory = Path.Combine(Path.GetTempPath(), "KOTORModSync_ModComponentTests_" + Guid.NewGuid());
			Directory.CreateDirectory(_testDirectory);

			// Initialize MainConfig - this is required for TryGenerateInstructionsFromArchive
			_mainConfig = new MainConfig();
			_mainConfig.sourcePath = new DirectoryInfo(_testDirectory);
			_mainConfig.destinationPath = new DirectoryInfo(Path.Combine(_testDirectory, "KOTOR"));
			Directory.CreateDirectory(_mainConfig.destinationPath.FullName);
		}

		[TearDown]
		public void TearDown()
		{
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

		#region Helper Methods

		private string CreateTslPatcherArchive(string archiveName)
		{
			Debug.Assert(_testDirectory != null);
			string archivePath = Path.Combine(_testDirectory, archiveName);
			using ( var archive = ZipArchive.Create() )
			{
				// Add TSLPatcher.exe
				var exeStream = new MemoryStream();
				var exeWriter = new BinaryWriter(exeStream);
				exeWriter.Write(new byte[] { 0x4D, 0x5A }); // PE header
				exeWriter.Flush();
				exeStream.Position = 0;
				archive.AddEntry("TSLPatcher.exe", exeStream);

				// Add tslpatchdata/changes.ini
				var changesStream = new MemoryStream();
				var changesWriter = new StreamWriter(changesStream);
				changesWriter.WriteLine("[Settings]");
				changesWriter.WriteLine("Version=1.0");
				changesWriter.Flush();
				changesStream.Position = 0;
				archive.AddEntry("tslpatchdata/changes.ini", changesStream);

				using ( var fileStream = File.Create(archivePath) )
				{
					archive.SaveTo(fileStream, new WriterOptions(CompressionType.Deflate));
				}
			}

			return archivePath;
		}

		private string CreateLooseFileArchive(string archiveName, params string[] fileNames)
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

			return archivePath;
		}

		#endregion

		[Test]
		public void TryGenerateInstructionsFromArchive_SwoopBikeUpgrades_GeneratesInstructions()
		{
			// Arrange: Create the exact mod from the TOML file
			// This is "Swoop Bike Upgrades" by Salk
			var component = new ModComponent
			{
				Guid = Guid.Parse("3b732fd8-4f55-4c34-891b-245303765eed"),
				Name = "Swoop Bike Upgrades",
				Author = "Salk",
				Tier = "4 - Optional",
				Description = "Originally, swoop bikes in KOTOR were intended to have upgrades available for purchase which would modify their performance.",
				InstallationMethod = "TSLPatcher Mod",
				IsSelected = false,
				Category = new List<string> { "Restored Content" },
				ModLink = new List<string> { "https://deadlystream.com/files/file/2473-kotor-swoop-bike-upgrades/" }
			};

			// Create a mock archive file that would be downloaded
			// The filename should be extracted from the URL
			string archiveName = "kotor-swoop-bike-upgrades.zip";
			string archivePath = CreateTslPatcherArchive(archiveName);

			// Act: Call the exact method that the real app uses
			bool result = component.TryGenerateInstructionsFromArchive();

			// Assert
			Assert.That(result, Is.True, "Should successfully generate instructions");
			Assert.That(component.Instructions, Is.Not.Empty, "Should have generated at least one instruction");
			Assert.That(component.InstallationMethod, Is.Not.Null.And.Not.Empty, "Should have set InstallationMethod");

			// Log results for debugging
			Console.WriteLine($"Generated {component.Instructions.Count} instructions");
			Console.WriteLine($"Installation Method: {component.InstallationMethod}");
			foreach ( var instruction in component.Instructions )
			{
				Console.WriteLine($"  - {instruction.Action}: {string.Join(", ", instruction.Source)}");
			}
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_WithExactFileName_UsesCorrectArchive()
		{
			// Arrange: Create multiple archives to ensure it picks the right one
			string wrongArchive1 = CreateTslPatcherArchive("wrong-mod-1.zip");
			string wrongArchive2 = CreateTslPatcherArchive("wrong-mod-2.zip");
			string correctArchive = CreateTslPatcherArchive("correct-mod.zip");

			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Correct Mod",
				ModLink = new List<string> { "https://example.com/download/correct-mod.zip" }
			};

			// Act
			bool result = component.TryGenerateInstructionsFromArchive();

			// Assert
			Assert.That(result, Is.True, "Should find the correct archive");
			Assert.That(component.Instructions, Is.Not.Empty);

			// Verify it used the correct archive
			var extractInstruction = component.Instructions.FirstOrDefault(i => i.Action == Instruction.ActionType.Extract);
			Assert.That(extractInstruction, Is.Not.Null);
			Assert.That(extractInstruction.Source[0], Does.Contain("correct-mod.zip"));
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_WithoutMatchingArchive_ReturnsFalse()
		{
			// Arrange
			// Create some archives but none that match
			CreateTslPatcherArchive("unrelated-mod.zip");

			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Non-Existent Mod",
				ModLink = new List<string> { "https://example.com/download/missing-mod.zip" }
			};

			// Act
			bool result = component.TryGenerateInstructionsFromArchive();

			// Assert
			Assert.That(result, Is.False, "Should return false when archive is not found");
			Assert.That(component.Instructions.Count, Is.EqualTo(0), "Should not generate any instructions");
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_WithFuzzyMatch_FindsArchive()
		{
			// Arrange: Create archive with slightly different name
			string archiveName = "Swoop_Bike_Upgrades_v1.2.zip";
			CreateTslPatcherArchive(archiveName);

			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Swoop Bike Upgrades",
				ModLink = new List<string> { "https://example.com/swoop-bike-upgrades.zip" }
			};

			// Act
			bool result = component.TryGenerateInstructionsFromArchive();

			// Assert
			Assert.That(result, Is.True, "Should find archive with fuzzy matching");
			Assert.That(component.Instructions, Is.Not.Empty);
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_MultipleComponents_EachGetsUniqueInstructions()
		{
			// Arrange: This tests the bug fix - multiple components should get different instructions
			string archive1 = CreateTslPatcherArchive("mod-one.zip");
			string archive2 = CreateLooseFileArchive("mod-two.zip", "file1.2da", "file2.tga");
			string archive3 = CreateTslPatcherArchive("mod-three.zip");

			var component1 = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Mod One",
				ModLink = new List<string> { "https://example.com/mod-one.zip" }
			};

			var component2 = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Mod Two",
				ModLink = new List<string> { "https://example.com/mod-two.zip" }
			};

			var component3 = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Mod Three",
				ModLink = new List<string> { "https://example.com/mod-three.zip" }
			};

			// Act
			bool result1 = component1.TryGenerateInstructionsFromArchive();
			bool result2 = component2.TryGenerateInstructionsFromArchive();
			bool result3 = component3.TryGenerateInstructionsFromArchive();

			// Assert
			Assert.That(result1, Is.True, "Component 1 should generate instructions");
			Assert.That(result2, Is.True, "Component 2 should generate instructions");
			Assert.That(result3, Is.True, "Component 3 should generate instructions");

			// Verify each component has different installation methods
			Assert.That(component1.InstallationMethod, Is.EqualTo("TSLPatcher"));
			Assert.That(component2.InstallationMethod, Is.EqualTo("Loose-File Mod"));
			Assert.That(component3.InstallationMethod, Is.EqualTo("TSLPatcher"));

			// Verify each uses its own archive
			var extract1 = component1.Instructions.First(i => i.Action == Instruction.ActionType.Extract);
			var extract2 = component2.Instructions.First(i => i.Action == Instruction.ActionType.Extract);
			var extract3 = component3.Instructions.First(i => i.Action == Instruction.ActionType.Extract);

			Assert.That(extract1.Source[0], Does.Contain("mod-one.zip"));
			Assert.That(extract2.Source[0], Does.Contain("mod-two.zip"));
			Assert.That(extract3.Source[0], Does.Contain("mod-three.zip"));
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_WithExistingInstructions_DoesNotRegenerate()
		{
			// Arrange
			CreateTslPatcherArchive("test-mod.zip");

			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Test Mod",
				ModLink = new List<string> { "https://example.com/test-mod.zip" }
			};

			// Add existing instruction
			var existingInstruction = new Instruction { Action = Instruction.ActionType.Move };
			existingInstruction.SetParentComponent(component);
			component.Instructions.Add(existingInstruction);

			// Act
			bool result = component.TryGenerateInstructionsFromArchive();

			// Assert
			Assert.That(result, Is.False, "Should not regenerate when instructions already exist");
			Assert.That(component.Instructions, Has.Count.EqualTo(1), "Should keep existing instruction");
			Assert.That(component.Instructions[0], Is.SameAs(existingInstruction));
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_WithEmptyModLink_ReturnsFalse()
		{
			// Arrange
			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "No Link Mod",
				ModLink = new List<string>() // Empty
			};

			// Act
			bool result = component.TryGenerateInstructionsFromArchive();

			// Assert
			Assert.That(result, Is.False);
			Assert.That(component.Instructions.Count, Is.EqualTo(0));
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_WithUrlModLink_ExtractsFilename()
		{
			// Arrange
			// Create archive with name that would come from URL
			string archiveName = "download-file-1234.zip";
			CreateTslPatcherArchive(archiveName);

			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "URL Test Mod",
				ModLink = new List<string> { $"https://deadlystream.com/files/download-file-1234.zip" }
			};

			// Act
			bool result = component.TryGenerateInstructionsFromArchive();

			// Assert
			Assert.That(result, Is.True, "Should extract filename from URL");
			Assert.That(component.Instructions, Is.Not.Empty);
		}
	}
}

