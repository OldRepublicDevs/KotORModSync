// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Text.RegularExpressions;
using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class ParameterizedMarkdownImportTests
	{

		private static IEnumerable<TestCaseData> GetAllMarkdownFiles()
		{

			string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? "";
			string solutionRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", ".."));
			string contentRoot = Path.Combine(solutionRoot, "mod-builds", "content");

			string k1Path = Path.Combine(contentRoot, "k1");
			if ( Directory.Exists(k1Path) )
			{
				foreach ( string mdFile in Directory.GetFiles(k1Path, "*.md", SearchOption.AllDirectories) )
				{

					if ( mdFile.Contains("../" + Path.DirectorySeparatorChar + "../" + Path.DirectorySeparatorChar + "validated" + Path.DirectorySeparatorChar) ||
						mdFile.Contains("/validated/") )
						continue;

					string relativePath = Path.GetRelativePath(contentRoot, mdFile);
					yield return new TestCaseData(mdFile)
						.SetName($"K1_{Path.GetFileNameWithoutExtension(mdFile)}")
						.SetCategory("K1")
						.SetCategory("MarkdownImport");
				}
			}

			string k2Path = Path.Combine(contentRoot, "k2");
			if ( Directory.Exists(k2Path) )
			{
				foreach ( string mdFile in Directory.GetFiles(k2Path, "*.md", SearchOption.AllDirectories) )
				{

					if ( mdFile.Contains("../" + Path.DirectorySeparatorChar + "../" + Path.DirectorySeparatorChar + "validated" + Path.DirectorySeparatorChar) ||
						mdFile.Contains("/validated/") )
						continue;

					string relativePath = Path.GetRelativePath(contentRoot, mdFile);
					yield return new TestCaseData(mdFile)
						.SetName($"K2_{Path.GetFileNameWithoutExtension(mdFile)}")
						.SetCategory("K2")
						.SetCategory("MarkdownImport");
				}
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void ComponentSectionPattern_MatchesModSections(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.ComponentSectionPattern, profile.ComponentSectionOptions);

			MatchCollection matches = regex.Matches(markdown);

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Sections matched: {matches.Count}");

			Assert.That(matches, Is.Not.Empty,
				$"Should match at least one section in {Path.GetFileName(mdFilePath)}");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void RawRegexPattern_ExtractsModNames(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(markdown);
			var names = result.Components.Select(c => c.Name).ToList();

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Extracted names: {names.Count}");

			if ( names.Count > 0 )
			{
				Console.WriteLine($"First name: {names[0]}");
				if ( names.Count > 1 )
				{
					Console.WriteLine($"Last name: {names[^1]}");
				}
			}

			Assert.That(names, Is.Not.Empty,
				$"Should extract at least one name from {Path.GetFileName(mdFilePath)}");

			foreach ( string name in names )
			{
				Assert.That(name, Is.Not.Null.And.Not.Empty, "Each extracted name should be non-empty");
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void RawRegexPattern_ExtractsAuthors(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			MatchCollection matches = regex.Matches(markdown);
			var authors = matches
				.Select(m => m.Groups["author"].Value.Trim())
				.Where(a => !string.IsNullOrWhiteSpace(a))
				.ToList();

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Authors found: {authors.Count}");

			if ( authors.Count > 0 )
			{
				Console.WriteLine("Sample authors:");
				foreach ( string author in authors.Take(5) )
				{
					Console.WriteLine($"  - {author}");
				}
			}

			Assert.That(authors, Is.Not.Empty,
				$"Should extract at least one author from {Path.GetFileName(mdFilePath)}");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void RawRegexPattern_ExtractsDescriptions(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			MatchCollection matches = regex.Matches(markdown);
			var descriptions = matches
				.Select(m => m.Groups["description"].Value.Trim())
				.Where(d => !string.IsNullOrWhiteSpace(d))
				.ToList();

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Descriptions found: {descriptions.Count}");

			if ( descriptions.Count > 0 )
			{
				Console.WriteLine($"First description preview: {descriptions[0].Substring(0, Math.Min(100, descriptions[0].Length))}...");
			}

			Assert.That(descriptions, Is.Not.Empty,
				$"Should extract at least one description from {Path.GetFileName(mdFilePath)}");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void RawRegexPattern_ExtractsCategoryTier(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(markdown);

			foreach ( var c in result.Components.Take(3) )
			{
				Console.WriteLine($"Component: {c.Name}");
				Console.WriteLine($"  Category.Count: {c.Category.Count}");
				if ( c.Category.Count > 0 )
				{
					Console.WriteLine($"  Category[0] type: {c.Category[0]?.GetType().FullName ?? "null"}");
					Console.WriteLine($"  Category[0] value: '{c.Category[0]}'");
				}
			}

			var categoryTiers = result.Components
				.Where(c => c.Category.Count > 0 || !string.IsNullOrWhiteSpace(c.Tier))
				.Select(c => $"{string.Join(", ", c.Category)} / {c.Tier}")
				.ToList();

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Category/Tier combinations found: {categoryTiers.Count}");

			if ( categoryTiers.Count > 0 )
			{
				Console.WriteLine("Sample category/tier combinations:");
				foreach ( string ct in categoryTiers.Take(5) )
				{
					Console.WriteLine($"  - {ct}");
				}
			}

			Assert.That(categoryTiers, Is.Not.Empty,
				$"Should extract at least one category/tier from {Path.GetFileName(mdFilePath)}");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void RawRegexPattern_ExtractsInstallationMethod(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			MatchCollection matches = regex.Matches(markdown);
			var methods = matches
				.Select(m => m.Groups["installation_method"].Value.Trim())
				.Where(m => !string.IsNullOrWhiteSpace(m))
				.ToList();

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Installation methods found: {methods.Count}");

			var uniqueMethods = methods.Distinct().ToList();
			Console.WriteLine($"Unique installation methods: {uniqueMethods.Count}");

			foreach ( string method in uniqueMethods )
			{
				int count = methods.Count(m => m == method);
				Console.WriteLine($"  - {method}: {count} occurrence(s)");
			}

			Assert.That(methods, Is.Not.Empty,
				$"Should extract at least one installation method from {Path.GetFileName(mdFilePath)}");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void RawRegexPattern_ExtractsInstallationInstructions(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			MatchCollection matches = regex.Matches(markdown);
			var instructions = matches
				.Select(m => m.Groups["installation_instructions"].Value.Trim())
				.Where(i => !string.IsNullOrWhiteSpace(i))
				.ToList();

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Installation instructions found: {instructions.Count}");

			if ( instructions.Count > 0 )
			{
				Console.WriteLine($"First instruction preview: {instructions[0].Substring(0, Math.Min(100, instructions[0].Length))}...");
			}

			Console.WriteLine($"Mods with installation instructions: {instructions.Count}");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void RawRegexPattern_ExtractsNonEnglishFunctionality(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions);

			MatchCollection matches = regex.Matches(markdown);
			var nonEnglishValues = matches
				.Select(m => m.Groups["non_english"].Value.Trim())
				.Where(v => !string.IsNullOrWhiteSpace(v))
				.ToList();

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Non-English functionality values found: {nonEnglishValues.Count}");

			int yesCount = nonEnglishValues.Count(v => v.Equals("YES", StringComparison.OrdinalIgnoreCase));
			int noCount = nonEnglishValues.Count(v => v.Equals("NO", StringComparison.OrdinalIgnoreCase));

			Console.WriteLine($"  YES: {yesCount}");
			Console.WriteLine($"  NO: {noCount}");
			Console.WriteLine($"  Other: {nonEnglishValues.Count - yesCount - noCount}");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void FullMarkdownFile_ParsesAllMods(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string fullMarkdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(fullMarkdown);
			IList<ModComponent> components = result.Components;

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Total mods found: {components.Count}");

			var modNames = components.Select(c => c.Name).ToList();
			var modAuthors = components.Select(c => c.Author).ToList();
			var modCategories = components.Select(c => $"{string.Join(", ", c.Category)} / {c.Tier}").ToList();
			var modDescriptions = components.Select(c => c.Description).ToList();

			Console.WriteLine($"Mods with authors: {modAuthors.Count(a => !string.IsNullOrWhiteSpace(a))}");
			Console.WriteLine($"Mods with categories: {modCategories.Count(c => !string.IsNullOrWhiteSpace(c))}");
			Console.WriteLine($"Mods with descriptions: {modDescriptions.Count(d => !string.IsNullOrWhiteSpace(d))}");

			Console.WriteLine("\nFirst 10 mods:");
			for ( int i = 0; i < Math.Min(10, components.Count); i++ )
			{
				ModComponent component = components[i];
				Console.WriteLine($"{i + 1}. {component.Name}");
				Console.WriteLine($"   Author: {component.Author}");
				string categoryStr = component.Category.Count > 0
					? string.Join(", ", component.Category)
					: "No category";
				Console.WriteLine($"   Category: {categoryStr} / {component.Tier}");
				Console.WriteLine($"   Installation Method: {component.InstallationMethod}");
			}

			Assert.That(components, Is.Not.Empty,
				$"Expected to find at least one mod entry in {Path.GetFileName(mdFilePath)}, found {components.Count}");

			Assert.Multiple(() =>
			{

				int expectedMinAuthors = (int)(components.Count * 0.5);
				int expectedMinCategories = (int)(components.Count * 0.5);
				int expectedMinDescriptions = (int)(components.Count * 0.5);

				Assert.That(modAuthors.Count(a => !string.IsNullOrWhiteSpace(a)), Is.GreaterThanOrEqualTo(expectedMinAuthors),
					"Most mods should have authors");
				Assert.That(modCategories.Count(c => !string.IsNullOrWhiteSpace(c)), Is.GreaterThanOrEqualTo(expectedMinCategories),
					"Most mods should have categories");
				Assert.That(modDescriptions.Count(d => !string.IsNullOrWhiteSpace(d)), Is.GreaterThanOrEqualTo(expectedMinDescriptions),
					"Most mods should have descriptions");
			});
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void NamePattern_ExtractsNameFromBrackets(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var nameRegex = new Regex(profile.NamePattern);

			var lines = markdown.Split('\n');
			var nameLines = lines.Where(l => l.Contains("**Name:**")).ToList();

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Name lines found: {nameLines.Count}");

			int matchedCount = 0;
			foreach ( string line in nameLines )
			{
				Match match = nameRegex.Match(line);
				if ( match.Success )
				{
					matchedCount++;
					string name = match.Groups["name"].Value;
					Assert.That(name, Is.Not.Null.And.Not.Empty, "Extracted name should not be empty");
				}
			}

			Console.WriteLine($"Successfully matched: {matchedCount}");

			Assert.That(matchedCount, Is.GreaterThan(0),
				$"Should extract at least one name from {Path.GetFileName(mdFilePath)}");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void ModLinkPattern_ExtractsLinkUrls(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var linkRegex = new Regex(profile.ModLinkPattern);

			MatchCollection matches = linkRegex.Matches(markdown);

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Links found: {matches.Count}");

			int sampleCount = Math.Min(5, matches.Count);
			Console.WriteLine($"\nSample links:");
			for ( int i = 0; i < sampleCount; i++ )
			{
				Match match = matches[i];
				string label = match.Groups["label"].Value;
				string link = match.Groups["link"].Value;
				Console.WriteLine($"  [{label}]({link})");

				bool isValidLink = link.StartsWith("http://") || link.StartsWith("https://") ||
								   link.StartsWith("#") || link.StartsWith("/");
				Assert.That(isValidLink, Is.True,
					$"Link should be a valid URL, anchor link, or relative path: {link}");
			}

			Assert.That(matches, Is.Not.Empty,
				$"Should extract at least one link from {Path.GetFileName(mdFilePath)}");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void Parse_ValidateAllComponentsHaveValidNames(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult result = parser.Parse(markdown);
			IList<ModComponent> components = result.Components;

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Components: {components.Count}");

			foreach ( ModComponent component in components )
			{
				Assert.That(component.Name, Is.Not.Null.And.Not.Empty,
					"Every component should have a non-empty name");
				Assert.That(component.Name.Trim(), Is.EqualTo(component.Name),
					$"Component name should not have leading/trailing whitespace: '{component.Name}'");
			}

			var nameGroups = components.GroupBy(c => c.Name).Where(g => g.Count() > 1).ToList();
			if ( nameGroups.Count != 0 )
			{
				Console.WriteLine($"\nWarning: Found {nameGroups.Count} duplicate name(s):");
				foreach ( var group in nameGroups )
				{
					Console.WriteLine($"  - '{group.Key}' appears {group.Count()} times");
				}
			}
		}
	}
}

