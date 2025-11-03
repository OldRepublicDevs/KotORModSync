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

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class ParameterizedModSyncMetadataTests : BaseParameterizedTest
    {
        protected override string TestCategory => "ModSyncMetadata";
        protected override bool RequiresTempDirectory => false; // These tests don't need temp directories
        protected override bool PreserveTestResults => true;

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

                    if (mdFile.Contains("../" + Path.DirectorySeparatorChar + "../" + Path.DirectorySeparatorChar + "validated" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                        mdFile.Contains("/validated/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string relativePath = Path.GetRelativePath(contentRoot, mdFile);
                    yield return new TestCaseData(mdFile)
                        .SetName($"K1_{Path.GetFileNameWithoutExtension(mdFile)}")
                        .SetCategory("K1")
                        .SetCategory("ModSyncMetadata");
                }
            }

            string k2Path = Path.Combine(contentRoot, "k2");
            if (Directory.Exists(k2Path))
            {
                foreach (string mdFile in Directory.GetFiles(k2Path, "*.md", SearchOption.AllDirectories))
                {

                    if (mdFile.Contains("../" + Path.DirectorySeparatorChar + "../" + Path.DirectorySeparatorChar + "validated" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                        mdFile.Contains("/validated/", StringComparison.Ordinal))
                    {
                        continue;
                    }

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

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdown);

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Components parsed: {result.Components.Count}");

            int componentsWithMetadata = 0;
            int componentsWithValidGuids = 0;

            foreach (var component in result.Components)
            {

                if (component.Instructions.Count > 0 || component.Options.Count > 0)
                {
                    componentsWithMetadata++;

                    if (component.Guid != Guid.Empty)
                    {
                        componentsWithValidGuids++;
                        WriteLogAndConsole($"  {component.Name}: GUID = {component.Guid}");
                    }
                    else
                    {
                        WriteLogAndConsole($"  {component.Name}: Missing or empty GUID!");
                    }
                }
            }

            WriteLogAndConsole($"Components with metadata: {componentsWithMetadata}");
            WriteLogAndConsole($"Components with valid GUIDs: {componentsWithValidGuids}");

            if (componentsWithMetadata > 0)
            {
                Assert.That(componentsWithValidGuids, Is.EqualTo(componentsWithMetadata),
                    "All components with ModSync metadata should have valid GUIDs");
            }
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void ParseModSyncMetadata_InstructionsHaveValidGuids(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdown);

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");

            int totalInstructions = 0;

            foreach (var component in result.Components)
            {
                foreach (var instruction in component.Instructions)
                {
                    totalInstructions++;
                }

                foreach (var option in component.Options)
                {
                    foreach (var instruction in option.Instructions)
                    {
                        totalInstructions++;
                    }
                }
            }

            WriteLogAndConsole($"Total instructions: {totalInstructions}");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void ParseModSyncMetadata_OptionsHaveValidGuids(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdown);

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");

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
                        WriteLogAndConsole($"  {component.Name} -> {option.Name}: Option has empty GUID");
                    }
                }
            }

            WriteLogAndConsole($"Total options: {totalOptions}");
            WriteLogAndConsole($"Options with invalid GUIDs: {invalidGuids}");

            if (totalOptions > 0)
            {
                Assert.That(invalidGuids, Is.EqualTo(0), "All options should have valid GUIDs");
            }
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        public void RoundTrip_ModSyncMetadata_PreservesAllData(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string originalMarkdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult firstParse = parser.Parse(originalMarkdown);

            var componentsWithMetadata = firstParse.Components
                .Where(c => c.Instructions.Count > 0 || c.Options.Count > 0)
                .ToList();

            if (componentsWithMetadata.Count == 0)
            {
                Assert.Pass($"No components with ModSync metadata in {Path.GetFileName(mdFilePath)}");
                return;
            }

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Components with metadata: {componentsWithMetadata.Count}");

            string generated = ModComponentSerializationService.GenerateModDocumentation(componentsWithMetadata);

            MarkdownParserResult secondParse = parser.Parse(generated);

            Assert.That(secondParse.Components, Has.Count.EqualTo(componentsWithMetadata.Count),
                "Component count should be preserved");

            for (int i = 0; i < componentsWithMetadata.Count; i++)
            {
                var first = componentsWithMetadata[i];
                var second = secondParse.Components[i];

                WriteLogAndConsole($"\nComparing component: {first.Name}");

                Assert.Multiple(() =>
                {
                    Assert.That(second.Guid, Is.EqualTo(first.Guid), $"{first.Name}: GUID preserved");
                    Assert.That(second.Name, Is.EqualTo(first.Name), $"{first.Name}: Name preserved");
                    Assert.That(second.Instructions, Has.Count.EqualTo(first.Instructions.Count),
                        $"{first.Name}: Instruction count preserved");
                    Assert.That(second.Options, Has.Count.EqualTo(first.Options.Count),
                        $"{first.Name}: Option count preserved");
                });

                for (int j = 0; j < first.Instructions.Count; j++)
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(second.Instructions[j].Action, Is.EqualTo(first.Instructions[j].Action),
                            $"{first.Name}: Instruction {j} Action preserved");
                    });
                }

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

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdown);

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");

            var validActions = Enum.GetValues(typeof(Instruction.ActionType)).Cast<Instruction.ActionType>().ToList();
            var actionCounts = new Dictionary<Instruction.ActionType, int>();

            foreach (var component in result.Components)
            {
                foreach (var instruction in component.Instructions)
                {
                    Assert.That(validActions, Contains.Item(instruction.Action),
                        $"{component.Name}: Instruction has invalid action {instruction.Action}");

                    if (!actionCounts.ContainsKey(instruction.Action))
                    {
                        actionCounts[instruction.Action] = 0;
                    }

                    actionCounts[instruction.Action]++;
                }

                foreach (var option in component.Options)
                {
                    foreach (var instruction in option.Instructions)
                    {
                        Assert.That(validActions, Contains.Item(instruction.Action),
                            $"{component.Name} -> {option.Name}: Instruction has invalid action {instruction.Action}");

                        if (!actionCounts.ContainsKey(instruction.Action))
                        {
                            actionCounts[instruction.Action] = 0;
                        }

                        actionCounts[instruction.Action]++;
                    }
                }
            }

            if (actionCounts.Count > 0)
            {
                WriteLogAndConsole("\nAction distribution:");
                foreach (var kvp in actionCounts.OrderByDescending(x => x.Value))
                {
                    WriteLogAndConsole($"  {kvp.Key}: {kvp.Value}");
                }
            }
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void ParseModSyncMetadata_ComponentsWithInstructionsHaveValidSourceDestination(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdown);

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");

            foreach (var component in result.Components)
            {
                foreach (var instruction in component.Instructions)
                {

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

                    if (instruction.Source?.Count > 0)
                    {
                        WriteLogAndConsole($"  {component.Name}: {instruction.Action} -> Source: {string.Join(", ", instruction.Source.Take(2))}");
                    }
                }
            }
        }
    }
}
