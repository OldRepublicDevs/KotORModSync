// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Utility;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    /// <summary>
    /// Tests that incoming component fields take priority during merge operations,
    /// specifically testing the --exclude-existing-only scenario where incoming
    /// Directions should override existing Directions.
    /// </summary>
    [TestFixture]
    public class MergeDirectionsPriorityTest
    {
        [Test]
        public async Task Merge_WithExcludeExistingOnly_ShouldPreferIncomingDirections()
        {
            // Arrange - Create temp directory for test files
            string tempDir = Path.Combine(Path.GetTempPath(), "KOTORModSyncTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create incoming markdown content with NEW Directions
                string incomingMarkdown = @"### Example Dialogue Enhancement

**Name:** [Example Dialogue Enhancement](https://deadlystream.com/files/file/1313-example-dialogue-enhancement/)

**Author:** Test Author A & Test Author B

**Description:** In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.

**Category & Tier:** Immersion / 1 - Essential

**Non-English Functionality:** NO

**Installation Method:** Loose-File Mod

**Installation Instructions:** The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.

___
";

                // Create existing TOML content with OLD Directions
                string existingToml = @"[[thisMod]]
Guid = ""a9aa5bf5-b4ac-4aa3-acbb-402337235e54""
Name = ""Example Dialogue Enhancement""
Author = ""Test Author A & Test Author B""
Category = ""Immersion""
Tier = ""Essential""
Description = ""In addition to fixing several typos, this mod takes the PC's dialogue--which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil--and replaces it with more moderate and reasonable responses, even for DS choices.""
Directions = ""Move the dialogue.tlk file from the \""PC Response Moderation\"" folder into the main KOTOR directory (where the executable file is).""
IsSelected = true
ModLinkFilenames = { ""https://deadlystream.com/files/file/1313-example-dialogue-enhancement/"" = {  } }

[[thisMod.Instructions]]
Action = ""Extract""
Overwrite = true
Source = [""<<modDirectory>>\\Example_Dialogue_Enhancement*.7z""]

[[thisMod.Instructions]]
Action = ""Choose""
Overwrite = true
Source = [""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"", ""6d593186-e356-4994-b6a8-f71445869937""]

[[thisMod.Options]]
Guid = ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
Name = ""Standard""
Description = ""Straight fixes to spelling errors/punctuation/grammar""
IsSelected = false
Restrictions = [""6d593186-e356-4994-b6a8-f71445869937""]

[[thisMod.Options.Instructions]]
Parent = ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
Action = ""Move""
Destination = ""<<kotorDirectory>>""
Overwrite = true
Source = [""<<modDirectory>>\\Example_Dialogue_Enhancement*\\Corrections only\\dialog.tlk""]

[[thisMod.Options]]
Guid = ""6d593186-e356-4994-b6a8-f71445869937""
Name = ""Revised""
Description = ""Everything in Straight Fixes, but also has changes from the PC Moderation changes.""
IsSelected = true
Restrictions = [""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""]

[[thisMod.Options.Instructions]]
Parent = ""6d593186-e356-4994-b6a8-f71445869937""
Action = ""Move""
Destination = ""<<kotorDirectory>>""
Overwrite = true
Source = [""<<modDirectory>>\\Example_Dialogue_Enhancement*\\PC Response Moderation version\\dialog.tlk""]
";

                // Write files to disk
                string incomingPath = Path.Combine(tempDir, "incoming.md");
                string existingPath = Path.Combine(tempDir, "existing.toml");

                await NetFrameworkCompatibility.WriteAllTextAsync(incomingPath, incomingMarkdown);
                // Strip any BOM from TOML string and write without BOM
                string tomlWithoutBom = existingToml.TrimStart('\uFEFF');
                await NetFrameworkCompatibility.WriteAllTextAsync(existingPath, tomlWithoutBom, new System.Text.UTF8Encoding(false));

                // Act - Perform merge using ComponentMergeService
                var mergeOptions = new MergeOptions
                {
                    ExcludeExistingOnly = true,
                    ExcludeIncomingOnly = false,
                    UseExistingOrder = false,
                    HeuristicsOptions = MergeHeuristicsOptions.CreateDefault(),
                    // Default: prefer incoming (all PreferExisting* flags are false)
                    PreferExistingName = false,
                    PreferExistingAuthor = false,
                    PreferExistingDescription = false,
                    PreferExistingDirections = false,
                    PreferExistingCategory = false,
                    PreferExistingTier = false,
                    PreferExistingInstallationMethod = false,
                    PreferExistingInstructions = false,
                    PreferExistingOptions = false,
                    // PreferExistingModLinkFilenames doesn't exist - using PreferExistingModLinks instead if needed
                    // PreferExistingModLinks = false,
                };

                List<ModComponent> mergedComponents = await ComponentMergeService.MergeInstructionSetsAsync(
                    existingPath,
                    incomingPath,
                    mergeOptions,
                    downloadCache: null,
                    cancellationToken: default
                );

                // Assert
                Assert.Multiple(() =>
                {
                    Assert.That(mergedComponents, Is.Not.Null, "Merged components list should not be null");
                    Assert.That(mergedComponents.Count, Is.EqualTo(1), "Should merge exactly 1 component");
                    Assert.That(File.Exists(incomingPath), Is.True, "Incoming file should exist");
                    Assert.That(File.Exists(existingPath), Is.True, "Existing file should exist");
                });

                ModComponent mergedComponent = mergedComponents[0];
                Assert.Multiple(() =>
                {
                    Assert.That(mergedComponent, Is.Not.Null, "Merged component should not be null");
                    Assert.That(mergedComponent.Name, Is.Not.Null.And.Not.Empty, "Merged component name should not be null or empty");
                    Assert.That(mergedComponent.Name, Is.EqualTo("Example Dialogue Enhancement"), "Merged component should have correct name");
                    Assert.That(mergedComponent.Author, Is.EqualTo("Test Author A & Test Author B"), "Merged component should have correct author");
                });

                // The critical assertion: Directions should come from INCOMING (markdown), not existing (TOML)
                string expectedDirections = "The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.";
                string actualDirections = mergedComponent.Directions;

                Assert.Multiple(() =>
                {
                    Assert.That(expectedDirections, Is.Not.Null.And.Not.Empty, "Expected directions should not be null or empty");
                    Assert.That(actualDirections, Is.Not.Null, "Actual directions should not be null");
                    Assert.That(actualDirections, Is.EqualTo(expectedDirections),
                                    $"Directions should come from INCOMING file, not EXISTING file.\nExpected (incoming): {expectedDirections}\nActual: {actualDirections}");

                    // Also verify other incoming fields are preserved
                    Assert.That(mergedComponent.InstallationMethod, Is.Not.Null, "Installation method should not be null");
                    Assert.That(mergedComponent.InstallationMethod, Is.EqualTo("Loose-File Mod"), "Installation method should come from incoming");
                    Assert.That(mergedComponent.Tier, Is.Not.Null, "Tier should not be null");
                    Assert.That(mergedComponent.Tier, Is.EqualTo("1 - Essential"), "Tier should come from incoming");
                    Assert.That(mergedComponent.Category, Is.Not.Null, "Category list should not be null");
                    Assert.That(mergedComponent.Category.Count, Is.EqualTo(1), "Category list should have exactly 1 item");
                });
                Assert.Multiple(() =>
                {
                    Assert.That(mergedComponent.Category[0], Is.EqualTo("Immersion"), "Category should match incoming value");
                    Assert.That(mergedComponent.Language, Is.Not.Null, "Language list should not be null");
                    Assert.That(mergedComponent.Language.Count, Is.EqualTo(1), "Language list should have exactly 1 item");
                    Assert.That(mergedComponent.Language[0], Is.EqualTo("NO"), "Language should match incoming value");
                });
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }

        [Test]
        public async Task Merge_WithExcludeExistingOnly_ShouldUseIncomingOrder()
        {
            // Arrange - Create temp directory for test files
            string tempDir = Path.Combine(Path.GetTempPath(), "KOTORModSyncTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create incoming markdown with component
                string incomingMarkdown = @"### Example Dialogue Enhancement

**Name:** [Example Dialogue Enhancement](https://deadlystream.com/files/file/1313-example-dialogue-enhancement/)

**Author:** Test Author A & Test Author B

**Description:** Incoming description

**Category & Tier:** Immersion / 1 - Essential

**Non-English Functionality:** NO

**Installation Method:** Loose-File Mod

**Installation Instructions:** Incoming directions text

___
";

                // Create existing TOML
                string existingToml = @"[[thisMod]]
Guid = ""a9aa5bf5-b4ac-4aa3-acbb-402337235e54""
Name = ""Example Dialogue Enhancement""
Author = ""Test Author A & Test Author B""
Category = ""Immersion""
Tier = ""Essential""
Description = ""Existing description""
Directions = ""Existing directions text""
IsSelected = true
";

                // Write files to disk
                string incomingPath = Path.Combine(tempDir, "incoming.md");
                string existingPath = Path.Combine(tempDir, "existing.toml");

                await NetFrameworkCompatibility.WriteAllTextAsync(incomingPath, incomingMarkdown);
                // Strip any BOM from TOML string and write without BOM
                string tomlWithoutBom = existingToml.TrimStart('\uFEFF');
                await NetFrameworkCompatibility.WriteAllTextAsync(existingPath, tomlWithoutBom, new System.Text.UTF8Encoding(false));

                // Act - Merge with default options (UseExistingOrder = false means use incoming order)
                var mergeOptions = new MergeOptions
                {
                    ExcludeExistingOnly = true,
                    UseExistingOrder = false, // Use incoming order
                    HeuristicsOptions = MergeHeuristicsOptions.CreateDefault(),
                };

                List<ModComponent> mergedComponents = await ComponentMergeService.MergeInstructionSetsAsync(
                    existingPath,
                    incomingPath,
                    mergeOptions,
                    downloadCache: null,
                    cancellationToken: default
                );

                // Assert - Incoming values should be preserved
                Assert.Multiple(() =>
                {
                    Assert.That(mergedComponents, Is.Not.Null, "Merged components list should not be null");
                    Assert.That(mergedComponents.Count, Is.EqualTo(1), "Should merge exactly 1 component");
                    Assert.That(File.Exists(incomingPath), Is.True, "Incoming file should exist");
                    Assert.That(File.Exists(existingPath), Is.True, "Existing file should exist");
                });

                ModComponent mergedComponent = mergedComponents[0];
                Assert.Multiple(() =>
                {
                    Assert.That(mergedComponent, Is.Not.Null, "Merged component should not be null");
                });

                Assert.Multiple(() =>
                {
                    // Description should come from INCOMING
                    Assert.That(mergedComponent.Description, Is.Not.Null, "Description should not be null");
                    Assert.That(mergedComponent.Description, Is.EqualTo("Incoming description"),
                        "Description should come from INCOMING file");

                    // Directions should come from INCOMING
                    Assert.That(mergedComponent.Directions, Is.Not.Null, "Directions should not be null");
                    Assert.That(mergedComponent.Directions, Is.EqualTo("Incoming directions text"),
                        "Directions should come from INCOMING file");

                    // InstallationMethod only exists in incoming
                    Assert.That(mergedComponent.InstallationMethod, Is.Not.Null, "Installation method should not be null");
                    Assert.That(mergedComponent.InstallationMethod, Is.EqualTo("Loose-File Mod"),
                        "InstallationMethod should come from INCOMING file");
                });
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }
    }
}
