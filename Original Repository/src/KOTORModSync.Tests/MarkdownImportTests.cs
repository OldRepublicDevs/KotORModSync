// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Text.RegularExpressions;

using KOTORModSync.Core;
using KOTORModSync.Core.CLI;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class MarkdownImportTests
	{
		private const string SampleMarkdown = @"### Example Dialogue Enhancement

**Name:** [Example Dialogue Enhancement](https://deadlystream.com/files/file/1313-example-dialogue-enhancement/)

**Author:** Test Author A & Test Author B

**Description:** In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.

**Category & Tier:** Immersion / 1 - Essential

**Non-English Functionality:** NO

**Installation Method:** Loose-File Mod

**Installation Instructions:** The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.

___

### Example Startup Modifier

**Name:** [Example Startup Modifier](http://deadlystream.com/files/file/349-example-startup-modifier/) and [**Patch**](https://mega.nz/file/sRw1GBIK#J8znLBwR6t7ZvZnpQbsUBYcUNfPCWA7wYNW3qU6gZSg)

**Author:** TestAuthor7, patch by TestPilot

**Description:** In a normal KOTOR start, your character's feats are pre-selected. This mod changes the initial level-up so that the number of feat points given is determined by class, but you can choose the feats you wish to invest into.

**Category & Tier:** Mechanics Change / 2 - Recommended

**Non-English Functionality:** YES

**Installation Method:** TSLPatcher, Loose-File Patch

**Usage Warning:** It's possible, if using auto level-up, to miss the feats to equip weapons and basic light armor while using this mod, unless you use the patch. Make sure to install it!

___

### Example Korriban Enhancement

**Name:** [Example Korriban Enhancement](https://www.nexusmods.com/kotor/mods/1367) and [**Patch**](https://mega.nz/file/sRw1GBIK#J8znLBwR6t7ZvZnpQbsUBYcUNfPCWA7wYNW3qU6gZSg)

**Author:** TestAuthorHD

**Description:** The Example series of mods is a comprehensive AI-upscale of planetary textures. Unlike previous AI upscales, the Example series has no transparency problems while still retaining reflections on textures, all without any additional steps required. This mod upscales the Sith world of Korriban.

**Category & Tier:** Graphics Improvement / 1 - Essential

**Non-English Functionality:** YES

**Installation Method:** Loose-File Mod

**Installation Instructions:** Download the .tpc variant of the mod. Don't worry about it saying it requires additional skyboxes, that mod will be installed later.

___";

		[Test]
		public void ComponentSectionPattern_MatchesModSections()
		{

			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.ComponentSectionPattern, profile.ComponentSectionOptions);

			MatchCollection matches = regex.Matches(SampleMarkdown);

			Assert.Multiple(() =>
			{
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(regex, Is.Not.Null, "Regex should not be null");
				Assert.That(SampleMarkdown, Is.Not.Null.And.Not.Empty, "Sample markdown should not be null or empty");
				Assert.That(matches, Is.Not.Null, "Match collection should not be null");
				Assert.That(matches, Has.Count.EqualTo(3), "Should match exactly 3 mod sections in sample markdown");
			});
		}

		[Test]
		public void RawRegexPattern_ExtractsModName()
		{

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(SampleMarkdown);
			var names = result.Components.Select(c => c.Name).ToList();

			Assert.Multiple(() =>
			{
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parser result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(3), "Should parse exactly 3 components");
				Assert.That(names, Is.Not.Null, "Names list should not be null");
				Assert.That(names, Has.Count.EqualTo(3), "Should extract exactly 3 names");
				Assert.That(names, Does.Contain("Example Dialogue Enhancement"), "Should contain first mod name");
				Assert.That(names, Does.Contain("Example Startup Modifier"), "Should contain second mod name");
				Assert.That(names, Does.Contain("Example Korriban Enhancement"), "Should contain third mod name");
			});
		}

		[Test]
		public void RawRegexPattern_ExtractsAuthor()
		{

			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			MatchCollection matches = regex.Matches(SampleMarkdown);
			Match firstMatch = matches[0];
			string author = firstMatch.Groups["author"].Value.Trim();

			Assert.Multiple(() =>
			{
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(regex, Is.Not.Null, "Regex should not be null");
				Assert.That(matches, Is.Not.Null, "Match collection should not be null");
				Assert.That(matches, Has.Count.GreaterThan(0), "Should have at least one match");
				Assert.That(firstMatch, Is.Not.Null, "First match should not be null");
				Assert.That(firstMatch.Success, Is.True, "First match should be successful");
				Assert.That(firstMatch.Groups["author"], Is.Not.Null, "Author group should not be null");
				Assert.That(author, Is.Not.Null, "Author string should not be null");
				Assert.That(author, Is.EqualTo("Test Author A & Test Author B"), "Should extract correct author name");
			});
		}

		[Test]
		public void RawRegexPattern_ExtractsDescription()
		{

			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			MatchCollection matches = regex.Matches(SampleMarkdown);
			Match firstMatch = matches[0];
			string description = firstMatch.Groups["description"].Value.Trim();

			Assert.Multiple(() =>
			{
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(regex, Is.Not.Null, "Regex should not be null");
				Assert.That(matches, Is.Not.Null, "Match collection should not be null");
				Assert.That(matches, Has.Count.GreaterThan(0), "Should have at least one match");
				Assert.That(firstMatch, Is.Not.Null, "First match should not be null");
				Assert.That(firstMatch.Success, Is.True, "First match should be successful");
				Assert.That(firstMatch.Groups["description"], Is.Not.Null, "Description group should not be null");
				Assert.That(description, Is.Not.Null, "Description string should not be null");
				Assert.That(description, Is.Not.Empty, "Description should not be empty");
				Assert.That(description, Does.StartWith("In addition to fixing several typos"), "Should extract correct description");
			});
		}

		[Test]
		public void CategoryTierPattern_ExtractsCategoryAndTier()
		{

			var profile = MarkdownImportProfile.CreateDefault();
			var categoryTierRegex = new Regex(profile.CategoryTierPattern);

			Match match = categoryTierRegex.Match("**Category & Tier:** Immersion / 1 - Essential");

			Assert.Multiple(() =>
			{
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(categoryTierRegex, Is.Not.Null, "Category tier regex should not be null");
				Assert.That(match, Is.Not.Null, "Match should not be null");
				Assert.That(match.Success, Is.True, "Category tier pattern should match");
				Assert.That(match.Groups["category"], Is.Not.Null, "Category group should not be null");
				Assert.That(match.Groups["tier"], Is.Not.Null, "Tier group should not be null");
				Assert.That(match.Groups["category"].Value.Trim(), Is.EqualTo("Immersion"), "Should extract correct category");
				Assert.That(match.Groups["tier"].Value.Trim(), Is.EqualTo("1 - Essential"), "Should extract correct tier");
			});
		}

		[Test]
		public void RawRegexPattern_ExtractsCategoryTier()
		{

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(SampleMarkdown);
			var categoryTiers = result.Components
				.Select(c => $"{c.Category} / {c.Tier}")
				.ToList();

			Assert.Multiple(() =>
			{
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(parser, Is.Not.Null, "Markdown parser should not be null");
				Assert.That(result, Is.Not.Null, "Parser result should not be null");
				Assert.That(result.Components, Is.Not.Null, "Components list should not be null");
				Assert.That(result.Components, Has.Count.EqualTo(3), "Should parse exactly 3 components");
				Assert.That(categoryTiers, Is.Not.Null, "Category tiers list should not be null");
				Assert.That(categoryTiers, Has.Count.EqualTo(3), "Should extract exactly 3 category/tier pairs");
				Assert.That(categoryTiers, Does.Contain("Immersion / 1 - Essential"), "Should contain first category/tier");
				Assert.That(categoryTiers, Does.Contain("Mechanics Change / 2 - Recommended"), "Should contain second category/tier");
				Assert.That(categoryTiers, Does.Contain("Graphics Improvement / 1 - Essential"), "Should contain third category/tier");
			});
		}

		[Test]
		public void RawRegexPattern_ExtractsInstallationMethod()
		{

			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			MatchCollection matches = regex.Matches(SampleMarkdown);
			var methods = matches
				.Select(m => m.Groups["installation_method"].Value.Trim())
				.ToList();

			Assert.Multiple(() =>
			{
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(regex, Is.Not.Null, "Regex should not be null");
				Assert.That(matches, Is.Not.Null, "Match collection should not be null");
				Assert.That(matches, Has.Count.GreaterThan(0), "Should have at least one match");
				Assert.That(methods, Is.Not.Null, "Methods list should not be null");
				Assert.That(methods, Is.Not.Empty, "Methods list should not be empty");
				Assert.That(methods, Does.Contain("Loose-File Mod"), "Should contain Loose-File Mod installation method");
				Assert.That(methods, Does.Contain("TSLPatcher, Loose-File Patch"), "Should contain TSLPatcher installation method");
			});
		}

		[Test]
		public void RawRegexPattern_ExtractsInstallationInstructions()
		{

			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			MatchCollection matches = regex.Matches(SampleMarkdown);
			Match firstMatch = matches[0];
			string instructions = firstMatch.Groups["installation_instructions"].Value.Trim();

			Assert.Multiple(() =>
			{
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(regex, Is.Not.Null, "Regex should not be null");
				Assert.That(matches, Is.Not.Null, "Match collection should not be null");
				Assert.That(matches, Has.Count.GreaterThan(0), "Should have at least one match");
				Assert.That(firstMatch, Is.Not.Null, "First match should not be null");
				Assert.That(firstMatch.Success, Is.True, "First match should be successful");
				Assert.That(firstMatch.Groups["installation_instructions"], Is.Not.Null, "Installation instructions group should not be null");
				Assert.That(instructions, Is.Not.Null, "Instructions string should not be null");
				Assert.That(instructions, Is.Not.Empty, "Instructions should not be empty");
				Assert.That(instructions, Does.StartWith("The choice of which version to use is up to you"), "Should extract correct installation instructions");
			});
		}

		[Test]
		public void RawRegexPattern_ExtractsNonEnglishFunctionality()
		{

			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			MatchCollection matches = regex.Matches(SampleMarkdown);
			var nonEnglishValues = matches
				.Select(m => m.Groups["non_english"].Value.Trim())
				.ToList();

			Assert.Multiple(() =>
			{
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(regex, Is.Not.Null, "Regex should not be null");
				Assert.That(matches, Is.Not.Null, "Match collection should not be null");
				Assert.That(matches, Has.Count.GreaterThan(0), "Should have at least one match");
				Assert.That(nonEnglishValues, Is.Not.Null, "Non-English values list should not be null");
				Assert.That(nonEnglishValues, Is.Not.Empty, "Non-English values list should not be empty");
				Assert.That(nonEnglishValues, Does.Contain("NO"), "Should contain NO for non-English functionality");
				Assert.That(nonEnglishValues, Does.Contain("YES"), "Should contain YES for non-English functionality");
			});
		}

		[Test]
		public void NamePattern_ExtractsNameFromBrackets()
		{

			var profile = MarkdownImportProfile.CreateDefault();
			var nameRegex = new Regex(profile.NamePattern);
			string testLine = "**Name:** [Example Dialogue Enhancement](https://deadlystream.com/files/file/1313-example-dialogue-enhancement/)";

			Match match = nameRegex.Match(testLine);

			Assert.Multiple(() =>
			{
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(nameRegex, Is.Not.Null, "Name regex should not be null");
				Assert.That(testLine, Is.Not.Null.And.Not.Empty, "Test line should not be null or empty");
				Assert.That(match, Is.Not.Null, "Match should not be null");
				Assert.That(match.Success, Is.True, "Name pattern should match test line");
				Assert.That(match.Groups["name"], Is.Not.Null, "Name group should not be null");
				Assert.That(match.Groups["name"].Value, Is.EqualTo("Example Dialogue Enhancement"), "Should extract correct name from brackets");
			});
		}

		[Test]
		public void ModLinkPattern_ExtractsLinkUrl()
		{

			var profile = MarkdownImportProfile.CreateDefault();
			var linkRegex = new Regex(profile.ModLinkPattern);
			string testLine = "[Example Dialogue Enhancement](https://deadlystream.com/files/file/1313-example-dialogue-enhancement/)";

			Match match = linkRegex.Match(testLine);

			Assert.Multiple(() =>
			{
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(linkRegex, Is.Not.Null, "Link regex should not be null");
				Assert.That(testLine, Is.Not.Null.And.Not.Empty, "Test line should not be null or empty");
				Assert.That(match, Is.Not.Null, "Match should not be null");
				Assert.That(match.Success, Is.True, "Mod link pattern should match test line");
				Assert.That(match.Groups["label"], Is.Not.Null, "Label group should not be null");
				Assert.That(match.Groups["link"], Is.Not.Null, "Link group should not be null");
				Assert.That(match.Groups["label"].Value, Is.EqualTo("Example Dialogue Enhancement"), "Should extract correct label from markdown link");
				Assert.That(match.Groups["link"].Value, Is.EqualTo("https://deadlystream.com/files/file/1313-example-dialogue-enhancement/"), "Should extract correct URL from markdown link");
			});
		}

		[Test]
		public void RawRegexPattern_HandlesModsWithoutAllFields()
		{

			const string minimalMod = @"### Minimal Mod

**Name:** [Minimal Mod](https://example.com)

**Author:** Test Author

**Category & Tier:** Test / 1 - Essential

___";
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			MatchCollection matches = regex.Matches(minimalMod);

			Assert.Multiple(() =>
			{
				Assert.That(profile, Is.Not.Null, "Markdown import profile should not be null");
				Assert.That(regex, Is.Not.Null, "Regex should not be null");
				Assert.That(minimalMod, Is.Not.Null.And.Not.Empty, "Minimal mod markdown should not be null or empty");
				Assert.That(matches, Is.Not.Null, "Match collection should not be null");
				Assert.That(matches, Has.Count.EqualTo(1), "Should match exactly one mod section");
				Assert.That(matches[0], Is.Not.Null, "First match should not be null");
				Assert.That(matches[0].Success, Is.True, "First match should be successful");
				Assert.That(matches[0].Groups["name"], Is.Not.Null, "Name group should not be null");
				Assert.That(matches[0].Groups["author"], Is.Not.Null, "Author group should not be null");
				Assert.That(matches[0].Groups["name"].Value.Trim(), Is.EqualTo("Minimal Mod"), "Should extract correct name from minimal mod");
				Assert.That(matches[0].Groups["author"].Value.Trim(), Is.EqualTo("Test Author"), "Should extract correct author from minimal mod");
			});
		}

		[Test]
		public void FullMarkdownFile_ParsesAllMods()
		{

			string fullMarkdownPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "mod-builds", "content", "k1", "full.md");
			string fullMarkdown = File.ReadAllText(fullMarkdownPath);

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(fullMarkdown);
			IList<CSharpKOTOR.ModComponent> components = result.Components;

			TestContext.Progress.WriteLine($"Total mods found: {components.Count}");

			var modNames = components.Select(c => c.Name).ToList();
			var modAuthors = components.Select(c => c.Author).ToList();
			var modCategories = components.Select(c => $"{c.Category} / {c.Tier}").ToList();
			var modDescriptions = components.Select(c => c.Description).ToList();

			TestContext.Progress.WriteLine($"Mods with authors: {modAuthors.Count(a => !string.IsNullOrWhiteSpace(a))}");
			TestContext.Progress.WriteLine($"Mods with categories: {modCategories.Count(c => !string.IsNullOrWhiteSpace(c))}");
			TestContext.Progress.WriteLine($"Mods with descriptions: {modDescriptions.Count(d => !string.IsNullOrWhiteSpace(d))}");

			TestContext.Progress.WriteLine("\nFirst 15 mods:");
			for (int i = 0; i < Math.Min(15, components.Count); i++)
			{
				CSharpKOTOR.ModComponent component = components[i];
				TestContext.Progress.WriteLine($"{i + 1}. {component.Name}");
				int linkIndex = 0;
				foreach (string modLink in component.ModLinkFilenames.Keys)
				{
					linkIndex++;
					TestContext.Progress.WriteLine($"   ModLinkFilenames {linkIndex}: {modLink}");
				}
				TestContext.Progress.WriteLine($"   Author: {component.Author}");
				string categoryStr = component.Category.Count > 0
					? string.Join(", ", component.Category)
					: "No category";
				TestContext.Progress.WriteLine($"   Category: {categoryStr} / {component.Tier}");
				TestContext.Progress.WriteLine($"   Description: {component.Description}");
				TestContext.Progress.WriteLine($"   Directions: {component.Directions}");
				TestContext.Progress.WriteLine($"   Installation Method: {component.InstallationMethod}");
			}

			TestContext.Progress.WriteLine("\nLast 5 mods:");
			for (int i = Math.Max(0, components.Count - 5); i < components.Count; i++)
			{
				CSharpKOTOR.ModComponent component = components[i];
				TestContext.Progress.WriteLine($"{i + 1}. {component.Name}");
				int linkIndex = 0;
				foreach (string modLink in component.ModLinkFilenames.Keys)
				{
					linkIndex++;
					TestContext.Progress.WriteLine($"   ModLinkFilenames {linkIndex}: {modLink}");
				}
				TestContext.Progress.WriteLine($"   Author: {component.Author}");
				string categoryStr = component.Category.Count > 0
					? string.Join(", ", component.Category)
					: "No category";
				TestContext.Progress.WriteLine($"   Category: {categoryStr} / {component.Tier}");
				TestContext.Progress.WriteLine($"   Description: {component.Description}");
				TestContext.Progress.WriteLine($"   Directions: {component.Directions}");
				TestContext.Progress.WriteLine($"   Installation Method: {component.InstallationMethod}");
			}

			Assert.That(components, Has.Count.GreaterThan(70), $"Expected to find more than 70 mod entries in full.md, found {components.Count}");

			Assert.Multiple(() =>
			{

				Assert.That(modNames, Does.Contain("Example Dialogue Enhancement"), "First mod should be captured");
				Assert.That(modNames, Does.Contain("Example Korriban Enhancement"), "Mid-section mod should be captured");
				Assert.That(modNames, Does.Contain("Example High Resolution Menus"), "Near-end mod should be captured");

				Assert.That(modAuthors, Does.Contain("Test Author A & Test Author B"), "Author with & character should be captured");
				Assert.That(modAuthors, Does.Contain("TestAuthorHD"), "Simple author should be captured");
				Assert.That(modAuthors, Does.Contain("TestAuthor426"), "Another common author should be captured");

				Assert.That(modCategories, Does.Contain("Immersion / 1 - Essential"), "Category with Essential tier");
				Assert.That(modCategories, Does.Contain("Graphics Improvement / 2 - Recommended"), "Graphics Improvement category");
				Assert.That(modCategories, Does.Contain("Bugfix / 3 - Suggested"), "Bugfix category");

				Assert.That(modAuthors.Count(a => !string.IsNullOrWhiteSpace(a)), Is.GreaterThan(65), "Most mods should have authors");
				Assert.That(modCategories.Count(c => !string.IsNullOrWhiteSpace(c)), Is.GreaterThan(65), "Most mods should have categories");
			});
		}

		[Test]
		public void ModSyncMetadata_ParsesInstructionsAndOptions()
		{

			const string markdownWithModSync = @"### Example Dialogue Enhancement

**Name:** [Example Dialogue Enhancement](https://deadlystream.com/files/file/1313-example-dialogue-enhancement/)

**Author:** Test Author A & Test Author B

**Description:** In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.

**Category & Tier:** Immersion / 1 - Essential

**Non-English Functionality:** NO

**Installation Method:** Loose-File Mod

**Installation Instructions:** The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.

<!--<<ModSync>>
- **GUID:** a9aa5bf5-b4ac-4aa3-acbb-402337235e54

#### Instructions
1. **GUID:** cea7e306-94fe-4a6b-957b-dbb3c189c2f5
   **Action:** Extract
   **Overwrite:** true
   **Source:** <<modDirectory>>\Example_Dialogue_Enhancement*.7z
2. **GUID:** fed09c7a-ac47-441c-a6e5-7a5d8ea56667
   **Action:** Choose
   **Overwrite:** true
   **Source:** cf2a12ec-3932-42f8-996d-b1b1bdfdbb48, 6d593186-e356-4994-b6a8-f71445869937

#### Options
##### Option 1
- **GUID:** cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
- **Name:** Standard
- **Description:** Straight fixes to spelling errors/punctuation/grammar
- **Is Selected:** false
- **Install State:** 0
- **Is Downloaded:** false
- **Restrictions:** 6d593186-e356-4994-b6a8-f71445869937
  - **Instruction:**
- **GUID:** 35b84009-a65b-42d0-8653-215471cf2451
- **Action:** Move
- **Destination:** <<kotorDirectory>>
- **Overwrite:** true
- **Source:** <<modDirectory>>\Example_Dialogue_Enhancement*\Corrections only\dialog.tlk

##### Option 2
- **GUID:** 6d593186-e356-4994-b6a8-f71445869937
- **Name:** Revised
- **Description:** Everything in Straight Fixes, but also has changes from the PC Moderation changes.
- **Restrictions:** cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
  - **Instruction:**
- **GUID:** ff818e5c-480b-437a-9ee1-8c862b952fa2
- **Action:** Move
- **Destination:** <<kotorDirectory>>
- **Overwrite:** true
- **Source:** <<modDirectory>>\Example_Dialogue_Enhancement*\PC Response Moderation version\dialog.tlk
-->

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(markdownWithModSync);

			Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse one component");

			var component = result.Components[0];
			Assert.Multiple(() =>
			{
				Assert.That(component.Name, Is.EqualTo("Example Dialogue Enhancement"), "Component name should be parsed");
				Assert.That(component.Guid.ToString(), Is.EqualTo("a9aa5bf5-b4ac-4aa3-acbb-402337235e54"), "Component GUID should be parsed from ModSync metadata");

				Assert.That(component.Instructions, Has.Count.EqualTo(2), "Should parse 2 instructions");
			});

			var firstInstruction = component.Instructions[0];
			Assert.Multiple(() =>
			{
				Assert.That(firstInstruction.Guid.ToString(), Is.EqualTo("cea7e306-94fe-4a6b-957b-dbb3c189c2f5"), "First instruction GUID");
				Assert.That(firstInstruction.Action.ToString(), Is.EqualTo("Extract"), "First instruction action");
				Assert.That(firstInstruction.Overwrite, Is.True, "First instruction overwrite flag");
				Assert.That(firstInstruction.Source, Has.Count.EqualTo(1), "First instruction should have one source");
				Assert.That(firstInstruction.Source[0], Does.Contain("Example_Dialogue_Enhancement"), "First instruction source path");
			});

			var secondInstruction = component.Instructions[1];
			Assert.Multiple(() =>
			{
				Assert.That(secondInstruction.Guid.ToString(), Is.EqualTo("fed09c7a-ac47-441c-a6e5-7a5d8ea56667"), "Second instruction GUID");
				Assert.That(secondInstruction.Action.ToString(), Is.EqualTo("Choose"), "Second instruction action");
				Assert.That(secondInstruction.Source, Has.Count.EqualTo(2), "Second instruction should have two source GUIDs");

				Assert.That(component.Options, Has.Count.EqualTo(2), "Should parse 2 options");
			});

			var option1 = component.Options[0];
			Assert.Multiple(() =>
			{
				Assert.That(option1.Guid.ToString(), Is.EqualTo("cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"), "Option 1 GUID");
				Assert.That(option1.Name, Is.EqualTo("Standard"), "Option 1 name");
				Assert.That(option1.Description, Does.Contain("Straight fixes"), "Option 1 description");
				Assert.That(option1.IsSelected, Is.False, "Option 1 should not be selected");
				Assert.That(option1.Instructions, Has.Count.EqualTo(1), "Option 1 should have 1 instruction");
			});

			var option1Instruction = option1.Instructions[0];
			Assert.Multiple(() =>
			{
				Assert.That(option1Instruction.Guid.ToString(), Is.EqualTo("35b84009-a65b-42d0-8653-215471cf2451"), "Option 1 instruction GUID");
				Assert.That(option1Instruction.Action.ToString(), Is.EqualTo("Move"), "Option 1 instruction action");
				Assert.That(option1Instruction.Destination, Does.Contain("kotorDirectory"), "Option 1 instruction destination");
			});

			var option2 = component.Options[1];
			Assert.Multiple(() =>
			{
				Assert.That(option2.Guid.ToString(), Is.EqualTo("6d593186-e356-4994-b6a8-f71445869937"), "Option 2 GUID");
				Assert.That(option2.Name, Is.EqualTo("Revised"), "Option 2 name");
				Assert.That(option2.Instructions, Has.Count.EqualTo(1), "Option 2 should have 1 instruction");
			});
		}

		[Test]
		public void ModSyncMetadata_RoundTrip_PreservesInstructionsAndOptions()
		{

			const string markdownWithModSync = @"### Example Dialogue Enhancement

**Name:** [Example Dialogue Enhancement](https://deadlystream.com/files/file/1313-example-dialogue-enhancement/)

**Author:** Test Author A & Test Author B

**Description:** In addition to fixing several typos, this mod takes the PC's dialogue.

**Category & Tier:** Immersion / 1 - Essential

**Non-English Functionality:** NO

**Installation Method:** Loose-File Mod

**Installation Instructions:** The choice of which version to use is up to you.

<!--<<ModSync>>
- **GUID:** a9aa5bf5-b4ac-4aa3-acbb-402337235e54

#### Instructions
1. **GUID:** cea7e306-94fe-4a6b-957b-dbb3c189c2f5
   **Action:** Extract
   **Overwrite:** true
   **Source:** <<modDirectory>>\Example_Dialogue_Enhancement*.7z
2. **GUID:** fed09c7a-ac47-441c-a6e5-7a5d8ea56667
   **Action:** Choose
   **Overwrite:** true
   **Source:** cf2a12ec-3932-42f8-996d-b1b1bdfdbb48, 6d593186-e356-4994-b6a8-f71445869937

#### Options
##### Option 1
- **GUID:** cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
- **Name:** Standard
- **Description:** Straight fixes to spelling errors/punctuation/grammar
- **Is Selected:** false
- **Install State:** 0
- **Is Downloaded:** false
- **Restrictions:** 6d593186-e356-4994-b6a8-f71445869937
  - **Instruction:**
- **GUID:** 35b84009-a65b-42d0-8653-215471cf2451
- **Action:** Move
- **Destination:** <<kotorDirectory>>
- **Overwrite:** true
- **Source:** <<modDirectory>>\Example_Dialogue_Enhancement*\Corrections only\dialog.tlk

##### Option 2
- **GUID:** 6d593186-e356-4994-b6a8-f71445869937
- **Name:** Revised
- **Description:** Everything in Straight Fixes, but also has changes from the PC Moderation changes.
- **Restrictions:** cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
  - **Instruction:**
- **GUID:** ff818e5c-480b-437a-9ee1-8c862b952fa2
- **Action:** Move
- **Destination:** <<kotorDirectory>>
- **Overwrite:** true
- **Source:** <<modDirectory>>\Example_Dialogue_Enhancement*\PC Response Moderation version\dialog.tlk
-->

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult firstParse = parser.Parse(markdownWithModSync);
			string generatedDocs = ModComponentSerializationService.GenerateModDocumentation(firstParse.Components.ToList());

			// Debug output to see what was generated
			TestContext.Progress.WriteLine("=== GENERATED MARKDOWN ===");
			TestContext.Progress.WriteLine(generatedDocs);
			TestContext.Progress.WriteLine("=== END GENERATED MARKDOWN ===");

			MarkdownParserResult secondParse = parser.Parse(generatedDocs);

			Assert.That(secondParse.Components, Has.Count.EqualTo(1), "Should have one component after round-trip");

			var firstComponent = firstParse.Components[0];
			var secondComponent = secondParse.Components[0];

			Assert.Multiple(() =>
			{

				Assert.That(secondComponent.Guid, Is.EqualTo(firstComponent.Guid), "Component GUID should be preserved");

				Assert.That(secondComponent.Instructions, Has.Count.EqualTo(firstComponent.Instructions.Count), "Instruction count should match");
			});

			for (int i = 0; i < firstComponent.Instructions.Count; i++)
			{
				var firstInst = firstComponent.Instructions[i];
				var secondInst = secondComponent.Instructions[i];

				Assert.Multiple(() =>
				{
					Assert.That(secondInst.Guid, Is.EqualTo(firstInst.Guid), $"Instruction {i} GUID should match");
					Assert.That(secondInst.Action, Is.EqualTo(firstInst.Action), $"Instruction {i} Action should match");
					Assert.That(secondInst.Overwrite, Is.EqualTo(firstInst.Overwrite), $"Instruction {i} Overwrite should match");
					Assert.That(secondInst.Source, Has.Count.EqualTo(firstInst.Source.Count), $"Instruction {i} Source count should match");
				});
			}

			Assert.That(secondComponent.Options, Has.Count.EqualTo(firstComponent.Options.Count), "Option count should match");

			for (int i = 0; i < firstComponent.Options.Count; i++)
			{
				var firstOpt = firstComponent.Options[i];
				var secondOpt = secondComponent.Options[i];

				Assert.Multiple(() =>
				{
					Assert.That(secondOpt.Guid, Is.EqualTo(firstOpt.Guid), $"Option {i} GUID should match");
					Assert.That(secondOpt.Name, Is.EqualTo(firstOpt.Name), $"Option {i} Name should match");
					Assert.That(secondOpt.Description, Is.EqualTo(firstOpt.Description), $"Option {i} Description should match");
					Assert.That(secondOpt.IsSelected, Is.EqualTo(firstOpt.IsSelected), $"Option {i} IsSelected should match");
					Assert.That(secondOpt.Instructions, Has.Count.EqualTo(firstOpt.Instructions.Count), $"Option {i} instruction count should match");
				});

				for (int j = 0; j < firstOpt.Instructions.Count; j++)
				{
					var firstInstruction = firstOpt.Instructions[j];
					var secondInstruction = secondOpt.Instructions[j];

					Assert.Multiple(() =>
					{
						Assert.That(secondInstruction.Guid, Is.EqualTo(firstInstruction.Guid), $"Option {i} Instruction {j} GUID should match");
						Assert.That(secondInstruction.Action, Is.EqualTo(firstInstruction.Action), $"Option {i} Instruction {j} Action should match");
					});
				}
			}
		}

		[Test]
		public void InstallationInstructions_DoesNotIncludeModSyncMetadata()
		{
			// Test for the specific bug where Installation Instructions field was including the <!--<<ModSync>> block
			const string markdownWithModSync = @"### Example Dialogue Enhancement

**Name:** [Example Dialogue Enhancement](https://deadlystream.com/files/file/1313-example-dialogue-enhancement/)

**Author:** Test Author A & Test Author B

**Description:** In addition to fixing several typos, this mod takes the PC's dialogue.

**Category & Tier:** Immersion / 1 - Essential

**Non-English Functionality:** NO

**Installation Instructions:** The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.

<!--<<ModSync>>
Guid = ""36186e16-12d0-450d-a3fa-d1d7d930a8d7""
Instructions = [
 = {
Guid = ""aef66ecd-6b32-436c-86e9-d6b79826f026""
Action = ""Extract""
Source = [
""<<modDirectory>>\\Example_Dialogue_Enhancement*.7z"",
]
}
]
-->

___";

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(markdownWithModSync);

			Assert.That(result.Components, Has.Count.EqualTo(1), "Should parse one component");

			var component = result.Components[0];

			Assert.Multiple(() =>
			{
				Assert.That(component.Name, Is.EqualTo("Example Dialogue Enhancement"), "Component name should be parsed");
				Assert.That(component.Directions, Is.Not.Null.And.Not.Empty, "Directions should be extracted");

				// The critical test: Directions should NOT contain the ModSync metadata block
				Assert.That(component.Directions, Does.Not.Contain("<!--<<ModSync>>"), "Directions should not contain ModSync opening tag");
				Assert.That(component.Directions, Does.Not.Contain("-->"), "Directions should not contain ModSync closing tag");
				Assert.That(component.Directions, Does.Not.Contain("Guid = "), "Directions should not contain TOML/metadata content");
				Assert.That(component.Directions, Does.Not.Contain("Instructions = ["), "Directions should not contain metadata Instructions array");

				// Verify the actual content is there
				Assert.That(component.Directions, Does.Contain("PC Response Moderation"), "Directions should contain the actual installation text");
				Assert.That(component.Directions, Does.Contain("main game directory"), "Directions should contain the actual installation text");
			});
		}
	}
}
