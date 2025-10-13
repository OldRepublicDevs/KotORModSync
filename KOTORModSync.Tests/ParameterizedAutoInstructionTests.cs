// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Services.Download;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class ParameterizedAutoInstructionTests
	{
		private string? _testDirectory;

		[SetUp]
		public void SetUp()
		{
			_testDirectory = Path.Combine(Path.GetTempPath(), "KOTORModSync_ParamAutoInstruct_" + Guid.NewGuid());
			Directory.CreateDirectory(_testDirectory);

			var mainConfig = new MainConfig();
			mainConfig.sourcePath = new DirectoryInfo(_testDirectory);
			mainConfig.destinationPath = new DirectoryInfo(Path.Combine(_testDirectory, "KOTOR"));
			Directory.CreateDirectory(mainConfig.destinationPath.FullName);
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

			}
		}

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
						.SetCategory("AutoInstruction");
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
						.SetCategory("AutoInstruction");
				}
			}
		}

		private static IEnumerable<TestCaseData> GetAllDeadlystreamComponents()
		{

			string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? "";
			string solutionRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", ".."));
			string contentRoot = Path.Combine(solutionRoot, "mod-builds", "content");

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			string k1Path = Path.Combine(contentRoot, "k1");
			if ( Directory.Exists(k1Path) )
			{
				foreach ( string mdFile in Directory.GetFiles(k1Path, "*.md", SearchOption.AllDirectories) )
				{
					if ( mdFile.Contains("../" + Path.DirectorySeparatorChar + "../" + Path.DirectorySeparatorChar + "validated" + Path.DirectorySeparatorChar) ||
						mdFile.Contains("/validated/") )
						continue;

					string markdown = File.ReadAllText(mdFile);
					MarkdownParserResult parseResult = parser.Parse(markdown);

					var deadlyStreamComponents = parseResult.Components
						.Where(c => c.ModLink?.Any(link => !string.IsNullOrWhiteSpace(link) &&
							link.Contains("deadlystream.com", StringComparison.OrdinalIgnoreCase)) == true)
						.ToList();

					foreach ( var component in deadlyStreamComponents )
					{
						string fileName = Path.GetFileNameWithoutExtension(mdFile);
						string testName = $"K1_{fileName}_{component.Name}";
						yield return new TestCaseData(component, mdFile)
							.SetName(testName)
							.SetCategory("K1")
							.SetCategory("AutoInstruction")
							.SetCategory("Individual");
					}
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

					string markdown = File.ReadAllText(mdFile);
					MarkdownParserResult parseResult = parser.Parse(markdown);

					var deadlyStreamComponents = parseResult.Components
						.Where(c => c.ModLink?.Any(link => !string.IsNullOrWhiteSpace(link) &&
							link.Contains("deadlystream.com", StringComparison.OrdinalIgnoreCase)) == true)
						.ToList();

					foreach ( var component in deadlyStreamComponents )
					{
						string fileName = Path.GetFileNameWithoutExtension(mdFile);
						string testName = $"K2_{fileName}_{component.Name}";
						yield return new TestCaseData(component, mdFile)
							.SetName(testName)
							.SetCategory("K2")
							.SetCategory("AutoInstruction")
							.SetCategory("Individual");
					}
				}
			}
		}

		[TestCaseSource(nameof(GetAllDeadlystreamComponents))]
		[Ignore("Test requires mod-builds repository which has been removed from the project")]
		public void IndividualComponent_HasModSyncInstructions(ModComponent component, string mdFilePath)
		{

			Console.WriteLine($"========================================");
			Console.WriteLine($"Testing component: {component.Name}");
			Console.WriteLine($"From file: {Path.GetFileName(mdFilePath)}");

			var deadlyStreamLinks = component.ModLink
				.Where(link => !string.IsNullOrWhiteSpace(link) &&
					link.Contains("deadlystream.com", StringComparison.OrdinalIgnoreCase))
				.ToList();

			Console.WriteLine($"Deadlystream link(s): {deadlyStreamLinks.Count}");
			foreach ( var link in deadlyStreamLinks )
			{
				Console.WriteLine($"  - {link}");
			}

			Console.WriteLine($"Installation Method: {component.InstallationMethod}");
			Console.WriteLine($"Instructions count: {component.Instructions.Count}");
			Console.WriteLine($"Options count: {component.Options.Count}");

			Assert.That(component.Instructions, Is.Not.Empty,
				$"Component '{component.Name}' has Deadlystream link(s) but no ModSync metadata/instructions. " +
				$"All mods with Deadlystream links MUST have instructions (ModSync metadata block in markdown).");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		[Ignore("Test requires mod-builds repository which has been removed from the project")]
		public void AutoGenerate_DeadlyStreamModsHaveInstructions(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult parseResult = parser.Parse(markdown);

			Console.WriteLine($"\n========================================");
			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Total components: {parseResult.Components.Count}");

			var deadlyStreamComponents = parseResult.Components
				.Where(c => c.ModLink?.Any(link => !string.IsNullOrWhiteSpace(link) &&
					link.Contains("deadlystream.com", StringComparison.OrdinalIgnoreCase)) == true)
				.ToList();

			Console.WriteLine($"Components with Deadlystream links: {deadlyStreamComponents.Count}");

			if ( deadlyStreamComponents.Count == 0 )
			{
				Assert.Pass($"No components with Deadlystream links in {Path.GetFileName(mdFilePath)}");
				return;
			}

			var componentsWithoutInstructions = new List<string>();
			int componentsWithInstructions = 0;
			int componentsAlreadyHaveInstructions = 0;

			foreach ( var component in deadlyStreamComponents )
			{
				bool hadInstructionsBeforeAutoGen = component.Instructions.Count > 0;
				if ( hadInstructionsBeforeAutoGen )
				{
					componentsAlreadyHaveInstructions++;
					Console.WriteLine($"\n✓ {component.Name}: Already has {component.Instructions.Count} instruction(s) (ModSync metadata)");
					continue;
				}

				var deadlyStreamLinks = component.ModLink
					.Where(link => !string.IsNullOrWhiteSpace(link) &&
						link.Contains("deadlystream.com", StringComparison.OrdinalIgnoreCase))
					.ToList();

				Console.WriteLine($"\n{component.Name}:");
				Console.WriteLine($"  Deadlystream link(s): {deadlyStreamLinks.Count}");
				foreach ( var link in deadlyStreamLinks.Take(2) )
				{
					Console.WriteLine($"    - {link}");
				}

				Console.WriteLine($"  Instructions before: {component.Instructions.Count}");
				Console.WriteLine($"  Options before: {component.Options.Count}");

				if ( component.Instructions.Count > 0 )
				{
					componentsWithInstructions++;
					Console.WriteLine($"  ✓ Has {component.Instructions.Count} instruction(s)");
				}
				else
				{
					componentsWithoutInstructions.Add(component.Name);
					Console.WriteLine($"  ✗ No instructions and no ModSync metadata");
				}
			}

			Console.WriteLine($"\n========================================");
			Console.WriteLine($"Summary for {Path.GetFileName(mdFilePath)}:");
			Console.WriteLine($"  Total Deadlystream components: {deadlyStreamComponents.Count}");
			Console.WriteLine($"  Components with ModSync metadata: {componentsAlreadyHaveInstructions}");
			Console.WriteLine($"  Components needing auto-generation: {deadlyStreamComponents.Count - componentsAlreadyHaveInstructions}");
			Console.WriteLine($"  Components without instructions: {componentsWithoutInstructions.Count}");

			if ( componentsWithoutInstructions.Count > 0 )
			{
				Console.WriteLine($"\nComponents without instructions:");
				foreach ( var name in componentsWithoutInstructions.Take(10) )
				{
					Console.WriteLine($"  - {name}");
				}

				if ( componentsWithoutInstructions.Count > 10 )
				{
					Console.WriteLine($"  ... and {componentsWithoutInstructions.Count - 10} more");
				}

				Assert.Fail($"{componentsWithoutInstructions.Count} Deadlystream component(s) in {Path.GetFileName(mdFilePath)} " +
					$"don't have ModSync metadata/instructions. All mods with Deadlystream links MUST have instructions.");
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		[Ignore("Test requires mod-builds repository which has been removed from the project")]
		public void ParsedComponents_DeadlyStreamLinksAreValidUrls(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult parseResult = parser.Parse(markdown);

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");

			int deadlyStreamLinkCount = 0;
			int invalidLinks = 0;

			foreach ( var component in parseResult.Components )
			{
				if ( component.ModLink == null ) continue;

				var deadlyStreamLinks = component.ModLink
					.Where(link => !string.IsNullOrWhiteSpace(link) &&
						link.Contains("deadlystream.com", StringComparison.OrdinalIgnoreCase))
					.ToList();

				deadlyStreamLinkCount += deadlyStreamLinks.Count;

				foreach ( var link in deadlyStreamLinks )
				{

					if ( !Uri.TryCreate(link, UriKind.Absolute, out Uri? uri) ||
						(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) )
					{
						invalidLinks++;
						Console.WriteLine($"  {component.Name}: Invalid URL format: {link}");
					}
					else if ( !link.Contains("deadlystream.com", StringComparison.OrdinalIgnoreCase) )
					{
						invalidLinks++;
						Console.WriteLine($"  {component.Name}: Not a Deadlystream URL: {link}");
					}
				}
			}

			Console.WriteLine($"Total Deadlystream links: {deadlyStreamLinkCount}");
			Console.WriteLine($"Invalid links: {invalidLinks}");

			Assert.That(invalidLinks, Is.EqualTo(0), "All Deadlystream links should be valid URLs");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		[Ignore("Test requires mod-builds repository which has been removed from the project")]
		public void ParsedComponents_WithModSyncMetadata_HaveValidInstructions(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult parseResult = parser.Parse(markdown);

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");

			int componentsWithInstructions = 0;
			int componentsWithMalformedInstructions = 0;

			foreach ( var component in parseResult.Components )
			{
				if ( component.Instructions.Count == 0 ) continue;

				componentsWithInstructions++;

				foreach ( var instruction in component.Instructions )
				{
					bool hasIssue = false;

					switch ( instruction.Action )
					{
						case Instruction.ActionType.Extract:
							if ( instruction.Source == null || instruction.Source.Count == 0 )
							{
								Console.WriteLine($"  {component.Name}: Extract instruction missing Source");
								hasIssue = true;
							}
							break;

						case Instruction.ActionType.Move:
						case Instruction.ActionType.Copy:
							if ( instruction.Source == null || instruction.Source.Count == 0 )
							{
								Console.WriteLine($"  {component.Name}: {instruction.Action} instruction missing Source");
								hasIssue = true;
							}
							if ( string.IsNullOrWhiteSpace(instruction.Destination) )
							{
								Console.WriteLine($"  {component.Name}: {instruction.Action} instruction missing Destination");
								hasIssue = true;
							}
							break;

						case Instruction.ActionType.Choose:
							if ( instruction.Source == null || instruction.Source.Count == 0 )
							{
								Console.WriteLine($"  {component.Name}: Choose instruction missing Source (option GUIDs)");
								hasIssue = true;
							}
							break;
					}

					if ( hasIssue )
					{
						componentsWithMalformedInstructions++;
					}
				}
			}

			Console.WriteLine($"Components with instructions: {componentsWithInstructions}");
			Console.WriteLine($"Components with malformed instructions: {componentsWithMalformedInstructions}");

			Assert.That(componentsWithMalformedInstructions, Is.EqualTo(0),
				"All components with instructions should have valid instruction structure");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		[Ignore("Test requires mod-builds repository which has been removed from the project")]
		public void ParsedComponents_StatisticsOnInstallationMethods(string mdFilePath)
		{

			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			MarkdownParserResult parseResult = parser.Parse(markdown);

			Console.WriteLine($"\n========================================");
			Console.WriteLine($"Installation Method Statistics for {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"========================================");

			var methodCounts = new Dictionary<string, int>();
			var componentsWithDeadlyStream = new Dictionary<string, int>();
			var componentsWithInstructions = new Dictionary<string, int>();

			foreach ( var component in parseResult.Components )
			{
				string method = component.InstallationMethod ?? "Not Specified";

				if ( !methodCounts.ContainsKey(method) )
					methodCounts[method] = 0;
				methodCounts[method]++;

				bool hasDeadlyStream = component.ModLink?.Any(link =>
					!string.IsNullOrWhiteSpace(link) &&
					link.Contains("deadlystream.com", StringComparison.OrdinalIgnoreCase)) == true;

				if ( hasDeadlyStream )
				{
					if ( !componentsWithDeadlyStream.ContainsKey(method) )
						componentsWithDeadlyStream[method] = 0;
					componentsWithDeadlyStream[method]++;

					if ( component.Instructions.Count > 0 )
					{
						if ( !componentsWithInstructions.ContainsKey(method) )
							componentsWithInstructions[method] = 0;
						componentsWithInstructions[method]++;
					}
				}
			}

			Console.WriteLine($"\nTotal components: {parseResult.Components.Count}");
			Console.WriteLine($"\nInstallation Methods:");
			foreach ( var kvp in methodCounts.OrderByDescending(x => x.Value) )
			{
				int dsCount = componentsWithDeadlyStream.GetValueOrDefault(kvp.Key, 0);
				int instrCount = componentsWithInstructions.GetValueOrDefault(kvp.Key, 0);

				Console.WriteLine($"  {kvp.Key}: {kvp.Value} total");
				if ( dsCount > 0 )
				{
					Console.WriteLine($"    - {dsCount} with Deadlystream links");
					Console.WriteLine($"    - {instrCount} with ModSync instructions");
				}
			}

			Assert.Pass($"Statistics gathered for {Path.GetFileName(mdFilePath)}");
		}
	}
}

