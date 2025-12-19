// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class ValidationCommentsTests
    {
        [Test]
        public void ComponentValidationContext_AddsComponentIssue()
        {
            // Arrange
            var context = new ComponentValidationContext();
            var guid = Guid.NewGuid();

            // Act
            context.AddModComponentIssue(guid, "Test issue 1");
            context.AddModComponentIssue(guid, "Test issue 2");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(context, Is.Not.Null, "Validation context should not be null");
                Assert.That(guid, Is.Not.EqualTo(Guid.Empty), "GUID should not be empty");
            });

            IReadOnlyList<string> issues = context.GetComponentIssues(guid);
            Assert.Multiple(() =>
            {
                Assert.That(issues, Is.Not.Null, "Issues list should not be null");
                Assert.That(issues.Count, Is.EqualTo(2), "Should have exactly 2 issues");
                Assert.That(issues[0], Is.Not.Null.And.Not.Empty, "First issue should not be null or empty");
                Assert.That(issues[0], Is.EqualTo("Test issue 1"), "First issue should match");
                Assert.That(issues[1], Is.Not.Null.And.Not.Empty, "Second issue should not be null or empty");
                Assert.That(issues[1], Is.EqualTo("Test issue 2"), "Second issue should match");
                Assert.That(context.HasIssues(guid), Is.True, "Context should report issues for this GUID");
            });
        }

        [Test]
        public void ComponentValidationContext_AddsInstructionIssue()
        {
            // Arrange
            var context = new ComponentValidationContext();
            var componentGuid = Guid.NewGuid();
            int instructionIndex = 0;

            // Act
            context.AddInstructionIssue(componentGuid, instructionIndex, "Instruction error");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(context, Is.Not.Null, "Validation context should not be null");
                Assert.That(componentGuid, Is.Not.EqualTo(Guid.Empty), "Component GUID should not be empty");
                Assert.That(instructionIndex, Is.GreaterThanOrEqualTo(0), "Instruction index should be non-negative");
            });

            List<string> issues = context.GetInstructionIssues(componentGuid, instructionIndex);
            Assert.Multiple(() =>
            {
                Assert.That(issues, Is.Not.Null, "Issues list should not be null");
                Assert.That(issues, Has.Count.EqualTo(1), "Should have exactly 1 issue");
                Assert.That(issues[0], Is.Not.Null.And.Not.Empty, "Issue should not be null or empty");
                Assert.That(issues[0], Is.EqualTo("Instruction error"), "Issue should match");
                Assert.That(context.HasInstructionIssues(componentGuid, instructionIndex), Is.True, "Context should report issues for this instruction");
            });
        }

        [Test]
        public void ComponentValidationContext_AddsUrlFailure()
        {
            // Arrange
            var context = new ComponentValidationContext();
            string url = "https://deadlystream.com/files/file/1234";

            // Act
            context.AddUrlFailure(url, "404 Not Found");
            context.AddUrlFailure(url, "Download timeout");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(context, Is.Not.Null, "Validation context should not be null");
                Assert.That(url, Is.Not.Null.And.Not.Empty, "URL should not be null or empty");
            });

            List<string> failures = context.GetUrlFailures(url);
            Assert.Multiple(() =>
            {
                Assert.That(failures, Is.Not.Null, "Failures list should not be null");
                Assert.That(failures.Count, Is.EqualTo(2), "Should have exactly 2 failures");
                Assert.That(failures[0], Is.Not.Null.And.Not.Empty, "First failure should not be null or empty");
                Assert.That(failures[0], Is.EqualTo("404 Not Found"), "First failure should match");
                Assert.That(failures[1], Is.Not.Null.And.Not.Empty, "Second failure should not be null or empty");
                Assert.That(failures[1], Is.EqualTo("Download timeout"), "Second failure should match");
                Assert.That(context.HasUrlFailures(url), Is.True, "Context should report failures for this URL");
            });
        }

        [Test]
        public void TomlSerialization_IncludesComponentValidationComments()
        {
            // Arrange
            var component = new ModComponent
            {
                Guid = Guid.NewGuid(),
                Name = "Test Component",
                Author = "Test Author",
            };

            var context = new ComponentValidationContext();
            context.AddModComponentIssue(component.Guid, "Missing required files");
            context.AddModComponentIssue(component.Guid, "Invalid instruction format");

            // Act
            string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(
                new List<ModComponent> { component },
                context);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(component, Is.Not.Null, "Component should not be null");
                Assert.That(context, Is.Not.Null, "Validation context should not be null");
                Assert.That(toml, Is.Not.Null.And.Not.Empty, "TOML string should not be null or empty");
                Assert.That(toml, Does.Contain("# VALIDATION ISSUES:"), "TOML should contain validation issues header");
                Assert.That(toml, Does.Contain("# Missing required files"), "TOML should contain first validation issue");
                Assert.That(toml, Does.Contain("# Invalid instruction format"), "TOML should contain second validation issue");
            });
        }

        [Test]
        public void TomlSerialization_IncludesUrlFailureComments()
        {
            // Arrange
            var component = new ModComponent
            {
                Guid = Guid.NewGuid(),
                Name = "Test Component",
                ResourceRegistry = new Dictionary<string, ResourceMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    { "https://example.com/mod.zip", new ResourceMetadata { } },
                },
            };

            var context = new ComponentValidationContext();
            context.AddUrlFailure("https://example.com/mod.zip", "Failed to resolve filename");

            // Act
            string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(
                new List<ModComponent> { component },
                context);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(component, Is.Not.Null, "Component should not be null");
                Assert.That(component.ResourceRegistry, Is.Not.Null, "Resource registry should not be null");
                Assert.That(context, Is.Not.Null, "Validation context should not be null");
                Assert.That(toml, Is.Not.Null.And.Not.Empty, "TOML string should not be null or empty");
                Assert.That(toml, Does.Contain("# URL RESOLUTION FAILURE: https://example.com/mod.zip"), "TOML should contain URL resolution failure header");
                Assert.That(toml, Does.Contain("# Failed to resolve filename"), "TOML should contain URL resolution failure message");
            });
        }

        [Test]
        public void TomlSerialization_IncludesInstructionValidationComments()
        {
            // Arrange
            var component = new ModComponent
            {
                Guid = Guid.NewGuid(),
                Name = "Test Component",
            };

            var instruction = new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { "<<modDirectory>>\\test.2da" },
                Destination = "<<kotorDirectory>>\\Override",
            };
            instruction.SetParentComponent(component);
            component.Instructions.Add(instruction);

            var context = new ComponentValidationContext();
            context.AddInstructionIssue(component.Guid, 0, "MoveFile: Source file does not exist");

            // Act
            string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(
                new List<ModComponent> { component },
                context);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(component, Is.Not.Null, "Component should not be null");
                Assert.That(instruction, Is.Not.Null, "Instruction should not be null");
                Assert.That(component.Instructions, Is.Not.Null, "Instructions list should not be null");
                Assert.That(component.Instructions, Has.Count.GreaterThan(0), "Component should have at least one instruction");
                Assert.That(context, Is.Not.Null, "Validation context should not be null");
                Assert.That(toml, Is.Not.Null.And.Not.Empty, "TOML string should not be null or empty");
                Assert.That(toml, Does.Contain("# INSTRUCTION VALIDATION ISSUES:"), "TOML should contain instruction validation issues header");
                Assert.That(toml, Does.Contain("# MoveFile: Source file does not exist"), "TOML should contain instruction validation issue message");
            });
        }

        [Test]
        public void YamlSerialization_IncludesValidationWarnings()
        {
            // Arrange
            var component = new ModComponent
            {
                Guid = Guid.NewGuid(),
                Name = "Test Component",
            };

            var instruction = new Instruction
            {
                Action = Instruction.ActionType.Extract,
                Source = new List<string> { "<<modDirectory>>\\missing.zip" },
            };
            instruction.SetParentComponent(component);
            component.Instructions.Add(instruction);

            var context = new ComponentValidationContext();
            context.AddModComponentIssue(component.Guid, "Component validation issue");
            context.AddInstructionIssue(component.Guid, 0, "ExtractArchive: Archive does not exist");

            // Act
            string yaml = ModComponentSerializationService.SerializeModComponentAsYamlString(
                new List<ModComponent> { component },
                context);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(component, Is.Not.Null, "Component should not be null");
                Assert.That(instruction, Is.Not.Null, "Instruction should not be null");
                Assert.That(context, Is.Not.Null, "Validation context should not be null");
                Assert.That(yaml, Is.Not.Null.And.Not.Empty, "YAML string should not be null or empty");
                Assert.That(yaml, Does.Contain("# VALIDATION ISSUES:"), "YAML should contain validation issues header");
                Assert.That(yaml, Does.Contain("# Component validation issue"), "YAML should contain component validation issue");
                // Note: YAML serialization does not render instruction validation warnings as comments
            });
        }

        [Test]
        public void JsonSerialization_IncludesValidationFields()
        {
            // Arrange
            var component = new ModComponent
            {
                Guid = Guid.NewGuid(),
                Name = "Test Component",
            };

            var context = new ComponentValidationContext();
            context.AddModComponentIssue(component.Guid, "JSON validation test");
            context.AddUrlFailure("https://example.com/test.zip", "Resolution failed");

            component.ResourceRegistry = new Dictionary<string, ResourceMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                { "https://example.com/test.zip", new ResourceMetadata { } },
            };

            // Act
            string json = ModComponentSerializationService.SerializeModComponentAsJsonString(
                new List<ModComponent> { component },
                context);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(component, Is.Not.Null, "Component should not be null");
                Assert.That(component.ResourceRegistry, Is.Not.Null, "Resource registry should not be null");
                Assert.That(context, Is.Not.Null, "Validation context should not be null");
                Assert.That(json, Is.Not.Null.And.Not.Empty, "JSON string should not be null or empty");
                Assert.That(json, Does.Contain("_validationWarnings"), "JSON should contain validation warnings field");
                Assert.That(json, Does.Contain("JSON validation test"), "JSON should contain validation warning message");
                Assert.That(json, Does.Contain("_urlResolutionFailures"), "JSON should contain URL resolution failures field");
                Assert.That(json, Does.Contain("Resolution failed"), "JSON should contain URL resolution failure message");
            });
        }

        [Test]
        public void MarkdownSerialization_IncludesValidationWarnings()
        {
            // Arrange
            var component = new ModComponent
            {
                Guid = Guid.NewGuid(),
                Name = "Test Component",
                Description = "Test description",
            };

            var context = new ComponentValidationContext();
            context.AddModComponentIssue(component.Guid, "Markdown test warning");

            // Act
            string markdown = ModComponentSerializationService.SerializeModComponentAsMarkdownString(
                new List<ModComponent> { component },
                context);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(component, Is.Not.Null, "Component should not be null");
                Assert.That(context, Is.Not.Null, "Validation context should not be null");
                Assert.That(markdown, Is.Not.Null.And.Not.Empty, "Markdown string should not be null or empty");
                Assert.That(markdown, Does.Contain("> **⚠️ VALIDATION WARNINGS:**"), "Markdown should contain validation warnings header");
                Assert.That(markdown, Does.Contain("> - Markdown test warning"), "Markdown should contain validation warning message");
            });
        }

        [Test]
        public void Serialization_WorksWithoutValidationContext()
        {
            // Arrange
            var component = new ModComponent
            {
                Guid = Guid.NewGuid(),
                Name = "Test Component",
            };

            // Act & Assert - should not throw
            Assert.Multiple(() =>
            {
                Assert.That(component, Is.Not.Null, "Component should not be null");
            });

            Assert.DoesNotThrow(() =>
            {
                string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(
                    new List<ModComponent> { component },
                    validationContext: null);
                Assert.Multiple(() =>
                {
                    Assert.That(toml, Is.Not.Null.And.Not.Empty, "TOML string should not be null or empty");
                    Assert.That(toml, Does.Not.Contain("# VALIDATION"), "TOML should not contain validation comments when context is null");
                });
            });
        }

        [Test]
        public void ValidationContext_CaseInsensitiveUrlMatching()
        {
            // Arrange
            var context = new ComponentValidationContext();

            // Act
            context.AddUrlFailure("https://Example.COM/Mod.ZIP", "Test error");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(context, Is.Not.Null, "Validation context should not be null");
            });

            List<string> failures1 = context.GetUrlFailures("https://example.com/mod.zip");
            List<string> failures2 = context.GetUrlFailures("https://EXAMPLE.COM/MOD.ZIP");

            Assert.Multiple(() =>
            {
                Assert.That(failures1, Is.Not.Null, "First failures list should not be null");
                Assert.That(failures1.Count, Is.EqualTo(1), "First failures list should contain exactly 1 failure");
                Assert.That(failures1[0], Is.EqualTo("Test error"), "First failure should match");
                Assert.That(failures2, Is.Not.Null, "Second failures list should not be null");
                Assert.That(failures2.Count, Is.EqualTo(1), "Second failures list should contain exactly 1 failure (case-insensitive)");
                Assert.That(failures2[0], Is.EqualTo("Test error"), "Second failure should match");
            });
        }

        [Test]
        public void ValidationContext_MultipleComponentsWithIssues()
        {
            // Arrange
            var comp1 = new ModComponent { Guid = Guid.NewGuid(), Name = "Mod 1" };
            var comp2 = new ModComponent { Guid = Guid.NewGuid(), Name = "Mod 2" };

            var context = new ComponentValidationContext();
            context.AddModComponentIssue(comp1.Guid, "Mod 1 issue");
            context.AddModComponentIssue(comp2.Guid, "Mod 2 issue");

            // Act
            string toml = ModComponentSerializationService.SerializeModComponentAsTomlString(
                new List<ModComponent> { comp1, comp2 },
                context);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(comp1, Is.Not.Null, "First component should not be null");
                Assert.That(comp2, Is.Not.Null, "Second component should not be null");
                Assert.That(context, Is.Not.Null, "Validation context should not be null");
                Assert.That(toml, Is.Not.Null.And.Not.Empty, "TOML string should not be null or empty");
                Assert.That(toml, Does.Contain("# Mod 1 issue"), "TOML should contain first component issue");
                Assert.That(toml, Does.Contain("# Mod 2 issue"), "TOML should contain second component issue");
            });
        }
    }
}
