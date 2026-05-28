// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class MarkdownTomlParityTests
    {
        private sealed class ParityLoadResult
        {
            public List<ModComponent> MarkdownComponents { get; set; } = new List<ModComponent>();
            public List<ModComponent> TomlComponents { get; set; } = new List<ModComponent>();
            public List<string> MarkdownNames { get; set; } = new List<string>();
            public List<string> TomlNames { get; set; } = new List<string>();
            public List<string> MissingInMarkdown { get; set; } = new List<string>();
            public List<string> ExtraInMarkdown { get; set; } = new List<string>();
        }

        private static string ResolveRepoRoot()
        {
            string[] candidates =
            {
                Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..")),
                Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..")),
                Path.GetFullPath(Environment.CurrentDirectory),
            };

            foreach (string candidate in candidates.Distinct(StringComparer.Ordinal))
            {
                if (File.Exists(Path.Combine(candidate, "KOTORModSync.sln")))
                {
                    return candidate;
                }
            }

            throw new DirectoryNotFoundException("Could not locate repository root containing KOTORModSync.sln");
        }

        [Test]
        public void Kotor1Full_OverlappingComponents_LoadEquivalentSemanticShapes()
        {
            ParityLoadResult loadResult = LoadKotor1ParityInputs();

            var markdownMap = loadResult.MarkdownComponents.ToDictionary(component => component.Name, StringComparer.Ordinal);
            var tomlMap = loadResult.TomlComponents.ToDictionary(component => component.Name, StringComparer.Ordinal);

            foreach (string componentName in loadResult.MarkdownNames.Intersect(loadResult.TomlNames, StringComparer.Ordinal))
            {
                ModComponent markdownComponent = markdownMap[componentName];
                ModComponent tomlComponent = tomlMap[componentName];

                Assert.That(
                    markdownComponent.Tier,
                    Is.EqualTo(tomlComponent.Tier),
                    $"Tier should match for '{componentName}'");
                Assert.That(markdownComponent.InstallationMethod, Is.Not.Null.And.Not.Empty, $"Markdown installation method should be populated for '{componentName}'");
            }
        }

        [Test]
        public void Kotor1Full_SourceFiles_CurrentlyContainKnownSemanticDivergences()
        {
            ParityLoadResult loadResult = LoadKotor1ParityInputs();

            Assert.Multiple(() =>
            {
                Assert.That(loadResult.MarkdownNames.Count, Is.EqualTo(186), "Current markdown loader produces 186 base-install components");
                Assert.That(loadResult.TomlNames.Count, Is.EqualTo(189), "Canonical TOML currently contains 189 base-install components");
                Assert.That(
                    loadResult.MissingInMarkdown.OrderBy(value => value, StringComparer.Ordinal).ToList(),
                    Is.EqualTo(new List<string>
                    {
                        "Carth Onasi and Male PC Romance",
                        "JC's Romance Enhancement: Biromantic Bastila for K1",
                        "JC's Romance Enhancement: Pan-Galactic Flirting for K1",
                    }),
                    "The current markdown source is missing three TOML components");
                Assert.That(loadResult.ExtraInMarkdown, Is.Empty, "Current markdown loader should not introduce extra base-install components");
            });
        }

        private static ParityLoadResult LoadKotor1ParityInputs()
        {
            string repoRoot = ResolveRepoRoot();
            string markdownPath = Path.Combine(repoRoot, "mod-builds", "content", "k1", "full.md");
            string tomlPath = Path.Combine(repoRoot, "mod-builds", "TOMLs", "KOTOR1_Full.toml");

            Assert.That(File.Exists(markdownPath), Is.True, $"Markdown file not found: {markdownPath}");
            Assert.That(File.Exists(tomlPath), Is.True, $"TOML file not found: {tomlPath}");

            List<ModComponent> markdownComponents = FileLoadingService.LoadFromFile(markdownPath).ToList();
            List<ModComponent> tomlComponents = FileLoadingService.LoadFromFile(tomlPath).ToList();

            Assert.That(markdownComponents, Is.Not.Empty, "Markdown import should produce components");
            Assert.That(tomlComponents, Is.Not.Empty, "TOML import should produce components");

            List<string> markdownNames = markdownComponents
                .Select(component => component.Name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            List<string> tomlNames = tomlComponents
                .Select(component => component.Name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            return new ParityLoadResult
            {
                MarkdownComponents = markdownComponents,
                TomlComponents = tomlComponents,
                MarkdownNames = markdownNames,
                TomlNames = tomlNames,
                MissingInMarkdown = tomlNames.Except(markdownNames, StringComparer.Ordinal).ToList(),
                ExtraInMarkdown = markdownNames.Except(tomlNames, StringComparer.Ordinal).ToList(),
            };
        }
    }
}
