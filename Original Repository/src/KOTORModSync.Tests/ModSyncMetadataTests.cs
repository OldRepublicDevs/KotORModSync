// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Linq;

using KOTORModSync.Core;
using KOTORModSync.Core.CLI;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class ModSyncMetadataTests
	{
		[Test]
		public void ParseModSyncMetadata_WithCompleteData_ParsesAllFields()
		{

			const string markdown = @"### Test Mod

**Name:** [Test Mod](https://example.com)

**Author:** Test Author

**Description:** Test description

**Category & Tier:** Testing / 1 - Essential

**Non-English Functionality:** YES

**Installation Method:** TSLPatcher

**Installation Instructions:** Test instructions

<!--<<ModSync>>
- **GUID:** 12345678-1234-1234-1234-123456789abc

#### Instructions
1. **GUID:** 11111111-1111-1111-1111-111111111111
   **Action:** Extract
   **Source:** test.zip
   **Destination:** testdest

#### Options
##### Option 1
- **GUID:** 22222222-2222-2222-2222-222222222222
- **Name:** Test Option
- **Description:** Test option description
- **Is Selected:** true
- **Install State:** 0
- **Is Downloaded:** true
- **Restrictions:** 33333333-3333-3333-3333-333333333333
  - **Instruction:**
- **GUID:** 44444444-4444-4444-4444-444444444444
- **Action:** Move
- **Destination:** dest
- **Overwrite:** true
- **Source:** src
-->

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(markdown);

			Assert.Multiple(() =>
			{
				Assert.That(markdown, Is.Not.Null.And.Not.Empty, "Markdown content should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse exactly one component");
			});

			var component = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(component, Is.Not.Null, "Component should not be null");
				Assert.That(component.Guid, Is.Not.EqualTo(Guid.Empty), "Component GUID should not be empty");
				Assert.That(component.Guid.ToString(), Is.EqualTo("12345678-1234-1234-1234-123456789abc"), "Component GUID should match");
				Assert.That(component.Name, Is.Not.Null.And.Not.Empty, "Component name should not be null or empty");
				Assert.That(component.Name, Is.EqualTo("Test Mod"), "Component name should match");
				Assert.That(component.Instructions, Is.Not.Null, "Instructions list should not be null");
				Assert.That(component.Instructions, Has.Count.EqualTo(1), "Should have exactly 1 instruction");
			});
			var instruction = component.Instructions[0];
			Assert.Multiple(() =>
			{
				Assert.That(instruction, Is.Not.Null, "Instruction should not be null");
				Assert.That(instruction.Action.ToString(), Is.EqualTo("Extract"), "Instruction action should match");
				Assert.That(instruction.Overwrite, Is.True, "Instruction overwrite should be true");
				Assert.That(instruction.Source, Is.Not.Null, "Instruction source list should not be null");
				Assert.That(instruction.Source, Is.Not.Empty, "Instruction source list should not be empty");
				Assert.That(instruction.Source[0], Is.EqualTo("test.zip"), "Instruction source should match");
				Assert.That(instruction.Destination, Is.EqualTo("testdest"), "Instruction destination should match");
				Assert.That(component.Options, Is.Not.Null, "Options list should not be null");
				Assert.That(component.Options, Has.Count.EqualTo(1), "Should have exactly 1 option");
			});
			var option = component.Options[0];
			Assert.Multiple(() =>
			{
				Assert.That(option, Is.Not.Null, "Option should not be null");
				Assert.That(option.Guid, Is.Not.EqualTo(Guid.Empty), "Option GUID should not be empty");
				Assert.That(option.Guid.ToString(), Is.EqualTo("22222222-2222-2222-2222-222222222222"), "Option GUID should match");
				Assert.That(option.Name, Is.Not.Null.And.Not.Empty, "Option name should not be null or empty");
				Assert.That(option.Name, Is.EqualTo("Test Option"), "Option name should match");
				Assert.That(option.Description, Is.EqualTo("Test option description"), "Option description should match");
				Assert.That(option.IsSelected, Is.True, "Option IsSelected should be true");
				Assert.That(option.IsDownloaded, Is.True, "Option IsDownloaded should be true");
				Assert.That(option.Instructions, Is.Not.Null, "Option instructions list should not be null");
				Assert.That(option.Instructions, Has.Count.EqualTo(1), "Option should have exactly 1 instruction");
			});
			var optionInstruction = option.Instructions[0];
			Assert.Multiple(() =>
			{
				Assert.That(optionInstruction, Is.Not.Null, "Option instruction should not be null");
				Assert.That(optionInstruction.Action.ToString(), Is.EqualTo("Move"), "Option instruction action should match");
			});
		}

		[Test]
		public void ParseModSyncMetadata_WithoutMetadata_ParsesNormally()
		{

			const string markdown = @"### Test Mod

**Name:** Test Mod

**Author:** Test Author

**Description:** Test description

**Category & Tier:** Testing / 1 - Essential

**Non-English Functionality:** YES

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(markdown);

			Assert.Multiple(() =>
			{
				Assert.That(markdown, Is.Not.Null.And.Not.Empty, "Markdown content should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse exactly one component");
			});

			var component = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(component, Is.Not.Null, "Component should not be null");
				Assert.That(component.Name, Is.Not.Null.And.Not.Empty, "Component name should not be null or empty");
				Assert.That(component.Name, Is.EqualTo("Test Mod"), "Component name should match");
				Assert.That(component.Instructions, Is.Not.Null, "Instructions list should not be null");
				Assert.That(component.Instructions, Is.Empty, "Should have no instructions when metadata is missing");
				Assert.That(component.Options, Is.Not.Null, "Options list should not be null");
				Assert.That(component.Options, Is.Empty, "Should have no options when metadata is missing");
			});
		}

		[Test]
		public void ParseModSyncMetadata_MultipleInstructions_ParsesAll()
		{

			const string markdown = @"### Test Mod

**Name:** Test Mod

**Author:** Test Author

**Description:** Test

**Category & Tier:** Testing / 1 - Essential

**Non-English Functionality:** YES

<!--<<ModSync>>
- **GUID:** 12345678-1234-1234-1234-123456789abc

#### Instructions
1. **GUID:** 11111111-1111-1111-1111-111111111111
   **Action:** Extract
   **Source:** file1.zip
2. **GUID:** 22222222-2222-2222-2222-222222222222
   **Action:** Move
   **Overwrite:** false
   **Source:** file2.txt
   **Destination:** dest
3. **GUID:** 33333333-3333-3333-3333-333333333333
   **Action:** Copy
   **Overwrite:** true
   **Source:** file3.dat
   **Destination:** dest2
-->

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(markdown);

			Assert.Multiple(() =>
			{
				Assert.That(markdown, Is.Not.Null.And.Not.Empty, "Markdown content should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse exactly one component");
			});

			var component = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(component, Is.Not.Null, "Component should not be null");
				Assert.That(component.Instructions, Is.Not.Null, "Instructions list should not be null");
				Assert.That(component.Instructions, Has.Count.EqualTo(3), "Should have exactly 3 instructions");
			});

			Assert.Multiple(() =>
			{
				Assert.That(component.Instructions[0], Is.Not.Null, "First instruction should not be null");
				Assert.That(component.Instructions[0].Action.ToString(), Is.EqualTo("Extract"), "First instruction action should match");
				Assert.That(component.Instructions[0].Overwrite, Is.True, "First instruction overwrite should be true");

				Assert.That(component.Instructions[1], Is.Not.Null, "Second instruction should not be null");
				Assert.That(component.Instructions[1].Action.ToString(), Is.EqualTo("Move"), "Second instruction action should match");
				Assert.That(component.Instructions[1].Overwrite, Is.False, "Second instruction overwrite should be false");

				Assert.That(component.Instructions[2], Is.Not.Null, "Third instruction should not be null");
				Assert.That(component.Instructions[2].Action.ToString(), Is.EqualTo("Copy"), "Third instruction action should match");
				Assert.That(component.Instructions[2].Overwrite, Is.True, "Third instruction overwrite should be true");
			});
		}

		[Test]
		public void ParseModSyncMetadata_MultipleOptions_ParsesAll()
		{

			const string markdown = @"### Test Mod

**Name:** Test Mod

**Description:** Test

**Category & Tier:** Testing / 1 - Essential

**Non-English Functionality:** YES

<!--<<ModSync>>
- **GUID:** 12345678-1234-1234-1234-123456789abc

#### Options
##### Option 1
- **GUID:** 11111111-1111-1111-1111-111111111111
- **Name:** Option One
- **Description:** First option
- **Is Selected:** true
- **Install State:** 0
- **Is Downloaded:** false

##### Option 2
- **GUID:** 22222222-2222-2222-2222-222222222222
- **Name:** Option Two
- **Description:** Second option
- **Is Selected:** false
- **Install State:** 0
- **Is Downloaded:** false

##### Option 3
- **GUID:** 33333333-3333-3333-3333-333333333333
- **Name:** Option Three
- **Description:** Third option
- **Is Selected:** false
- **Install State:** 0
- **Is Downloaded:** true
-->

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(markdown);

			Assert.Multiple(() =>
			{
				Assert.That(markdown, Is.Not.Null.And.Not.Empty, "Markdown content should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse exactly one component");
			});

			var component = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(component, Is.Not.Null, "Component should not be null");
				Assert.That(component.Options, Is.Not.Null, "Options list should not be null");
				Assert.That(component.Options, Has.Count.EqualTo(3), "Should have exactly 3 options");
			});

			Assert.Multiple(() =>
			{
				Assert.That(component.Options[0], Is.Not.Null, "First option should not be null");
				Assert.That(component.Options[0].Name, Is.Not.Null.And.Not.Empty, "First option name should not be null or empty");
				Assert.That(component.Options[0].Name, Is.EqualTo("Option One"), "First option name should match");
				Assert.That(component.Options[0].IsSelected, Is.True, "First option should be selected");
				Assert.That(component.Options[0].IsDownloaded, Is.False, "First option should not be downloaded");

				Assert.That(component.Options[1], Is.Not.Null, "Second option should not be null");
				Assert.That(component.Options[1].Name, Is.Not.Null.And.Not.Empty, "Second option name should not be null or empty");
				Assert.That(component.Options[1].Name, Is.EqualTo("Option Two"), "Second option name should match");
				Assert.That(component.Options[1].IsSelected, Is.False, "Second option should not be selected");

				Assert.That(component.Options[2], Is.Not.Null, "Third option should not be null");
				Assert.That(component.Options[2].Name, Is.Not.Null.And.Not.Empty, "Third option name should not be null or empty");
				Assert.That(component.Options[2].Name, Is.EqualTo("Option Three"), "Third option name should match");
				Assert.That(component.Options[2].IsDownloaded, Is.True, "Third option should be downloaded");
			});
		}

		[Test]
		public void GenerateModSyncMetadata_WithInstructions_GeneratesCorrectFormat()
		{

			var component = new ModComponent
			{
				Guid = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
				Name = "Test Mod",
				Author = "Test Author",
				Description = "Test description",
				Category = new System.Collections.Generic.List<string> { "Testing" },
				Tier = "1 - Essential",
				Language = new System.Collections.Generic.List<string> { "YES" },
				InstallationMethod = "TSLPatcher"
			};

			var instruction = new Instruction
			{
				Guid = Guid.Parse("11111111-1111-1111-1111-111111111111"),
				Action = Instruction.ActionType.Extract,
				Overwrite = true,
				Source = new System.Collections.Generic.List<string> { "test.zip" },
				Destination = "testdest"
			};
			instruction.SetParentComponent(component);
			component.Instructions.Add(instruction);

			string generated = ModComponentSerializationService.GenerateModDocumentation(new System.Collections.Generic.List<ModComponent> { component });

			Assert.Multiple(() =>
			{
				Assert.That(component, Is.Not.Null, "Component should not be null");
				Assert.That(component.Instructions, Is.Not.Null, "Instructions list should not be null");
				Assert.That(component.Instructions, Has.Count.GreaterThan(0), "Component should have at least one instruction");
				Assert.That(generated, Is.Not.Null.And.Not.Empty, "Generated markdown should not be null or empty");
				Assert.That(generated, Does.Contain("<!--<<ModSync>>"), "Should contain ModSync opening tag");
				Assert.That(generated, Does.Contain("-->"), "Should contain ModSync closing tag");
				Assert.That(generated, Does.Contain("- **GUID:** 12345678-1234-1234-1234-123456789abc"), "Should contain component GUID");
				Assert.That(generated, Does.Contain("#### Instructions"), "Should contain Instructions header");
				Assert.That(generated, Does.Contain("1. **GUID:** 11111111-1111-1111-1111-111111111111"), "Should contain instruction GUID");
				Assert.That(generated, Does.Contain("**Action:** Extract"), "Should contain instruction action");
				Assert.That(generated, Does.Contain("**Overwrite:** true"), "Should contain overwrite flag");
				Assert.That(generated, Does.Contain("**Source:** test.zip"), "Should contain source");
				Assert.That(generated, Does.Contain("**Destination:** testdest"), "Should contain destination");
			});
		}

		[Test]
		public void GenerateModSyncMetadata_WithOptions_GeneratesCorrectFormat()
		{

			var component = new ModComponent
			{
				Guid = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
				Name = "Test Mod",
				Category = new System.Collections.Generic.List<string> { "Testing" },
				Tier = "1 - Essential"
			};

			var option = new Option
			{
				Guid = Guid.Parse("22222222-2222-2222-2222-222222222222"),
				Name = "Test Option",
				Description = "Test option description",
				IsSelected = true,
				IsDownloaded = false
			};
			component.Options.Add(option);

			string generated = ModComponentSerializationService.GenerateModDocumentation(new System.Collections.Generic.List<ModComponent> { component });

			Assert.Multiple(() =>
			{
				Assert.That(component, Is.Not.Null, "Component should not be null");
				Assert.That(component.Options, Is.Not.Null, "Options list should not be null");
				Assert.That(component.Options, Has.Count.GreaterThan(0), "Component should have at least one option");
				Assert.That(generated, Is.Not.Null.And.Not.Empty, "Generated markdown should not be null or empty");
				Assert.That(generated, Does.Contain("#### Options"), "Should contain Options header");
				Assert.That(generated, Does.Contain("##### Option 1"), "Should contain option number");
				Assert.That(generated, Does.Contain("- **GUID:** 22222222-2222-2222-2222-222222222222"), "Should contain option GUID");
				Assert.That(generated, Does.Contain("- **Name:** Test Option"), "Should contain option name");
				Assert.That(generated, Does.Contain("- **Description:** Test option description"), "Should contain option description");
				Assert.That(generated, Does.Contain("- **Is Selected:** true"), "Should contain IsSelected");
				Assert.That(generated, Does.Contain("- **Is Downloaded:** false"), "Should contain IsDownloaded");
			});
		}

		[Test]
		public void GenerateModSyncMetadata_WithoutInstructionsOrOptions_DoesNotGenerateMetadata()
		{

			var component = new ModComponent
			{
				Guid = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
				Name = "Test Mod",
				Category = new System.Collections.Generic.List<string> { "Testing" },
				Tier = "1 - Essential"
			};

			string generated = ModComponentSerializationService.GenerateModDocumentation(new System.Collections.Generic.List<ModComponent> { component });

			Assert.Multiple(() =>
			{
				Assert.That(component, Is.Not.Null, "Component should not be null");
				Assert.That(component.Instructions, Is.Not.Null, "Instructions list should not be null");
				Assert.That(component.Options, Is.Not.Null, "Options list should not be null");
				Assert.That(component.Instructions, Is.Empty, "Component should have no instructions");
				Assert.That(component.Options, Is.Empty, "Component should have no options");
				Assert.That(generated, Is.Not.Null.And.Not.Empty, "Generated markdown should not be null or empty");
				Assert.That(generated, Does.Not.Contain("<!--<<ModSync>>"), "Should not contain ModSync metadata when no instructions/options");
				Assert.That(generated, Does.Contain("### Test Mod"), "Should still contain component name");
			});
		}

		[Test]
		public void RoundTrip_ComplexComponent_PreservesAllData()
		{

			const string markdown = @"### Complex Mod

**Name:** Complex Mod

**Author:** Complex Author

**Description:** Complex description with multiple lines
and special characters: & < > "" ' / \

**Category & Tier:** Category1 & Category2 / 2 - Recommended

**Non-English Functionality:** PARTIAL - Some text will be blank

**Installation Method:** HoloPatcher, TSLPatcher

**Installation Instructions:** Complex instructions with multiple steps.

<!--<<ModSync>>
- **GUID:** aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee

#### Instructions
1. **GUID:** 11111111-1111-1111-1111-111111111111
   **Action:** Extract
   **Source:** file1.zip, file2.zip, file3.zip
2. **GUID:** 22222222-2222-2222-2222-222222222222
   **Action:** Delete
   **Overwrite:** false
   **Source:** unwanted.txt
3. **GUID:** 33333333-3333-3333-3333-333333333333
   **Action:** Move
   **Overwrite:** true
   **Source:** source.dat
   **Destination:** destination

#### Options
##### Option 1
- **GUID:** 44444444-4444-4444-4444-444444444444
- **Name:** Option Alpha
- **Description:** First complex option
- **Is Selected:** true
- **Install State:** 2
- **Is Downloaded:** true
- **Restrictions:** 55555555-5555-5555-5555-555555555555, 66666666-6666-6666-6666-666666666666
  - **Instruction:**
- **GUID:** 77777777-7777-7777-7777-777777777777
- **Action:** Copy
- **Destination:** dest1
- **Overwrite:** true
- **Source:** src1, src2

##### Option 2
- **GUID:** 88888888-8888-8888-8888-888888888888
- **Name:** Option Beta
- **Description:** Second complex option
- **Is Selected:** false
- **Install State:** 0
- **Is Downloaded:** false
  - **Instruction:**
- **GUID:** 99999999-9999-9999-9999-999999999999
- **Action:** Rename
- **Destination:** newname.txt
- **Overwrite:** false
- **Source:** oldname.txt
-->

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult firstParse = parser.Parse(markdown);

			Assert.Multiple(() =>
			{
				Assert.That(markdown, Is.Not.Null.And.Not.Empty, "Markdown content should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(firstParse, Is.Not.Null, "First parse result should not be null");
				Assert.That(firstParse.Components, Is.Not.Null, "First parse components list should not be null");
				Assert.That(firstParse.Components, Has.Count.EqualTo(1), "First parse should produce exactly one component");
			});

			string generated = ModComponentSerializationService.GenerateModDocumentation(firstParse.Components.ToList());
			Assert.Multiple(() =>
			{
				Assert.That(generated, Is.Not.Null.And.Not.Empty, "Generated markdown should not be null or empty");
			});

			MarkdownParserResult secondParse = parser.Parse(generated);

			Assert.Multiple(() =>
			{
				Assert.That(secondParse, Is.Not.Null, "Second parse result should not be null");
				Assert.That(secondParse.Components, Is.Not.Null, "Second parse components list should not be null");
				Assert.That(secondParse.Components, Has.Count.EqualTo(1), "Second parse should produce exactly one component");
			});

			var first = firstParse.Components[0];
			var second = secondParse.Components[0];

			Assert.Multiple(() =>
			{
				Assert.That(first, Is.Not.Null, "First component should not be null");
				Assert.That(second, Is.Not.Null, "Second component should not be null");
				Assert.That(second.Guid, Is.EqualTo(first.Guid), "Component GUID should be preserved");
				Assert.That(second.Name, Is.Not.Null, "Second component name should not be null");
				Assert.That(first.Name, Is.Not.Null, "First component name should not be null");
				Assert.That(second.Name, Is.EqualTo(first.Name), "Component name should be preserved");
				Assert.That(second.Author, Is.EqualTo(first.Author), "Component author should be preserved");
				Assert.That(second.Instructions, Is.Not.Null, "Second component instructions list should not be null");
				Assert.That(first.Instructions, Is.Not.Null, "First component instructions list should not be null");
				Assert.That(second.Instructions, Has.Count.EqualTo(first.Instructions.Count), "Instruction count should be preserved");
			});
			for (int i = 0; i < first.Instructions.Count; i++)
			{
				Assert.Multiple(() =>
				{
					Assert.That(first.Instructions[i], Is.Not.Null, $"First component instruction {i} should not be null");
					Assert.That(second.Instructions[i], Is.Not.Null, $"Second component instruction {i} should not be null");
					Assert.That(second.Instructions[i].Guid, Is.EqualTo(first.Instructions[i].Guid), $"Instruction {i} GUID should be preserved");
					Assert.That(second.Instructions[i].Action, Is.EqualTo(first.Instructions[i].Action), $"Instruction {i} Action should be preserved");
					Assert.That(second.Instructions[i].Overwrite, Is.EqualTo(first.Instructions[i].Overwrite), $"Instruction {i} Overwrite should be preserved");
					Assert.That(second.Instructions[i].Source, Is.Not.Null, $"Second component instruction {i} source list should not be null");
					Assert.That(first.Instructions[i].Source, Is.Not.Null, $"First component instruction {i} source list should not be null");
					Assert.That(second.Instructions[i].Source, Has.Count.EqualTo(first.Instructions[i].Source.Count), $"Instruction {i} Source count should be preserved");
				});
			}

			Assert.Multiple(() =>
			{
				Assert.That(second.Options, Is.Not.Null, "Second component options list should not be null");
				Assert.That(first.Options, Is.Not.Null, "First component options list should not be null");
				Assert.That(second.Options, Has.Count.EqualTo(first.Options.Count), "Option count should be preserved");
			});

			for (int i = 0; i < first.Options.Count; i++)
			{
				Assert.Multiple(() =>
				{
					Assert.That(first.Options[i], Is.Not.Null, $"First component option {i} should not be null");
					Assert.That(second.Options[i], Is.Not.Null, $"Second component option {i} should not be null");
					Assert.That(second.Options[i].Guid, Is.EqualTo(first.Options[i].Guid), $"Option {i} GUID should be preserved");
					Assert.That(second.Options[i].Name, Is.EqualTo(first.Options[i].Name), $"Option {i} Name should be preserved");
					Assert.That(second.Options[i].Description, Is.EqualTo(first.Options[i].Description), $"Option {i} Description should be preserved");
					Assert.That(second.Options[i].IsSelected, Is.EqualTo(first.Options[i].IsSelected), $"Option {i} IsSelected should be preserved");
					Assert.That(second.Options[i].Instructions, Is.Not.Null, $"Second component option {i} instructions list should not be null");
					Assert.That(first.Options[i].Instructions, Is.Not.Null, $"First component option {i} instructions list should not be null");
					Assert.That(second.Options[i].Instructions, Has.Count.EqualTo(first.Options[i].Instructions.Count), $"Option {i} Instruction count should be preserved");
				});
			}
		}

		[Test]
		public void ParseModSyncMetadata_MultipleComponents_ParsesEachCorrectly()
		{

			const string markdown = @"### Mod One

**Name:** Mod One

**Description:** Test

**Category & Tier:** Testing / 1 - Essential

**Non-English Functionality:** YES

<!--<<ModSync>>
- **GUID:** 11111111-1111-1111-1111-111111111111

#### Instructions
1. **GUID:** aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
   **Action:** Extract
   **Source:** mod1.zip
-->

___

### Mod Two

**Name:** Mod Two

**Description:** Test

**Category & Tier:** Testing / 2 - Recommended

**Non-English Functionality:** YES

<!--<<ModSync>>
- **GUID:** 22222222-2222-2222-2222-222222222222

#### Instructions
1. **GUID:** bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb
   **Action:** Move
   **Overwrite:** false
   **Source:** mod2.txt
   **Destination:** dest
-->

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(markdown);

			Assert.Multiple(() =>
			{
				Assert.That(markdown, Is.Not.Null.And.Not.Empty, "Markdown content should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(2), "Should parse exactly two components");
			});

			var mod1 = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(mod1, Is.Not.Null, "First component should not be null");
				Assert.That(mod1.Name, Is.Not.Null.And.Not.Empty, "First component name should not be null or empty");
				Assert.That(mod1.Name, Is.EqualTo("Mod One"), "First component name should match");
				Assert.That(mod1.Guid, Is.Not.EqualTo(Guid.Empty), "First component GUID should not be empty");
				Assert.That(mod1.Guid.ToString(), Is.EqualTo("11111111-1111-1111-1111-111111111111"), "First component GUID should match");
				Assert.That(mod1.Instructions, Is.Not.Null, "First component instructions list should not be null");
				Assert.That(mod1.Instructions, Has.Count.EqualTo(1), "First component should have exactly 1 instruction");
				Assert.That(mod1.Instructions[0], Is.Not.Null, "First component instruction should not be null");
				Assert.That(mod1.Instructions[0].Action.ToString(), Is.EqualTo("Extract"), "First component instruction action should match");
			});

			var mod2 = result.Components[1];
			Assert.Multiple(() =>
			{
				Assert.That(mod2, Is.Not.Null, "Second component should not be null");
				Assert.That(mod2.Name, Is.Not.Null.And.Not.Empty, "Second component name should not be null or empty");
				Assert.That(mod2.Name, Is.EqualTo("Mod Two"), "Second component name should match");
				Assert.That(mod2.Guid, Is.Not.EqualTo(Guid.Empty), "Second component GUID should not be empty");
				Assert.That(mod2.Guid.ToString(), Is.EqualTo("22222222-2222-2222-2222-222222222222"), "Second component GUID should match");
				Assert.That(mod2.Instructions, Is.Not.Null, "Second component instructions list should not be null");
				Assert.That(mod2.Instructions, Has.Count.EqualTo(1), "Second component should have exactly 1 instruction");
				Assert.That(mod2.Instructions[0], Is.Not.Null, "Second component instruction should not be null");
				Assert.That(mod2.Instructions[0].Action.ToString(), Is.EqualTo("Move"), "Second component instruction action should match");
			});
		}

		[Test]
		public void ParseModSyncMetadata_OptionWithMultipleInstructions_ParsesAll()
		{

			const string markdown = @"### Test Mod

**Name:** Test Mod

**Description:** Test

**Category & Tier:** Testing / 1 - Essential

**Non-English Functionality:** YES

<!--<<ModSync>>
- **GUID:** 12345678-1234-1234-1234-123456789abc

#### Options
##### Option 1
- **GUID:** 11111111-1111-1111-1111-111111111111
- **Name:** Multi-Instruction Option
- **Description:** Option with multiple instructions
- **Is Selected:** true
- **Install State:** 0
- **Is Downloaded:** false
  - **Instruction:**
- **GUID:** aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa
- **Action:** Extract
- **Destination:** dest1
- **Overwrite:** true
- **Source:** file1.zip
  - **Instruction:**
- **GUID:** bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb
- **Action:** Move
- **Destination:** dest2
- **Overwrite:** false
- **Source:** file2.txt
  - **Instruction:**
- **GUID:** cccccccc-cccc-cccc-cccc-cccccccccccc
- **Action:** Copy
- **Destination:** dest3
- **Overwrite:** true
- **Source:** file3.dat
-->

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(markdown);

			Assert.Multiple(() =>
			{
				Assert.That(markdown, Is.Not.Null.And.Not.Empty, "Markdown content should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse exactly one component");
			});

			var component = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(component, Is.Not.Null, "Component should not be null");
				Assert.That(component.Options, Is.Not.Null, "Options list should not be null");
				Assert.That(component.Options, Has.Count.EqualTo(1), "Should have exactly 1 option");
			});

			var option = component.Options[0];
			Assert.Multiple(() =>
			{
				Assert.That(option, Is.Not.Null, "Option should not be null");
				Assert.That(option.Instructions, Is.Not.Null, "Option instructions list should not be null");
				Assert.That(option.Instructions, Has.Count.EqualTo(3), "Option should have exactly 3 instructions");
			});

			Assert.Multiple(() =>
			{
				Assert.That(option.Instructions[0], Is.Not.Null, "First option instruction should not be null");
				Assert.That(option.Instructions[0].Action.ToString(), Is.EqualTo("Extract"), "First option instruction action should match");
				Assert.That(option.Instructions[1], Is.Not.Null, "Second option instruction should not be null");
				Assert.That(option.Instructions[1].Action.ToString(), Is.EqualTo("Move"), "Second option instruction action should match");
				Assert.That(option.Instructions[2], Is.Not.Null, "Third option instruction should not be null");
				Assert.That(option.Instructions[2].Action.ToString(), Is.EqualTo("Copy"), "Third option instruction action should match");
			});
		}

		[Test]
		public void ParseModSyncMetadata_InstructionWithMultipleSources_ParsesAllSources()
		{

			const string markdown = @"### Test Mod

**Name:** Test Mod

**Description:** Test

**Category & Tier:** Testing / 1 - Essential

**Non-English Functionality:** YES

<!--<<ModSync>>
- **GUID:** 12345678-1234-1234-1234-123456789abc

#### Instructions
1. **GUID:** 11111111-1111-1111-1111-111111111111
   **Action:** Choose
   **Source:** option1-guid, option2-guid, option3-guid, option4-guid
-->

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(markdown);

			Assert.Multiple(() =>
			{
				Assert.That(markdown, Is.Not.Null.And.Not.Empty, "Markdown content should not be null or empty");
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parse result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse exactly one component");
			});

			var component = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(component, Is.Not.Null, "Component should not be null");
				Assert.That(component.Instructions, Is.Not.Null, "Instructions list should not be null");
				Assert.That(component.Instructions, Has.Count.EqualTo(1), "Should have exactly 1 instruction");
			});

			var instruction = component.Instructions[0];
			Assert.Multiple(() =>
			{
				Assert.That(instruction, Is.Not.Null, "Instruction should not be null");
				Assert.That(instruction.Source, Is.Not.Null, "Instruction source list should not be null");
				Assert.That(instruction.Source, Has.Count.EqualTo(4), "Should have exactly 4 sources");
				Assert.That(instruction.Source[0], Is.Not.Null, "First source should not be null");
				Assert.That(instruction.Source[0].Trim(), Is.EqualTo("option1-guid"), "First source should match");
				Assert.That(instruction.Source[1], Is.Not.Null, "Second source should not be null");
				Assert.That(instruction.Source[1].Trim(), Is.EqualTo("option2-guid"), "Second source should match");
				Assert.That(instruction.Source[2], Is.Not.Null, "Third source should not be null");
				Assert.That(instruction.Source[2].Trim(), Is.EqualTo("option3-guid"), "Third source should match");
				Assert.That(instruction.Source[3], Is.Not.Null, "Fourth source should not be null");
				Assert.That(instruction.Source[3].Trim(), Is.EqualTo("option4-guid"), "Fourth source should match");
			});
		}
	}
}
