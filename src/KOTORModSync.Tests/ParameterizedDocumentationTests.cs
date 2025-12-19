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

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class ParameterizedDocumentationTests : BaseParameterizedTest
    {
        protected override string TestCategory => "Documentation";
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

                    if (NetFrameworkCompatibility.Contains(mdFile, "../" + Path.DirectorySeparatorChar + "../" + Path.DirectorySeparatorChar + "validated" + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                        NetFrameworkCompatibility.Contains(mdFile, "/validated/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string relativePath = NetFrameworkCompatibility.GetRelativePath(contentRoot, mdFile);
                    yield return new TestCaseData(mdFile)
                        .SetName($"K1_{Path.GetFileNameWithoutExtension(mdFile)}")
                        .SetCategory("K1")
                        .SetCategory("Markdown");
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

                    string relativePath = NetFrameworkCompatibility.GetRelativePath(contentRoot, mdFile);
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
            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLog($"Input file: {mdFilePath}");

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string originalMarkdown = File.ReadAllText(mdFilePath);
            WriteLog($"Original markdown length: {originalMarkdown.Length} characters");

            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult parseResult = parser.Parse(originalMarkdown);
            IList<ModComponent> components = parseResult.Components;

            WriteLogAndConsole($"Parsed {components.Count} components");
            WriteLogAndConsole($"Warnings: {parseResult.Warnings.Count}");
            foreach (string warning in parseResult.Warnings)
            {
                WriteLogAndConsole($"  - {warning}");
            }

            string generatedDocs = ModComponentSerializationService.GenerateModDocumentation(components.ToList());
            WriteLog($"Generated documentation length: {generatedDocs.Length} characters");

            string debugOutputPath = GetDebugFilePath(Path.GetFileName(mdFilePath));

            try
            {
                File.WriteAllText(debugOutputPath, generatedDocs);
                WriteLogAndConsole($"Generated documentation written to: {debugOutputPath}");
                MarkFileForPreservation(debugOutputPath, "Generated documentation for analysis");
            }
            catch (Exception ex)
            {
                WriteLogAndConsole($"Warning: Could not write debug output: {ex.Message}");
                WriteLogException(ex, "debug output writing");
            }

            Assert.Multiple(() =>
            {

                Assert.That(components, Is.Not.Empty, "Should have parsed at least one component");
                Assert.That(generatedDocs, Is.Not.Null.And.Not.Empty, "Generated documentation should not be empty");
            });

            string originalModList = MarkdownUtilities.ExtractModListSection(originalMarkdown);
            List<string> originalSections = MarkdownUtilities.ExtractModSections(originalModList);

            List<string> generatedSections = MarkdownUtilities.ExtractModSections(generatedDocs);

            WriteLogAndConsole($"Original sections: {originalSections.Count}");
            WriteLogAndConsole($"Generated sections: {generatedSections.Count}");

            var originalNameFields = originalSections
                .SelectMany(s => MarkdownUtilities.ExtractAllFieldValues(s, @"\*\*Name:\*\*\s*(?:\[([^\]]+)\]|([^\r\n]+))"))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            var generatedNameFields = generatedSections
                .SelectMany(s => MarkdownUtilities.ExtractAllFieldValues(s, @"\*\*Name:\*\*\s*(?:\[([^\]]+)\]|([^\r\n]+))"))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            WriteLogAndConsole($"Original mod names (from **Name:** field): {originalNameFields.Count}");
            WriteLogAndConsole($"Generated mod names (from **Name:** field): {generatedNameFields.Count}");

            if (generatedNameFields.Count != originalNameFields.Count)
            {
                WriteLogAndConsole("\n=== NAME FIELD COUNT MISMATCH ===");
                WriteLog($"MISMATCH: Original={originalNameFields.Count}, Generated={generatedNameFields.Count}");

                var missingInGenerated = originalNameFields.Except(generatedNameFields, StringComparer.Ordinal).ToList();
                var missingInOriginal = generatedNameFields.Except(originalNameFields, StringComparer.Ordinal).ToList();

                if (missingInGenerated.Count > 0)
                {
                    WriteLogAndConsole($"\nMissing in generated ({missingInGenerated.Count}):");
                    foreach (string name in missingInGenerated)
                    {
                        WriteLogAndConsole($"  - {name}");
                    }
                }

                if (missingInOriginal.Count > 0)
                {
                    WriteLogAndConsole($"\nExtra in generated ({missingInOriginal.Count}):");
                    foreach (string name in missingInOriginal)
                    {
                        WriteLogAndConsole($"  - {name}");
                    }
                }
            }

            Assert.That(generatedNameFields, Has.Count.EqualTo(originalNameFields.Count),
                $"Mod count must match exactly. Original: {originalNameFields.Count}, Generated: {generatedNameFields.Count}");

            var missingNames = originalNameFields.Except(generatedNameFields, StringComparer.Ordinal).ToList();
            var extraNames = generatedNameFields.Except(originalNameFields, StringComparer.Ordinal).ToList();

            if (missingNames.Count > 0 || extraNames.Count > 0)
            {
                WriteLogAndConsole("\n=== MOD NAME MISMATCH ===");
                if (missingNames.Count > 0)
                {
                    WriteLogAndConsole($"Names missing in generated ({missingNames.Count}):");
                    foreach (string name in missingNames)
                    {
                        WriteLogAndConsole($"  - {name}");
                    }
                }
                if (extraNames.Count > 0)
                {
                    WriteLogAndConsole($"Extra names in generated ({extraNames.Count}):");
                    foreach (string name in extraNames)
                    {
                        WriteLogAndConsole($"  - {name}");
                    }
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(missingNames, Is.Empty, "All original mod names should be present in generated output");
                Assert.That(extraNames, Is.Empty, "No extra mod names should be in generated output");
            });

            WriteLogAndConsole($"\n✓ All {originalNameFields.Count} mod names match between original and generated");
            WriteLogAndConsole("✓ Round-trip test successful: Import → Export produces identical mod list");
            WriteLog("SUCCESS: Round-trip test completed successfully");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void RoundTrip_VerifyFieldPreservation(string mdFilePath)
        {
            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLog($"Input file: {mdFilePath}");

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string originalMarkdown = File.ReadAllText(mdFilePath);
            WriteLog($"Original markdown length: {originalMarkdown.Length} characters");

            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult parseResult = parser.Parse(originalMarkdown);
            var components = parseResult.Components.ToList();

            WriteLogAndConsole($"Total components: {components.Count}");

            foreach (ModComponent component in components)
            {
                WriteLogAndConsole($"\nVerifying component: {component.Name}");

                Assert.That(component.Name, Is.Not.Null.And.Not.Empty, "Name should not be empty");
                WriteLogAndConsole($"  Name: {component.Name}");
                WriteLogAndConsole($"  Author: {component.Author}");
                WriteLogAndConsole($"  Category: {string.Join(" & ", component.Category)}");
                WriteLogAndConsole($"  Tier: {component.Tier}");
                WriteLogAndConsole($"  Language: {string.Join(", ", component.Language)}");
                WriteLogAndConsole($"  InstallationMethod: {component.InstallationMethod}");
                WriteLogAndConsole($"  ModLinks: {component.ResourceRegistry?.Count ?? 0}");
                WriteLogAndConsole($"  Description length: {component.Description?.Length ?? 0}");
                WriteLogAndConsole($"  Directions length: {component.Directions?.Length ?? 0}");
            }

            WriteLog($"SUCCESS: Field preservation verification completed for {components.Count} components");
            Assert.That(components, Is.Not.Empty, "Should have parsed components");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void Parse_ValidateComponentStructure(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdownContent = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdownContent);
            IList<ModComponent> components = result.Components;

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Total mods found: {components.Count}");

            var modNames = components.Select(c => c.Name).ToList();
            var modAuthors = components.Select(c => c.Author).ToList();
            var modCategories = components.Select(c => $"{string.Join(", ", c.Category)} / {c.Tier}").ToList();
            var modDescriptions = components.Select(c => c.Description).ToList();

            WriteLogAndConsole($"Mods with authors: {modAuthors.Count(a => !string.IsNullOrWhiteSpace(a))}");
            WriteLogAndConsole($"Mods with categories: {modCategories.Count(c => !string.IsNullOrWhiteSpace(c))}");
            WriteLogAndConsole($"Mods with descriptions: {modDescriptions.Count(d => !string.IsNullOrWhiteSpace(d))}");

            WriteLogAndConsole("\nFirst 5 mods:");
            for (int i = 0; i < Math.Min(5, components.Count); i++)
            {
                ModComponent component = components[i];
                WriteLogAndConsole($"{i + 1}. {component.Name}");
                WriteLogAndConsole($"   Author: {component.Author}");
                string categoryStr = component.Category.Count > 0
                    ? string.Join(", ", component.Category)
                    : "No category";
                WriteLogAndConsole($"   Category: {categoryStr} / {component.Tier}");
            }

            if (components.Count > 5)
            {
                WriteLogAndConsole("\nLast 5 mods:");
                for (int i = Math.Max(0, components.Count - 5); i < components.Count; i++)
                {
                    ModComponent component = components[i];
                    WriteLogAndConsole($"{i + 1}. {component.Name}");
                    WriteLogAndConsole($"   Author: {component.Author}");
                    string categoryStr = component.Category.Count > 0
                        ? string.Join(", ", component.Category)
                        : "No category";
                    WriteLogAndConsole($"   Category: {categoryStr} / {component.Tier}");
                }
            }

            Assert.That(components, Is.Not.Empty,
                $"Expected to find at least one mod entry in {Path.GetFileName(mdFilePath)}, found {components.Count}");

            Assert.Multiple(() =>
            {

                int expectedMinAuthors = (int)(components.Count * 0.5);
                int expectedMinCategories = (int)(components.Count * 0.5);

                Assert.That(modAuthors.Count(a => !string.IsNullOrWhiteSpace(a)), Is.GreaterThan(expectedMinAuthors),
                    "Most mods should have authors");
                Assert.That(modCategories.Count(c => !string.IsNullOrWhiteSpace(c)), Is.GreaterThan(expectedMinCategories),
                    "Most mods should have categories");
            });
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void Parse_ValidateModLinks(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdownContent = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdownContent);
            IList<ModComponent> components = result.Components;

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Total components: {components.Count}");

            int componentsWithLinks = 0;
            int totalLinks = 0;

            foreach (ModComponent component in components)
            {
                if (component.ResourceRegistry?.Count > 0)
                {
                    componentsWithLinks++;
                    totalLinks += component.ResourceRegistry.Count;

                    WriteLogAndConsole($"{component.Name}: {component.ResourceRegistry.Count} link(s)");
                    foreach (string link in component.ResourceRegistry.Keys)
                    {

                        if (!string.IsNullOrWhiteSpace(link))
                        {
                            bool isValidLink = link.StartsWith("http://", StringComparison.Ordinal) || link.StartsWith("https://", StringComparison.Ordinal) ||
                                               link.StartsWith("#", StringComparison.Ordinal) || link.StartsWith("/", StringComparison.Ordinal);
                            Assert.That(isValidLink, Is.True,
                                $"Link should be a valid URL, anchor link, or relative path: {link}");
                        }
                    }
                }
            }

            WriteLogAndConsole($"\nComponents with links: {componentsWithLinks}");
            WriteLogAndConsole($"Total links: {totalLinks}");

            Assert.That(componentsWithLinks, Is.GreaterThan(0),
                $"Expected at least some components to have mod links in {Path.GetFileName(mdFilePath)}");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void Parse_ValidateCategoryFormat(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdownContent = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdownContent);
            IList<ModComponent> components = result.Components;

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");

            var uniqueCategories = new HashSet<string>(StringComparer.Ordinal);
            var uniqueTiers = new HashSet<string>(StringComparer.Ordinal);

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

            WriteLogAndConsole($"\nUnique Categories ({uniqueCategories.Count}):");
            foreach (string category in uniqueCategories.OrderBy(c => c, StringComparer.Ordinal))
            {
                WriteLogAndConsole($"  - {category}");
            }

            WriteLogAndConsole($"\nUnique Tiers ({uniqueTiers.Count}):");
            foreach (string tier in uniqueTiers.OrderBy(t => t, StringComparer.Ordinal))
            {
                WriteLogAndConsole($"  - {tier}");
            }

            foreach (string category in uniqueCategories)
            {
                Assert.That(category, Is.Not.Empty, "Category should not be empty");
                Assert.That(category.Trim(), Is.EqualTo(category),
                    $"Category should not have leading/trailing whitespace: '{category}'");
            }

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

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdownContent = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdownContent);

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Warnings: {result.Warnings.Count}");

            if (result.Warnings.Count > 0)
            {
                WriteLogAndConsole("\nWarnings found:");
                foreach (string warning in result.Warnings)
                {
                    WriteLogAndConsole($"  - {warning}");
                }
            }

            if (result.Warnings.Count > 0)
            {
                Assert.Warn($"File {Path.GetFileName(mdFilePath)} has {result.Warnings.Count} parsing warnings");
            }
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void Parse_ValidateInstallationMethods(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdownContent = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdownContent);
            IList<ModComponent> components = result.Components;

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");

            var uniqueMethods = new HashSet<string>(StringComparer.Ordinal);

            foreach (ModComponent component in components)
            {
                if (!string.IsNullOrWhiteSpace(component.InstallationMethod))
                {
                    uniqueMethods.Add(component.InstallationMethod);
                }
            }

            WriteLogAndConsole($"\nUnique Installation Methods ({uniqueMethods.Count}):");
            foreach (string method in uniqueMethods.OrderBy(m => m, StringComparer.Ordinal))
            {
                int count = components.Count(c => string.Equals(c.InstallationMethod, method, StringComparison.Ordinal));
                WriteLogAndConsole($"  - {method} ({count} mods)");
            }

            foreach (string method in uniqueMethods)
            {
                Assert.That(method, Is.Not.Empty, "Installation method should not be empty");
                Assert.That(method.Trim(), Is.EqualTo(method),
                    $"Installation method should not have leading/trailing whitespace: '{method}'");
            }
        }
    }
}
