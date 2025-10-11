// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
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

			// Initialize MainConfig for the tests
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
				// Ignore cleanup errors
			}
		}

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
			if ( Directory.Exists(k1Path) )
			{
				foreach ( string mdFile in Directory.GetFiles(k1Path, "*.md", SearchOption.AllDirectories) )
				{
					// Exclude files in validated subdirectory
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

			// Get all .md files from k2 directory (exclude validated subdirectory)
			string k2Path = Path.Combine(contentRoot, "k2");
			if ( Directory.Exists(k2Path) )
			{
				foreach ( string mdFile in Directory.GetFiles(k2Path, "*.md", SearchOption.AllDirectories) )
				{
					// Exclude files in validated subdirectory
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

		/// <summary>
		/// Dynamically generates individual test cases for each Deadlystream component in all .md files.
		/// Uses the PRODUCTION CODE pipeline to download archives and auto-generate instructions.
		/// </summary>
		private static IEnumerable<TestCaseData> GetAllDeadlystreamComponents()
		{
			// Get the solution root
			string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? "";
			string solutionRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", ".."));
			string contentRoot = Path.Combine(solutionRoot, /*"KOTORModSync.Tests", */"mod-builds", "content");

			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Initialize the PRODUCTION unified pipeline for processing ModLinks
			// Set up DownloadCacheService with proper DownloadManager and handlers
			// IMPORTANT: Order matters! Specific handlers must come before generic DirectDownloadHandler
			var downloadCacheService = new DownloadCacheService();
			var httpClient = new System.Net.Http.HttpClient();
			var handlers = new List<IDownloadHandler>
		{
			new DeadlyStreamDownloadHandler(httpClient),
			new MegaDownloadHandler(),
			new NexusModsDownloadHandler(httpClient, ""),
			new GameFrontDownloadHandler(httpClient),
			new DirectDownloadHandler(httpClient)  // Must be LAST - it's a catch-all
		};
			var downloadManager = new DownloadManager(handlers);
			downloadCacheService.SetDownloadManager(downloadManager);

			var modLinkProcessor = new ModLinkProcessingService(downloadCacheService);
			string downloadDirectory = Path.Combine(Path.GetTempPath(), "KOTORModSync_TestDownloads");
			Directory.CreateDirectory(downloadDirectory);

			// Process K1 files
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

					// Use PRODUCTION CODE to process ModLinks and auto-generate instructions
					if ( deadlyStreamComponents.Count > 0 )
					{
						modLinkProcessor.ProcessComponentModLinksSync(
							deadlyStreamComponents,
							downloadDirectory,
							progress: null,
							cancellationToken: CancellationToken.None);
					}

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

			// Process K2 files
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

					// Use PRODUCTION CODE to process ModLinks and auto-generate instructions
					if ( deadlyStreamComponents.Count > 0 )
					{
						modLinkProcessor.ProcessComponentModLinksSync(
							deadlyStreamComponents,
							downloadDirectory,
							progress: null,
							cancellationToken: CancellationToken.None);
					}

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
		public void IndividualComponent_HasModSyncInstructions(ModComponent component, string mdFilePath)
		{
			// Arrange
			Console.WriteLine($"========================================");
			Console.WriteLine($"Testing component: {component.Name}");
			Console.WriteLine($"From file: {Path.GetFileName(mdFilePath)}");

			// Get deadlystream URLs
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

			// Assert
			Assert.That(component.Instructions, Is.Not.Empty,
				$"Component '{component.Name}' has Deadlystream link(s) but no ModSync metadata/instructions. " +
				$"All mods with Deadlystream links MUST have instructions (ModSync metadata block in markdown).");
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void AutoGenerate_DeadlyStreamModsHaveInstructions(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Parse components from markdown
			MarkdownParserResult parseResult = parser.Parse(markdown);

			Console.WriteLine($"\n========================================");
			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"Total components: {parseResult.Components.Count}");

			// Filter to components with deadlystream links
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

			// Track failures
			var componentsWithoutInstructions = new List<string>();
			int componentsWithInstructions = 0;
			int componentsAlreadyHaveInstructions = 0;

			// Test each component
			foreach ( var component in deadlyStreamComponents )
			{
				bool hadInstructionsBeforeAutoGen = component.Instructions.Count > 0;
				if ( hadInstructionsBeforeAutoGen )
				{
					componentsAlreadyHaveInstructions++;
					Console.WriteLine($"\n✓ {component.Name}: Already has {component.Instructions.Count} instruction(s) (ModSync metadata)");
					continue;
				}

				// Get deadlystream URLs
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

				// Try to find a matching archive file in the test directory
				// Note: This won't work in the test environment since we don't have actual archives
				// But we check if the component already has instructions from ModSync metadata

				Console.WriteLine($"  Instructions before: {component.Instructions.Count}");
				Console.WriteLine($"  Options before: {component.Options.Count}");

				// The actual test: Components with Deadlystream links should either:
				// 1. Already have instructions (from ModSync metadata)
				// 2. Have a way to generate them (would need actual archives)

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

			// Report summary
			Console.WriteLine($"\n========================================");
			Console.WriteLine($"Summary for {Path.GetFileName(mdFilePath)}:");
			Console.WriteLine($"  Total Deadlystream components: {deadlyStreamComponents.Count}");
			Console.WriteLine($"  Components with ModSync metadata: {componentsAlreadyHaveInstructions}");
			Console.WriteLine($"  Components needing auto-generation: {deadlyStreamComponents.Count - componentsAlreadyHaveInstructions}");
			Console.WriteLine($"  Components without instructions: {componentsWithoutInstructions.Count}");

			// FAIL if any deadlystream mods don't have instructions
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

				// FAIL the test - all deadlystream mods MUST have instructions (from ModSync metadata)
				Assert.Fail($"{componentsWithoutInstructions.Count} Deadlystream component(s) in {Path.GetFileName(mdFilePath)} " +
					$"don't have ModSync metadata/instructions. All mods with Deadlystream links MUST have instructions.");
			}
		}

		[TestCaseSource(nameof(GetAllMarkdownFiles))]
		public void ParsedComponents_DeadlyStreamLinksAreValidUrls(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult parseResult = parser.Parse(markdown);

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");

			// Assert - All deadlystream links should be valid URLs
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
					// Check if it's a valid URL format
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
		public void ParsedComponents_WithModSyncMetadata_HaveValidInstructions(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult parseResult = parser.Parse(markdown);

			Console.WriteLine($"Testing file: {Path.GetFileName(mdFilePath)}");

			// Assert - Components with instructions should have proper setup
			int componentsWithInstructions = 0;
			int componentsWithMalformedInstructions = 0;

			foreach ( var component in parseResult.Components )
			{
				if ( component.Instructions.Count == 0 ) continue;

				componentsWithInstructions++;

				// Validate instruction structure
				foreach ( var instruction in component.Instructions )
				{
					bool hasIssue = false;

					// Check for required fields based on action type
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
		public void ParsedComponents_StatisticsOnInstallationMethods(string mdFilePath)
		{
			// Arrange
			Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

			string markdown = File.ReadAllText(mdFilePath);
			var profile = MarkdownImportProfile.CreateDefault();
			var parser = new MarkdownParser(profile);

			// Act
			MarkdownParserResult parseResult = parser.Parse(markdown);

			Console.WriteLine($"\n========================================");
			Console.WriteLine($"Installation Method Statistics for {Path.GetFileName(mdFilePath)}");
			Console.WriteLine($"========================================");

			// Collect statistics
			var methodCounts = new Dictionary<string, int>();
			var componentsWithDeadlyStream = new Dictionary<string, int>();
			var componentsWithInstructions = new Dictionary<string, int>();

			foreach ( var component in parseResult.Components )
			{
				string method = component.InstallationMethod ?? "Not Specified";

				if ( !methodCounts.ContainsKey(method) )
					methodCounts[method] = 0;
				methodCounts[method]++;

				// Check if has deadlystream link
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

			// This test is informational only
			Assert.Pass($"Statistics gathered for {Path.GetFileName(mdFilePath)}");
		}
	}
}

