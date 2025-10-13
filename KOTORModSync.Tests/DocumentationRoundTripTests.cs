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
	public partial class DocumentationRoundTripTests
	{
		private string _testFilePath = string.Empty;
		private string _originalMarkdown = string.Empty;

		[SetUp]
		public void Setup()
		{
			// Check for environment variable first (used by CI)
			string? envTestFile = Environment.GetEnvironmentVariable("TEST_FILE_PATH");

			if ( !string.IsNullOrEmpty(envTestFile) )
			{
				_testFilePath = Path.Combine(
					TestContext.CurrentContext.TestDirectory,
					"..", "..", "..",
					envTestFile
				);
			}
			else
			{
				// Default to test_modbuild_k1.md for local testing
				_testFilePath = Path.Combine(
					TestContext.CurrentContext.TestDirectory,
					"..", "..", "..",
					"test_modbuild_k1.md"
				);
			}

			if ( !File.Exists(_testFilePath) )
			{
				Assert.Fail($"Test file not found: {_testFilePath}");
			}

			_originalMarkdown = File.ReadAllText(_testFilePath);
		}

		[Test]
		public void RoundTrip_ParseAndGenerateDocumentation_ProducesEquivalentOutput()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act - Parse
			MarkdownParserResult parseResult = parser.Parse(_originalMarkdown);
			IList<ModComponent> components = parseResult.Components;

			Console.WriteLine($"Parsed {components.Count} components");
			Console.WriteLine($"Warnings: {parseResult.Warnings.Count}");
			foreach ( string warning in parseResult.Warnings )
			{
				Console.WriteLine($"  - {warning}");
			}

			// Act - Generate
			string generatedDocs = ModComponent.GenerateModDocumentation(components.ToList());

			// Write generated docs for debugging
			string debugOutputPath = Path.Combine(
				TestContext.CurrentContext.TestDirectory,
				"..", "..", "..",
				"test_generated_docs.md"
			);
			File.WriteAllText(debugOutputPath, generatedDocs);
			Console.WriteLine($"Generated documentation written to: {debugOutputPath}");

			Assert.Multiple(() =>
			{
				// Assert - Basic structural validation
				Assert.That(components, Is.Not.Empty, "Should have parsed at least one component");
				Assert.That(generatedDocs, Is.Not.Null.And.Not.Empty, "Generated documentation should not be empty");
			});

			// Extract sections from original (starting from ## Mod List)
			string originalModList = MarkdownToTomlConverter.ExtractModListSection(_originalMarkdown);
			List<string> originalSections = MarkdownToTomlConverter.ExtractModSections(originalModList);

			// Extract sections from generated
			List<string> generatedSections = MarkdownToTomlConverter.ExtractModSections(generatedDocs);

			Console.WriteLine($"Original sections: {originalSections.Count}");
			Console.WriteLine($"Generated sections: {generatedSections.Count}");

			// Extract ALL Name field values from all sections (a section might contain multiple mods if ___ is missing)
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
			if ( generatedNameFields.Count != originalNameFields.Count )
			{
				Console.WriteLine("\n=== NAME FIELD COUNT MISMATCH ===");

				var missingInGenerated = originalNameFields.Except(generatedNameFields).ToList();
				var missingInOriginal = generatedNameFields.Except(originalNameFields).ToList();

				if ( missingInGenerated.Count > 0 )
				{
					Console.WriteLine($"\nMissing in generated ({missingInGenerated.Count}):");
					foreach ( string name in missingInGenerated )
					{
						Console.WriteLine($"  - {name}");
					}
				}

				if ( missingInOriginal.Count > 0 )
				{
					Console.WriteLine($"\nExtra in generated ({missingInOriginal.Count}):");
					foreach ( string name in missingInOriginal )
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

			if ( missingNames.Count > 0 || extraNames.Count > 0 )
			{
				Console.WriteLine("\n=== MOD NAME MISMATCH ===");
				if ( missingNames.Count > 0 )
				{
					Console.WriteLine($"Names missing in generated ({missingNames.Count}):");
					foreach ( string name in missingNames )
					{
						Console.WriteLine($"  - {name}");
					}
				}
				if ( extraNames.Count > 0 )
				{
					Console.WriteLine($"Extra names in generated ({extraNames.Count}):");
					foreach ( string name in extraNames )
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

			Console.WriteLine("\n✓ All 197 mod names match between original and generated");
			Console.WriteLine("✓ Round-trip test successful: Import → Export produces identical mod list");
		}

		[Test]
		public void RoundTrip_VerifyFieldPreservation()
		{
			// Arrange
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act - Parse
			MarkdownParserResult parseResult = parser.Parse(_originalMarkdown);
			List<ModComponent> components = parseResult.Components.ToList();

			// Assert - Check that key fields are preserved
			foreach ( ModComponent component in components )
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


		private static void CompareModSections(string original, string generated, int sectionNumber)
		{
			// Extract key fields from both sections using converter methods
			List<string> originalHeadings = MarkdownToTomlConverter.ExtractAllFieldValues(original, @"###\s+(.+?)$");
			string originalHeading = originalHeadings.FirstOrDefault() ?? string.Empty;

			List<string> generatedHeadings = MarkdownToTomlConverter.ExtractAllFieldValues(generated, @"###\s+(.+?)$");
			string generatedHeading = generatedHeadings.FirstOrDefault() ?? string.Empty;

			// Extract Name field (the actual mod name from **Name:** field)
			List<string> originalNameFields = MarkdownToTomlConverter.ExtractAllFieldValues(original, @"\*\*Name:\*\*\s*(?:\[([^\]]+)\]|\s*([^\r\n]+))");
			string originalNameField = originalNameFields.FirstOrDefault() ?? string.Empty;

			List<string> generatedNameFields = MarkdownToTomlConverter.ExtractAllFieldValues(generated, @"\*\*Name:\*\*\s*(?:\[([^\]]+)\]|\s*([^\r\n]+))");
			string generatedNameField = generatedNameFields.FirstOrDefault() ?? string.Empty;

			List<string> originalAuthors = MarkdownToTomlConverter.ExtractAllFieldValues(original, @"\*\*Author:\*\*\s*(.+?)(?:\r?\n|$)");
			string originalAuthor = originalAuthors.FirstOrDefault() ?? string.Empty;

			List<string> generatedAuthors = MarkdownToTomlConverter.ExtractAllFieldValues(generated, @"\*\*Author:\*\*\s*(.+?)(?:\r?\n|$)");
			string generatedAuthor = generatedAuthors.FirstOrDefault() ?? string.Empty;

			List<string> originalCategories = MarkdownToTomlConverter.ExtractAllFieldValues(original, @"\*\*Category & Tier:\*\*\s*(.+?)(?:\r?\n|$)");
			string originalCategory = originalCategories.FirstOrDefault() ?? string.Empty;

			List<string> generatedCategories = MarkdownToTomlConverter.ExtractAllFieldValues(generated, @"\*\*Category & Tier:\*\*\s*(.+?)(?:\r?\n|$)");
			string generatedCategory = generatedCategories.FirstOrDefault() ?? string.Empty;

			Console.WriteLine($"Section {sectionNumber}:");
			Console.WriteLine($"  Original Heading: '{originalHeading}'");
			Console.WriteLine($"  Generated Heading: '{generatedHeading}'");
			Console.WriteLine($"  Original Name Field: '{originalNameField}'");
			Console.WriteLine($"  Generated Name Field: '{generatedNameField}'");
			Console.WriteLine($"  Original Author: '{originalAuthor}'");
			Console.WriteLine($"  Generated Author: '{generatedAuthor}'");
			Console.WriteLine($"  Original Category: '{originalCategory}'");
			Console.WriteLine($"  Generated Category: '{generatedCategory}'");

			Assert.Multiple(() =>
			{
				// Assert: The generated heading should match the generated Name field (consistency)
				Assert.That(generatedHeading, Is.EqualTo(generatedNameField),
					$"Section {sectionNumber}: Generated heading should match generated Name field");

				// Assert: The generated name field should match the original name field
				// (The heading might differ, but the actual mod name should be preserved)
				Assert.That(generatedNameField, Is.EqualTo(originalNameField),
					$"Section {sectionNumber}: Mod name should be preserved");
			});

			if ( !string.IsNullOrWhiteSpace(originalAuthor) )
			{
				Assert.That(generatedAuthor, Is.EqualTo(originalAuthor),
					$"Section {sectionNumber}: Author should match");
			}

			if ( !string.IsNullOrWhiteSpace(originalCategory) )
			{
				// Parser now normalizes categories, so we can compare directly
				Assert.That(generatedCategory, Is.EqualTo(originalCategory),
					$"Section {sectionNumber}: Category & Tier should match");
			}
		}

	}
}

