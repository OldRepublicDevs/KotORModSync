// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Utility;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class ParameterizedMarkdownImportTests : BaseParameterizedTest
    {
        protected override string TestCategory => "MarkdownImport";
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
                        .SetCategory("MarkdownImport");
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
                        .SetCategory("MarkdownImport");
                }
            }
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void ComponentSectionPattern_MatchesModSections(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var regex = new Regex(profile.ComponentSectionPattern, profile.ComponentSectionOptions, TimeSpan.FromSeconds(10));

            MatchCollection matches = regex.Matches(markdown);

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Sections matched: {matches.Count}");

            Assert.That(matches, Is.Not.Empty,
                $"Should match at least one section in {Path.GetFileName(mdFilePath)}");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void RawRegexPattern_ExtractsModNames(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdown);
            var names = result.Components.Select(c => c.Name).ToList();

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Extracted names: {names.Count}");

            if (names.Count > 0)
            {
                WriteLogAndConsole($"First name: {names[0]}");
                if (names.Count > 1)
                {
                    WriteLogAndConsole($"Last name: {names[names.Count - 1]}");
                }
            }

            Assert.That(names, Is.Not.Empty,
                $"Should extract at least one name from {Path.GetFileName(mdFilePath)}");

            foreach (string name in names)
            {
                Assert.That(name, Is.Not.Null.And.Not.Empty, "Each extracted name should be non-empty");
            }
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void RawRegexPattern_ExtractsAuthors(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions, TimeSpan.FromSeconds(10));

            MatchCollection matches = regex.Matches(markdown);
            var authors = matches.Cast<Match>()
                .Select(m => m.Groups["author"].Value.Trim())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList();

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Authors found: {authors.Count}");

            if (authors.Count > 0)
            {
                WriteLogAndConsole("Sample authors:");
                foreach (string author in authors.Take(5))
                {
                    WriteLogAndConsole($"  - {author}");
                }
            }

            Assert.That(authors, Is.Not.Empty,
                $"Should extract at least one author from {Path.GetFileName(mdFilePath)}");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void RawRegexPattern_ExtractsDescriptions(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions, TimeSpan.FromSeconds(10));

            MatchCollection matches = regex.Matches(markdown);
            var descriptions = matches.Cast<Match>()
                .Select(m => m.Groups["description"].Value.Trim())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .ToList();

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Descriptions found: {descriptions.Count}");

            if (descriptions.Count > 0)
            {
                WriteLogAndConsole($"First description preview: {descriptions[0].Substring(0, Math.Min(100, descriptions[0].Length))}...");
            }

            Assert.That(descriptions, Is.Not.Empty,
                $"Should extract at least one description from {Path.GetFileName(mdFilePath)}");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void RawRegexPattern_ExtractsCategoryTier(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdown);

            foreach (ModComponent c in result.Components.Take(3))
            {
                WriteLogAndConsole($"Component: {c.Name}");
                WriteLogAndConsole($"  Category.Count: {c.Category.Count}");
                if (c.Category.Count > 0)
                {
                    WriteLogAndConsole($"  Category[0] type: {c.Category[0]?.GetType().FullName ?? "null"}");
                    WriteLogAndConsole($"  Category[0] value: '{c.Category[0]}'");
                }
            }

            var categoryTiers = result.Components
                .Where(c => c.Category.Count > 0 || !string.IsNullOrWhiteSpace(c.Tier))
                .Select(c => $"{string.Join(", ", c.Category)} / {c.Tier}")
                .ToList();

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Category/Tier combinations found: {categoryTiers.Count}");

            if (categoryTiers.Count > 0)
            {
                WriteLogAndConsole("Sample category/tier combinations:");
                foreach (string ct in categoryTiers.Take(5))
                {
                    WriteLogAndConsole($"  - {ct}");
                }
            }

            Assert.That(categoryTiers, Is.Not.Empty,
                $"Should extract at least one category/tier from {Path.GetFileName(mdFilePath)}");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void RawRegexPattern_ExtractsInstallationMethod(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions, TimeSpan.FromSeconds(10));

            MatchCollection matches = regex.Matches(markdown);
            var methods = matches.Cast<Match>()
                .Select(m => m.Groups["installation_method"].Value.Trim())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Installation methods found: {methods.Count}");

            var uniqueMethods = methods.Distinct(StringComparer.Ordinal).ToList();
            WriteLogAndConsole($"Unique installation methods: {uniqueMethods.Count}");

            foreach (string method in uniqueMethods)
            {
                int count = methods.Count(m => string.Equals(m, method, StringComparison.Ordinal));
                WriteLogAndConsole($"  - {method}: {count} occurrence(s)");
            }

            Assert.That(methods, Is.Not.Empty,
                $"Should extract at least one installation method from {Path.GetFileName(mdFilePath)}");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void RawRegexPattern_ExtractsInstallationInstructions(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions, TimeSpan.FromSeconds(10));

            MatchCollection matches = regex.Matches(markdown);
            var instructions = matches.Cast<Match>()
                .Select(m => m.Groups["installation_instructions"].Value.Trim())
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .ToList();

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Installation instructions found: {instructions.Count}");

            if (instructions.Count > 0)
            {
                WriteLogAndConsole($"First instruction preview: {instructions[0].Substring(0, Math.Min(100, instructions[0].Length))}...");
            }

            WriteLogAndConsole($"Mods with installation instructions: {instructions.Count}");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void RawRegexPattern_ExtractsNonEnglishFunctionality(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var regex = new Regex(profile.RawRegexPattern, profile.RawRegexOptions, TimeSpan.FromSeconds(10));

            MatchCollection matches = regex.Matches(markdown);
            var nonEnglishValues = matches.Cast<Match>()
                .Select(m => m.Groups["non_english"].Value.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Non-English functionality values found: {nonEnglishValues.Count}");

            int yesCount = nonEnglishValues.Count(v => v.Equals("YES", StringComparison.OrdinalIgnoreCase));
            int noCount = nonEnglishValues.Count(v => v.Equals("NO", StringComparison.OrdinalIgnoreCase));

            WriteLogAndConsole($"  YES: {yesCount}");
            WriteLogAndConsole($"  NO: {noCount}");
            WriteLogAndConsole($"  Other: {nonEnglishValues.Count - yesCount - noCount}");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void FullMarkdownFile_ParsesAllMods(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string fullMarkdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(fullMarkdown);
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

            WriteLogAndConsole("\nFirst 10 mods:");
            for (int i = 0; i < Math.Min(10, components.Count); i++)
            {
                ModComponent component = components[i];
                WriteLogAndConsole($"{i + 1}. {component.Name}");
                WriteLogAndConsole($"   Author: {component.Author}");
                string categoryStr = component.Category.Count > 0
                    ? string.Join(", ", component.Category)
                    : "No category";
                WriteLogAndConsole($"   Category: {categoryStr} / {component.Tier}");
                WriteLogAndConsole($"   Installation Method: {component.InstallationMethod}");
            }

            Assert.That(components, Is.Not.Empty,
                $"Expected to find at least one mod entry in {Path.GetFileName(mdFilePath)}, found {components.Count}");

            Assert.Multiple(() =>
            {

                int expectedMinAuthors = (int)(components.Count * 0.5);
                int expectedMinCategories = (int)(components.Count * 0.5);
                int expectedMinDescriptions = (int)(components.Count * 0.5);

                Assert.That(modAuthors.Count(a => !string.IsNullOrWhiteSpace(a)), Is.GreaterThanOrEqualTo(expectedMinAuthors),
                    "Most mods should have authors");
                Assert.That(modCategories.Count(c => !string.IsNullOrWhiteSpace(c)), Is.GreaterThanOrEqualTo(expectedMinCategories),
                    "Most mods should have categories");
                Assert.That(modDescriptions.Count(d => !string.IsNullOrWhiteSpace(d)), Is.GreaterThanOrEqualTo(expectedMinDescriptions),
                    "Most mods should have descriptions");
            });
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void NamePattern_ExtractsNameFromBrackets(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var nameRegex = new Regex(profile.NamePattern);

            string[] lines = markdown.Split('\n');
            var nameLines = lines.Where(l => l.Contains("**Name:**")).ToList();

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Name lines found: {nameLines.Count}");

            int matchedCount = 0;
            foreach (string line in nameLines)
            {
                Match match = nameRegex.Match(line);
                if (match.Success)
                {
                    matchedCount++;
                    string name = match.Groups["name"].Value;
                    Assert.That(name, Is.Not.Null.And.Not.Empty, "Extracted name should not be empty");
                }
            }

            WriteLogAndConsole($"Successfully matched: {matchedCount}");

            Assert.That(matchedCount, Is.GreaterThan(0),
                $"Should extract at least one name from {Path.GetFileName(mdFilePath)}");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void ModLinkPattern_ExtractsLinkUrls(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var linkRegex = new Regex(profile.ModLinkPattern, RegexOptions.None, TimeSpan.FromSeconds(10));

            MatchCollection matches = linkRegex.Matches(markdown);

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Links found: {matches.Count}");

            int sampleCount = Math.Min(5, matches.Count);
            WriteLogAndConsole($"\nSample links:");
            for (int i = 0; i < sampleCount; i++)
            {
                Match match = matches[i];
                string label = match.Groups["label"].Value;
                string link = match.Groups["link"].Value;
                WriteLogAndConsole($"  [{label}]({link})");

                bool isValidLink = link.StartsWith("http://", StringComparison.Ordinal) || link.StartsWith("https://", StringComparison.Ordinal) ||
                                   link.StartsWith("#", StringComparison.Ordinal) || link.StartsWith("/", StringComparison.Ordinal);
                Assert.That(isValidLink, Is.True,
                    $"Link should be a valid URL, anchor link, or relative path: {link}");
            }

            Assert.That(matches, Is.Not.Empty,
                $"Should extract at least one link from {Path.GetFileName(mdFilePath)}");
        }

        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void Parse_ValidateAllComponentsHaveValidNames(string mdFilePath)
        {

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");

            string markdown = File.ReadAllText(mdFilePath);
            var profile = MarkdownImportProfile.CreateDefault();
            var parser = new MarkdownParser(profile);

            MarkdownParserResult result = parser.Parse(markdown);
            IList<ModComponent> components = result.Components;

            WriteLogAndConsole($"Testing file: {Path.GetFileName(mdFilePath)}");
            WriteLogAndConsole($"Components: {components.Count}");

            foreach (ModComponent component in components)
            {
                Assert.That(component.Name, Is.Not.Null.And.Not.Empty,
                    "Every component should have a non-empty name");
                Assert.That(component.Name.Trim(), Is.EqualTo(component.Name),
                    $"Component name should not have leading/trailing whitespace: '{component.Name}'");
            }

            var nameGroups = components.GroupBy(c => c.Name, StringComparer.Ordinal).Where(g => g.Count() > 1).ToList();
            if (nameGroups.Count != 0)
            {
                WriteLogAndConsole($"\nWarning: Found {nameGroups.Count} duplicate name(s):");
                foreach (IGrouping<string, ModComponent> group in nameGroups)
                {
                    WriteLogAndConsole($"  - '{group.Key}' appears {group.Count()} times");
                }
            }
        }
    }
}
