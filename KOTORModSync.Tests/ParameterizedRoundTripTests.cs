// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using KOTORModSync.Core;
using KOTORModSync.Core.CLI;
using KOTORModSync.Core.Parsing;
using KOTORModSync.Core.Services;

using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace KOTORModSync.Tests
{

    [TestFixture]
    public class ParameterizedRoundTripTests : BaseParameterizedTest
    {
        protected override string TestCategory => "RoundTrip";
        protected override bool RequiresTempDirectory => true;
        protected override bool PreserveTestResults => true;
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            // TestTempDirectory is now created in base class
        }

        [TearDown]
        public override void TearDown()
        {
            // TestTempDirectory cleanup is handled in base class
            base.TearDown();
        }

        #region Test Case Providers

        private static IEnumerable<TestCaseData> GetAllMarkdownFiles()
        {
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? "";
            string contentRoot = Path.Combine(assemblyDir, "mod-builds", "content");

            // Only scan the specific directories we know contain markdown files
            string k1Path = Path.Combine(contentRoot, "k1");
            if (Directory.Exists(k1Path))
            {
                foreach (string mdFile in Directory.GetFiles(k1Path, "*.md", SearchOption.TopDirectoryOnly))
                {
                    if (mdFile.Contains("validated", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    yield return new TestCaseData(mdFile)
                        .SetName($"K1_{Path.GetFileNameWithoutExtension(mdFile)}")
                        .SetCategory("K1")
                        .SetCategory("RoundTrip")
                        .SetCategory("Markdown");
                }
            }

            string k2Path = Path.Combine(contentRoot, "k2");
            if (Directory.Exists(k2Path))
            {
                foreach (string mdFile in Directory.GetFiles(k2Path, "*.md", SearchOption.TopDirectoryOnly))
                {
                    if (mdFile.Contains("validated", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    yield return new TestCaseData(mdFile)
                        .SetName($"K2_{Path.GetFileNameWithoutExtension(mdFile)}")
                        .SetCategory("K2")
                        .SetCategory("RoundTrip")
                        .SetCategory("Markdown");
                }
            }
        }

        private static IEnumerable<TestCaseData> GetAllTomlFiles()
        {
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path.GetDirectoryName(assemblyPath) ?? "";

            string[] tomlSearchPaths = new[]
            {
                Path.Combine(assemblyDir, "KOTOR.Modbuilds.Rev10"),
                Path.Combine(assemblyDir, "mod-builds", "validated"),
            };

            foreach (string searchPath in tomlSearchPaths)
            {
                if (!Directory.Exists(searchPath))
                {
                    continue;
                }

                // Only scan the specific directory, not recursively
                foreach (string tomlFile in Directory.GetFiles(searchPath, "*.toml", SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileNameWithoutExtension(tomlFile);
                    string gameType = fileName.Contains("KOTOR1", StringComparison.Ordinal) || fileName.Contains("K1", StringComparison.Ordinal) ? "K1" : "K2";

                    yield return new TestCaseData(tomlFile)
                        .SetName($"{gameType}_{fileName}")
                        .SetCategory(gameType)
                        .SetCategory("RoundTrip")
                        .SetCategory("TOML");
                }
            }
        }

        #endregion

        #region Markdown Round-Trip Tests


        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void MarkdownRoundTrip_LoadGenerateLoadGenerate_SecondGenerationMatchesFirst(string mdFilePath)
        {
            WriteLogAndConsole($"Testing: {Path.GetFileName(mdFilePath)}");
            WriteLog($"Input file: {mdFilePath}");

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");
            string originalMarkdown = File.ReadAllText(mdFilePath);
            var parser = new MarkdownParser(MarkdownImportProfile.CreateDefault());

            WriteLog($"Original markdown length: {originalMarkdown.Length} characters");

            MarkdownParserResult parseResult1 = parser.Parse(originalMarkdown);
            var components1 = parseResult1.Components.ToList();
            string generatedMarkdown1 = ModComponentSerializationService.GenerateModDocumentation(components1);

            WriteLogAndConsole($"First parse: {components1.Count} components");
            WriteLogAndConsole($"First generation: {generatedMarkdown1.Length} characters");

            MarkdownParserResult parseResult2 = parser.Parse(generatedMarkdown1);
            var components2 = parseResult2.Components.ToList();
            string generatedMarkdown2 = ModComponentSerializationService.GenerateModDocumentation(components2);

            WriteLogAndConsole($"Second parse: {components2.Count} components");
            WriteLogAndConsole($"Second generation: {generatedMarkdown2.Length} characters");

            Assert.Multiple(() =>
            {
                Assert.That(components2, Has.Count.EqualTo(components1.Count),
                            "Component count should remain stable across generations");

                Assert.That(generatedMarkdown2, Is.EqualTo(generatedMarkdown1),
                    "Second markdown generation should match first generation (idempotent)");
            });

            var names1 = components1.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var names2 = components2.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.That(names2, Is.EqualTo(names1).AsCollection, "All component names should be preserved");

            MarkdownParserResult originalParse = parser.Parse(originalMarkdown);
            var originalNames = originalParse.Components.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.That(names1, Is.EqualTo(originalNames).AsCollection,
                "Generated markdown should preserve all original component names");

            WriteLogAndConsole("✓ Markdown round-trip successful - all generations match");
        }


        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void MarkdownRoundTrip_GeneratedMarkdown_PreservesAllOriginalComponents(string mdFilePath)
        {
            WriteLogAndConsole($"Testing component preservation: {Path.GetFileName(mdFilePath)}");
            WriteLog($"Input file: {mdFilePath}");

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");
            string originalMarkdown = File.ReadAllText(mdFilePath);
            var parser = new MarkdownParser(MarkdownImportProfile.CreateDefault());

            WriteLog($"Original markdown length: {originalMarkdown.Length} characters");

            MarkdownParserResult originalResult = parser.Parse(originalMarkdown);
            var originalComponents = originalResult.Components.ToList();
            WriteLog($"Original components parsed: {originalComponents.Count}");

            string generatedMarkdown = ModComponentSerializationService.GenerateModDocumentation(originalComponents);
            WriteLog($"Generated markdown length: {generatedMarkdown.Length} characters");

            MarkdownParserResult generatedResult = parser.Parse(generatedMarkdown);
            var generatedComponents = generatedResult.Components.ToList();
            WriteLog($"Generated components parsed: {generatedComponents.Count}");

            Assert.That(generatedComponents, Has.Count.EqualTo(originalComponents.Count),
                $"Should preserve all {originalComponents.Count} components");

            var originalNames = originalComponents.Select(c => c.Name).ToList();
            var generatedNames = generatedComponents.Select(c => c.Name).ToList();
            Assert.That(generatedNames, Is.EqualTo(originalNames).AsCollection,
                "All component names should be preserved in order");

            for (int i = 0; i < originalComponents.Count; i++)
            {
                var orig = originalComponents[i];
                var gen = generatedComponents[i];

                Assert.Multiple(() =>
                {
                    Assert.That(gen.Name, Is.EqualTo(orig.Name), $"Component {i}: Name mismatch");
                    Assert.That(gen.Author, Is.EqualTo(orig.Author), $"Component {i}: Author mismatch");
                });

                string origCategory = string.Join(" & ", orig.Category ?? new List<string>());
                string genCategory = string.Join(" & ", gen.Category ?? new List<string>());
                Assert.Multiple(() =>
                {
                    Assert.That(genCategory, Is.EqualTo(origCategory), $"Component {i}: Category mismatch");

                    Assert.That(gen.Tier, Is.EqualTo(orig.Tier), $"Component {i}: Tier mismatch");
                });
            }

            WriteLogAndConsole($"✓ All {originalComponents.Count} components preserved with key fields intact");
        }

        #endregion

        #region TOML Round-Trip Tests


        [TestCaseSource(nameof(GetAllTomlFiles))]
        public void TomlRoundTrip_LoadGenerateLoadGenerate_SecondGenerationMatchesFirst(string tomlFilePath)
        {
            WriteLogAndConsole($"Testing: {Path.GetFileName(tomlFilePath)}");
            WriteLog($"Input file: {tomlFilePath}");

            Assert.That(File.Exists(tomlFilePath), Is.True, $"Test file not found: {tomlFilePath}");

            List<ModComponent> components1 = FileLoadingService.LoadFromFile(tomlFilePath).ToList() ?? throw new InvalidDataException();
            Debug.Assert(!(TestTempDirectory is null), "Test directory is null");
            string tomlPath1 = GetTempDebugFilePath("generation1.toml");
            FileLoadingService.SaveToFile(components1, tomlPath1);
            string generatedToml1 = File.ReadAllText(tomlPath1);
            MarkFileForPreservation(tomlPath1, "First TOML generation for round-trip testing");

            WriteLogAndConsole($"First load: {components1.Count} components");
            WriteLogAndConsole($"First generation: {generatedToml1.Length} characters");
            WriteLog($"First generation saved to: {tomlPath1}");

            List<ModComponent> components2 = FileLoadingService.LoadFromFile(tomlPath1).ToList() ?? throw new InvalidDataException();
            string tomlPath2 = GetTempDebugFilePath("generation2.toml");
            FileLoadingService.SaveToFile(components2, tomlPath2);
            string generatedToml2 = File.ReadAllText(tomlPath2);
            MarkFileForPreservation(tomlPath2, "Second TOML generation for round-trip testing");

            WriteLogAndConsole($"Second load: {components2.Count} components");
            WriteLogAndConsole($"Second generation: {generatedToml2.Length} characters");
            WriteLog($"Second generation saved to: {tomlPath2}");

            Assert.That(components2, Has.Count.EqualTo(components1.Count),
                "Component count should remain stable across generations");

            if (!string.Equals(generatedToml2, generatedToml1, StringComparison.Ordinal))
            {
                WriteLog("WARNING: TOML difference detected between generations");
                string diffFile1 = GetDebugFilePath("diff_gen1.toml");
                string diffFile2 = GetDebugFilePath("diff_gen2.toml");
                File.WriteAllText(diffFile1, generatedToml1);
                File.WriteAllText(diffFile2, generatedToml2);
                MarkFileForPreservation(diffFile1, "TOML diff file 1 - first generation");
                MarkFileForPreservation(diffFile2, "TOML diff file 2 - second generation");
                WriteLogAndConsole($"TOML difference detected - diff files written to test directory");
                WriteLog($"Diff file 1: {diffFile1}");
                WriteLog($"Diff file 2: {diffFile2}");
                WriteLog($"Original TOML files preserved in temp directory: {TestTempDirectory}");
            }

            Assert.That(generatedToml2, Is.EqualTo(generatedToml1),
                "Second TOML generation should match first generation (idempotent)");

            var guids1 = components1.Select(c => c.Guid).OrderBy(g => g).ToList();
            var guids2 = components2.Select(c => c.Guid).OrderBy(g => g).ToList();
            Assert.That(guids2, Is.EqualTo(guids1).AsCollection, "All component GUIDs should be preserved");

            var names1 = components1.Select(c => c.Name).ToList();
            var names2 = components2.Select(c => c.Name).ToList();
            Assert.That(names2, Is.EqualTo(names1).AsCollection, "All component names should be preserved in order");

            WriteLogAndConsole("✓ TOML round-trip successful - all generations match");
        }


        [TestCaseSource(nameof(GetAllTomlFiles))]
        public void TomlRoundTrip_GeneratedToml_PreservesAllOriginalData(string tomlFilePath)
        {
            WriteLogAndConsole($"Testing data preservation: {Path.GetFileName(tomlFilePath)}");
            WriteLog($"Input file: {tomlFilePath}");

            Assert.That(File.Exists(tomlFilePath), Is.True, $"Test file not found: {tomlFilePath}");

            List<ModComponent> originalComponents = FileLoadingService.LoadFromFile(tomlFilePath).ToList() ?? throw new InvalidDataException();
            WriteLog($"Original components loaded: {originalComponents.Count}");

            Debug.Assert(!(TestTempDirectory is null), "Test directory is null");
            string generatedTomlPath = GetTempDebugFilePath("regenerated.toml");
            FileLoadingService.SaveToFile(originalComponents, generatedTomlPath);
            MarkFileForPreservation(generatedTomlPath, "Regenerated TOML for data preservation testing");
            WriteLog($"Regenerated TOML saved to: {generatedTomlPath}");

            List<ModComponent> regeneratedComponents = FileLoadingService.LoadFromFile(generatedTomlPath).ToList() ?? throw new InvalidDataException();
            WriteLog($"Regenerated components loaded: {regeneratedComponents.Count}");

            Assert.That(regeneratedComponents, Has.Count.EqualTo(originalComponents.Count),
                $"Should preserve all {originalComponents.Count} components");

            for (int i = 0; i < originalComponents.Count; i++)
            {
                var orig = originalComponents[i];
                var regen = regeneratedComponents[i];

                Assert.Multiple(() =>
                {
                    Assert.That(regen.Guid, Is.EqualTo(orig.Guid), $"Component {i}: GUID mismatch");
                    Assert.That(regen.Name, Is.EqualTo(orig.Name), $"Component {i}: Name mismatch");
                    Assert.That(regen.Author, Is.EqualTo(orig.Author), $"Component {i}: Author mismatch");
                    Assert.That(regen.Tier, Is.EqualTo(orig.Tier), $"Component {i}: Tier mismatch");
                    Assert.That(regen.Description, Is.EqualTo(orig.Description), $"Component {i}: Description mismatch");
                    Assert.That(regen.InstallationMethod, Is.EqualTo(orig.InstallationMethod), $"Component {i}: InstallationMethod mismatch");
                    Assert.That(regen.Instructions, Has.Count.EqualTo(orig.Instructions.Count), $"Component {i}: Instructions count mismatch");
                    Assert.That(regen.Options, Has.Count.EqualTo(orig.Options.Count), $"Component {i}: Options count mismatch");
                });

                Assert.Multiple(() =>
                {
                    Assert.That(regen.Category, Is.EqualTo(orig.Category).AsCollection, $"Component {i}: Category mismatch");
                    Assert.That(regen.Language, Is.EqualTo(orig.Language).AsCollection, $"Component {i}: Language mismatch");
                    Assert.That(regen.ResourceRegistry, Is.EqualTo(orig.ResourceRegistry).AsCollection, $"Component {i}: ResourceRegistry mismatch");
                    Assert.That(regen.Dependencies, Is.EqualTo(orig.Dependencies).AsCollection, $"Component {i}: Dependencies mismatch");
                    Assert.That(regen.Restrictions, Is.EqualTo(orig.Restrictions).AsCollection, $"Component {i}: Restrictions mismatch");
                });
            }

            WriteLogAndConsole($"✓ All {originalComponents.Count} components preserved with complete data integrity");
        }

        #endregion

        #region Cross-Format Round-Trip Tests


        [TestCaseSource(nameof(GetAllMarkdownFiles))]
        public void CrossFormat_MarkdownToToml_RoundTripIsIdempotent(string mdFilePath)
        {
            WriteLogAndConsole($"Testing cross-format: {Path.GetFileName(mdFilePath)}");
            WriteLog($"Input file: {mdFilePath}");

            Assert.That(File.Exists(mdFilePath), Is.True, $"Test file not found: {mdFilePath}");
            string originalMarkdown = File.ReadAllText(mdFilePath);
            var parser = new MarkdownParser(MarkdownImportProfile.CreateDefault());

            WriteLog($"Original markdown length: {originalMarkdown.Length} characters");

            MarkdownParserResult parseResult1 = parser.Parse(originalMarkdown);
            var components1 = parseResult1.Components.ToList();
            Debug.Assert(!(TestTempDirectory is null), "Test directory is null");
            string tomlPath1 = GetTempDebugFilePath("from_markdown_1.toml");
            FileLoadingService.SaveToFile(components1, tomlPath1);
            MarkFileForPreservation(tomlPath1, "Cross-format TOML 1 - from markdown");
            WriteLog($"First TOML saved to: {tomlPath1}");

            List<ModComponent> componentsFromToml = FileLoadingService.LoadFromFile(tomlPath1).ToList() ?? throw new InvalidDataException();
            string generatedMarkdown = ModComponentSerializationService.GenerateModDocumentation(componentsFromToml);
            WriteLog($"Generated markdown length: {generatedMarkdown.Length} characters");

            MarkdownParserResult parseResult2 = parser.Parse(generatedMarkdown);
            var components2 = parseResult2.Components.ToList();
            string tomlPath2 = GetTempDebugFilePath("from_markdown_2.toml");
            FileLoadingService.SaveToFile(components2, tomlPath2);
            MarkFileForPreservation(tomlPath2, "Cross-format TOML 2 - from regenerated markdown");
            WriteLog($"Second TOML saved to: {tomlPath2}");

            string toml1 = File.ReadAllText(tomlPath1);
            string toml2 = File.ReadAllText(tomlPath2);

            WriteLogAndConsole($"Components: MD1={components1.Count}, TOML1={componentsFromToml.Count}, MD2={components2.Count}");

            Assert.That(components2, Has.Count.EqualTo(componentsFromToml.Count),
                "Component count should remain stable");

            var names1 = componentsFromToml.Select(c => c.Name).ToList();
            var names2 = components2.Select(c => c.Name).ToList();
            Assert.That(names2, Is.EqualTo(names1).AsCollection, "Component names should be preserved across formats");

            WriteLogAndConsole("✓ Cross-format round-trip successful");
        }


        [TestCaseSource(nameof(GetAllTomlFiles))]
        public void CrossFormat_TomlToMarkdown_PreservesComponentData(string tomlFilePath)
        {
            WriteLogAndConsole($"Testing TOML→Markdown→TOML: {Path.GetFileName(tomlFilePath)}");
            WriteLog($"Input file: {tomlFilePath}");

            Assert.That(File.Exists(tomlFilePath), Is.True, $"Test file not found: {tomlFilePath}");

            List<ModComponent> originalComponents = FileLoadingService.LoadFromFile(tomlFilePath).ToList() ?? throw new InvalidDataException();
            WriteLog($"Original components loaded: {originalComponents.Count}");

            string generatedMarkdown = ModComponentSerializationService.GenerateModDocumentation(originalComponents);
            WriteLog($"Generated markdown length: {generatedMarkdown.Length} characters");

            var parser = new MarkdownParser(MarkdownImportProfile.CreateDefault());
            MarkdownParserResult parseResult = parser.Parse(generatedMarkdown);
            var componentsFromMarkdown = parseResult.Components.ToList();

            string debugMdPath = GetDebugFilePath(Path.GetFileNameWithoutExtension(tomlFilePath) + ".md");
            File.WriteAllText(debugMdPath, generatedMarkdown);
            MarkFileForPreservation(debugMdPath, "Debug markdown generated from TOML");
            WriteLog($"Debug markdown saved to: {debugMdPath}");

            WriteLogAndConsole($"Original TOML: {originalComponents.Count} components");
            WriteLogAndConsole($"After MD round-trip: {componentsFromMarkdown.Count} components");

            Assert.That(componentsFromMarkdown, Has.Count.EqualTo(originalComponents.Count),
                "All components should survive TOML→Markdown→Parse cycle");

            for (int i = 0; i < originalComponents.Count; i++)
            {
                var orig = originalComponents[i];
                var fromMd = componentsFromMarkdown[i];

                Assert.Multiple(() =>
                {
                    Assert.That(fromMd.Name, Is.EqualTo(orig.Name), $"Component {i}: Name mismatch");
                    Assert.That(fromMd.Author, Is.EqualTo(orig.Author), $"Component {i}: Author mismatch");
                });

            }

            WriteLogAndConsole("✓ TOML→Markdown conversion preserves all components and key fields");
        }

        #endregion
    }
}
