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
    [Category("Integration")]
    public sealed class FullBuildSerializationRoundTripTests
    {
        private static readonly string[] Formats = { "TOML", "JSON", "YAML", "XML" };

        private static readonly (string Label, string RelativePath, int ExpectedCount)[] FullBuilds =
        {
            ("KOTOR1", Path.Combine("mod-builds", "TOMLs", "KOTOR1_Full.toml"), 189),
            ("KOTOR2", Path.Combine("mod-builds", "TOMLs", "KOTOR2_Full.toml"), 145),
        };

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

        [TestCaseSource(nameof(FullBuildRoundTripCases))]
        public void FullBuild_RoundTrip_PreservesStructure(string buildLabel, string relativeTomlPath, string format)
        {
            string repoRoot = ResolveRepoRoot();
            string tomlPath = Path.Combine(repoRoot, relativeTomlPath);

            if (!File.Exists(tomlPath))
            {
                Assert.Ignore($"Full build file not found: {tomlPath}");
            }

            string sourceToml = File.ReadAllText(tomlPath);
            IReadOnlyList<ModComponent> original = ModComponentSerializationService.DeserializeModComponentFromString(sourceToml, "toml");
            Assert.That(original, Is.Not.Null);
            Assert.That(original.Count, Is.GreaterThan(0), $"{buildLabel} should load at least one component");

            string serialized = ModComponentSerializationService.SerializeModComponentAsString(original.ToList(), format);
            Assert.That(serialized, Is.Not.Null.And.Not.Empty, $"{buildLabel} {format} serialization should not be empty");

            IReadOnlyList<ModComponent> roundTripped = ModComponentSerializationService.DeserializeModComponentFromString(serialized, format);
            AssertStructuralParity(original, roundTripped, $"{buildLabel}/{format}");
        }

        private static IEnumerable<TestCaseData> FullBuildRoundTripCases()
        {
            foreach ((string label, string relativePath, _) in FullBuilds)
            {
                foreach (string format in Formats)
                {
                    yield return new TestCaseData(label, relativePath, format)
                        .SetName($"{label}_RoundTrip_{format}");
                }
            }
        }

        [Test]
        public void Kotor2Full_LoadsExpectedComponentCount()
        {
            string repoRoot = ResolveRepoRoot();
            string tomlPath = Path.Combine(repoRoot, "mod-builds", "TOMLs", "KOTOR2_Full.toml");

            if (!File.Exists(tomlPath))
            {
                Assert.Ignore($"KOTOR2 full build not found: {tomlPath}");
            }

            IReadOnlyList<ModComponent> components = ModComponentSerializationService.DeserializeModComponentFromString(
                File.ReadAllText(tomlPath),
                "toml");

            Assert.That(components.Count, Is.GreaterThan(100), "KOTOR2 full build should contain a large component set");
        }

        private static void AssertStructuralParity(
            IReadOnlyList<ModComponent> original,
            IReadOnlyList<ModComponent> roundTripped,
            string context)
        {
            Assert.Multiple(() =>
            {
                Assert.That(roundTripped, Is.Not.Null, $"{context}: round-tripped list should not be null");
                Assert.That(roundTripped.Count, Is.EqualTo(original.Count), $"{context}: component count should match");
            });

            var originalByName = original
                .GroupBy(c => c.Name?.Trim() ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var roundTrippedByName = roundTripped
                .GroupBy(c => c.Name?.Trim() ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            Assert.That(roundTrippedByName.Keys, Is.EquivalentTo(originalByName.Keys), $"{context}: component names should match");

            foreach (string name in originalByName.Keys)
            {
                ModComponent a = originalByName[name];
                ModComponent b = roundTrippedByName[name];

                Assert.Multiple(() =>
                {
                    Assert.That(b.Tier, Is.EqualTo(a.Tier), $"{context}: tier for '{name}'");
                    Assert.That(b.InstallationMethod, Is.EqualTo(a.InstallationMethod), $"{context}: install method for '{name}'");
                    Assert.That(b.Instructions.Count, Is.EqualTo(a.Instructions.Count), $"{context}: instruction count for '{name}'");
                    Assert.That(b.Options.Count, Is.EqualTo(a.Options.Count), $"{context}: option count for '{name}'");
                });
            }
        }
    }
}
