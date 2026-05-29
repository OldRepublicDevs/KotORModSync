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
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Services.FileSystem;
using KOTORModSync.Core.Services.Validation;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class FullBuildMergedDryRunTests
    {
        private static readonly (string Label, string MarkdownRelative, string TomlRelative, int ExpectedTomlCount)[] FullBuilds =
        {
            ("KOTOR1", Path.Combine("mod-builds", "content", "k1", "full.md"), Path.Combine("mod-builds", "TOMLs", "KOTOR1_Full.toml"), 189),
            ("KOTOR2", Path.Combine("mod-builds", "content", "k2", "full.md"), Path.Combine("mod-builds", "TOMLs", "KOTOR2_Full.toml"), 145),
        };

        private string _testDirectory;
        private string _modDirectory;
        private string _kotorDirectory;
        private MainConfig _config;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "KOTORModSync_FullBuildDryRun_" + Guid.NewGuid());
            _modDirectory = Path.Combine(_testDirectory, "Mods");
            _kotorDirectory = Path.Combine(_testDirectory, "KOTOR");
            Directory.CreateDirectory(_modDirectory);
            Directory.CreateDirectory(_kotorDirectory);
            Directory.CreateDirectory(Path.Combine(_kotorDirectory, "Override"));
            Directory.CreateDirectory(Path.Combine(_kotorDirectory, "data"));
            File.WriteAllText(Path.Combine(_kotorDirectory, "swkotor.exe"), "fake exe");
            File.WriteAllText(Path.Combine(_kotorDirectory, "dialog.tlk"), "fake dialog");

            _config = new MainConfig
            {
                sourcePath = new DirectoryInfo(_modDirectory),
                destinationPath = new DirectoryInfo(_kotorDirectory),
            };
            MainConfig.Instance = _config;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
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

        private static (string markdownPath, string tomlPath) ResolvePaths(string markdownRelative, string tomlRelative)
        {
            string repoRoot = ResolveRepoRoot();
            string markdownPath = Path.Combine(repoRoot, markdownRelative);
            string tomlPath = Path.Combine(repoRoot, tomlRelative);

            if (!File.Exists(markdownPath) || !File.Exists(tomlPath))
            {
                Assert.Ignore($"mod-builds sources not found: {markdownPath} / {tomlPath}");
            }

            return (markdownPath, tomlPath);
        }

        private static List<ModComponent> MergeModBuildsFull(string markdownPath, string tomlPath)
        {
            var mergeOptions = new MergeOptions
            {
                UseExistingOrder = true,
                PreferExistingInstructions = true,
                PreferExistingOptions = true,
                PreferExistingResourceRegistry = true,
            };

            return ComponentMergeService.MergeInstructionSets(
                tomlPath,
                markdownPath,
                mergeOptions);
        }

        [TestCaseSource(nameof(FullBuildDryRunCases))]
        public async Task FullBuild_Merged_DryRunValidator_CompletesWithoutThrowing(
            string buildLabel,
            string markdownRelative,
            string tomlRelative,
            int expectedTomlCount)
        {
            (string markdownPath, string tomlPath) = ResolvePaths(markdownRelative, tomlRelative);

            List<ModComponent> merged = MergeModBuildsFull(markdownPath, tomlPath);

            Assert.That(merged.Count, Is.EqualTo(expectedTomlCount), $"{buildLabel} merged component count");
            Assert.That(merged.Sum(c => c.Instructions.Count), Is.GreaterThan(0), $"{buildLabel} should carry install instructions");

            foreach (ModComponent component in merged)
            {
                component.IsSelected = true;
            }

            _config.allComponents = merged;

            DryRunValidationResult result = await DryRunValidator.ValidateInstallationAsync(
                merged,
                skipDependencyCheck: false,
                CancellationToken.None).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null, $"{buildLabel} dry-run result");
                Assert.That(result.Issues, Is.Not.Null, $"{buildLabel} dry-run issues list");
                Assert.That(result.IsValid, Is.True, $"{buildLabel} merged full-build VFS dry-run should pass on template dirs");
            });

            TestContext.WriteLine($"{buildLabel} dry-run: valid={result.IsValid}, issues={result.Issues.Count}");
        }

        [TestCaseSource(nameof(FullBuildDryRunCases))]
        public void FullBuild_Merged_HasInstructionCoverage(
            string buildLabel,
            string markdownRelative,
            string tomlRelative,
            int expectedTomlCount)
        {
            (string markdownPath, string tomlPath) = ResolvePaths(markdownRelative, tomlRelative);

            List<ModComponent> canonical = ModComponentSerializationService
                .DeserializeModComponentFromString(File.ReadAllText(tomlPath), "toml")
                .ToList();
            List<ModComponent> merged = MergeModBuildsFull(markdownPath, tomlPath);

            Assert.That(canonical.Count, Is.EqualTo(expectedTomlCount));
            Assert.That(merged.Count, Is.EqualTo(expectedTomlCount));

            int canonicalInstructions = canonical.Sum(c => c.Instructions.Count);
            int mergedInstructions = merged.Sum(c => c.Instructions.Count);

            Assert.That(mergedInstructions, Is.EqualTo(canonicalInstructions), $"{buildLabel} instruction count parity");
            Assert.That(merged.Count(c => c.Instructions.Count > 0), Is.GreaterThan(expectedTomlCount / 2),
                $"{buildLabel} majority of components should have instructions");
        }

        private static IEnumerable<TestCaseData> FullBuildDryRunCases()
        {
            foreach ((string label, string md, string toml, int count) in FullBuilds)
            {
                yield return new TestCaseData(label, md, toml, count)
                    .SetName($"{label}_Merged_DryRun_Smoke");
            }
        }
    }
}
