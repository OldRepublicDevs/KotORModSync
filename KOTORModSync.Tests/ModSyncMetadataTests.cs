// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Linq;
using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class ModSyncMetadataTests
	{
		[Test]
		public void ParseModSyncMetadata_WithCompleteData_ParsesAllFields()
		{
			// Arrange
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
   **Overwrite:** true
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

			// Act
			MarkdownParserResult result = parser.Parse(markdown);

			// Assert
			Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse one component");

			var component = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(component.Guid.ToString(), Is.EqualTo("12345678-1234-1234-1234-123456789abc"), "Component GUID");
				Assert.That(component.Name, Is.EqualTo("Test Mod"), "Component name");

				Assert.That(component.Instructions, Has.Count.EqualTo(1), "Should have 1 instruction");
			});
			var instruction = component.Instructions[0];
			Assert.Multiple(() =>
			{
				Assert.That(instruction.Guid.ToString(), Is.EqualTo("11111111-1111-1111-1111-111111111111"), "Instruction GUID");
				Assert.That(instruction.Action.ToString(), Is.EqualTo("Extract"), "Instruction action");
				Assert.That(instruction.Overwrite, Is.True, "Instruction overwrite");
				Assert.That(instruction.Source[0], Is.EqualTo("test.zip"), "Instruction source");
				Assert.That(instruction.Destination, Is.EqualTo("testdest"), "Instruction destination");

				Assert.That(component.Options, Has.Count.EqualTo(1), "Should have 1 option");
			});
			var option = component.Options[0];
			Assert.Multiple(() =>
			{
				Assert.That(option.Guid.ToString(), Is.EqualTo("22222222-2222-2222-2222-222222222222"), "Option GUID");
				Assert.That(option.Name, Is.EqualTo("Test Option"), "Option name");
				Assert.That(option.Description, Is.EqualTo("Test option description"), "Option description");
				Assert.That(option.IsSelected, Is.True, "Option IsSelected");
				Assert.That(option.IsDownloaded, Is.True, "Option IsDownloaded");

				Assert.That(option.Instructions, Has.Count.EqualTo(1), "Option should have 1 instruction");
			});
			var optionInstruction = option.Instructions[0];
			Assert.Multiple(() =>
			{
				Assert.That(optionInstruction.Guid.ToString(), Is.EqualTo("44444444-4444-4444-4444-444444444444"), "Option instruction GUID");
				Assert.That(optionInstruction.Action.ToString(), Is.EqualTo("Move"), "Option instruction action");
			});
		}

		[Test]
		public void ParseModSyncMetadata_WithoutMetadata_ParsesNormally()
		{
			// Arrange
			const string markdown = @"### Test Mod

**Name:** Test Mod

**Author:** Test Author

**Description:** Test description

**Category & Tier:** Testing / 1 - Essential

**Non-English Functionality:** YES

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(markdown);

			// Assert
			Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse one component");
			var component = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(component.Name, Is.EqualTo("Test Mod"), "Component name");
				Assert.That(component.Instructions.Count, Is.EqualTo(0), "Should have no instructions");
				Assert.That(component.Options.Count, Is.EqualTo(0), "Should have no options");
			});
		}

		[Test]
		public void ParseModSyncMetadata_MultipleInstructions_ParsesAll()
		{
			// Arrange
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
   **Overwrite:** true
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

			// Act
			MarkdownParserResult result = parser.Parse(markdown);

			// Assert
			Assert.That(result.Components, Has.Count.EqualTo(1));
			var component = result.Components[0];
			Assert.That(component.Instructions, Has.Count.EqualTo(3), "Should have 3 instructions");

			Assert.Multiple(() =>
			{
				Assert.That(component.Instructions[0].Action.ToString(), Is.EqualTo("Extract"));
				Assert.That(component.Instructions[0].Overwrite, Is.True);

				Assert.That(component.Instructions[1].Action.ToString(), Is.EqualTo("Move"));
				Assert.That(component.Instructions[1].Overwrite, Is.False);

				Assert.That(component.Instructions[2].Action.ToString(), Is.EqualTo("Copy"));
				Assert.That(component.Instructions[2].Overwrite, Is.True);
			});
		}

		[Test]
		public void ParseModSyncMetadata_MultipleOptions_ParsesAll()
		{
			// Arrange
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

			// Act
			MarkdownParserResult result = parser.Parse(markdown);

			// Assert
			Assert.That(result.Components, Has.Count.EqualTo(1));
			var component = result.Components[0];
			Assert.That(component.Options, Has.Count.EqualTo(3), "Should have 3 options");

			Assert.Multiple(() =>
			{
				Assert.That(component.Options[0].Name, Is.EqualTo("Option One"));
				Assert.That(component.Options[0].IsSelected, Is.True);
				Assert.That(component.Options[0].IsDownloaded, Is.False);

				Assert.That(component.Options[1].Name, Is.EqualTo("Option Two"));
				Assert.That(component.Options[1].IsSelected, Is.False);

				Assert.That(component.Options[2].Name, Is.EqualTo("Option Three"));
				Assert.That(component.Options[2].IsDownloaded, Is.True);
			});
		}

		[Test]
		public void GenerateModSyncMetadata_WithInstructions_GeneratesCorrectFormat()
		{
			// Arrange
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

			// Act
			string generated = ModComponent.GenerateModDocumentation(new System.Collections.Generic.List<ModComponent> { component });

			// Assert
			Assert.That(generated, Does.Contain("<!--<<ModSync>>"), "Should contain ModSync opening tag");
			Assert.That(generated, Does.Contain("-->"), "Should contain ModSync closing tag");
			Assert.That(generated, Does.Contain("- **GUID:** 12345678-1234-1234-1234-123456789abc"), "Should contain component GUID");
			Assert.That(generated, Does.Contain("#### Instructions"), "Should contain Instructions header");
			Assert.That(generated, Does.Contain("1. **GUID:** 11111111-1111-1111-1111-111111111111"), "Should contain instruction GUID");
			Assert.That(generated, Does.Contain("**Action:** Extract"), "Should contain instruction action");
			Assert.That(generated, Does.Contain("**Overwrite:** true"), "Should contain overwrite flag");
			Assert.That(generated, Does.Contain("**Source:** test.zip"), "Should contain source");
			Assert.That(generated, Does.Contain("**Destination:** testdest"), "Should contain destination");
		}

		[Test]
		public void GenerateModSyncMetadata_WithOptions_GeneratesCorrectFormat()
		{
			// Arrange
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

			// Act
			string generated = ModComponent.GenerateModDocumentation(new System.Collections.Generic.List<ModComponent> { component });

			// Assert
			Assert.That(generated, Does.Contain("#### Options"), "Should contain Options header");
			Assert.That(generated, Does.Contain("##### Option 1"), "Should contain option number");
			Assert.That(generated, Does.Contain("- **GUID:** 22222222-2222-2222-2222-222222222222"), "Should contain option GUID");
			Assert.That(generated, Does.Contain("- **Name:** Test Option"), "Should contain option name");
			Assert.That(generated, Does.Contain("- **Description:** Test option description"), "Should contain option description");
			Assert.That(generated, Does.Contain("- **Is Selected:** true"), "Should contain IsSelected");
			Assert.That(generated, Does.Contain("- **Is Downloaded:** false"), "Should contain IsDownloaded");
		}

		[Test]
		public void GenerateModSyncMetadata_WithoutInstructionsOrOptions_DoesNotGenerateMetadata()
		{
			// Arrange
			var component = new ModComponent
			{
				Guid = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
				Name = "Test Mod",
				Category = new System.Collections.Generic.List<string> { "Testing" },
				Tier = "1 - Essential"
			};

			// Act
			string generated = ModComponent.GenerateModDocumentation(new System.Collections.Generic.List<ModComponent> { component });

			// Assert
			Assert.That(generated, Does.Not.Contain("<!--<<ModSync>>"), "Should not contain ModSync metadata");
			Assert.That(generated, Does.Contain("### Test Mod"), "Should still contain component name");
		}

		[Test]
		public void RoundTrip_ComplexComponent_PreservesAllData()
		{
			// Arrange
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
   **Overwrite:** true
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

			// Act - First parse
			MarkdownParserResult firstParse = parser.Parse(markdown);

			// Act - Generate
			string generated = ModComponent.GenerateModDocumentation(firstParse.Components.ToList());

			// Act - Second parse
			MarkdownParserResult secondParse = parser.Parse(generated);

			// Assert - Component level
			Assert.That(secondParse.Components, Has.Count.EqualTo(1));
			var first = firstParse.Components[0];
			var second = secondParse.Components[0];

			Assert.Multiple(() =>
			{
				Assert.That(second.Guid, Is.EqualTo(first.Guid), "Component GUID preserved");
				Assert.That(second.Name, Is.EqualTo(first.Name), "Name preserved");
				Assert.That(second.Author, Is.EqualTo(first.Author), "Author preserved");

				// Assert - Instructions
				Assert.That(second.Instructions, Has.Count.EqualTo(first.Instructions.Count), "Instruction count preserved");
			});
			for ( int i = 0; i < first.Instructions.Count; i++ )
			{
				Assert.Multiple(() =>
				{
					Assert.That(second.Instructions[i].Guid, Is.EqualTo(first.Instructions[i].Guid), $"Instruction {i} GUID preserved");
					Assert.That(second.Instructions[i].Action, Is.EqualTo(first.Instructions[i].Action), $"Instruction {i} Action preserved");
					Assert.That(second.Instructions[i].Overwrite, Is.EqualTo(first.Instructions[i].Overwrite), $"Instruction {i} Overwrite preserved");
					Assert.That(second.Instructions[i].Source, Has.Count.EqualTo(first.Instructions[i].Source.Count), $"Instruction {i} Source count preserved");
				});
			}

			// Assert - Options
			Assert.That(second.Options, Has.Count.EqualTo(first.Options.Count), "Option count preserved");
			for ( int i = 0; i < first.Options.Count; i++ )
			{
				Assert.Multiple(() =>
				{
					Assert.That(second.Options[i].Guid, Is.EqualTo(first.Options[i].Guid), $"Option {i} GUID preserved");
					Assert.That(second.Options[i].Name, Is.EqualTo(first.Options[i].Name), $"Option {i} Name preserved");
					Assert.That(second.Options[i].Description, Is.EqualTo(first.Options[i].Description), $"Option {i} Description preserved");
					Assert.That(second.Options[i].IsSelected, Is.EqualTo(first.Options[i].IsSelected), $"Option {i} IsSelected preserved");
					Assert.That(second.Options[i].Instructions, Has.Count.EqualTo(first.Options[i].Instructions.Count), $"Option {i} Instruction count preserved");
				});
			}
		}

		[Test]
		public void ParseModSyncMetadata_MultipleComponents_ParsesEachCorrectly()
		{
			// Arrange
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
   **Overwrite:** true
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

			// Act
			MarkdownParserResult result = parser.Parse(markdown);

			// Assert
			Assert.That(result.Components, Has.Count.EqualTo(2), "Should parse two components");

			var mod1 = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(mod1.Name, Is.EqualTo("Mod One"));
				Assert.That(mod1.Guid.ToString(), Is.EqualTo("11111111-1111-1111-1111-111111111111"));
				Assert.That(mod1.Instructions, Has.Count.EqualTo(1));
			});
			Assert.That(mod1.Instructions[0].Action.ToString(), Is.EqualTo("Extract"));

			var mod2 = result.Components[1];
			Assert.Multiple(() =>
			{
				Assert.That(mod2.Name, Is.EqualTo("Mod Two"));
				Assert.That(mod2.Guid.ToString(), Is.EqualTo("22222222-2222-2222-2222-222222222222"));
				Assert.That(mod2.Instructions, Has.Count.EqualTo(1));
			});
			Assert.That(mod2.Instructions[0].Action.ToString(), Is.EqualTo("Move"));
		}

		[Test]
		public void ParseModSyncMetadata_OptionWithMultipleInstructions_ParsesAll()
		{
			// Arrange
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

			// ActHas.Count
			MarkdownParserResult result = parser.Parse(markdown);
			// Assert
			Assert.That(result.Components, Has.Count.EqualTo(1));
			var component = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(component.Options, Has.Count.EqualTo(1));
			});

			var option = component.Options[0];
			Assert.That(option.Instructions, Has.Count.EqualTo(3), "Option should have 3 instructions");

			Assert.Multiple(() =>
			{
				Assert.That(option.Instructions[0].Action.ToString(), Is.EqualTo("Extract"));
				Assert.That(option.Instructions[1].Action.ToString(), Is.EqualTo("Move"));
				Assert.That(option.Instructions[2].Action.ToString(), Is.EqualTo("Copy"));
			});
		}

		[Test]
		public void ParseModSyncMetadata_InstructionWithMultipleSources_ParsesAllSources()
		{
			// Arrange
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
   **Overwrite:** true
   **Source:** option1-guid, option2-guid, option3-guid, option4-guid
-->

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(markdown);

			// Assert
			Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse one component");
			var component = result.Components[0];
			Assert.That(component.Instructions, Has.Count.EqualTo(1));

			var instruction = component.Instructions[0];
			Assert.That(instruction.Source, Has.Count.EqualTo(4), "Should have 4 sources");
			Assert.Multiple(() =>
			{
				Assert.That(instruction.Source[0].Trim(), Is.EqualTo("option1-guid"));
				Assert.That(instruction.Source[1].Trim(), Is.EqualTo("option2-guid"));
				Assert.That(instruction.Source[2].Trim(), Is.EqualTo("option3-guid"));
				Assert.That(instruction.Source[3].Trim(), Is.EqualTo("option4-guid"));
			});
		}
	}
}

