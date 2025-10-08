// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System.Text.RegularExpressions;
using KOTORModSync.Core.Parsing;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class MarkdownImportTests
	{
		private const string SampleMarkdown = @"### KOTOR Dialogue Fixes

**Name:** [KOTOR Dialogue Fixes](https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/)

**Author:** Salk & Kainzorus Prime

**Description:** In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.

**Category & Tier:** Immersion / 1 - Essential

**Non-English Functionality:** NO

**Installation Method:** Loose-File Mod

**Installation Instructions:** The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.

___

### Character Startup Changes

**Name:** [Character Startup Changes](http://deadlystream.com/files/file/349-character-start-up-change/) and [**Patch**](https://mega.nz/file/sRw1GBIK#J8znLBwR6t7ZvZnpQbsUBYcUNfPCWA7wYNW3qU6gZSg)

**Author:** jonathan7, patch by A Future Pilot

**Description:** In a normal KOTOR start, your character's feats are pre-selected. This mod changes the initial level-up so that the number of feat points given is determined by class, but you can choose the feats you wish to invest into.

**Category & Tier:** Mechanics Change / 2 - Recommended

**Non-English Functionality:** YES

**Installation Method:** TSLPatcher, Loose-File Patch

**Usage Warning:** It's possible, if using auto level-up, to miss the feats to equip weapons and basic light armor while using this mod, unless you use the patch. Make sure to install it!

___

### Ultimate Korriban

**Name:** [Ultimate Korriban High Resolution](https://www.nexusmods.com/kotor/mods/1367) and [**Patch**](https://mega.nz/file/NEpH3AoZ#5RVfQHjkdk6b3lgcJzgitpCb1YQlfYqhTM0XF3Z9LMM)

**Author:** ShiningRedHD

**Description:** The Ultimate series of mods is a comprehensive AI-upscale of planetary textures. Unlike previous AI upscales, the Ultimate series has no transparency problems while still retaining reflections on textures, all without any additional steps required. This mod upscales the Sith world of Korriban.

**Category & Tier:** Graphics Improvement / 1 - Essential

**Non-English Functionality:** YES

**Installation Method:** Loose-File Mod

**Installation Instructions:** Download the .tpc variant of the mod. Don't worry about it saying it requires Kexikus's skyboxes, that mod will be installed later.

___";

		[Test]
		public void ComponentSectionPattern_MatchesModSections()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.ComponentSectionPattern, profile.ComponentSectionOptions);

			// Act
			MatchCollection matches = regex.Matches(SampleMarkdown);

			// Assert
			Assert.That(matches.Count, Is.EqualTo(3));
		}

		[Test]
		public void RawRegexPattern_ExtractsModName()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(SampleMarkdown);
			var names = result.Components.Select(c => c.Name).ToList();

			// Assert
			Assert.That(names, Does.Contain("KOTOR Dialogue Fixes"));
			Assert.That(names, Does.Contain("Character Startup Changes"));
			Assert.That(names, Does.Contain("Ultimate Korriban High Resolution"));
		}

		[Test]
		public void RawRegexPattern_ExtractsAuthor()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			// Act
			MatchCollection matches = regex.Matches(SampleMarkdown);
			Match firstMatch = matches[0];
			string author = firstMatch.Groups["author"].Value.Trim();

			// Assert
			Assert.That(author, Is.EqualTo("Salk & Kainzorus Prime"));
		}

		[Test]
		public void RawRegexPattern_ExtractsDescription()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			// Act
			MatchCollection matches = regex.Matches(SampleMarkdown);
			Match firstMatch = matches[0];
			string description = firstMatch.Groups["description"].Value.Trim();

			// Assert
			Assert.That(description, Does.StartWith("In addition to fixing several typos"));
		}

		[Test]
		public void CategoryTierPattern_ExtractsCategoryAndTier()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var categoryTierRegex = new Regex(profile.CategoryTierPattern);

			// Act
			Match match = categoryTierRegex.Match("**Category & Tier:** Immersion / 1 - Essential");

			Assert.Multiple(() =>
			{
				// Assert
				Assert.That(match.Success, Is.True);
				Assert.That(match.Groups["category"].Value.Trim(), Is.EqualTo("Immersion"));
				Assert.That(match.Groups["tier"].Value.Trim(), Is.EqualTo("1 - Essential"));
			});
		}

		[Test]
		public void RawRegexPattern_ExtractsCategoryTier()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(SampleMarkdown);
			var categoryTiers = result.Components
				.Select(c => $"{c.Category} / {c.Tier}")
				.ToList();

			// Assert
			Assert.That(categoryTiers, Does.Contain("Immersion / 1 - Essential"));
			Assert.That(categoryTiers, Does.Contain("Mechanics Change / 2 - Recommended"));
			Assert.That(categoryTiers, Does.Contain("Graphics Improvement / 1 - Essential"));
		}

		[Test]
		public void RawRegexPattern_ExtractsInstallationMethod()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			// Act
			MatchCollection matches = regex.Matches(SampleMarkdown);
			var methods = matches
				.Select(m => m.Groups["installation_method"].Value.Trim())
				.ToList();

			// Assert
			Assert.That(methods, Does.Contain("Loose-File Mod"));
			Assert.That(methods, Does.Contain("TSLPatcher, Loose-File Patch"));
		}

		[Test]
		public void RawRegexPattern_ExtractsInstallationInstructions()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			// Act
			MatchCollection matches = regex.Matches(SampleMarkdown);
			Match firstMatch = matches[0];
			string instructions = firstMatch.Groups["installation_instructions"].Value.Trim();

			// Assert
			Assert.That(instructions, Does.StartWith("The choice of which version to use is up to you"));
		}

		[Test]
		public void RawRegexPattern_ExtractsNonEnglishFunctionality()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			// Act
			MatchCollection matches = regex.Matches(SampleMarkdown);
			var nonEnglishValues = matches
				.Select(m => m.Groups["non_english"].Value.Trim())
				.ToList();

			// Assert
			Assert.That(nonEnglishValues, Does.Contain("NO"));
			Assert.That(nonEnglishValues, Does.Contain("YES"));
		}

		[Test]
		public void NamePattern_ExtractsNameFromBrackets()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var nameRegex = new Regex(profile.NamePattern);
			string testLine = "**Name:** [KOTOR Dialogue Fixes](https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/)";

			// Act
			Match match = nameRegex.Match(testLine);

			Assert.Multiple(() =>
			{
				// Assert
				Assert.That(match.Success, Is.True);
				Assert.That(match.Groups["name"].Value, Is.EqualTo("KOTOR Dialogue Fixes"));
			});
		}

		[Test]
		public void ModLinkPattern_ExtractsLinkUrl()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var linkRegex = new Regex(profile.ModLinkPattern);
			string testLine = "[KOTOR Dialogue Fixes](https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/)";

			// Act
			Match match = linkRegex.Match(testLine);

			Assert.Multiple(() =>
			{
				// Assert
				Assert.That(match.Success, Is.True);
				Assert.That(match.Groups["label"].Value, Is.EqualTo("KOTOR Dialogue Fixes"));
				Assert.That(match.Groups["link"].Value, Is.EqualTo("https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/"));
			});
		}

		[Test]
		public void RawRegexPattern_HandlesModsWithoutAllFields()
		{
			// Arrange
			const string minimalMod = @"### Minimal Mod

**Name:** [Minimal Mod](https://example.com)

**Author:** Test Author

**Category & Tier:** Test / 1 - Essential

___";
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			// Act
			MatchCollection matches = regex.Matches(minimalMod);

			// Assert
			Assert.That(matches.Count, Is.EqualTo(expected: 1));
			Assert.Multiple(() =>
			{
				Assert.That(matches[0].Groups["name"].Value.Trim(), Is.EqualTo("Minimal Mod"));
				Assert.That(matches[0].Groups["author"].Value.Trim(), Is.EqualTo("Test Author"));
			});
		}

		[Test]
		public void FullMarkdownFile_ParsesAllMods()
		{
			// Arrange
			string fullMarkdownPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "mod-builds", "content", "k1", "full.md");
			string fullMarkdown = File.ReadAllText(fullMarkdownPath);

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(fullMarkdown);
			IList<Core.ModComponent> components = result.Components;

			// Output detailed summary for verification
			Console.WriteLine($"Total mods found: {components.Count}");

			// Verify some known mods are captured correctly
			var modNames = components.Select(c => c.Name).ToList();
			var modAuthors = components.Select(c => c.Author).ToList();
			var modCategories = components.Select(c => $"{c.Category} / {c.Tier}").ToList();
			var modDescriptions = components.Select(c => c.Description).ToList();

			Console.WriteLine($"Mods with authors: {modAuthors.Count(a => !string.IsNullOrWhiteSpace(a))}");
			Console.WriteLine($"Mods with categories: {modCategories.Count(c => !string.IsNullOrWhiteSpace(c))}");
			Console.WriteLine($"Mods with descriptions: {modDescriptions.Count(d => !string.IsNullOrWhiteSpace(d))}");

			// Show first 15 and last 5 mod names for manual verification
			Console.WriteLine("\nFirst 15 mods:");
			for ( int i = 0; i < Math.Min(15, components.Count); i++ )
			{
				Core.ModComponent component = components[i];
				Console.WriteLine($"{i + 1}. {component.Name}");
				for ( int j = 0; j < component.ModLink.Count; j++ )
				{
					string? modLink = component.ModLink[j];
					Console.WriteLine($"   ModLink {j + 1}: {modLink}");
				}
				Console.WriteLine($"   Author: {component.Author}");
				string categoryStr = component.Category.Count > 0
					? string.Join(", ", component.Category)
					: "No category";
				Console.WriteLine($"   Category: {categoryStr} / {component.Tier}");
				Console.WriteLine($"   Description: {component.Description}");
				Console.WriteLine($"   Directions: {component.Directions}");
				Console.WriteLine($"   Installation Method: {component.InstallationMethod}");
			}

			Console.WriteLine("\nLast 5 mods:");
			for ( int i = Math.Max(0, components.Count - 5); i < components.Count; i++ )
			{
				Core.ModComponent component = components[i];
				Console.WriteLine($"{i + 1}. {component.Name}");
				for ( int j = 0; j < component.ModLink.Count; j++ )
				{
					string? modLink = component.ModLink[j];
					Console.WriteLine($"   ModLink {j + 1}: {modLink}");
				}
				Console.WriteLine($"   Author: {component.Author}");
				string categoryStr = component.Category.Count > 0
					? string.Join(", ", component.Category)
					: "No category";
				Console.WriteLine($"   Category: {categoryStr} / {component.Tier}");
				Console.WriteLine($"   Description: {component.Description}");
				Console.WriteLine($"   Directions: {component.Directions}");
				Console.WriteLine($"   Installation Method: {component.InstallationMethod}");
			}

			// Assert - The file should have a substantial number of mod entries (adjusted expectation)
			Assert.That(components.Count, Is.GreaterThan(70), $"Expected to find more than 70 mod entries in full.md, found {components.Count}");

			Assert.Multiple(() =>
			{
				// Check some known mods from different parts of the file
				Assert.That(modNames, Does.Contain("KOTOR Dialogue Fixes"), "First mod should be captured");
				Assert.That(modNames, Does.Contain("Ultimate Korriban High Resolution"), "Mid-section mod should be captured");
				Assert.That(modNames, Does.Contain("KOTOR High Resolution Menus"), "Near-end mod should be captured");

				// Verify authors are being captured
				Assert.That(modAuthors, Does.Contain("Salk & Kainzorus Prime"), "Author with & character should be captured");
				Assert.That(modAuthors, Does.Contain("ShiningRedHD"), "Simple author should be captured");
				Assert.That(modAuthors, Does.Contain("JCarter426"), "Another common author should be captured");

				// Verify categories are being captured
				Assert.That(modCategories, Does.Contain("Immersion / 1 - Essential"), "Category with Essential tier");
				Assert.That(modCategories, Does.Contain("Graphics Improvement / 2 - Recommended"), "Graphics Improvement category");
				Assert.That(modCategories, Does.Contain("Bugfix / 3 - Suggested"), "Bugfix category");

				// Ensure most entries have the key fields
				Assert.That(modAuthors.Count(a => !string.IsNullOrWhiteSpace(a)), Is.GreaterThan(65), "Most mods should have authors");
				Assert.That(modCategories.Count(c => !string.IsNullOrWhiteSpace(c)), Is.GreaterThan(65), "Most mods should have categories");
			});
		}
	}
}
