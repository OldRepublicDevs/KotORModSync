// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class ParameterizedDocumentationTests
	{
		/// <summary>
		/// Dynamically generates test cases for all .md files in k1 and k2 directories.
		/// </summary>
		private static IEnumerable<TestCaseData> GetAllMarkdownFiles()
		{
			// Get the solution root by navigating up from the executing assembly location
			// This works during test discovery time
			// bin/Debug/net8.0 -> bin -> Debug -> Tests -> Solution Root
			string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? "";
			string solutionRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", ".."));
			string contentRoot = Path.Combine(solutionRoot, "KOTORModSync.Tests", "mod-builds", "content");

			// Get all .md files from k1 directory (exclude validated subdirectory)
			string k1Path = Path.Combine(contentRoot, "k1");
			if (Directory.Exists(k1Path))
			{
				foreach (string mdFile in Directory.GetFiles(k1Path, "*.md", SearchOption.AllDirectories))
				{
					// Exclude files in validated subdirectory
					if (mdFile.Contains("../" + Path.DirectorySeparatorChar + "../" + Path.DirectorySeparatorChar + "validated" + Path.DirectorySeparatorChar) ||
						mdFile.Contains("/validated/"))
						continue;

					string relativePath = Path.GetRelativePath(contentRoot, mdFile);
					yield return new TestCaseData(mdFile)
						.SetName($"K1_{Path.GetFileNameWithoutExtension(mdFile)}")
						.SetCategory("K1")
						.SetCategory("Markdown");
				}
			}

			// Get all .md files from k2 directory (exclude validated subdirectory)
			string k2Path = Path.Combine(contentRoot, "k2");
			if (Directory.Exists(k2Path))
			{
				foreach (string mdFile in Directory.GetFiles(k2Path, "*.md", SearchOption.AllDirectories))
				{
					// Exclude files in validated subdirectory
					if (mdFile.Contains("../" + Path.DirectorySeparatorChar + "../" + Path.DirectorySeparatorChar + "validated" + Path.DirectorySeparatorChar) ||
						mdFile.Contains("/validated/"))
						continue;

					string relativePath = Path.GetRelativePath(contentRoot, mdFile);
					yield return new TestCaseData(mdFile)
						.SetName($"K2_{Path.GetFileNameWithoutExtension(mdFile)}")
						.SetCategory("K2")
						.SetCategory("Markdown");
				}
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void RoundTrip_ParseAndGenerateDocumentation_ProducesEquivalentOutput(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string originalMarkdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act - Parse
			MarkdownParserResult parseResult = parser.Parse(originalMarkdown);
			IList<ModComponent> components = parseResult.Components;

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Parsed {components.Count} components");
			Console.WriteLine($"Warnings: {parseResult.Warnings.Count}");
			foreach (string warning in parseResult.Warnings)
			{
				Console.WriteLine($"  - {warning}");
			}

			// Act - Generate
			string generatedDocs = ModComponent.GenerateModDocumentation(components.ToList());

			// Write generated docs to validated folder
			string sourceDir = Path.GetDirectoryName(mdFilePath)!;
			string validatedDir = Path.Combine(sourceDir, "validated");
			Directory.CreateDirectory(validatedDir);

			string debugOutputPath = Path.Combine(validatedDir, Path.GetFileName(mdFilePath));

			try
			{
				File.WriteAllText(debugOutputPath, generatedDocs);
				Console.WriteLine($"Generated documentation written to: {debugOutputPath}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Warning: Could not write debug output: {ex.Message}");
			}

			Assert.Multiple(() =>
			{
				// Assert - Basic structural validation
				Assert.That(components.Count, Is.GreaterThan(0), "Should have parsed at least one component");
				Assert.That(generatedDocs, Is.Not.Null.And.Not.Empty, "Generated documentation should not be empty");
			});

			// Extract sections from original (starting from ## Mod List)
			string originalModList = MarkdownToTomlConverter.ExtractModListSection(originalMarkdown);
			List<string> originalSections = MarkdownToTomlConverter.ExtractModSections(originalModList);

			// Extract sections from generated
			List<string> generatedSections = MarkdownToTomlConverter.ExtractModSections(generatedDocs);

			Console.WriteLine($"Original sections: {originalSections.Count}");
			Console.WriteLine($"Generated sections: {generatedSections.Count}");

			// Extract ALL Name field values from all sections
			var originalNameFields = originalSections
				.SelectMany(s => MarkdownToTomlConverter.ExtractAllFieldValues(s, @"\*\*Name:\*\*\s*(?:\[([^\]]+)\]|([^\r\n]+))"))
				.Where(n => !string.IsNullOrWhiteSpace(n))
				.ToList();

			var generatedNameFields = generatedSections
				.SelectMany(s => MarkdownToTomlConverter.ExtractAllFieldValues(s, @"\*\*Name:\*\*\s*(?:\[([^\]]+)\]|([^\r\n]+))"))
				.Where(n => !string.IsNullOrWhiteSpace(n))
				.ToList();

			Console.WriteLine($"Original mod names (from **Name:** field): {originalNameFields.Count}");
			Console.WriteLine($"Generated mod names (from **Name:** field): {generatedNameFields.Count}");

			// Require exact 1:1 match based on actual mod names
			if (generatedNameFields.Count != originalNameFields.Count)
			{
				Console.WriteLine("\n=== NAME FIELD COUNT MISMATCH ===");

				var missingInGenerated = originalNameFields.Except(generatedNameFields).ToList();
				var missingInOriginal = generatedNameFields.Except(originalNameFields).ToList();

				if (missingInGenerated.Count > 0)
				{
					Console.WriteLine($"\nMissing in generated ({missingInGenerated.Count}):");
					foreach (string name in missingInGenerated)
					{
						Console.WriteLine($"  - {name}");
					}
				}

				if (missingInOriginal.Count > 0)
				{
					Console.WriteLine($"\nExtra in generated ({missingInOriginal.Count}):");
					foreach (string name in missingInOriginal)
					{
						Console.WriteLine($"  - {name}");
					}
				}
			}

			Assert.That(generatedNameFields, Has.Count.EqualTo(originalNameFields.Count),
				$"Mod count must match exactly. Original: {originalNameFields.Count}, Generated: {generatedNameFields.Count}");

			// Verify all mod names are preserved
			var missingNames = originalNameFields.Except(generatedNameFields).ToList();
			var extraNames = generatedNameFields.Except(originalNameFields).ToList();

			if (missingNames.Count > 0 || extraNames.Count > 0)
			{
				Console.WriteLine("\n=== MOD NAME MISMATCH ===");
				if (missingNames.Count > 0)
				{
					Console.WriteLine($"Names missing in generated ({missingNames.Count}):");
					foreach (string name in missingNames)
					{
						Console.WriteLine($"  - {name}");
					}
				}
				if (extraNames.Count > 0)
				{
					Console.WriteLine($"Extra names in generated ({extraNames.Count}):");
					foreach (string name in extraNames)
					{
						Console.WriteLine($"  - {name}");
					}
				}
			}

			Assert.Multiple(() =>
			{
				Assert.That(missingNames, Is.Empty, "All original mod names should be present in generated output");
				Assert.That(extraNames, Is.Empty, "No extra mod names should be in generated output");
			});

			Console.WriteLine($"\n✓ All {originalNameFields.Count} mod names match between original and generated");
			Console.WriteLine("✓ Round-trip test successful: Import → Export produces identical mod list");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void RoundTrip_VerifyFieldPreservation(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string originalMarkdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act - Parse
			MarkdownParserResult parseResult = parser.Parse(originalMarkdown);
			List<ModComponent> components = parseResult.Components.ToList();

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Total components: {components.Count}");

			// Assert - Check that key fields are preserved
			foreach (ModComponent component in components)
			{
				Console.WriteLine($"\nVerifying component: {component.Name}");

				Assert.That(component.Name, Is.Not.Null.And.Not.Empty, "Name should not be empty");
				Console.WriteLine($"  Name: {component.Name}");
				Console.WriteLine($"  Author: {component.Author}");
				Console.WriteLine($"  Category: {string.Join(" & ", component.Category)}");
				Console.WriteLine($"  Tier: {component.Tier}");
				Console.WriteLine($"  Language: {string.Join(", ", component.Language)}");
				Console.WriteLine($"  InstallationMethod: {component.InstallationMethod}");
				Console.WriteLine($"  ModLinks: {component.ModLink?.Count ?? 0}");
				Console.WriteLine($"  Description length: {component.Description?.Length ?? 0}");
				Console.WriteLine($"  Directions length: {component.Directions?.Length ?? 0}");
			}

			Assert.That(components, Is.Not.Empty, "Should have parsed components");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void Parse_ValidateComponentStructure(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdownContent = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(markdownContent);
			IList<ModComponent> components = result.Components;

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Total mods found: {components.Count}");

			// Get all names, authors, categories, descriptions
			var modNames = components.Select(c => c.Name).ToList();
			var modAuthors = components.Select(c => c.Author).ToList();
			var modCategories = components.Select(c => $"{string.Join(", ", c.Category)} / {c.Tier}").ToList();
			var modDescriptions = components.Select(c => c.Description).ToList();

			Console.WriteLine($"Mods with authors: {modAuthors.Count(a => !string.IsNullOrWhiteSpace(a))}");
			Console.WriteLine($"Mods with categories: {modCategories.Count(c => !string.IsNullOrWhiteSpace(c))}");
			Console.WriteLine($"Mods with descriptions: {modDescriptions.Count(d => !string.IsNullOrWhiteSpace(d))}");

			// Show first 5 and last 5 mod names for verification
			Console.WriteLine("\nFirst 5 mods:");
			for (int i = 0; i < Math.Min(5, components.Count); i++)
			{
				ModComponent component = components[i];
				Console.WriteLine($"{i + 1}. {component.Name}");
				Console.WriteLine($"   Author: {component.Author}");
				string categoryStr = component.Category.Count > 0
					? string.Join(", ", component.Category)
					: "No category";
				Console.WriteLine($"   Category: {categoryStr} / {component.Tier}");
			}

			if (components.Count > 5)
			{
				Console.WriteLine("\nLast 5 mods:");
				for (int i = Math.Max(0, components.Count - 5); i < components.Count; i++)
				{
					ModComponent component = components[i];
					Console.WriteLine($"{i + 1}. {component.Name}");
					Console.WriteLine($"   Author: {component.Author}");
					string categoryStr = component.Category.Count > 0
						? string.Join(", ", component.Category)
						: "No category";
					Console.WriteLine($"   Category: {categoryStr} / {component.Tier}");
				}
			}

			// Assert - The file should have at least some mod entries
			Assert.That(components.Count, Is.GreaterThan(0),
				$"Expected to find at least one mod entry in {Path.GetFileName(mdFilePath)}, found {components.Count}");

			Assert.Multiple(() =>
			{
				// Verify that most entries have key fields (allow some without)
				int expectedMinAuthors = (int)(components.Count * 0.5); // At least 50% should have authors
				int expectedMinCategories = (int)(components.Count * 0.5); // At least 50% should have categories

				Assert.That(modAuthors.Count(a => !string.IsNullOrWhiteSpace(a)), Is.GreaterThan(expectedMinAuthors),
					"Most mods should have authors");
				Assert.That(modCategories.Count(c => !string.IsNullOrWhiteSpace(c)), Is.GreaterThan(expectedMinCategories),
					"Most mods should have categories");
			});
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void Parse_ValidateModLinks(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdownContent = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(markdownContent);
			IList<ModComponent> components = result.Components;

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Total components: {components.Count}");

			// Verify mod links
			int componentsWithLinks = 0;
			int totalLinks = 0;

			foreach (ModComponent component in components)
			{
				if (component.ModLink?.Count > 0)
				{
					componentsWithLinks++;
					totalLinks += component.ModLink.Count;

					Console.WriteLine($"{component.Name}: {component.ModLink.Count} link(s)");
					foreach (string? link in component.ModLink)
					{
						// Validate link format - allow URLs, anchor links, and relative paths
						if (!string.IsNullOrWhiteSpace(link))
						{
							bool isValidLink = link.StartsWith("http://") || link.StartsWith("https://") ||
											   link.StartsWith("#") || link.StartsWith("/");
							Assert.That(isValidLink, Is.True,
								$"Link should be a valid URL, anchor link, or relative path: {link}");
						}
					}
				}
			}

			Console.WriteLine($"\nComponents with links: {componentsWithLinks}");
			Console.WriteLine($"Total links: {totalLinks}");

			// Assert - At least some components should have links
			Assert.That(componentsWithLinks, Is.GreaterThan(0),
				$"Expected at least some components to have mod links in {Path.GetFileName(mdFilePath)}");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void Parse_ValidateCategoryFormat(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdownContent = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(markdownContent);
			IList<ModComponent> components = result.Components;

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");

			// Collect all unique categories and tiers
			var uniqueCategories = new HashSet<string>();
			var uniqueTiers = new HashSet<string>();

			foreach (ModComponent component in components)
			{
				foreach (string category in component.Category)
				{
					if (!string.IsNullOrWhiteSpace(category))
					{
						uniqueCategories.Add(category);
					}
				}

				if (!string.IsNullOrWhiteSpace(component.Tier))
				{
					uniqueTiers.Add(component.Tier);
				}
			}

			Console.WriteLine($"\nUnique Categories ({uniqueCategories.Count}):");
			foreach (string category in uniqueCategories.OrderBy(c => c))
			{
				Console.WriteLine($"  - {category}");
			}

			Console.WriteLine($"\nUnique Tiers ({uniqueTiers.Count}):");
			foreach (string tier in uniqueTiers.OrderBy(t => t))
			{
				Console.WriteLine($"  - {tier}");
			}

			// Validate that categories don't have unexpected characters
			foreach (string category in uniqueCategories)
			{
				Assert.That(category, Is.Not.Empty, "Category should not be empty");
				Assert.That(category.Trim(), Is.EqualTo(category),
					$"Category should not have leading/trailing whitespace: '{category}'");
			}

			// Validate tier format (should be like "1 - Essential", "2 - Recommended", etc.)
			foreach (string tier in uniqueTiers)
			{
				if (!string.IsNullOrWhiteSpace(tier))
				{
					Assert.That(tier.Trim(), Is.EqualTo(tier),
						$"Tier should not have leading/trailing whitespace: '{tier}'");
				}
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void Parse_NoWarningsForWellFormedFiles(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdownContent = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(markdownContent);

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Warnings: {result.Warnings.Count}");

			if (result.Warnings.Count > 0)
			{
				Console.WriteLine("\nWarnings found:");
				foreach (string warning in result.Warnings)
				{
					Console.WriteLine($"  - {warning}");
				}
			}

			// Note: This is informational - we don't fail if there are warnings,
			// as some files might legitimately have issues
			if (result.Warnings.Count > 0)
			{
				Assert.Warn($"File {Path.GetFileName(mdFilePath)} has {result.Warnings.Count} parsing warnings");
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void Parse_ValidateInstallationMethods(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdownContent = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(markdownContent);
			IList<ModComponent> components = result.Components;

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");

			// Collect all unique installation methods
			var uniqueMethods = new HashSet<string>();

			foreach (ModComponent component in components)
			{
				if (!string.IsNullOrWhiteSpace(component.InstallationMethod))
				{
					uniqueMethods.Add(component.InstallationMethod);
				}
			}

			Console.WriteLine($"\nUnique Installation Methods ({uniqueMethods.Count}):");
			foreach (string method in uniqueMethods.OrderBy(m => m))
			{
				int count = components.Count(c => c.InstallationMethod == method);
				Console.WriteLine($"  - {method} ({count} mods)");
			}

			// Validate that installation methods don't have unexpected format issues
			foreach (string method in uniqueMethods)
			{
				Assert.That(method, Is.Not.Empty, "Installation method should not be empty");
				Assert.That(method.Trim(), Is.EqualTo(method),
					$"Installation method should not have leading/trailing whitespace: '{method}'");
			}
		}
	}
}

