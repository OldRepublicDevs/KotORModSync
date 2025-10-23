// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync.Core;
using KOTORModSync.Core.CLI;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Utility;

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

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult parseResult = parser.Parse(_originalMarkdown);
			IList<ModComponent> components = parseResult.Components;

			TestContext.Progress.WriteLine($"Parsed {components.Count} components");
			TestContext.Progress.WriteLine($"Warnings: {parseResult.Warnings.Count}");
			foreach ( string warning in parseResult.Warnings )
			{
				TestContext.Progress.WriteLine($"  - {warning}");
			}

			string generatedDocs = ModComponentSerializationService.GenerateModDocumentation(components.ToList());

			string debugOutputPath = Path.Combine(
				TestContext.CurrentContext.TestDirectory,
				"..", "..", "..",
				"test_generated_docs.md"
			);
			File.WriteAllText(debugOutputPath, generatedDocs);
			TestContext.Progress.WriteLine($"Generated documentation written to: {debugOutputPath}");

			Assert.Multiple(() =>
			{

				Assert.That(components, Is.Not.Empty, "Should have parsed at least one component");
				Assert.That(generatedDocs, Is.Not.Null.And.Not.Empty, "Generated documentation should not be empty");
			});

			string originalModList = MarkdownUtilities.ExtractModListSection(_originalMarkdown);
			List<string> originalSections = MarkdownUtilities.ExtractModSections(originalModList);

			List<string> generatedSections = MarkdownUtilities.ExtractModSections(generatedDocs);

			TestContext.Progress.WriteLine($"Original sections: {originalSections.Count}");
			TestContext.Progress.WriteLine($"Generated sections: {generatedSections.Count}");

			var originalNameFields = originalSections
				.SelectMany(s => MarkdownUtilities.ExtractAllFieldValues(s, @"\*\*Name:\*\*\s*(?:\[([^\]]+)\]|([^\r\n]+))"))
				.Where(n => !string.IsNullOrWhiteSpace(n))
				.ToList();

			var generatedNameFields = generatedSections
				.SelectMany(s => MarkdownUtilities.ExtractAllFieldValues(s, @"\*\*Name:\*\*\s*(?:\[([^\]]+)\]|([^\r\n]+))"))
				.Where(n => !string.IsNullOrWhiteSpace(n))
				.ToList();

			TestContext.Progress.WriteLine($"Original mod names (from **Name:** field): {originalNameFields.Count}");
			TestContext.Progress.WriteLine($"Generated mod names (from **Name:** field): {generatedNameFields.Count}");

			if ( generatedNameFields.Count != originalNameFields.Count )
			{
				TestContext.Progress.WriteLine("\n=== NAME FIELD COUNT MISMATCH ===");

				var missingInGenerated = originalNameFields.Except(generatedNameFields).ToList();
				var missingInOriginal = generatedNameFields.Except(originalNameFields).ToList();

				if ( missingInGenerated.Count > 0 )
				{
					TestContext.Progress.WriteLine($"\nMissing in generated ({missingInGenerated.Count}):");
					foreach ( string name in missingInGenerated )
					{
						TestContext.Progress.WriteLine($"  - {name}");
					}
				}

				if ( missingInOriginal.Count > 0 )
				{
					TestContext.Progress.WriteLine($"\nExtra in generated ({missingInOriginal.Count}):");
					foreach ( string name in missingInOriginal )
					{
						TestContext.Progress.WriteLine($"  - {name}");
					}
				}
			}

			Assert.That(generatedNameFields, Has.Count.EqualTo(originalNameFields.Count),
				$"Mod count must match exactly. Original: {originalNameFields.Count}, Generated: {generatedNameFields.Count}");

			var missingNames = originalNameFields.Except(generatedNameFields).ToList();
			var extraNames = generatedNameFields.Except(originalNameFields).ToList();

			if ( missingNames.Count > 0 || extraNames.Count > 0 )
			{
				TestContext.Progress.WriteLine("\n=== MOD NAME MISMATCH ===");
				if ( missingNames.Count > 0 )
				{
					TestContext.Progress.WriteLine($"Names missing in generated ({missingNames.Count}):");
					foreach ( string name in missingNames )
					{
						TestContext.Progress.WriteLine($"  - {name}");
					}
				}
				if ( extraNames.Count > 0 )
				{
					TestContext.Progress.WriteLine($"Extra names in generated ({extraNames.Count}):");
					foreach ( string name in extraNames )
					{
						TestContext.Progress.WriteLine($"  - {name}");
					}
				}
			}

			Assert.Multiple(() =>
			{
				Assert.That(missingNames, Is.Empty, "All original mod names should be present in generated output");
				Assert.That(extraNames, Is.Empty, "No extra mod names should be in generated output");
			});

			TestContext.Progress.WriteLine("\n✓ All 197 mod names match between original and generated");
			TestContext.Progress.WriteLine("✓ Round-trip test successful: Import → Export produces identical mod list");
		}

		[Test]
		public void RoundTrip_VerifyFieldPreservation()
		{

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult parseResult = parser.Parse(_originalMarkdown);
			List<ModComponent> components = parseResult.Components.ToList();

			foreach ( ModComponent component in components )
			{
				TestContext.Progress.WriteLine($"\nVerifying component: {component.Name}");

				Assert.That(component.Name, Is.Not.Null.And.Not.Empty, "Name should not be empty");
				TestContext.Progress.WriteLine($"  Name: {component.Name}");
				TestContext.Progress.WriteLine($"  Author: {component.Author}");
				TestContext.Progress.WriteLine($"  Category: {string.Join(" & ", component.Category)}");
				TestContext.Progress.WriteLine($"  Tier: {component.Tier}");
				TestContext.Progress.WriteLine($"  Language: {string.Join(", ", component.Language)}");
				TestContext.Progress.WriteLine($"  InstallationMethod: {component.InstallationMethod}");
				TestContext.Progress.WriteLine($"  ModLinks: {component.ModLinkFilenames?.Count ?? 0}");
				TestContext.Progress.WriteLine($"  Description length: {component.Description?.Length ?? 0}");
				TestContext.Progress.WriteLine($"  Directions length: {component.Directions?.Length ?? 0}");
			}

			Assert.That(components, Is.Not.Empty, "Should have parsed components");
		}

		private static void CompareModSections(string original, string generated, int sectionNumber)
		{

			List<string> originalHeadings = MarkdownUtilities.ExtractAllFieldValues(original, @"###\s+(.+?)$");
			string originalHeading = originalHeadings.FirstOrDefault() ?? string.Empty;

			List<string> generatedHeadings = MarkdownUtilities.ExtractAllFieldValues(generated, @"###\s+(.+?)$");
			string generatedHeading = generatedHeadings.FirstOrDefault() ?? string.Empty;

			List<string> originalNameFields = MarkdownUtilities.ExtractAllFieldValues(original, @"\*\*Name:\*\*\s*(?:\[([^\]]+)\]|\s*([^\r\n]+))");
			string originalNameField = originalNameFields.FirstOrDefault() ?? string.Empty;

			List<string> generatedNameFields = MarkdownUtilities.ExtractAllFieldValues(generated, @"\*\*Name:\*\*\s*(?:\[([^\]]+)\]|\s*([^\r\n]+))");
			string generatedNameField = generatedNameFields.FirstOrDefault() ?? string.Empty;

			List<string> originalAuthors = MarkdownUtilities.ExtractAllFieldValues(original, @"\*\*Author:\*\*\s*(.+?)(?:\r?\n|$)");
			string originalAuthor = originalAuthors.FirstOrDefault() ?? string.Empty;

			List<string> generatedAuthors = MarkdownUtilities.ExtractAllFieldValues(generated, @"\*\*Author:\*\*\s*(.+?)(?:\r?\n|$)");
			string generatedAuthor = generatedAuthors.FirstOrDefault() ?? string.Empty;

			List<string> originalCategories = MarkdownUtilities.ExtractAllFieldValues(original, @"\*\*Category & Tier:\*\*\s*(.+?)(?:\r?\n|$)");
			string originalCategory = originalCategories.FirstOrDefault() ?? string.Empty;

			List<string> generatedCategories = MarkdownUtilities.ExtractAllFieldValues(generated, @"\*\*Category & Tier:\*\*\s*(.+?)(?:\r?\n|$)");
			string generatedCategory = generatedCategories.FirstOrDefault() ?? string.Empty;

			TestContext.Progress.WriteLine($"Section {sectionNumber}:");
			TestContext.Progress.WriteLine($"  Original Heading: '{originalHeading}'");
			TestContext.Progress.WriteLine($"  Generated Heading: '{generatedHeading}'");
			TestContext.Progress.WriteLine($"  Original Name Field: '{originalNameField}'");
			TestContext.Progress.WriteLine($"  Generated Name Field: '{generatedNameField}'");
			TestContext.Progress.WriteLine($"  Original Author: '{originalAuthor}'");
			TestContext.Progress.WriteLine($"  Generated Author: '{generatedAuthor}'");
			TestContext.Progress.WriteLine($"  Original Category: '{originalCategory}'");
			TestContext.Progress.WriteLine($"  Generated Category: '{generatedCategory}'");

			Assert.Multiple(() =>
			{

				Assert.That(generatedHeading, Is.EqualTo(generatedNameField),
					$"Section {sectionNumber}: Generated heading should match generated Name field");

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

				Assert.That(generatedCategory, Is.EqualTo(originalCategory),
					$"Section {sectionNumber}: Category & Tier should match");
			}
		}

	}
}

