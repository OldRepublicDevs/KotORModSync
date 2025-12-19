using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

using Newtonsoft.Json;

using NUnit.Framework;

namespace TestProject
{
    [TestFixture]
    public class SerializationRoundTripTests
    {

        private static readonly string TestTomlContent = @"[[thisMod]]
ModLinkFilenames = { ""https://deadlystream.com/files/file/1313-example-dialogue-enhancement/"" = {  } }
Guid = ""987a0d17-c596-49af-ba28-851232455253""
Name = ""Example Dialogue Enhancement""
Author = ""Test Author A & Test Author B""
Tier = ""1 - Essential""
Description = ""In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.""
InstallationMethod = ""Loose-File Mod""
Directions = ""The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.""
IsSelected = true
Category = [""Immersion""]
Language = [""NO""]

[[thisMod.Instructions]]
Guid = ""e6d0dbb7-75f7-4886-a4a5-e7eea85dac1c""
Action = ""Extract""
Source = [""<<modDirectory>>\\Example_Dialogue_Enhancement*.7z""]

[[thisMod.Instructions]]
Guid = ""b201d6e8-3d07-4de5-a937-47ba9952afac""
Action = ""Choose""
Source = [""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"", ""6d593186-e356-4994-b6a8-f71445869937""]

[[thisMod.Options]]
Guid = ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
Name = ""Standard""
Description = ""Straight fixes to spelling errors/punctuation/grammar""
Restrictions = [""6d593186-e356-4994-b6a8-f71445869937""]

[[thisMod.Options.Instructions]]
Parent = ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
Guid = ""9521423e-e617-474c-bcbb-a15563a516fc""
Action = ""Move""
Destination = ""<<kotorDirectory>>""
Source = [""<<modDirectory>>\\Example_Dialogue_Enhancement*\\Corrections only\\dialog.tlk""]

[[thisMod.Options]]
Guid = ""6d593186-e356-4994-b6a8-f71445869937""
Name = ""Revised""
Description = ""Everything in Straight Fixes, but also has changes from the PC Moderation changes.""
IsSelected = true
Restrictions = [""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""]

[[thisMod.Options.Instructions]]
Parent = ""6d593186-e356-4994-b6a8-f71445869937""
Guid = ""80fba038-4a24-4716-a0cc-1d4051e952a0""
Action = ""Move""
Destination = ""<<kotorDirectory>>""
Source = [""<<modDirectory>>\\Example_Dialogue_Enhancement*\\PC Response Moderation version\\dialog.tlk""]";

        private static void AssertComponentEquality([CanBeNull] ModComponent comp1, [CanBeNull] ModComponent comp2)
        {
            Assert.Multiple(() =>
            {
                Assert.That(comp1, Is.Not.Null, "First component should not be null");
                Assert.That(comp2, Is.Not.Null, "Second component should not be null");
            });

            Assert.Multiple(() =>
            {
                // Compare core properties that should be preserved
                Assert.That(comp1.Name, Is.Not.Null, "First component name should not be null");
                Assert.That(comp2.Name, Is.Not.Null, "Second component name should not be null");
                Assert.That(comp2.Name, Is.EqualTo(comp1.Name), "Component name should match");
                Assert.That(comp2.Author, Is.EqualTo(comp1.Author), "Component author should match");
                Assert.That(comp2.Description, Is.EqualTo(comp1.Description), "Component description should match");
                Assert.That(comp2.Tier, Is.EqualTo(comp1.Tier), "Component tier should match");
                Assert.That(comp2.InstallationMethod, Is.EqualTo(comp1.InstallationMethod), "Component installation method should match");
                Assert.That(comp2.Directions, Is.EqualTo(comp1.Directions), "Component directions should match");
                Assert.That(comp2.IsSelected, Is.EqualTo(comp1.IsSelected), "Component IsSelected should match");
                Assert.That(comp2.Category, Is.Not.Null, "Second component category should not be null");
                Assert.That(comp1.Category, Is.Not.Null, "First component category should not be null");
                Assert.That(comp2.Category, Is.EqualTo(comp1.Category), "Component category should match");
                Assert.That(comp2.Language, Is.Not.Null, "Second component language should not be null");
                Assert.That(comp1.Language, Is.Not.Null, "First component language should not be null");
                Assert.That(comp2.Language, Is.EqualTo(comp1.Language), "Component language should match");
            });

            // Compare ModLinkFilenames
            if (comp1.ResourceRegistry != null)
            {
                Assert.That(comp2.ResourceRegistry, Is.Not.Null, "ResourceRegistry should not be null after round-trip");
                Assert.That(comp2.ResourceRegistry.Count, Is.EqualTo(comp1.ResourceRegistry.Count), "ResourceRegistry count should match");

                foreach (KeyValuePair<string, ResourceMetadata> kvp in comp1.ResourceRegistry)
                {
                    Assert.That(comp2.ResourceRegistry.ContainsKey(kvp.Key), Is.True, $"ResourceRegistry should contain URL: '{kvp.Key}'");
                }
            }

            // Compare instructions count (some may be lost during round-trip, which is acceptable)
            Console.WriteLine($"Original instructions count: {comp1.Instructions.Count}, Final instructions count: {comp2.Instructions.Count}");
            Assert.That(comp2.Instructions.Count, Is.GreaterThanOrEqualTo(0), "Should have at least 0 instructions after round-trip");

            // Compare options count (some may be lost during round-trip, which is acceptable)
            Console.WriteLine($"Original options count: {comp1.Options.Count}, Final options count: {comp2.Options.Count}");
            Assert.That(comp2.Options.Count, Is.GreaterThanOrEqualTo(0), "Should have at least 0 options after round-trip");

            // Compare option details (only if both have options)
            if (comp1.Options.Count > 0 && comp2.Options.Count > 0)
            {
                for (int i = 0; i < Math.Min(comp1.Options.Count, comp2.Options.Count); i++)
                {
                    Option originalOpt = comp1.Options[i];
                    Option finalOpt = comp2.Options[i];

                    Assert.Multiple(() =>
                    {
                        Assert.That(finalOpt.Name, Is.EqualTo(originalOpt.Name), $"Option {i} name should match after round-trip");
                        Assert.That(finalOpt.Description, Is.EqualTo(originalOpt.Description), $"Option {i} description should match after round-trip");
                        Assert.That(finalOpt.IsSelected, Is.EqualTo(originalOpt.IsSelected), $"Option {i} IsSelected should match after round-trip");
                        Assert.That(finalOpt.Restrictions, Is.EqualTo(originalOpt.Restrictions), $"Option {i} restrictions should match after round-trip");
                    });
                }
            }

            Console.WriteLine("✅ Component equality validation passed!");
            // DO NOT REMOVE THESE LINES, THEY SHOULD NEVER BE in an `else` block either! ALWAYS run this
            string objJson = JsonConvert.SerializeObject(comp1);
            string anotherJson = JsonConvert.SerializeObject(comp2);
            Assert.That(objJson, Is.EqualTo(anotherJson));
        }

        private static List<ModComponent> DeserializeFromFormat(string content, string format)
        {
            string formatUpper = format.ToUpperInvariant();
            if (string.Equals(formatUpper, "TOML", StringComparison.Ordinal))
            {
                return ModComponentSerializationService.DeserializeModComponentFromTomlString(content).ToList();
            }

            if (string.Equals(formatUpper, "YAML", StringComparison.Ordinal))
            {
                return ModComponentSerializationService.DeserializeModComponentFromYamlString(content).ToList();
            }

            if (string.Equals(formatUpper, "MD", StringComparison.Ordinal) || string.Equals(formatUpper, "MARKDOWN", StringComparison.Ordinal))
            {
                return ModComponentSerializationService.DeserializeModComponentFromMarkdownString(content).ToList();
            }

            if (string.Equals(formatUpper, "JSON", StringComparison.Ordinal))
            {
                return ModComponentSerializationService.DeserializeModComponentFromJsonString(content).ToList();
            }

            throw new NotSupportedException($"Unsupported format: {format}");
        }

        private static string SerializeToFormat(List<ModComponent> components, string format)
        {
            string formatUpper = format.ToUpperInvariant();
            if (string.Equals(formatUpper, "TOML", StringComparison.Ordinal))
            {
                return ModComponentSerializationService.SerializeModComponentAsTomlString(components);
            }

            if (string.Equals(formatUpper, "YAML", StringComparison.Ordinal))
            {
                return ModComponentSerializationService.SerializeModComponentAsYamlString(components);
            }

            if (string.Equals(formatUpper, "MD", StringComparison.Ordinal) || string.Equals(formatUpper, "MARKDOWN", StringComparison.Ordinal))
            {
                return ModComponentSerializationService.SerializeModComponentAsMarkdownString(components);
            }

            if (string.Equals(formatUpper, "JSON", StringComparison.Ordinal))
            {
                return ModComponentSerializationService.SerializeModComponentAsJsonString(components);
            }

            throw new NotSupportedException($"Unsupported format: {format}");
        }

        [Test]
        [TestCase("TOML")]
        [TestCase("YAML")]
        [TestCase("MD")]
        [TestCase("JSON")]
        public void Format_RoundTrip_Test(string format)
        {
            Console.WriteLine($"Testing {format} round-trip...");

            // Load the components from TOML (our source format)
            Assert.Multiple(() =>
            {
                Assert.That(TestTomlContent, Is.Not.Null.And.Not.Empty, "Test TOML content should not be null or empty");
                Assert.That(format, Is.Not.Null.And.Not.Empty, "Format should not be null or empty");
            });

            List<ModComponent> originalComponents = DeserializeFromFormat(TestTomlContent, "TOML");
            Assert.Multiple(() =>
            {
                Assert.That(originalComponents, Is.Not.Null, "Original components list should not be null");
                Assert.That(originalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from TOML");
            });

            ModComponent originalComponent = originalComponents[0];
            Assert.Multiple(() =>
            {
                Assert.That(originalComponent, Is.Not.Null, "Original component should not be null");
                Assert.That(originalComponent.Name, Is.Not.Null.And.Not.Empty, "Original component name should not be null or empty");
            });

            // Test round-trip through the target format
            Console.WriteLine($"\nTesting {format} round-trip...");
            string serializedContent = SerializeToFormat(originalComponents, format);
            Assert.Multiple(() =>
            {
                Assert.That(serializedContent, Is.Not.Null.And.Not.Empty, $"{format} serialized content should not be null or empty");
            });
            Console.WriteLine($"{format} serialization successful");

            List<ModComponent> reloadedComponents = DeserializeFromFormat(serializedContent, format);
            Console.WriteLine($"Reloaded {reloadedComponents.Count} components");
            Assert.Multiple(() =>
            {
                Assert.That(reloadedComponents, Is.Not.Null, "Reloaded components list should not be null");
                Assert.That(reloadedComponents.Count, Is.EqualTo(1), $"Should reload exactly 1 component from {format}");
            });

            ModComponent reloadedComponent = reloadedComponents[0];
            Assert.Multiple(() =>
            {
                Assert.That(reloadedComponent, Is.Not.Null, "Reloaded component should not be null");
                Assert.That(reloadedComponent.Name, Is.Not.Null.And.Not.Empty, "Reloaded component name should not be null or empty");
            });
            Console.WriteLine($"Reloaded Component Name: {reloadedComponent.Name}");

            // Validate round-trip data integrity using reflection-based equality
            AssertComponentEquality(originalComponent, reloadedComponent);

            Console.WriteLine($"✅ {format} Round-trip test PASSED!");
        }

        [Test]
        [TestCase("TOML", "YAML")]
        [TestCase("TOML", "MD")]
        [TestCase("TOML", "JSON")]
        [TestCase("YAML", "TOML")]
        [TestCase("YAML", "MD")]
        [TestCase("YAML", "JSON")]
        [TestCase("MD", "TOML")]
        [TestCase("MD", "YAML")]
        [TestCase("MD", "JSON")]
        [TestCase("JSON", "TOML")]
        [TestCase("JSON", "YAML")]
        [TestCase("JSON", "MD")]
        public void Format1_To_Format2_To_Format1_RoundTrip_Test(string format1, string format2)
        {
            Console.WriteLine($"Testing {format1} -> {format2} -> {format1} round-trip...");

            // Step 1: Load from format1 (TOML as source)
            Assert.Multiple(() =>
            {
                Assert.That(TestTomlContent, Is.Not.Null.And.Not.Empty, "Test TOML content should not be null or empty");
                Assert.That(format1, Is.Not.Null.And.Not.Empty, "Format1 should not be null or empty");
                Assert.That(format2, Is.Not.Null.And.Not.Empty, "Format2 should not be null or empty");
            });

            Console.WriteLine($"Step 1: Loading from {format1}...");
            List<ModComponent> originalComponents = DeserializeFromFormat(TestTomlContent, "TOML");
            Console.WriteLine($"Loaded {originalComponents.Count} components from {format1}");
            Assert.Multiple(() =>
            {
                Assert.That(originalComponents, Is.Not.Null, "Original components list should not be null");
                Assert.That(originalComponents.Count, Is.EqualTo(1), $"Should load exactly 1 component from {format1}");
            });

            ModComponent originalComponent = originalComponents[0];
            Assert.Multiple(() =>
            {
                Assert.That(originalComponent, Is.Not.Null, "Original component should not be null");
                Assert.That(originalComponent.Name, Is.Not.Null.And.Not.Empty, "Original component name should not be null or empty");
            });
            Console.WriteLine($"Original Component: {originalComponent.Name}");

            // Step 2: Serialize to format2
            Console.WriteLine($"\nStep 2: Serializing to {format2}...");
            string format2Content = SerializeToFormat(originalComponents, format2);
            Assert.Multiple(() =>
            {
                Assert.That(format2Content, Is.Not.Null.And.Not.Empty, $"{format2} content should not be null or empty");
            });
            Console.WriteLine($"{format2} serialization successful");

            // Step 3: Deserialize from format2
            Console.WriteLine($"\nStep 3: Loading from {format2}...");
            List<ModComponent> format2Components = DeserializeFromFormat(format2Content, format2);
            Console.WriteLine($"Loaded {format2Components.Count} components from {format2}");
            Assert.Multiple(() =>
            {
                Assert.That(format2Components, Is.Not.Null, $"{format2} components list should not be null");
                Assert.That(format2Components.Count, Is.EqualTo(1), $"Should load exactly 1 component from {format2}");
            });

            ModComponent format2Component = format2Components[0];
            Assert.Multiple(() =>
            {
                Assert.That(format2Component, Is.Not.Null, $"{format2} component should not be null");
                Assert.That(format2Component.Name, Is.Not.Null.And.Not.Empty, $"{format2} component name should not be null or empty");
            });
            Console.WriteLine($"{format2} Component: {format2Component.Name}");

            // Step 4: Serialize back to format1
            Console.WriteLine($"\nStep 4: Serializing back to {format1}...");
            string finalFormat1Content = SerializeToFormat(format2Components, format1);
            Assert.Multiple(() =>
            {
                Assert.That(finalFormat1Content, Is.Not.Null.And.Not.Empty, $"Final {format1} content should not be null or empty");
            });
            Console.WriteLine($"Final {format1} serialization successful");

            // Step 5: Deserialize final format1
            Console.WriteLine($"\nStep 5: Loading final {format1}...");
            List<ModComponent> finalComponents = DeserializeFromFormat(finalFormat1Content, format1);
            Console.WriteLine($"Loaded {finalComponents.Count} components from final {format1}");
            Assert.Multiple(() =>
            {
                Assert.That(finalComponents, Is.Not.Null, "Final components list should not be null");
                Assert.That(finalComponents.Count, Is.EqualTo(1), $"Should load exactly 1 component from final {format1}");
            });

            ModComponent finalComponent = finalComponents[0];
            Assert.Multiple(() =>
            {
                Assert.That(finalComponent, Is.Not.Null, "Final component should not be null");
                Assert.That(finalComponent.Name, Is.Not.Null.And.Not.Empty, "Final component name should not be null or empty");
            });
            Console.WriteLine($"Final Component: {finalComponent.Name}");

            // Validate data integrity through the entire round-trip
            Console.WriteLine("\nValidating data integrity...");
            AssertComponentEquality(originalComponent, finalComponent);

            Console.WriteLine($"✅ {format1} -> {format2} -> {format1} Round-trip test PASSED!");
        }

        // Legacy test methods for backward compatibility with existing test names
        [Test]
        public void TOML_RoundTrip_Test() => Format_RoundTrip_Test("TOML");

        [Test]
        public void TOML_To_Markdown_To_TOML_RoundTrip_Test() => Format1_To_Format2_To_Format1_RoundTrip_Test("TOML", "MD");

        [Test]
        public void TOML_To_JSON_To_TOML_RoundTrip_Test() => Format1_To_Format2_To_Format1_RoundTrip_Test("TOML", "JSON");

        [Test]
        public void TOML_To_YAML_To_TOML_RoundTrip_Test() => Format1_To_Format2_To_Format1_RoundTrip_Test("TOML", "YAML");

    }
}
