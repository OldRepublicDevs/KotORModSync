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
using KOTORModSync.Core.Utility;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class ParameterizedAutoInstructionTests : BaseParameterizedTest
    {
        protected override string TestCategory => "AutoInstruction";
        protected override bool RequiresTempDirectory => true;
        protected override bool PreserveTestResults => false; // These tests are ignored anyway
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // TestTempDirectory is now created in base class

            var mainConfig = new MainConfig();
            mainConfig.sourcePath = new DirectoryInfo(TestTempDirectory);
            mainConfig.destinationPath = new DirectoryInfo(Path.Combine(TestTempDirectory, "KOTOR"));
            Directory.CreateDirectory(mainConfig.destinationPath.FullName);
            WriteLog($"KOTOR directory created: {mainConfig.destinationPath.FullName}");
        }

        [TearDown]
        public override void TearDown()
        {
            // TestTempDirectory cleanup is handled in base class
            base.TearDown();
        }

        private static IEnumerable<TestCaseData> GetAllMarkdownFiles()
        {

            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? "";
            string contentRoot = Path.Combine(assemblyDir, "mod-builds", "content");

            string k1Path = Path.Combine(contentRoot, "k1");
            if (Directory.Exists(k1Path))
            {
                foreach (string mdFile in Directory.GetFiles(k1Path, "*.md", SearchOption.AllDirectories))
                {

                    if (NetFrameworkCompatibility.Contains(mdFile, "../" + Path.DirectorySeparatorChar + "../" + Path.DirectorySeparatorChar + "validated" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                        NetFrameworkCompatibility.Contains(mdFile, "/validated/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    yield return new TestCaseData(mdFile)
                        .SetName($"K1_{Path.GetFileNameWithoutExtension(mdFile)}")
                        .SetCategory("K1")
                        .SetCategory("AutoInstruction");
                }
            }

            string k2Path = Path.Combine(contentRoot, "k2");
            if (Directory.Exists(k2Path))
            {
                foreach (string mdFile in Directory.GetFiles(k2Path, "*.md", SearchOption.AllDirectories))
                {

                    if (NetFrameworkCompatibility.Contains(mdFile, "../" + Path.DirectorySeparatorChar + "../" + Path.DirectorySeparatorChar + "validated" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                        NetFrameworkCompatibility.Contains(mdFile, "/validated/", StringComparison.Ordinal))
                    {
                        continue;
                    }

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
            string contentRoot = Path.Combine(assemblyDir, "mod-builds", "content");

            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            string k1Path = Path.Combine(contentRoot, "k1");
            if (Directory.Exists(k1Path))
            {
                foreach (string mdFile in Directory.GetFiles(k1Path, "*.md", SearchOption.AllDirectories))
                {
                    if (NetFrameworkCompatibility.Contains(mdFile, "../" + Path.DirectorySeparatorChar + "../" + Path.DirectorySeparatorChar + "validated" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                        NetFrameworkCompatibility.Contains(mdFile, "/validated/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string markdown = File.ReadAllText(mdFile);
                    MarkdownParserResult parseResult = parser.Parse(markdown);

                    var deadlyStreamComponents = parseResult.Components
                        .Where(c => c.ResourceRegistry != null && c.ResourceRegistry.Keys.Any(link => !string.IsNullOrWhiteSpace(link) &&
                            NetFrameworkCompatibility.Contains(link, "deadlystream.com", StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    foreach (ModComponent component in deadlyStreamComponents)
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
            if (Directory.Exists(k2Path))
            {
                foreach (string mdFile in Directory.GetFiles(k2Path, "*.md", SearchOption.AllDirectories))
                {
                    if (NetFrameworkCompatibility.Contains(mdFile, "../" + Path.DirectorySeparatorChar + "../" + Path.DirectorySeparatorChar + "validated" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                        NetFrameworkCompatibility.Contains(mdFile, "/validated/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string markdown = File.ReadAllText(mdFile);
                    MarkdownParserResult parseResult = parser.Parse(markdown);

                    var deadlyStreamComponents = parseResult.Components
                        .Where(c => c.ResourceRegistry != null && c.ResourceRegistry.Keys.Any(link => !string.IsNullOrWhiteSpace(link) &&
                            NetFrameworkCompatibility.Contains(link, "deadlystream.com", StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    foreach (ModComponent component in deadlyStreamComponents)
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
            WriteLogAndConsole($"========================================");
            WriteLogAndConsole($"Testing component: {component.Name}");
            WriteLogAndConsole($"From file: {Path.GetFileName(mdFilePath)}");
            WriteLog($"Component GUID: {component.Guid}");
            WriteLog($"Component Author: {component.Author}");

            var deadlyStreamLinks = component.ResourceRegistry.Keys
                .Where(link => !string.IsNullOrWhiteSpace(link) &&
                    NetFrameworkCompatibility.Contains(link, "deadlystream.com", StringComparison.OrdinalIgnoreCase))
                .ToList();

            WriteLogAndConsole($"Deadlystream link(s): {deadlyStreamLinks.Count}");
            foreach (string link in deadlyStreamLinks)
            {
                WriteLogAndConsole($"  - {link}");
            }

            WriteLogAndConsole($"Installation Method: {component.InstallationMethod}");
            WriteLogAndConsole($"Instructions count: {component.Instructions.Count}");
            WriteLogAndConsole($"Options count: {component.Options.Count}");

            Assert.That(component.Instructions, Is.Not.Empty,
                $"Component '{component.Name}' has Deadlystream link(s) but no ModSync metadata/instructions. " +
                $"All mods with Deadlystream links MUST have instructions (ModSync metadata block in markdown).");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void AutoGenerate_DeadlyStreamModsHaveInstructions(string mdFilePath)
        {
            WriteLogAndConsole($"\n========================================");
            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLog($"Input file: {mdFilePath}");

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            WriteLog($"Markdown length: {markdown.Length} characters");

            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult parseResult = parser.Parse(markdown);

            WriteLogAndConsole($"Total components: {parseResult.Components.Count}");

            var deadlyStreamComponents = parseResult.Components
                .Where(c => c.ResourceRegistry != null && c.ResourceRegistry.Keys.Any(link => !string.IsNullOrWhiteSpace(link) &&
                    NetFrameworkCompatibility.Contains(link, "deadlystream.com", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            WriteLogAndConsole($"Components with Deadlystream links: {deadlyStreamComponents.Count}");

            if (deadlyStreamComponents.Count == 0)
            {
                WriteLog("No Deadlystream components found - test passed");
                Assert.Pass($"No components with Deadlystream links in {Path.GetFileName(mdFilePath)}");
                return;
            }

            var componentsWithoutInstructions = new List<string>();
            int componentsWithInstructions = 0;
            int componentsAlreadyHaveInstructions = 0;

            foreach (ModComponent component in deadlyStreamComponents)
            {
                bool hadInstructionsBeforeAutoGen = component.Instructions.Count > 0;
                if (hadInstructionsBeforeAutoGen)
                {
                    componentsAlreadyHaveInstructions++;
                    WriteLogAndConsole($"\n✓ {component.Name}: Already has {component.Instructions.Count} instruction(s) (ModSync metadata)");
                    continue;
                }

                var deadlyStreamLinks = component.ResourceRegistry
                    .Keys.Where(link => !string.IsNullOrWhiteSpace(link) &&
                        NetFrameworkCompatibility.Contains(link, "deadlystream.com", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                WriteLogAndConsole($"\n{component.Name}:");
                WriteLogAndConsole($"  Deadlystream link(s): {deadlyStreamLinks.Count}");
                foreach (string link in deadlyStreamLinks.Take(2))
                {
                    WriteLogAndConsole($"    - {link}");
                }

                WriteLogAndConsole($"  Instructions before: {component.Instructions.Count}");
                WriteLogAndConsole($"  Options before: {component.Options.Count}");

                if (component.Instructions.Count > 0)
                {
                    componentsWithInstructions++;
                    WriteLogAndConsole($"  ✓ Has {component.Instructions.Count} instruction(s)");
                }
                else
                {
                    componentsWithoutInstructions.Add(component.Name);
                    WriteLogAndConsole($"  ✗ No instructions and no ModSync metadata");
                }
            }

            WriteLogAndConsole($"\n========================================");
            WriteLogAndConsole($"Summary for {Path.GetFileName(mdFilePath)}:");
            WriteLogAndConsole($"  Total Deadlystream components: {deadlyStreamComponents.Count}");
            WriteLogAndConsole($"  Components with ModSync metadata: {componentsAlreadyHaveInstructions}");
            WriteLogAndConsole($"  Components needing auto-generation: {deadlyStreamComponents.Count - componentsAlreadyHaveInstructions}");
            WriteLogAndConsole($"  Components without instructions: {componentsWithoutInstructions.Count}");

            if (componentsWithoutInstructions.Count > 0)
            {
                WriteLogAndConsole($"\nComponents without instructions:");
                foreach (string name in componentsWithoutInstructions.Take(10))
                {
                    WriteLogAndConsole($"  - {name}");
                }

                if (componentsWithoutInstructions.Count > 10)
                {
                    WriteLogAndConsole($"  ... and {componentsWithoutInstructions.Count - 10} more");
                }

                WriteLog($"FAILURE: {componentsWithoutInstructions.Count} components without instructions");
                Assert.Fail($"{componentsWithoutInstructions.Count} Deadlystream component(s) in {Path.GetFileName(mdFilePath)} " +
                    $"don't have ModSync metadata/instructions. All mods with Deadlystream links MUST have instructions.");
            }
            else
            {
                WriteLog("SUCCESS: All Deadlystream components have instructions");
            }
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void ParsedComponents_DeadlyStreamLinksAreValidUrls(string mdFilePath)
        {
            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLog($"Input file: {mdFilePath}");

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            WriteLog($"Markdown length: {markdown.Length} characters");

            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult parseResult = parser.Parse(markdown);
            WriteLog($"Components parsed: {parseResult.Components.Count}");

            int deadlyStreamLinkCount = 0;
            int invalidLinks = 0;

            foreach (ModComponent component in parseResult.Components)
            {
                if (component.ResourceRegistry is null)
                {
                    continue;
                }

                var deadlyStreamLinks = component.ResourceRegistry
                    .Keys.Where(link => !string.IsNullOrWhiteSpace(link) &&
                        NetFrameworkCompatibility.Contains(link, "deadlystream.com", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                deadlyStreamLinkCount += deadlyStreamLinks.Count;

                foreach (string link in deadlyStreamLinks)
                {
                    if (!Uri.TryCreate(link, UriKind.Absolute, out Uri uri) ||
                        (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
                    {
                        invalidLinks++;
                        WriteLogAndConsole($"  {component.Name}: Invalid URL format: {link}");
                    }
                    else if (!NetFrameworkCompatibility.Contains(link, "deadlystream.com", StringComparison.OrdinalIgnoreCase))
                    {
                        invalidLinks++;
                        WriteLogAndConsole($"  {component.Name}: Not a Deadlystream URL: {link}");
                    }
                }
            }

            WriteLogAndConsole($"Total Deadlystream links: {deadlyStreamLinkCount}");
            WriteLogAndConsole($"Invalid links: {invalidLinks}");

            if (invalidLinks == 0)
            {
                WriteLog("SUCCESS: All Deadlystream links are valid URLs");
            }
            else
            {
                WriteLog($"FAILURE: {invalidLinks} invalid Deadlystream links found");
            }

            Assert.That(invalidLinks, Is.EqualTo(0), "All Deadlystream links should be valid URLs");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void ParsedComponents_WithModSyncMetadata_HaveValidInstructions(string mdFilePath)
        {
            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLog($"Input file: {mdFilePath}");

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            WriteLog($"Markdown length: {markdown.Length} characters");

            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult parseResult = parser.Parse(markdown);
            WriteLog($"Components parsed: {parseResult.Components.Count}");

            int componentsWithInstructions = 0;
            int componentsWithMalformedInstructions = 0;

            foreach (ModComponent component in parseResult.Components)
            {
                if (component.Instructions.Count == 0)
                {
                    continue;
                }

                componentsWithInstructions++;

                foreach (Instruction instruction in component.Instructions)
                {
                    bool hasIssue = false;

                    switch (instruction.Action)
                    {
                        case Instruction.ActionType.Extract:
                            if (instruction.Source is null || instruction.Source.Count == 0)
                            {
                                TestContext.Progress.WriteLine($"  {component.Name}: Extract instruction missing Source");
                                hasIssue = true;
                            }
                            break;

                        case Instruction.ActionType.Move:
                        case Instruction.ActionType.Copy:
                            if (instruction.Source is null || instruction.Source.Count == 0)
                            {
                                TestContext.Progress.WriteLine($"  {component.Name}: {instruction.Action} instruction missing Source");
                                hasIssue = true;
                            }
                            if (string.IsNullOrWhiteSpace(instruction.Destination))
                            {
                                TestContext.Progress.WriteLine($"  {component.Name}: {instruction.Action} instruction missing Destination");
                                hasIssue = true;
                            }
                            break;

                        case Instruction.ActionType.Choose:
                            if (instruction.Source is null || instruction.Source.Count == 0)
                            {
                                TestContext.Progress.WriteLine($"  {component.Name}: Choose instruction missing Source (option GUIDs)");
                                hasIssue = true;
                            }
                            break;
                    }

                    if (hasIssue)
                    {
                        componentsWithMalformedInstructions++;
                    }
                }
            }

            WriteLogAndConsole($"Components with instructions: {componentsWithInstructions}");
            WriteLogAndConsole($"Components with malformed instructions: {componentsWithMalformedInstructions}");

            if (componentsWithMalformedInstructions == 0)
            {
                WriteLog("SUCCESS: All components with instructions have valid structure");
            }
            else
            {
                WriteLog($"FAILURE: {componentsWithMalformedInstructions} components have malformed instructions");
            }

            Assert.That(componentsWithMalformedInstructions, Is.EqualTo(0),
                "All components with instructions should have valid instruction structure");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void ParsedComponents_StatisticsOnInstallationMethods(string mdFilePath)
        {
            WriteLogAndConsole($"\n========================================");
            WriteLogAndConsole($"Installation Method Statistics for {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"========================================");
            WriteLog($"Input file: {mdFilePath}");

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            WriteLog($"Markdown length: {markdown.Length} characters");

            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult parseResult = parser.Parse(markdown);
            WriteLog($"Components parsed: {parseResult.Components.Count}");

            var methodCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var componentsWithDeadlyStream = new Dictionary<string, int>(StringComparer.Ordinal);
            var componentsWithInstructions = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (ModComponent component in parseResult.Components)
            {
                string method = component.InstallationMethod ?? "Not Specified";

                if (!methodCounts.ContainsKey(method))
                {
                    methodCounts[method] = 0;
                }

                methodCounts[method]++;

                bool hasDeadlyStream = component.ResourceRegistry.Keys.Any(link =>
                    !string.IsNullOrWhiteSpace(link) &&
                    NetFrameworkCompatibility.Contains(link, "deadlystream.com", StringComparison.OrdinalIgnoreCase));

                if (hasDeadlyStream)
                {
                    if (!componentsWithDeadlyStream.ContainsKey(method))
                    {
                        componentsWithDeadlyStream[method] = 0;
                    }

                    componentsWithDeadlyStream[method]++;

                    if (component.Instructions.Count > 0)
                    {
                        if (!componentsWithInstructions.ContainsKey(method))
                        {
                            componentsWithInstructions[method] = 0;
                        }

                        componentsWithInstructions[method]++;
                    }
                }
            }

            WriteLogAndConsole($"\nTotal components: {parseResult.Components.Count}");
            WriteLogAndConsole($"\nInstallation Methods:");
            foreach (KeyValuePair<string, int> kvp in methodCounts.OrderByDescending(x => x.Value))
            {
                int dsCount = NetFrameworkCompatibility.GetValueOrDefault(componentsWithDeadlyStream, kvp.Key, 0);
                int instrCount = NetFrameworkCompatibility.GetValueOrDefault(componentsWithInstructions, kvp.Key, 0);

                WriteLogAndConsole($"  {kvp.Key}: {kvp.Value} total");
                if (dsCount > 0)
                {
                    WriteLogAndConsole($"    - {dsCount} with Deadlystream links");
                    WriteLogAndConsole($"    - {instrCount} with ModSync instructions");
                }
            }

            WriteLog($"Statistics gathered for {Path.GetFileName(mdFilePath)}");
            Assert.Pass($"Statistics gathered for {Path.GetFileName(mdFilePath)}");
        }
    }
}
