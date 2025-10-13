// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
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
	public class ParameterizedModSyncMetadataTests
	{
		/// <summary>
		/// Dynamically generates test cases for all .md files in k1 and k2 directories.
		/// </summary>
		private static IEnumerable<TestCaseData> GetAllMarkdownFiles()
		{
			// Get the solution root by navigating up from the executing assembly location
			// bin/Debug/net8.0 -> bin -> Debug -> Tests -> Solution Root
			string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? "";
			string solutionRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", ".."));
			string contentRoot = Path.Combine(solutionRoot, /*"KOTORModSync.Tests", */"mod-builds", "content");

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
						.SetCategory("ModSyncMetadata");
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
						.SetCategory("ModSyncMetadata");
				}
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void ParseModSyncMetadata_AllComponentsHaveValidGuids(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(markdown);

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Components parsed: {result.Components.Count}");

			// Assert - All components with ModSync metadata should have valid GUIDs
			int componentsWithMetadata = 0;
			int componentsWithValidGuids = 0;

			foreach (var component in result.Components)
			{
				// Check if component has ModSync metadata (instructions or options)
				if (component.Instructions.Count > 0 || component.Options.Count > 0)
				{
					componentsWithMetadata++;

					if (component.Guid != Guid.Empty)
					{
						componentsWithValidGuids++;
						Console.WriteLine($"  {component.Name}: GUID = {component.Guid}");
					}
					else
					{
						Console.WriteLine($"  {component.Name}: Missing or empty GUID!");
					}
				}
			}

			Console.WriteLine($"Components with metadata: {componentsWithMetadata}");
			Console.WriteLine($"Components with valid GUIDs: {componentsWithValidGuids}");

			// Note: Not all components will have metadata, so we only check those that do
			if (componentsWithMetadata > 0)
			{
				Assert.That(componentsWithValidGuids, Is.EqualTo(componentsWithMetadata),
					"All components with ModSync metadata should have valid GUIDs");
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void ParseModSyncMetadata_InstructionsHaveValidGuids(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(markdown);

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");

			// Assert - All instructions should have valid GUIDs
			int totalInstructions = 0;
			int invalidGuids = 0;

			foreach (var component in result.Components)
			{
				foreach (var instruction in component.Instructions)
				{
					totalInstructions++;
					if (instruction.Guid == Guid.Empty)
					{
						invalidGuids++;
						Console.WriteLine($"  {component.Name}: Instruction with action {instruction.Action} has empty GUID");
					}
				}

				// Check option instructions
				foreach (var option in component.Options)
				{
					foreach (var instruction in option.Instructions)
					{
						totalInstructions++;
						if (instruction.Guid == Guid.Empty)
						{
							invalidGuids++;
							Console.WriteLine($"  {component.Name} -> {option.Name}: Instruction with action {instruction.Action} has empty GUID");
						}
					}
				}
			}

			Console.WriteLine($"Total instructions: {totalInstructions}");
			Console.WriteLine($"Instructions with invalid GUIDs: {invalidGuids}");

			if (totalInstructions > 0)
			{
				Assert.That(invalidGuids, Is.EqualTo(0), "All instructions should have valid GUIDs");
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void ParseModSyncMetadata_OptionsHaveValidGuids(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(markdown);

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");

			// Assert - All options should have valid GUIDs
			int totalOptions = 0;
			int invalidGuids = 0;

			foreach (var component in result.Components)
			{
				foreach (var option in component.Options)
				{
					totalOptions++;
					if (option.Guid == Guid.Empty)
					{
						invalidGuids++;
						Console.WriteLine($"  {component.Name} -> {option.Name}: Option has empty GUID");
					}
				}
			}

			Console.WriteLine($"Total options: {totalOptions}");
			Console.WriteLine($"Options with invalid GUIDs: {invalidGuids}");

			if (totalOptions > 0)
			{
				Assert.That(invalidGuids, Is.EqualTo(0), "All options should have valid GUIDs");
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void RoundTrip_ModSyncMetadata_PreservesAllData(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string originalMarkdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act - First parse
			MarkdownParserResult firstParse = parser.Parse(originalMarkdown);

			// Filter to only components with metadata
			var componentsWithMetadata = firstParse.Components
				.Where(c => c.Instructions.Count > 0 || c.Options.Count > 0)
				.ToList();

			if (componentsWithMetadata.Count == 0)
			{
				Assert.Pass($"No components with ModSync metadata in {Path.GetFileName(mdFilePath)}");
				return;
			}

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Components with metadata: {componentsWithMetadata.Count}");

			// Act - Generate
			string generated = ModComponent.GenerateModDocumentation(componentsWithMetadata);

			// Act - Second parse
			MarkdownParserResult secondParse = parser.Parse(generated);

			// Assert - Component count preserved
			Assert.That(secondParse.Components, Has.Count.EqualTo(componentsWithMetadata.Count),
				"Component count should be preserved");

			// Assert - Detailed comparison
			for (int i = 0; i < componentsWithMetadata.Count; i++)
			{
				var first = componentsWithMetadata[i];
				var second = secondParse.Components[i];

				Console.WriteLine($"\nComparing component: {first.Name}");

				Assert.Multiple(() =>
				{
					Assert.That(second.Guid, Is.EqualTo(first.Guid), $"{first.Name}: GUID preserved");
					Assert.That(second.Name, Is.EqualTo(first.Name), $"{first.Name}: Name preserved");
					Assert.That(second.Instructions.Count, Is.EqualTo(first.Instructions.Count),
						$"{first.Name}: Instruction count preserved");
					Assert.That(second.Options.Count, Is.EqualTo(first.Options.Count),
						$"{first.Name}: Option count preserved");
				});

				// Check instructions
				for (int j = 0; j < first.Instructions.Count; j++)
				{
					Assert.Multiple(() =>
					{
						Assert.That(second.Instructions[j].Guid, Is.EqualTo(first.Instructions[j].Guid),
							$"{first.Name}: Instruction {j} GUID preserved");
						Assert.That(second.Instructions[j].Action, Is.EqualTo(first.Instructions[j].Action),
							$"{first.Name}: Instruction {j} Action preserved");
					});
				}

				// Check options
				for (int j = 0; j < first.Options.Count; j++)
				{
					Assert.Multiple(() =>
					{
						Assert.That(second.Options[j].Guid, Is.EqualTo(first.Options[j].Guid),
							$"{first.Name}: Option {j} GUID preserved");
						Assert.That(second.Options[j].Name, Is.EqualTo(first.Options[j].Name),
							$"{first.Name}: Option {j} Name preserved");
					});
				}
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void ParseModSyncMetadata_InstructionActionsAreValid(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(markdown);

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");

			// Assert - All instruction actions should be valid enum values
			var validActions = Enum.GetValues(typeof(Instruction.ActionType)).Cast<Instruction.ActionType>().ToList();
			var actionCounts = new Dictionary<Instruction.ActionType, int>();

			foreach (var component in result.Components)
			{
				foreach (var instruction in component.Instructions)
				{
					Assert.That(validActions, Contains.Item(instruction.Action),
						$"{component.Name}: Instruction has invalid action {instruction.Action}");

					if (!actionCounts.ContainsKey(instruction.Action))
						actionCounts[instruction.Action] = 0;
					actionCounts[instruction.Action]++;
				}

				foreach (var option in component.Options)
				{
					foreach (var instruction in option.Instructions)
					{
						Assert.That(validActions, Contains.Item(instruction.Action),
							$"{component.Name} -> {option.Name}: Instruction has invalid action {instruction.Action}");

						if (!actionCounts.ContainsKey(instruction.Action))
							actionCounts[instruction.Action] = 0;
						actionCounts[instruction.Action]++;
					}
				}
			}

			if (actionCounts.Count > 0)
			{
				Console.WriteLine("\nAction distribution:");
				foreach (var kvp in actionCounts.OrderByDescending(x => x.Value))
				{
					Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
				}
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void ParseModSyncMetadata_ComponentsWithInstructionsHaveValidSourceDestination(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult result = parser.Parse(markdown);

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");

			// Assert - Instructions that need source/destination should have them
			foreach (var component in result.Components)
			{
				foreach (var instruction in component.Instructions)
				{
					// Check based on action type
					switch (instruction.Action)
					{
						case Instruction.ActionType.Extract:
						case Instruction.ActionType.Move:
						case Instruction.ActionType.Copy:
						case Instruction.ActionType.Rename:
							Assert.That(instruction.Source, Is.Not.Null.And.Not.Empty,
								$"{component.Name}: {instruction.Action} instruction should have Source");
							break;

						case Instruction.ActionType.Choose:
							Assert.That(instruction.Source, Is.Not.Null.And.Not.Empty,
								$"{component.Name}: Choose instruction should have Source (option GUIDs)");
							break;
					}

					// Log for verification
					if (instruction.Source?.Count > 0)
					{
						Console.WriteLine($"  {component.Name}: {instruction.Action} -> Source: {string.Join(", ", instruction.Source.Take(2))}");
					}
				}
			}
		}
	}
}

