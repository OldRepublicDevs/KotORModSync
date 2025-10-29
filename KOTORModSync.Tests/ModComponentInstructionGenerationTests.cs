// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Diagnostics;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;

using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace KOTORModSync.Tests
{

	[TestFixture]
	public class ModComponentInstructionGenerationTests
	{
		private string? _testDirectory;
		private MainConfig? _mainConfig;

		[SetUp]
		public void SetUp()
		{
			_testDirectory = Path.Combine( Path.GetTempPath(), "KOTORModSync_ModComponentTests_" + Guid.NewGuid() );
			Directory.CreateDirectory( _testDirectory );

			_mainConfig = new MainConfig();
			_mainConfig.sourcePath = new DirectoryInfo( _testDirectory );
			_mainConfig.destinationPath = new DirectoryInfo( Path.Combine( _testDirectory, "KOTOR" ) );
			Directory.CreateDirectory( _mainConfig.destinationPath.FullName );
		}

		[TearDown]
		public void TearDown()
		{
			try
			{
				if (Directory.Exists( _testDirectory ))
					Directory.Delete( _testDirectory, recursive: true );
			}
			catch
			{

			}
		}

		#region Helper Methods

		private string CreateTslPatcherArchive( string archiveName )
		{
			Debug.Assert( _testDirectory != null );
			string archivePath = Path.Combine( _testDirectory, archiveName );
			using (var archive = ZipArchive.Create())
			{

				var exeStream = new MemoryStream();
				var exeWriter = new BinaryWriter( exeStream );
				exeWriter.Write( new byte[] { 0x4D, 0x5A } );
				exeWriter.Flush();
				exeStream.Position = 0;
				archive.AddEntry( "TSLPatcher.exe", exeStream );

				var changesStream = new MemoryStream();
				var changesWriter = new StreamWriter( changesStream );
				changesWriter.WriteLine( "[Settings]" );
				changesWriter.WriteLine( "Version=1.0" );
				changesWriter.Flush();
				changesStream.Position = 0;
				archive.AddEntry( "tslpatchdata/changes.ini", changesStream );

				using (var fileStream = File.Create( archivePath ))
				{
					archive.SaveTo( fileStream, new WriterOptions( CompressionType.Deflate ) );
				}
			}

			return archivePath;
		}

		private void CreateLooseFileArchive( string archiveName, params string[] fileNames )
		{
			Debug.Assert( _testDirectory != null );
			string archivePath = Path.Combine( _testDirectory, archiveName );
			using (var archive = ZipArchive.Create())
			{
				foreach (string fileName in fileNames)
				{
					var memStream = new MemoryStream();
					var writer = new StreamWriter( memStream );
					writer.WriteLine( $"Test content for {fileName}" );
					writer.Flush();
					memStream.Position = 0;
					archive.AddEntry( fileName, memStream );
				}

				using (FileStream fileStream = File.Create( archivePath ))
				{
					archive.SaveTo( fileStream, new WriterOptions( CompressionType.Deflate ) );
				}
			}
		}

		#endregion

		[Test]
		public void TryGenerateInstructionsFromArchive_SwoopBikeUpgrades_GeneratesInstructions()
		{

			var component = new ModComponent
			{
				Guid = Guid.Parse( "3b732fd8-4f55-4c34-891b-245303765eed" ),
				Name = "Swoop Bike Upgrades",
				Author = "Salk",
				Tier = "4 - Optional",
				Description = "Originally, swoop bikes in KOTOR were intended to have upgrades available for purchase which would modify their performance.",
				InstallationMethod = "TSLPatcher Mod",
				IsSelected = false,
				Category = new List<string> { "Restored Content" },
				ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase )
				{
					{
						"https://deadlystream.com/files/file/2473-kotor-swoop-bike-upgrades/",
						new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase)
					}
				}
			};

			const string archiveName = "kotor-swoop-bike-upgrades.zip";
			CreateTslPatcherArchive( archiveName );

			bool result = AutoInstructionGenerator.TryGenerateInstructionsFromArchive( component );

			Assert.Multiple( () =>
			{
				Assert.That( result, Is.True, "Should successfully generate instructions" );
				Assert.That( component.Instructions, Is.Not.Empty, "Should have generated at least one instruction" );
				Assert.That( component.InstallationMethod, Is.Not.Null.And.Not.Empty, "Should have set InstallationMethod" );
			} );

			TestContext.Progress.WriteLine( $"Generated {component.Instructions.Count} instructions" );
			TestContext.Progress.WriteLine( $"Installation Method: {component.InstallationMethod}" );
			foreach (Instruction instruction in component.Instructions)
			{
				TestContext.Progress.WriteLine( $"  - {instruction.Action}: {string.Join( ", ", instruction.Source )}" );
			}
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_WithExactFileName_UsesCorrectArchive()
		{

			CreateTslPatcherArchive( "wrong-mod-1.zip" );
			CreateTslPatcherArchive( "wrong-mod-2.zip" );
			CreateTslPatcherArchive( "correct-mod.zip" );

			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Correct Mod",
				ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase )
				{
					{
						"https://example.com/download/correct-mod.zip",
						new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase)
					}
				}
			};

			bool result = AutoInstructionGenerator.TryGenerateInstructionsFromArchive( component );

			Assert.Multiple( () =>
			{
				Assert.That( result, Is.True, "Should find the correct archive" );
				Assert.That( component.Instructions, Is.Not.Empty );
			} );

			Instruction? extractInstruction = component.Instructions.FirstOrDefault( i => i.Action == Instruction.ActionType.Extract );
			Assert.That( extractInstruction, Is.Not.Null );
			Assert.That( extractInstruction.Source[0], Does.Contain( "correct-mod.zip" ) );
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_WithoutMatchingArchive_ReturnsFalse()
		{

			CreateTslPatcherArchive( "unrelated-mod.zip" );

			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Non-Existent Mod",
				ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase )
				{
					{ "https://example.com/download/missing-mod.zip", new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase) }
				}
			};

			bool result = AutoInstructionGenerator.TryGenerateInstructionsFromArchive( component );

			Assert.Multiple( () =>
			{
				Assert.That( result, Is.False, "Should return false when archive is not found" );
				Assert.That( component.Instructions, Is.Empty, "Should not generate any instructions" );
			} );
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_WithFuzzyMatch_FindsArchive()
		{

			const string archiveName = "Swoop_Bike_Upgrades_v1.2.zip";
			CreateTslPatcherArchive( archiveName );

			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Swoop Bike Upgrades",
				ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase )
				{
					{ "https://example.com/swoop-bike-upgrades.zip", new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase) }
				}
			};

			bool result = AutoInstructionGenerator.TryGenerateInstructionsFromArchive( component );

			Assert.Multiple( () =>
			{
				Assert.That( result, Is.True, "Should find archive with fuzzy matching" );
				Assert.That( component.Instructions, Is.Not.Empty );
			} );
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_MultipleComponents_EachGetsUniqueInstructions()
		{

			CreateTslPatcherArchive( "mod-one.zip" );
			CreateLooseFileArchive( "mod-two.zip", "file1.2da", "file2.tga" );
			CreateTslPatcherArchive( "mod-three.zip" );

			var component1 = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Mod One",
				ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase )
				{
					{ "https://example.com/mod-one.zip", new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase) }
				}
			};

			var component2 = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Mod Two",
				ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase )
				{
					{ "https://example.com/mod-two.zip", new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase) }
				}
			};

			var component3 = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Mod Three",
				ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase )
				{
					{ "https://example.com/mod-three.zip", new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase) }
				}
			};

			bool result1 = AutoInstructionGenerator.TryGenerateInstructionsFromArchive( component1 );
			bool result2 = AutoInstructionGenerator.TryGenerateInstructionsFromArchive( component2 );
			bool result3 = AutoInstructionGenerator.TryGenerateInstructionsFromArchive( component3 );

			Assert.Multiple( () =>
			{
				Assert.That( result1, Is.True, "Component 1 should generate instructions" );
				Assert.That( result2, Is.True, "Component 2 should generate instructions" );
				Assert.That( result3, Is.True, "Component 3 should generate instructions" );

				Assert.That( component1.InstallationMethod, Is.EqualTo( "TSLPatcher" ) );
				Assert.That( component2.InstallationMethod, Is.EqualTo( "Loose-File Mod" ) );
				Assert.That( component3.InstallationMethod, Is.EqualTo( "TSLPatcher" ) );
			} );

			Instruction? extract1 = component1.Instructions.First( i => i.Action == Instruction.ActionType.Extract );
			Instruction? extract2 = component2.Instructions.First( i => i.Action == Instruction.ActionType.Extract );
			Instruction? extract3 = component3.Instructions.First( i => i.Action == Instruction.ActionType.Extract );

			Assert.Multiple( () =>
			{
				Assert.That( extract1.Source[0], Does.Contain( "mod-one.zip" ) );
				Assert.That( extract2.Source[0], Does.Contain( "mod-two.zip" ) );
				Assert.That( extract3.Source[0], Does.Contain( "mod-three.zip" ) );
			} );
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_WithExistingInstructions_DoesNotRegenerate()
		{

			CreateTslPatcherArchive( "test-mod.zip" );

			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "Test Mod",
				ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase )
				{
					{ "https://example.com/test-mod.zip", new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase) }
				}
			};

			var existingInstruction = new Instruction { Action = Instruction.ActionType.Move };
			existingInstruction.SetParentComponent( component );
			component.Instructions.Add( existingInstruction );

			bool result = AutoInstructionGenerator.TryGenerateInstructionsFromArchive( component );

			Assert.Multiple( () =>
			{
				Assert.That( result, Is.False, "Should not regenerate when instructions already exist" );
				Assert.That( component.Instructions, Has.Count.EqualTo( 1 ), "Should keep existing instruction" );
			} );
			Assert.That( component.Instructions[0], Is.SameAs( existingInstruction ) );
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_WithEmptyModLink_ReturnsFalse()
		{

			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "No Link Mod",
				ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase )
			};

			bool result = AutoInstructionGenerator.TryGenerateInstructionsFromArchive( component );

			Assert.Multiple( () =>
			{
				Assert.That( result, Is.False );
				Assert.That( component.Instructions, Is.Empty );
			} );
		}

		[Test]
		public void TryGenerateInstructionsFromArchive_WithUrlModLink_ExtractsFilename()
		{

			const string archiveName = "download-file-1234.zip";
			CreateTslPatcherArchive( archiveName );

			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
				Name = "URL Test Mod",
				ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>( StringComparer.OrdinalIgnoreCase )
				{
					{ "https://deadlystream.com/files/download-file-1234.zip", new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase) }
				}
			};

			bool result = AutoInstructionGenerator.TryGenerateInstructionsFromArchive( component );

			Assert.Multiple( () =>
			{
				Assert.That( result, Is.True, "Should extract filename from URL" );
				Assert.That( component.Instructions, Is.Not.Empty );
			} );
		}
	}
}