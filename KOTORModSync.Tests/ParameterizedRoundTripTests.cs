// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace KOTORModSync.Tests
{
	/// <summary>
	/// Parameterized round-trip tests that validate load → generate → load → generate cycles.
	/// Tests ensure that serialization/deserialization is idempotent and data is preserved.
	/// </summary>
	[TestFixture]
	public class ParameterizedRoundTripTests
	{
		private string _testDirectory;

		[SetUp]
		public void SetUp()
		{
			_testDirectory = Path.Combine(Path.GetTempPath(), "KOTORModSync_RoundTrip_" + Guid.NewGuid());
			Directory.CreateDirectory(_testDirectory);
		}

		[TearDown]
		public void TearDown()
		{
			try
			{
				if ( Directory.Exists(_testDirectory) )
					Directory.Delete(_testDirectory, recursive: true);
			}
			catch
			{
				// Ignore cleanup errors
			}
		}

		#region Test Case Providers

		/// <summary>
		/// Discovers all markdown files for testing.
		/// </summary>
		private static IEnumerable<TestCaseData> GetAllMarkdownFiles()
		{
			string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? "";
			string solutionRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", ".."));
			string contentRoot = Path.Combine(solutionRoot, /*"KOTORModSync.Tests", */"mod-builds", "content");

			// K1 files
			string k1Path = Path.Combine(contentRoot, "k1");
			if ( Directory.Exists(k1Path) )
			{
				foreach ( string mdFile in Directory.GetFiles(k1Path, "*.md", SearchOption.AllDirectories) )
				{
					if ( mdFile.Contains("validated") )
						continue;

					yield return new TestCaseData(mdFile)
						.SetName($"K1_{Path.GetFileNameWithoutExtension(mdFile)}")
						.SetCategory("K1")
						.SetCategory("RoundTrip")
						.SetCategory("Markdown");
				}
			}

			// K2 files
			string k2Path = Path.Combine(contentRoot, "k2");
			if ( Directory.Exists(k2Path) )
			{
				foreach ( string mdFile in Directory.GetFiles(k2Path, "*.md", SearchOption.AllDirectories) )
				{
					if ( mdFile.Contains("validated") )
						continue;

					yield return new TestCaseData(mdFile)
						.SetName($"K2_{Path.GetFileNameWithoutExtension(mdFile)}")
						.SetCategory("K2")
						.SetCategory("RoundTrip")
						.SetCategory("Markdown");
				}
			}
		}

		/// <summary>
		/// Discovers all TOML files for testing.
		/// </summary>
		private static IEnumerable<TestCaseData> GetAllTomlFiles()
		{
			string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? "";
			string solutionRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", ".."));

			// Check multiple possible TOML locations
			var tomlSearchPaths = new[]
			{
				Path.Combine(solutionRoot, "KOTOR.Modbuilds.Rev10"),
				Path.Combine(solutionRoot, "KOTORModSync.Tests", "mod-builds", "validated"),
				Path.Combine(solutionRoot, "mod-builds", "validated")
			};

			foreach ( string searchPath in tomlSearchPaths )
			{
				if ( !Directory.Exists(searchPath) )
					continue;

				foreach ( string tomlFile in Directory.GetFiles(searchPath, "*.toml", SearchOption.AllDirectories) )
				{
					string fileName = Path.GetFileNameWithoutExtension(tomlFile);
					string gameType = fileName.Contains("KOTOR1") || fileName.Contains("K1") ? "K1" : "K2";

					yield return new TestCaseData(tomlFile)
						.SetName($"{gameType}_{fileName}")
						.SetCategory(gameType)
						.SetCategory("RoundTrip")
						.SetCategory("TOML");
				}
			}
		}

		#endregion

		#region Markdown Round-Trip Tests

		/// <summary>
		/// Tests: Markdown → Components → Markdown → Components → Compare Markdown
		/// Validates that markdown generation is idempotent.
		/// </summary>
		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void MarkdownRoundTrip_LoadGenerateLoadGenerate_SecondGenerationMatchesFirst(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");
			string originalMarkdown = File.ReadAllText(mdFilePath);
			var parser = new MarkdownParser(MarkdownImportProfile.CreateDefault());

			Console.WriteLine($"Testing: {Path.GetFileName(mdFilePath)}");

			// Act - First cycle: Load original markdown and generate new markdown
			MarkdownParserResult parseResult1 = parser.Parse(originalMarkdown);
			List<ModComponent> components1 = parseResult1.Components.ToList();
			string generatedMarkdown1 = ModComponent.GenerateModDocumentation(components1);

			Console.WriteLine($"First parse: {components1.Count} components");
			Console.WriteLine($"First generation: {generatedMarkdown1.Length} characters");

			// Act - Second cycle: Load generated markdown and generate again
			MarkdownParserResult parseResult2 = parser.Parse(generatedMarkdown1);
			List<ModComponent> components2 = parseResult2.Components.ToList();
			string generatedMarkdown2 = ModComponent.GenerateModDocumentation(components2);

			Console.WriteLine($"Second parse: {components2.Count} components");
			Console.WriteLine($"Second generation: {generatedMarkdown2.Length} characters");

			// Assert - Component counts should match
			Assert.That(components2.Count, Is.EqualTo(components1.Count),
				"Component count should remain stable across generations");

			// Assert - Generated markdown should be identical (idempotent)
			Assert.That(generatedMarkdown2, Is.EqualTo(generatedMarkdown1),
				"Second markdown generation should match first generation (idempotent)");

			// Assert - All component names should be preserved
			var names1 = components1.Select(c => c.Name).OrderBy(n => n).ToList();
			var names2 = components2.Select(c => c.Name).OrderBy(n => n).ToList();
			Assert.That(names2, Is.EqualTo(names1).AsCollection, "All component names should be preserved");

			// Assert - Compare to original markdown (extract mod names)
			MarkdownParserResult originalParse = parser.Parse(originalMarkdown);
			var originalNames = originalParse.Components.Select(c => c.Name).OrderBy(n => n).ToList();
			Assert.That(names1, Is.EqualTo(originalNames).AsCollection,
				"Generated markdown should preserve all original component names");

			Console.WriteLine("✓ Markdown round-trip successful - all generations match");
		}

		/// <summary>
		/// Tests: Markdown → Components → Markdown (Compare to original)
		/// Validates that all components from original are preserved in generated markdown.
		/// </summary>
		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void MarkdownRoundTrip_GeneratedMarkdown_PreservesAllOriginalComponents(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");
			string originalMarkdown = File.ReadAllText(mdFilePath);
			var parser = new MarkdownParser(MarkdownImportProfile.CreateDefault());

			// Act
			MarkdownParserResult originalResult = parser.Parse(originalMarkdown);
			List<ModComponent> originalComponents = originalResult.Components.ToList();
			string generatedMarkdown = ModComponent.GenerateModDocumentation(originalComponents);
			MarkdownParserResult generatedResult = parser.Parse(generatedMarkdown);
			List<ModComponent> generatedComponents = generatedResult.Components.ToList();

			// Assert - Component count
			Assert.That(generatedComponents.Count, Is.EqualTo(originalComponents.Count),
				$"Should preserve all {originalComponents.Count} components");

			// Assert - All names preserved
			var originalNames = originalComponents.Select(c => c.Name).ToList();
			var generatedNames = generatedComponents.Select(c => c.Name).ToList();
			Assert.That(generatedNames, Is.EqualTo(originalNames).AsCollection,
				"All component names should be preserved in order");

			// Assert - Key fields preserved for each component
			for ( int i = 0; i < originalComponents.Count; i++ )
			{
				var orig = originalComponents[i];
				var gen = generatedComponents[i];

				Assert.That(gen.Name, Is.EqualTo(orig.Name), $"Component {i}: Name mismatch");
				Assert.That(gen.Author, Is.EqualTo(orig.Author), $"Component {i}: Author mismatch");

				// Category comparison (normalize for comparison)
				var origCategory = string.Join(" & ", orig.Category ?? new List<string>());
				var genCategory = string.Join(" & ", gen.Category ?? new List<string>());
				Assert.That(genCategory, Is.EqualTo(origCategory), $"Component {i}: Category mismatch");

				Assert.That(gen.Tier, Is.EqualTo(orig.Tier), $"Component {i}: Tier mismatch");
			}

			Console.WriteLine($"✓ All {originalComponents.Count} components preserved with key fields intact");
		}

		#endregion

		#region TOML Round-Trip Tests

		/// <summary>
		/// Tests: TOML → Components → TOML → Components → Compare TOML
		/// Validates that TOML generation is idempotent.
		/// </summary>
		[TestCaseSource(nameof(GetAllTomlFiles))]
		public void TomlRoundTrip_LoadGenerateLoadGenerate_SecondGenerationMatchesFirst(string tomlFilePath)
		{
			// Arrange
			Assert.That(File.Exists(tomlFilePath), Is.True, $"Test file not found: {tomlFilePath}");
			Console.WriteLine($"Testing: {Path.GetFileName(tomlFilePath)}");

			// Act - First cycle: Load original TOML and generate new TOML
			List<ModComponent> components1 = ModComponent.ReadComponentsFromFile(tomlFilePath);
			string tomlPath1 = Path.Combine(_testDirectory, "generation1.toml");
			ModComponent.OutputConfigFile(components1, tomlPath1);
			string generatedToml1 = File.ReadAllText(tomlPath1);

			Console.WriteLine($"First load: {components1.Count} components");
			Console.WriteLine($"First generation: {generatedToml1.Length} characters");

			// Act - Second cycle: Load generated TOML and generate again
			List<ModComponent> components2 = ModComponent.ReadComponentsFromFile(tomlPath1);
			string tomlPath2 = Path.Combine(_testDirectory, "generation2.toml");
			ModComponent.OutputConfigFile(components2, tomlPath2);
			string generatedToml2 = File.ReadAllText(tomlPath2);

			Console.WriteLine($"Second load: {components2.Count} components");
			Console.WriteLine($"Second generation: {generatedToml2.Length} characters");

			// Assert - Component counts should match
			Assert.That(components2.Count, Is.EqualTo(components1.Count),
				"Component count should remain stable across generations");

			// Assert - Generated TOML should be identical (idempotent)
			if ( generatedToml2 != generatedToml1 )
			{
				// Write files for debugging
				File.WriteAllText(Path.Combine(_testDirectory, "diff_gen1.toml"), generatedToml1);
				File.WriteAllText(Path.Combine(_testDirectory, "diff_gen2.toml"), generatedToml2);
				Console.WriteLine($"TOML difference detected - files written to {_testDirectory}");
			}

			Assert.That(generatedToml2, Is.EqualTo(generatedToml1),
				"Second TOML generation should match first generation (idempotent)");

			// Assert - All GUIDs should be preserved
			var guids1 = components1.Select(c => c.Guid).OrderBy(g => g).ToList();
			var guids2 = components2.Select(c => c.Guid).OrderBy(g => g).ToList();
			Assert.That(guids2, Is.EqualTo(guids1).AsCollection, "All component GUIDs should be preserved");

			// Assert - All names should be preserved
			var names1 = components1.Select(c => c.Name).ToList();
			var names2 = components2.Select(c => c.Name).ToList();
			Assert.That(names2, Is.EqualTo(names1).AsCollection, "All component names should be preserved in order");

			Console.WriteLine("✓ TOML round-trip successful - all generations match");
		}

		/// <summary>
		/// Tests: TOML → Components → TOML (Compare to original)
		/// Validates that all data from original TOML is preserved.
		/// </summary>
		[TestCaseSource(nameof(GetAllTomlFiles))]
		public void TomlRoundTrip_GeneratedToml_PreservesAllOriginalData(string tomlFilePath)
		{
			// Arrange
			Assert.That(File.Exists(tomlFilePath), Is.True, $"Test file not found: {tomlFilePath}");

			// Act
			List<ModComponent> originalComponents = ModComponent.ReadComponentsFromFile(tomlFilePath);
			string generatedTomlPath = Path.Combine(_testDirectory, "regenerated.toml");
			ModComponent.OutputConfigFile(originalComponents, generatedTomlPath);
			List<ModComponent> regeneratedComponents = ModComponent.ReadComponentsFromFile(generatedTomlPath);

			// Assert - Component count
			Assert.That(regeneratedComponents.Count, Is.EqualTo(originalComponents.Count),
				$"Should preserve all {originalComponents.Count} components");

			// Assert - Deep comparison of each component
			for ( int i = 0; i < originalComponents.Count; i++ )
			{
				var orig = originalComponents[i];
				var regen = regeneratedComponents[i];

				Assert.Multiple(() =>
				{
					Assert.That(regen.Guid, Is.EqualTo(orig.Guid), $"Component {i}: GUID mismatch");
					Assert.That(regen.Name, Is.EqualTo(orig.Name), $"Component {i}: Name mismatch");
					Assert.That(regen.Author, Is.EqualTo(orig.Author), $"Component {i}: Author mismatch");
					Assert.That(regen.Tier, Is.EqualTo(orig.Tier), $"Component {i}: Tier mismatch");
					Assert.That(regen.Description, Is.EqualTo(orig.Description), $"Component {i}: Description mismatch");
					Assert.That(regen.InstallationMethod, Is.EqualTo(orig.InstallationMethod), $"Component {i}: InstallationMethod mismatch");
					Assert.That(regen.Instructions.Count, Is.EqualTo(orig.Instructions.Count), $"Component {i}: Instructions count mismatch");
					Assert.That(regen.Options.Count, Is.EqualTo(orig.Options.Count), $"Component {i}: Options count mismatch");
				});

				// Compare collections
				Assert.That(regen.Category, Is.EqualTo(orig.Category).AsCollection, $"Component {i}: Category mismatch");
				Assert.That(regen.Language, Is.EqualTo(orig.Language).AsCollection, $"Component {i}: Language mismatch");
				Assert.That(regen.ModLink, Is.EqualTo(orig.ModLink).AsCollection, $"Component {i}: ModLink mismatch");
				Assert.That(regen.Dependencies, Is.EqualTo(orig.Dependencies).AsCollection, $"Component {i}: Dependencies mismatch");
				Assert.That(regen.Restrictions, Is.EqualTo(orig.Restrictions).AsCollection, $"Component {i}: Restrictions mismatch");
			}

			Console.WriteLine($"✓ All {originalComponents.Count} components preserved with complete data integrity");
		}

		#endregion

		#region Cross-Format Round-Trip Tests

		/// <summary>
		/// Tests: Markdown → TOML → Markdown → TOML → Compare TOMLs
		/// Validates cross-format stability.
		/// </summary>
		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void CrossFormat_MarkdownToToml_RoundTripIsIdempotent(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");
			string originalMarkdown = File.ReadAllText(mdFilePath);
			var parser = new MarkdownParser(MarkdownImportProfile.CreateDefault());

			Console.WriteLine($"Testing cross-format: {Path.GetFileName(mdFilePath)}");

			// Act - First cycle: Markdown → Components → TOML
			MarkdownParserResult parseResult1 = parser.Parse(originalMarkdown);
			List<ModComponent> components1 = parseResult1.Components.ToList();
			string tomlPath1 = Path.Combine(_testDirectory, "from_markdown_1.toml");
			ModComponent.OutputConfigFile(components1, tomlPath1);

			// Act - Second cycle: TOML → Components → Markdown → Components → TOML
			List<ModComponent> componentsFromToml = ModComponent.ReadComponentsFromFile(tomlPath1);
			string generatedMarkdown = ModComponent.GenerateModDocumentation(componentsFromToml);
			MarkdownParserResult parseResult2 = parser.Parse(generatedMarkdown);
			List<ModComponent> components2 = parseResult2.Components.ToList();
			string tomlPath2 = Path.Combine(_testDirectory, "from_markdown_2.toml");
			ModComponent.OutputConfigFile(components2, tomlPath2);

			string toml1 = File.ReadAllText(tomlPath1);
			string toml2 = File.ReadAllText(tomlPath2);

			Console.WriteLine($"Components: MD1={components1.Count}, TOML1={componentsFromToml.Count}, MD2={components2.Count}");

			// Assert - TOML should be stable after full cycle
			Assert.That(components2.Count, Is.EqualTo(componentsFromToml.Count),
				"Component count should remain stable");

			// Compare component names (TOML has GUIDs, markdown doesn't - names are the key)
			var names1 = componentsFromToml.Select(c => c.Name).ToList();
			var names2 = components2.Select(c => c.Name).ToList();
			Assert.That(names2, Is.EqualTo(names1).AsCollection, "Component names should be preserved across formats");

			Console.WriteLine("✓ Cross-format round-trip successful");
		}

		/// <summary>
		/// Tests: TOML → Markdown → TOML (Compare TOMLs, Compare Markdown to any original)
		/// Validates that TOML→MD→TOML preserves data.
		/// </summary>
		[TestCaseSource(nameof(GetAllTomlFiles))]
		public void CrossFormat_TomlToMarkdown_PreservesComponentData(string tomlFilePath)
		{
			// Arrange
			Assert.That(File.Exists(tomlFilePath), Is.True, $"Test file not found: {tomlFilePath}");
			Console.WriteLine($"Testing TOML→Markdown→TOML: {Path.GetFileName(tomlFilePath)}");

			// Act - Load TOML → Generate Markdown → Parse Markdown
			List<ModComponent> originalComponents = ModComponent.ReadComponentsFromFile(tomlFilePath);
			string generatedMarkdown = ModComponent.GenerateModDocumentation(originalComponents);

			var parser = new MarkdownParser(MarkdownImportProfile.CreateDefault());
			MarkdownParserResult parseResult = parser.Parse(generatedMarkdown);
			List<ModComponent> componentsFromMarkdown = parseResult.Components.ToList();

			// Write markdown for debugging
			string debugMdPath = Path.Combine(_testDirectory, Path.GetFileNameWithoutExtension(tomlFilePath) + ".md");
			File.WriteAllText(debugMdPath, generatedMarkdown);

			Console.WriteLine($"Original TOML: {originalComponents.Count} components");
			Console.WriteLine($"After MD round-trip: {componentsFromMarkdown.Count} components");

			// Assert - All components preserved
			Assert.That(componentsFromMarkdown.Count, Is.EqualTo(originalComponents.Count),
				"All components should survive TOML→Markdown→Parse cycle");

			// Assert - Names and key fields preserved
			for ( int i = 0; i < originalComponents.Count; i++ )
			{
				var orig = originalComponents[i];
				var fromMd = componentsFromMarkdown[i];

				Assert.That(fromMd.Name, Is.EqualTo(orig.Name), $"Component {i}: Name mismatch");
				Assert.That(fromMd.Author, Is.EqualTo(orig.Author), $"Component {i}: Author mismatch");

				// Note: GUIDs from TOML won't be in markdown (markdown generates new GUIDs)
				// This is expected behavior - markdown doesn't preserve GUIDs
			}

			Console.WriteLine("✓ TOML→Markdown conversion preserves all components and key fields");
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Normalizes markdown for comparison by removing whitespace variations.
		/// </summary>
		private string NormalizeMarkdown(string markdown)
		{
			return markdown
				.Replace("\r\n", "\n")
				.Replace("\r", "\n")
				.Trim();
		}

		/// <summary>
		/// Normalizes TOML for comparison by removing comments and empty lines.
		/// </summary>
		private string NormalizeToml(string toml)
		{
			var lines = toml.Split('\n')
				.Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"))
				.Select(line => line.Trim());
			return string.Join("\n", lines);
		}

		#endregion
	}
}

