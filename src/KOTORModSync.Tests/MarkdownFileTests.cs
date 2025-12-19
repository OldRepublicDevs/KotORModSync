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
			_filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".md");
			File.WriteAllText(_filePath, _exampleMarkdown);
		}

		[TearDown]
		public void TearDown()
		{
			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " != null");
			if (File.Exists(_filePath))
				File.Delete(_filePath);
		}

		private string _filePath = string.Empty;

		private readonly string _exampleMarkdown = @"## Mod List

### Name: Example Dantooine Enhancement
**Name:** [Example Dantooine Enhancement](https://deadlystream.com/files/file/1103-example-dantooine-enhancement/)
**Author:** TestAuthorHD
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
  - Example Dantooine Enhancement High Resolution - TPC Version-1103-2-1-1670680013.rar
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

### Name: Example Tweak Pack
**Name:** [Example Tweak Pack](https://deadlystream.com/files/file/296-example-tweak-pack/)
**Author:** TestAuthor
**Description:** Various tweaks for ExampleMod
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
			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " is null");
			string markdownContents = File.ReadAllText(_filePath);

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);
			var result = parser.Parse(markdownContents);

			Assert.Multiple(() =>
			{
				Assert.That(_filePath, Is.Not.Null, "File path should not be null");
				Assert.That(File.Exists(_filePath), Is.True, "Markdown file should exist");
				Assert.That(markdownContents, Is.Not.Null.And.Not.Empty, "Markdown contents should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(2), "Should parse exactly 2 components");
			});

			var firstComponent = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(firstComponent, Is.Not.Null, "First component should not be null");
				Assert.That(firstComponent.Name, Is.Not.Null.And.Not.Empty, "First component name should not be null or empty");
				Assert.That(firstComponent.Name, Does.Contain("Example Dantooine Enhancement"), "First component should contain correct name");
				Assert.That(firstComponent.Author, Is.EqualTo("TestAuthorHD"), "First component should have correct author");
				Assert.That(firstComponent.Guid, Is.EqualTo(Guid.Parse("{B3525945-BDBD-45D8-A324-AAF328A5E13E}")), "First component should have correct GUID");
			});

			var secondComponent = result.Components[1];
			Assert.Multiple(() =>
			{
				Assert.That(secondComponent, Is.Not.Null, "Second component should not be null");
				Assert.That(secondComponent.Name, Is.Not.Null.And.Not.Empty, "Second component name should not be null or empty");
				Assert.That(secondComponent.Name, Does.Contain("Example Tweak Pack"), "Second component should contain correct name");
				Assert.That(secondComponent.Author, Is.EqualTo("TestAuthor"), "Second component should have correct author");
				Assert.That(secondComponent.Guid, Is.EqualTo(Guid.Parse("{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}")), "Second component should have correct GUID");
			});
		}

		[Test]
		public void ParseMarkdownFile_Instructions()
		{
			string markdownContents = File.ReadAllText(_filePath);

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);
			var result = parser.Parse(markdownContents);

			Assert.Multiple(() =>
			{
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.GreaterThan(0), "Should have at least one component");
			});

			var firstComponent = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(firstComponent, Is.Not.Null, "First component should not be null");
				Assert.That(firstComponent.Instructions, Is.Not.Null, "Instructions list should not be null");
				Assert.That(firstComponent.Instructions.Count, Is.GreaterThan(0), "Should have at least one instruction");
			});

			var extractInstruction = firstComponent.Instructions.FirstOrDefault(i => i.Action == Instruction.ActionType.Extract);
			Assert.Multiple(() =>
			{
				Assert.That(extractInstruction, Is.Not.Null, "Extract instruction should not be null");
				Assert.That(extractInstruction.Source, Is.Not.Null, "Extract instruction source should not be null");
				Assert.That(extractInstruction.Source, Has.Count.GreaterThan(0), "Extract instruction should have at least one source file");
			});
		}

		[Test]
		public void ParseMarkdownFile_EmptyFile()
		{
			string emptyFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".md");
			try
			{
				File.WriteAllText(emptyFilePath, string.Empty);

				var profile = MarkdownImportProfile.CreateDefault();
				var parser = new MarkdownParser(profile);
				var result = parser.Parse(string.Empty);

				Assert.Multiple(() =>
				{
					Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
					Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
					Assert.That(result, Is.Not.Null, "Parse result should not be null");
					Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
					Assert.That(result.Components, Is.Empty.Or.Count.EqualTo(0), "Empty markdown should produce no components");
				});
			}
			finally
			{
				if (File.Exists(emptyFilePath))
					File.Delete(emptyFilePath);
			}
		}

		[Test]
		public void ParseMarkdownFile_WhitespaceTests()
		{
			string markdownContents = File.ReadAllText(_filePath);
			markdownContents = "    \r\n\t   \r\n\r\n\r\n" + markdownContents + "    \r\n\t   \r\n\r\n\r\n";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);
			var result = parser.Parse(markdownContents);

			Assert.Multiple(() =>
			{
				Assert.That(markdownContents, Is.Not.Null.And.Not.Empty, "Markdown contents should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(2), "Should parse exactly 2 components despite whitespace");
			});
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
			var parser = new MarkdownParser(profile);
			var result = parser.Parse(markdownWithoutName);

			Assert.Multiple(() =>
			{
				Assert.That(markdownWithoutName, Is.Not.Null.And.Not.Empty, "Markdown without name should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
			});
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
			var parser = new MarkdownParser(profile);

			foreach (string markdown in markdownContents)
			{
				var result = parser.Parse(markdown);

				Assert.Multiple(() =>
				{
					Assert.That(markdown, Is.Not.Null.And.Not.Empty, "Markdown content should not be null or empty");
					Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
					Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
					Assert.That(result, Is.Not.Null, "Parse result should not be null");
					Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
					Assert.That(result.Components.Count, Is.GreaterThan(0), "Should parse at least one component per round");
				});
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
			var parser = new MarkdownParser(profile);
			var result = parser.Parse(markdownWithYaml);

			Assert.Multiple(() =>
			{
				Assert.That(markdownWithYaml, Is.Not.Null.And.Not.Empty, "Markdown with YAML should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse exactly one component with YAML metadata");
			});

			var component = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(component, Is.Not.Null, "Component should not be null");
				Assert.That(component.Guid, Is.EqualTo(Guid.Parse("{B3525945-BDBD-45D8-A324-AAF328A5E13E}")), "Component should have correct GUID from YAML metadata");
				Assert.That(component.Instructions, Is.Not.Null, "Instructions list should not be null");
				Assert.That(component.Instructions, Has.Count.GreaterThan(0), "Component should have at least one instruction from YAML metadata");
			});
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
			var parser = new MarkdownParser(profile);
			var result = parser.Parse(markdownWithToml);

			Assert.Multiple(() =>
			{
				Assert.That(markdownWithToml, Is.Not.Null.And.Not.Empty, "Markdown with TOML should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse exactly one component with TOML metadata");
			});

			var component = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(component, Is.Not.Null, "Component should not be null");
				Assert.That(component.Guid, Is.EqualTo(Guid.Parse("{B3525945-BDBD-45D8-A324-AAF328A5E13E}")), "Component should have correct GUID from TOML metadata");
				Assert.That(component.Instructions, Is.Not.Null, "Instructions list should not be null");
				Assert.That(component.Instructions, Has.Count.GreaterThan(0), "Component should have at least one instruction from TOML metadata");
			});
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
			var parser = new MarkdownParser(profile);
			var result = parser.Parse(markdownWithSections);

			Assert.Multiple(() =>
			{
				Assert.That(markdownWithSections, Is.Not.Null.And.Not.Empty, "Markdown with sections should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.BeforeModListContent, Is.Not.Null, "Before mod list content should not be null");
				Assert.That(result.BeforeModListContent, Is.Not.Empty, "Before mod list content should not be empty");
				Assert.That(result.BeforeModListContent, Does.Contain("Introduction"), "Before mod list content should contain introduction section");
				Assert.That(result.AfterModListContent, Is.Not.Null, "After mod list content should not be null");
				Assert.That(result.AfterModListContent, Is.Not.Empty, "After mod list content should not be empty");
				Assert.That(result.AfterModListContent, Does.Contain("Appendix"), "After mod list content should contain appendix section");
			});
		}

		[Test]
		public void ParseMarkdownFile_ComponentEquality()
		{
			string markdownContents = File.ReadAllText(_filePath);

			var profile = MarkdownImportProfile.CreateDefault();
			var parser1 = new MarkdownParser(profile);
			var result1 = parser1.Parse(markdownContents);

			var parser2 = new MarkdownParser(profile);
			var result2 = parser2.Parse(markdownContents);

			Assert.Multiple(() =>
			{
				Assert.That(result1, Is.Not.Null, "First parse result should not be null");
				Assert.That(result2, Is.Not.Null, "Second parse result should not be null");
				Assert.That(result1.Components, Is.Not.Null, "First components list should not be null");
				Assert.That(result2.Components, Is.Not.Null, "Second components list should not be null");
				Assert.That(result1.Components, Has.Count.EqualTo(result2.Components.Count), "Both parse results should have same component count");
			});

			for (int i = 0; i < result1.Components.Count; i++)
			{
				Assert.Multiple(() =>
				{
					Assert.That(result1.Components[i], Is.Not.Null, $"First result component at index {i} should not be null");
					Assert.That(result2.Components[i], Is.Not.Null, $"Second result component at index {i} should not be null");
					Assert.That(result1.Components[i].Name, Is.EqualTo(result2.Components[i].Name), $"Component names at index {i} should match");
					Assert.That(result1.Components[i].Author, Is.EqualTo(result2.Components[i].Author), $"Component authors at index {i} should match");
					Assert.That(result1.Components[i].Guid, Is.EqualTo(result2.Components[i].Guid), $"Component GUIDs at index {i} should match");
				});
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
			var parser = new MarkdownParser(profile);
			var result = parser.Parse(markdownWithIssues);

			Assert.Multiple(() =>
			{
				Assert.That(markdownWithIssues, Is.Not.Null.And.Not.Empty, "Markdown with issues should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Warnings, Is.Not.Null, "Warnings collection should not be null");
			});
		}
	}
}
