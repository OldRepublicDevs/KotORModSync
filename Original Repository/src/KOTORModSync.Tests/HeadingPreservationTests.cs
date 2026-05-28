// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    /// <summary>
    /// Comprehensive tests for Heading field preservation across all serialization format combinations.
    /// These tests ensure that Heading is correctly preserved during round-trip serialization/deserialization
    /// through TOML, YAML, JSON, and Markdown formats.
    /// </summary>
    [TestFixture]
    public class HeadingPreservationTests
    {
        private static readonly string TestTomlWithHeading = @"[[thisMod]]
Guid = ""987a0d17-c596-49af-ba28-851232455253""
Name = ""Example Dialogue Enhancement""
Heading = ""Example Dialogue Enhancement""
Author = ""Test Author""
Category = [""Immersion""]
Language = [""NO""]

[[thisMod.Instructions]]
Guid = ""e6d0dbb7-75f7-4886-a4a5-e7eea85dac1c""
Action = ""Extract""
Source = [""<<modDirectory>>\\Example_Dialogue_Enhancement*.7z""]";

        private static readonly string TestTomlWithoutHeading = @"[[thisMod]]
Guid = ""987a0d17-c596-49af-ba28-851232455253""
Name = ""Example Dialogue Enhancement""
Author = ""Test Author""
Category = [""Immersion""]
Language = [""NO""]

[[thisMod.Instructions]]
Guid = ""e6d0dbb7-75f7-4886-a4a5-e7eea85dac1c""
Action = ""Extract""
Source = [""<<modDirectory>>\\Example_Dialogue_Enhancement*.7z""]";

        [Test]
        public void TOML_WithHeading_To_JSON_To_TOML_PreservesHeading()
        {
            // Arrange
            var original = ModComponentSerializationService.DeserializeModComponentFromTomlString(TestTomlWithHeading).First();

            // Act
            string json = ModComponentSerializationService.SerializeModComponentAsJsonString(new[] { original });
            var jsonComponent = ModComponentSerializationService.DeserializeModComponentFromJsonString(json).First();
            string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(new[] { jsonComponent });
            var final = ModComponentSerializationService.DeserializeModComponentFromTomlString(toml).First();

            // Assert
            Assert.That(final.Heading, Is.EqualTo(original.Heading), "Heading should be preserved through TOML->JSON->TOML round-trip");
            Assert.That(final.Name, Is.EqualTo(original.Name), "Name should be preserved");
        }

        [Test]
        public void TOML_WithoutHeading_To_Markdown_To_TOML_PreservesNameAsHeading()
        {
            // Arrange
            var original = ModComponentSerializationService.DeserializeModComponentFromTomlString(TestTomlWithoutHeading).First();
            Assert.That(original.Heading, Is.Empty, "Original should not have Heading");

            // Act
            string markdown = ModComponentSerializationService.SerializeModComponentAsMarkdownString(new[] { original });
            var markdownComponent = ModComponentSerializationService.DeserializeModComponentFromMarkdownString(markdown).First();
            string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(new[] { markdownComponent });
            var final = ModComponentSerializationService.DeserializeModComponentFromTomlString(toml).First();

            // Assert
            // When Heading is empty, Markdown uses Name for heading text, which should be extracted and preserved
            Assert.That(final.Heading, Is.Not.Empty, "Heading should be extracted from Markdown text");
            Assert.That(final.Heading, Is.EqualTo(original.Name), "Heading should equal Name when extracted from Markdown");
            Assert.That(final.Name, Is.EqualTo(original.Name), "Name should be preserved");
        }

        [Test]
        public void TOML_WithHeading_To_Markdown_To_TOML_PreservesHeading()
        {
            // Arrange
            var original = ModComponentSerializationService.DeserializeModComponentFromTomlString(TestTomlWithHeading).First();
            Assert.That(original.Heading, Is.Not.Empty, "Original should have Heading");

            // Act
            string markdown = ModComponentSerializationService.SerializeModComponentAsMarkdownString(new[] { original });
            var markdownComponent = ModComponentSerializationService.DeserializeModComponentFromMarkdownString(markdown).First();
            string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(new[] { markdownComponent });
            var final = ModComponentSerializationService.DeserializeModComponentFromTomlString(toml).First();

            // Assert
            Assert.That(final.Heading, Is.EqualTo(original.Heading), "Heading should be preserved through TOML->Markdown->TOML round-trip");
            Assert.That(final.Name, Is.EqualTo(original.Name), "Name should be preserved");
        }

        [Test]
        public void TOML_WithoutHeading_To_JSON_To_TOML_HeadingRemainsEmpty()
        {
            // Arrange
            var original = ModComponentSerializationService.DeserializeModComponentFromTomlString(TestTomlWithoutHeading).First();
            Assert.That(original.Heading, Is.Empty, "Original should not have Heading");

            // Act
            string json = ModComponentSerializationService.SerializeModComponentAsJsonString(new[] { original });
            var jsonComponent = ModComponentSerializationService.DeserializeModComponentFromJsonString(json).First();
            string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(new[] { jsonComponent });
            var final = ModComponentSerializationService.DeserializeModComponentFromTomlString(toml).First();

            // Assert
            // JSON/TOML round-trip should preserve empty Heading as empty
            Assert.That(final.Heading, Is.Empty, "Heading should remain empty through TOML->JSON->TOML round-trip");
            Assert.That(final.Name, Is.EqualTo(original.Name), "Name should be preserved");
        }

        [Test]
        public void JSON_WithHeading_To_Markdown_To_JSON_PreservesHeading()
        {
            // Arrange
            var component = new ModComponent
            {
                Guid = Guid.Parse("987a0d17-c596-49af-ba28-851232455253"),
                Name = "Test Mod",
                Heading = "Test Mod Heading",
                Author = "Test Author",
            };

            // Act
            string json1 = ModComponentSerializationService.SerializeModComponentAsJsonString(new[] { component });
            var jsonComponent = ModComponentSerializationService.DeserializeModComponentFromJsonString(json1).First();
            string markdown = ModComponentSerializationService.SerializeModComponentAsMarkdownString(new[] { jsonComponent });
            var markdownComponent = ModComponentSerializationService.DeserializeModComponentFromMarkdownString(markdown).First();
            string json2 = ModComponentSerializationService.SerializeModComponentAsJsonString(new[] { markdownComponent });
            var final = ModComponentSerializationService.DeserializeModComponentFromJsonString(json2).First();

            // Assert
            Assert.That(final.Heading, Is.EqualTo(component.Heading), "Heading should be preserved through JSON->Markdown->JSON round-trip");
            Assert.That(final.Name, Is.EqualTo(component.Name), "Name should be preserved");
        }

        [Test]
        public void JSON_WithoutHeading_To_Markdown_To_JSON_PreservesNameAsHeading()
        {
            // Arrange
            var component = new ModComponent
            {
                Guid = Guid.Parse("987a0d17-c596-49af-ba28-851232455253"),
                Name = "Test Mod",
                Heading = string.Empty,
                Author = "Test Author",
            };

            // Act
            string json1 = ModComponentSerializationService.SerializeModComponentAsJsonString(new[] { component });
            var jsonComponent = ModComponentSerializationService.DeserializeModComponentFromJsonString(json1).First();
            string markdown = ModComponentSerializationService.SerializeModComponentAsMarkdownString(new[] { jsonComponent });
            var markdownComponent = ModComponentSerializationService.DeserializeModComponentFromMarkdownString(markdown).First();
            string json2 = ModComponentSerializationService.SerializeModComponentAsJsonString(new[] { markdownComponent });
            var final = ModComponentSerializationService.DeserializeModComponentFromJsonString(json2).First();

            // Assert
            // When Heading is empty, Markdown uses Name for heading text, which should be extracted and preserved
            Assert.That(final.Heading, Is.Not.Empty, "Heading should be extracted from Markdown text");
            Assert.That(final.Heading, Is.EqualTo(component.Name), "Heading should equal Name when extracted from Markdown");
            Assert.That(final.Name, Is.EqualTo(component.Name), "Name should be preserved");
        }

        [Test]
        [TestCase("TOML", "YAML")]
        [TestCase("TOML", "JSON")]
        [TestCase("TOML", "MD")]
        [TestCase("YAML", "TOML")]
        [TestCase("YAML", "JSON")]
        [TestCase("YAML", "MD")]
        [TestCase("JSON", "TOML")]
        [TestCase("JSON", "YAML")]
        [TestCase("JSON", "MD")]
        [TestCase("MD", "TOML")]
        [TestCase("MD", "YAML")]
        [TestCase("MD", "JSON")]
        public void HeadingPreservation_AllFormatCombinations_WithHeading(string format1, string format2)
        {
            // Arrange
            var component = new ModComponent
            {
                Guid = Guid.Parse("987a0d17-c596-49af-ba28-851232455253"),
                Name = "Test Mod",
                Heading = "Test Mod Heading",
                Author = "Test Author",
                Category = new List<string> { "Immersion" },
                Language = new List<string> { "NO" },
            };

            // Act
            string serialized1 = SerializeToFormat(component, format1);
            var deserialized1 = DeserializeFromFormat(serialized1, format1).First();
            string serialized2 = SerializeToFormat(deserialized1, format2);
            var deserialized2 = DeserializeFromFormat(serialized2, format2).First();
            string serialized3 = SerializeToFormat(deserialized2, format1);
            var final = DeserializeFromFormat(serialized3, format1).First();

            // Assert
            Assert.That(final.Heading, Is.EqualTo(component.Heading),
                $"Heading should be preserved through {format1}->{format2}->{format1} round-trip");
            Assert.That(final.Name, Is.EqualTo(component.Name), "Name should be preserved");
        }

        [Test]
        [TestCase("TOML", "YAML")]
        [TestCase("TOML", "JSON")]
        [TestCase("YAML", "TOML")]
        [TestCase("YAML", "JSON")]
        [TestCase("JSON", "TOML")]
        [TestCase("JSON", "YAML")]
        public void HeadingPreservation_AllFormatCombinations_WithoutHeading_NonMarkdown(string format1, string format2)
        {
            // Arrange
            var component = new ModComponent
            {
                Guid = Guid.Parse("987a0d17-c596-49af-ba28-851232455253"),
                Name = "Test Mod",
                Heading = string.Empty,
                Author = "Test Author",
                Category = new List<string> { "Immersion" },
                Language = new List<string> { "NO" },
            };

            // Act
            string serialized1 = SerializeToFormat(component, format1);
            var deserialized1 = DeserializeFromFormat(serialized1, format1).First();
            string serialized2 = SerializeToFormat(deserialized1, format2);
            var deserialized2 = DeserializeFromFormat(serialized2, format2).First();
            string serialized3 = SerializeToFormat(deserialized2, format1);
            var final = DeserializeFromFormat(serialized3, format1).First();

            // Assert
            // For non-Markdown formats, empty Heading should remain empty
            Assert.That(final.Heading, Is.Empty,
                $"Heading should remain empty through {format1}->{format2}->{format1} round-trip (non-Markdown)");
            Assert.That(final.Name, Is.EqualTo(component.Name), "Name should be preserved");
        }

        [Test]
        [TestCase("TOML", "MD")]
        [TestCase("YAML", "MD")]
        [TestCase("JSON", "MD")]
        [TestCase("MD", "TOML")]
        [TestCase("MD", "YAML")]
        [TestCase("MD", "JSON")]
        public void HeadingPreservation_AllFormatCombinations_WithoutHeading_WithMarkdown(string format1, string format2)
        {
            // Arrange
            var component = new ModComponent
            {
                Guid = Guid.Parse("987a0d17-c596-49af-ba28-851232455253"),
                Name = "Test Mod",
                Heading = string.Empty,
                Author = "Test Author",
                Category = new List<string> { "Immersion" },
                Language = new List<string> { "NO" },
            };

            // Act
            string serialized1 = SerializeToFormat(component, format1);
            var deserialized1 = DeserializeFromFormat(serialized1, format1).First();
            string serialized2 = SerializeToFormat(deserialized1, format2);
            var deserialized2 = DeserializeFromFormat(serialized2, format2).First();
            string serialized3 = SerializeToFormat(deserialized2, format1);
            var final = DeserializeFromFormat(serialized3, format1).First();

            // Assert
            // When Markdown is involved and Heading is empty, Markdown uses Name for heading text
            // This should be extracted and preserved, so Heading should equal Name
            if (format1 == "MD" || format2 == "MD")
            {
                Assert.That(final.Heading, Is.Not.Empty,
                    $"Heading should be extracted from Markdown when going through {format1}->{format2}->{format1}");
                Assert.That(final.Heading, Is.EqualTo(component.Name),
                    $"Heading should equal Name when extracted from Markdown through {format1}->{format2}->{format1}");
            }
            else
            {
                Assert.That(final.Heading, Is.Empty,
                    $"Heading should remain empty through {format1}->{format2}->{format1} round-trip");
            }
            Assert.That(final.Name, Is.EqualTo(component.Name), "Name should be preserved");
        }

        private static string SerializeToFormat(ModComponent component, string format)
        {
            string formatUpper = format.ToUpperInvariant();
            return formatUpper switch
            {
                "TOML" => ModComponentSerializationService.SerializeModComponentAsTomlString(new[] { component }),
                "YAML" => ModComponentSerializationService.SerializeModComponentAsYamlString(new[] { component }),
                "JSON" => ModComponentSerializationService.SerializeModComponentAsJsonString(new[] { component }),
                "MD" or "MARKDOWN" => ModComponentSerializationService.SerializeModComponentAsMarkdownString(new[] { component }),
                _ => throw new ArgumentException($"Unknown format: {format}", nameof(format)),
            };
        }

        private static List<ModComponent> DeserializeFromFormat(string content, string format)
        {
            string formatUpper = format.ToUpperInvariant();
            return formatUpper switch
            {
                "TOML" => ModComponentSerializationService.DeserializeModComponentFromTomlString(content).ToList(),
                "YAML" => ModComponentSerializationService.DeserializeModComponentFromYamlString(content).ToList(),
                "JSON" => ModComponentSerializationService.DeserializeModComponentFromJsonString(content).ToList(),
                "MD" or "MARKDOWN" => ModComponentSerializationService.DeserializeModComponentFromMarkdownString(content).ToList(),
                _ => throw new ArgumentException($"Unknown format: {format}", nameof(format)),
            };
        }
    }
}

