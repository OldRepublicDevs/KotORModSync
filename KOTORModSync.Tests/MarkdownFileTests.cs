// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Text;

using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Utility;

using Newtonsoft.Json;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class MarkdownFileTests
	{
		[SetUp]
		public void SetUp()
		{
			_filePath = Path.Combine( Path.GetTempPath(), Path.GetRandomFileName() + ".md" );
			File.WriteAllText( _filePath, _exampleMarkdown );
		}

		[TearDown]
		public void TearDown()
		{
			Assert.That( _filePath, Is.Not.Null, nameof( _filePath ) + " != null" );
			if (File.Exists( _filePath ))
				File.Delete( _filePath );
		}

		private string _filePath = string.Empty;

		private readonly string _exampleMarkdown = @"## Mod List

### Name: Ultimate Dantooine
**Name:** [Ultimate Dantooine](https://deadlystream.com/files/file/1103-ultimate-dantooine-high-resolution/)
**Author:** ShiningRedHD
**Description:** High-resolution retexture of Dantooine
**Category:** Graphics Improvement / Immersion
**Tier:** Recommended
**Installation Method:** TSLPatcher
**Installation Instructions:** Run TSLPatcher and select destination

<!--<<ModSync>>
Guid: {B3525945-BDBD-45D8-A324-AAF328A5E13E}
Instructions:
  - Guid: {11111111-1111-1111-1111-111111111111}
Action: Extract
Source:
  - Ultimate Dantooine High Resolution - TPC Version-1103-2-1-1670680013.rar
  - Guid: {22222222-2222-2222-2222-222222222222}
Action: Delete
Source:
  - DAN_wall03.tpc
  - DAN_NEW1.tpc
  - Guid: {33333333-3333-3333-3333-333333333333}
Action: Move
Source:
  - dantooine_files
Destination: <<kotorDirectory>>\Override
Overwrite: true
-->

### Name: TSLRCM Tweak Pack
**Name:** [TSLRCM Tweak Pack](https://deadlystream.com/files/file/296-tslrcm-tweak-pack/)
**Author:** Pavijan
**Description:** Various tweaks for TSLRCM
**Category:** Gameplay / Immersion
**Tier:** Recommended

<!--<<ModSync>>
Guid: {C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}
Instructions:
  - Guid: {44444444-4444-4444-4444-444444444444}
Action: Extract
Source:
  - URCMTP 1.3.rar
  - Guid: {55555555-5555-5555-5555-555555555555}
Action: Patcher
Source:
  - tslpatchdata
Destination: <<kotorDirectory>>
-->
";

		[Test]
		public void ParseMarkdownFile_ValidComponents()
		{
			Assert.That( _filePath, Is.Not.Null, nameof( _filePath ) + " is null" );
			string markdownContents = File.ReadAllText( _filePath );

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser( profile );
			var result = parser.Parse( markdownContents );

			Assert.That( result, Is.Not.Null );
			Assert.That( result.Components, Is.Not.Null );
			Assert.That( result.Components, Has.Count.EqualTo( 2 ) );

			var firstComponent = result.Components[0];
			Assert.Multiple( () =>
			{
				Assert.That( firstComponent.Name, Does.Contain( "Ultimate Dantooine" ) );
				Assert.That( firstComponent.Author, Is.EqualTo( "ShiningRedHD" ) );
				Assert.That( firstComponent.Guid, Is.EqualTo( Guid.Parse( "{B3525945-BDBD-45D8-A324-AAF328A5E13E}" ) ) );
			} );

			var secondComponent = result.Components[1];
			Assert.Multiple( () =>
			{
				Assert.That( secondComponent.Name, Does.Contain( "TSLRCM Tweak Pack" ) );
				Assert.That( secondComponent.Author, Is.EqualTo( "Pavijan" ) );
				Assert.That( secondComponent.Guid, Is.EqualTo( Guid.Parse( "{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}" ) ) );
			} );
		}

		[Test]
		public void ParseMarkdownFile_Instructions()
		{
			string markdownContents = File.ReadAllText( _filePath );

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser( profile );
			var result = parser.Parse( markdownContents );

			var firstComponent = result.Components[0];
			Assert.That( firstComponent.Instructions, Is.Not.Null );
			Assert.That( firstComponent.Instructions.Count, Is.GreaterThan( 0 ) );

			var extractInstruction = firstComponent.Instructions.FirstOrDefault( i => i.Action == Instruction.ActionType.Extract );
			Assert.That( extractInstruction, Is.Not.Null );
			Assert.That( extractInstruction.Source, Is.Not.Null );
			Assert.That( extractInstruction.Source, Has.Count.GreaterThan( 0 ) );
		}

		[Test]
		public void ParseMarkdownFile_EmptyFile()
		{
			string emptyFilePath = Path.Combine( Path.GetTempPath(), Path.GetRandomFileName() + ".md" );
			try
			{
				File.WriteAllText( emptyFilePath, string.Empty );

				var profile = MarkdownImportProfile.CreateDefault();
				var parser = new MarkdownParser( profile );
				var result = parser.Parse( string.Empty );

				Assert.That( result, Is.Not.Null );
				Assert.That( result.Components, Is.Empty.Or.Count.EqualTo( 0 ) );
			}
			finally
			{
				if (File.Exists( emptyFilePath ))
					File.Delete( emptyFilePath );
			}
		}

		[Test]
		public void ParseMarkdownFile_WhitespaceTests()
		{
			string markdownContents = File.ReadAllText( _filePath );
			markdownContents = "    \r\n\t   \r\n\r\n\r\n" + markdownContents + "    \r\n\t   \r\n\r\n\r\n";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser( profile );
			var result = parser.Parse( markdownContents );

			Assert.That( result, Is.Not.Null );
			Assert.That( result.Components, Is.Not.Null );
			Assert.That( result.Components, Has.Count.EqualTo( 2 ) );
		}

		[Test]
		public void ParseMarkdownFile_MissingNameField()
		{
			string markdownWithoutName = @"## Mod List

### Some Section
**Author:** TestAuthor
**Description:** Test description
**Category:** Graphics Improvement
**Tier:** Recommended
";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser( profile );
			var result = parser.Parse( markdownWithoutName );

			Assert.That( result, Is.Not.Null );
			Assert.That( result.Components, Is.Not.Null );
		}

		[Test]
		public void ParseMarkdownFile_MultipleRounds()
		{
			var markdownContents = new[]
			{
				@"## Mod List

### Name: ModComponent 1
**Name:** ModComponent 1
**Author:** Author 1
**Category:** Graphics Improvement
**Tier:** Recommended
",
				@"## Mod List

### Name: ModComponent 2
**Name:** ModComponent 2
**Author:** Author 2
**Category:** Gameplay
**Tier:** Essential

### Name: ModComponent 3
**Name:** ModComponent 3
**Author:** Author 3
**Category:** Immersion
**Tier:** Optional
",
			};

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser( profile );

			foreach (string markdown in markdownContents)
			{
				var result = parser.Parse( markdown );

				Assert.That( result, Is.Not.Null );
				Assert.That( result.Components, Is.Not.Null );
				Assert.That( result.Components.Count, Is.GreaterThan( 0 ) );
			}
		}

		[Test]
		public void ParseMarkdownFile_YAMLMetadataBlock()
		{
			string markdownWithYaml = @"## Mod List

### Name: Test Mod with YAML
**Name:** Test Mod with YAML
**Author:** Test Author
**Category:** Graphics Improvement
**Tier:** Recommended

<!--<<ModSync>>
Guid: B3525945-BDBD-45D8-A324-AAF328A5E13E
Instructions:
  - Guid: 11111111-1111-1111-1111-111111111111
Action: Extract
Source:
  - test.rar
-->
";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser( profile );
			var result = parser.Parse( markdownWithYaml );

			Assert.That( result, Is.Not.Null );
			Assert.That( result.Components, Has.Count.EqualTo( 1 ) );

			var component = result.Components[0];
			Assert.Multiple( () =>
			{
				Assert.That( component.Guid, Is.EqualTo( Guid.Parse( "{B3525945-BDBD-45D8-A324-AAF328A5E13E}" ) ) );
				Assert.That( component.Instructions, Has.Count.GreaterThan( 0 ) );
			} );
		}

		[Test]
		public void ParseMarkdownFile_TOMLMetadataBlock()
		{
			string markdownWithToml = @"## Mod List

### Name: Test Mod with TOML
**Name:** Test Mod with TOML
**Author:** Test Author
**Category:** Graphics Improvement
**Tier:** Recommended

<!--<<ModSync>>
Guid = ""{B3525945-BDBD-45D8-A324-AAF328A5E13E}""

[[Instructions]]
Guid = ""{11111111-1111-1111-1111-111111111111}""
Action = ""Extract""
Source = [""test.rar""]
-->
";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser( profile );
			var result = parser.Parse( markdownWithToml );

			Assert.That( result, Is.Not.Null );
			Assert.That( result.Components, Has.Count.EqualTo( 1 ) );

			var component = result.Components[0];
			Assert.Multiple( () =>
			{
				Assert.That( component.Guid, Is.EqualTo( Guid.Parse( "{B3525945-BDBD-45D8-A324-AAF328A5E13E}" ) ) );
				Assert.That( component.Instructions, Has.Count.GreaterThan( 0 ) );
			} );
		}

		[Test]
		public void ParseMarkdownFile_CaptureBeforeAndAfterModList()
		{
			string markdownWithSections = @"# Introduction Section

This is before the mod list.

## Mod List

### Name: Test Mod
**Name:** Test Mod
**Author:** Test Author
**Category:** Graphics Improvement
**Tier:** Recommended

## Appendix Section

This is after the mod list.
";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser( profile );
			var result = parser.Parse( markdownWithSections );

			Assert.That( result, Is.Not.Null );
			Assert.That( result.BeforeModListContent, Is.Not.Empty );
			Assert.Multiple( () =>
			{
				Assert.That( result.BeforeModListContent, Does.Contain( "Introduction" ) );
				Assert.That( result.AfterModListContent, Is.Not.Empty );
			} );
			Assert.That( result.AfterModListContent, Does.Contain( "Appendix" ) );
		}

		[Test]
		public void ParseMarkdownFile_ComponentEquality()
		{
			string markdownContents = File.ReadAllText( _filePath );

			var profile = MarkdownImportProfile.CreateDefault();
			var parser1 = new MarkdownParser( profile );
			var result1 = parser1.Parse( markdownContents );

			var parser2 = new MarkdownParser( profile );
			var result2 = parser2.Parse( markdownContents );

			Assert.That( result1.Components, Has.Count.EqualTo( result2.Components.Count ) );

			for (int i = 0; i < result1.Components.Count; i++)
			{
				Assert.Multiple( () =>
				{
					Assert.That( result1.Components[i].Name, Is.EqualTo( result2.Components[i].Name ) );
					Assert.That( result1.Components[i].Author, Is.EqualTo( result2.Components[i].Author ) );
					Assert.That( result1.Components[i].Guid, Is.EqualTo( result2.Components[i].Guid ) );
				} );
			}
		}

		[Test]
		public void ParseMarkdownFile_WarningsCollection()
		{
			string markdownWithIssues = @"## Mod List

### Name: Valid Mod
**Name:** Valid Mod
**Author:** Test Author
**Category:** Graphics Improvement
**Tier:** Recommended

### Invalid Entry
**Author:** Missing Name Field
**Category:** Graphics Improvement
";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser( profile );
			var result = parser.Parse( markdownWithIssues );

			Assert.That( result, Is.Not.Null );
			Assert.That( result.Warnings, Is.Not.Null );
		}
	}
}