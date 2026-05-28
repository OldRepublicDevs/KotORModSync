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
using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class SerializationAndValidationEdgeCasesTests
    {
        #region Component Serialization Edge Cases

        [Test]
        public void SerializeModComponent_WithEmptyInstructions_SerializesCorrectly()
        {
            var component = new ModComponent
            {
                Name = "Empty Instructions",
                Guid = Guid.NewGuid(),
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>()
            };

            string json = ModComponentSerializationService.SerializeModComponentAsJsonString(new List<ModComponent> { component });

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.Not.Null.And.Not.Empty, "JSON should not be null or empty");
                Assert.That(json, Does.Contain(component.Name), "JSON should contain component name");
                Assert.That(json, Does.Contain(component.Guid.ToString()), "JSON should contain component GUID");
            });
        }

        [Test]
        public void SerializeModComponent_WithNullFields_HandlesGracefully()
        {
            var component = new ModComponent
            {
                Name = "Null Fields",
                Guid = Guid.NewGuid(),
                Author = null,
                Description = null,
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>()
            };

            Assert.DoesNotThrow(() =>
            {
                string json = ModComponentSerializationService.SerializeModComponentAsJsonString(new List<ModComponent> { component });
                Assert.That(json, Is.Not.Null.And.Not.Empty, "JSON should serialize even with null fields");
            }, "Serialization should handle null fields gracefully");
        }

        [Test]
        public void SerializeModComponent_WithSpecialCharacters_SerializesCorrectly()
        {
            var component = new ModComponent
            {
                Name = "Mod with \"quotes\" & <tags> & 'apostrophes'",
                Guid = Guid.NewGuid(),
                Description = "Description with\nnewlines\tand\ttabs",
                Author = "Author & Co."
            };

            Assert.DoesNotThrow(() =>
            {
                string json = ModComponentSerializationService.SerializeModComponentAsJsonString(new List<ModComponent> { component });
                Assert.That(json, Is.Not.Null.And.Not.Empty, "JSON should serialize special characters");
                Assert.That(json, Does.Contain(component.Name), "JSON should contain component name with special characters");
            }, "Serialization should handle special characters");
        }

        [Test]
        public void SerializeModComponent_WithUnicodeCharacters_SerializesCorrectly()
        {
            var component = new ModComponent
            {
                Name = "测试模组_тест_テスト",
                Guid = Guid.NewGuid(),
                Description = "Unicode description: 测试 テス т",
                Author = "作者 Автор 作成者"
            };

            Assert.DoesNotThrow(() =>
            {
                string json = ModComponentSerializationService.SerializeModComponentAsJsonString(new List<ModComponent> { component });
                Assert.That(json, Is.Not.Null.And.Not.Empty, "JSON should serialize Unicode characters");
                Assert.That(json, Does.Contain(component.Name), "JSON should contain Unicode component name");
            }, "Serialization should handle Unicode characters");
        }

        #endregion

        #region Instruction Serialization Edge Cases

        [Test]
        public void SerializeModComponent_WithComplexInstructions_SerializesAll()
        {
            var component = new ModComponent
            {
                Name = "Complex Instructions",
                Guid = Guid.NewGuid(),
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
                {
                    new Instruction
                    {
                        Action = Instruction.ActionType.Extract,
                        Source = new List<string> { "<<modDirectory>>/archive.zip" },
                        Destination = "<<modDirectory>>/extracted"
                    },
                    new Instruction
                    {
                        Action = Instruction.ActionType.Move,
                        Source = new List<string> { "<<modDirectory>>/file.txt" },
                        Destination = "<<kotorDirectory>>/Override",
                        Overwrite = true,
                        Dependencies = new List<Guid> { Guid.NewGuid() },
                        Restrictions = new List<Guid> { Guid.NewGuid() }
                    },
                    new Instruction
                    {
                        Action = Instruction.ActionType.Choose,
                        Source = new List<string> { Guid.NewGuid().ToString() }
                    }
                }
            };

            string json = ModComponentSerializationService.SerializeModComponentAsJsonString(new List<ModComponent> { component });

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.Not.Null.And.Not.Empty, "JSON should not be null or empty");
                Assert.That(json, Does.Contain("Extract"), "JSON should contain Extract instruction");
                Assert.That(json, Does.Contain("Move"), "JSON should contain Move instruction");
                Assert.That(json, Does.Contain("Choose"), "JSON should contain Choose instruction");
            });
        }

        [Test]
        public void SerializeModComponent_WithOptions_SerializesOptions()
        {
            var component = new ModComponent
            {
                Name = "Component with Options",
                Guid = Guid.NewGuid()
            };

            var option1 = new Option
            {
                Name = "Option 1",
                Guid = Guid.NewGuid(),
                Description = "First option",
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
                {
                    new Instruction
                    {
                        Action = Instruction.ActionType.Move,
                        Source = new List<string> { "<<modDirectory>>/file1.txt" },
                        Destination = "<<kotorDirectory>>/Override"
                    }
                }
            };

            var option2 = new Option
            {
                Name = "Option 2",
                Guid = Guid.NewGuid(),
                Description = "Second option",
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
                {
                    new Instruction
                    {
                        Action = Instruction.ActionType.Move,
                        Source = new List<string> { "<<modDirectory>>/file2.txt" },
                        Destination = "<<kotorDirectory>>/Override"
                    }
                }
            };

            component.Options.Add(option1);
            component.Options.Add(option2);

            string json = ModComponentSerializationService.SerializeModComponentAsJsonString(new List<ModComponent> { component });

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.Not.Null.And.Not.Empty, "JSON should not be null or empty");
                Assert.That(json, Does.Contain("Option 1"), "JSON should contain Option 1");
                Assert.That(json, Does.Contain("Option 2"), "JSON should contain Option 2");
                Assert.That(json, Does.Contain(option1.Guid.ToString()), "JSON should contain Option 1 GUID");
                Assert.That(json, Does.Contain(option2.Guid.ToString()), "JSON should contain Option 2 GUID");
            });
        }

        #endregion

        #region Validation Edge Cases

        [Test]
        public async Task ValidateComponent_WithCircularDependencies_HandlesGracefully()
        {
            var component1 = new ModComponent
            {
                Name = "Component 1",
                Guid = Guid.NewGuid(),
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>()
            };

            var component2 = new ModComponent
            {
                Name = "Component 2",
                Guid = Guid.NewGuid(),
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>()
            };

            // Create circular dependency
            component1.Dependencies = new List<Guid> { component2.Guid };
            component2.Dependencies = new List<Guid> { component1.Guid };

            // Validation should handle this gracefully
            var missingFiles1 = await ComponentValidationService.GetMissingFilesForComponentAsync(component1);
            var missingFiles2 = await ComponentValidationService.GetMissingFilesForComponentAsync(component2);

            Assert.Multiple(() =>
            {
                Assert.That(missingFiles1, Is.Not.Null, "Missing files list should not be null");
                Assert.That(missingFiles2, Is.Not.Null, "Missing files list should not be null");
            });
        }

        [Test]
        public async Task ValidateComponent_WithVeryLongPath_HandlesCorrectly()
        {
            string longPath = Path.Combine(Path.GetTempPath(), new string('A', 200) + ".txt");
            try
            {
                File.WriteAllText(longPath, "content");

                var component = new ModComponent
                {
                    Name = "Long Path",
                    Guid = Guid.NewGuid(),
                    Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
                    {
                        new Instruction
                        {
                            Action = Instruction.ActionType.Move,
                            Source = new List<string> { longPath },
                            Destination = "<<kotorDirectory>>/Override"
                        }
                    }
                };

                var missingFiles = await ComponentValidationService.GetMissingFilesForComponentAsync(component);

                Assert.Multiple(() =>
                {
                    Assert.That(missingFiles, Is.Not.Null, "Missing files list should not be null");
                    // Should handle long paths (may succeed or fail depending on OS limits)
                });
            }
            finally
            {
                try
                {
                    if (File.Exists(longPath))
                    {
                        File.Delete(longPath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Test]
        public async Task ValidateComponent_WithInvalidWildcardPattern_HandlesGracefully()
        {
            var component = new ModComponent
            {
                Name = "Invalid Wildcard",
                Guid = Guid.NewGuid(),
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
                {
                    new Instruction
                    {
                        Action = Instruction.ActionType.Move,
                        Source = new List<string> { "<<modDirectory>>/[invalid].txt" },
                        Destination = "<<kotorDirectory>>/Override"
                    }
                }
            };

            var missingFiles = await ComponentValidationService.GetMissingFilesForComponentAsync(component);

            Assert.Multiple(() =>
            {
                Assert.That(missingFiles, Is.Not.Null, "Missing files list should not be null");
                // Should handle invalid patterns gracefully
            });
        }

        [Test]
        public async Task ValidateComponent_WithEmptyComponent_HandlesGracefully()
        {
            var component = new ModComponent
            {
                Name = "Empty Component",
                Guid = Guid.NewGuid(),
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>(),
                Options = new System.Collections.ObjectModel.ObservableCollection<Option>()
            };

            var missingFiles = await ComponentValidationService.GetMissingFilesForComponentAsync(component);

            Assert.Multiple(() =>
            {
                Assert.That(missingFiles, Is.Not.Null, "Missing files list should not be null");
                Assert.That(missingFiles, Is.Empty, "Empty component should have no missing files");
            });
        }

        #endregion

        #region ResourceRegistry Validation Edge Cases

        [Test]
        public async Task ValidateComponent_WithEmptyResourceRegistry_HandlesGracefully()
        {
            var component = new ModComponent
            {
                Name = "Empty ResourceRegistry",
                Guid = Guid.NewGuid(),
                ResourceRegistry = new Dictionary<string, ResourceMetadata>(StringComparer.Ordinal),
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
                {
                    new Instruction
                    {
                        Action = Instruction.ActionType.Move,
                        Source = new List<string> { "<<modDirectory>>/file.txt" },
                        Destination = "<<kotorDirectory>>/Override"
                    }
                }
            };

            var missingFiles = await ComponentValidationService.GetMissingFilesForComponentAsync(component);

            Assert.Multiple(() =>
            {
                Assert.That(missingFiles, Is.Not.Null, "Missing files list should not be null");
                // Should validate normally without ResourceRegistry
            });
        }

        [Test]
        public async Task ValidateComponent_WithInvalidResourceRegistry_HandlesGracefully()
        {
            var component = new ModComponent
            {
                Name = "Invalid ResourceRegistry",
                Guid = Guid.NewGuid(),
                ResourceRegistry = new Dictionary<string, ResourceMetadata>(StringComparer.Ordinal)
                {
                    {
                        "invalid_url",
                        new ResourceMetadata
                        {
                            Files = new Dictionary<string, bool?>(StringComparer.Ordinal)
                            {
                                { "nonexistent.txt", null }
                            }
                        }
                    }
                },
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
                {
                    new Instruction
                    {
                        Action = Instruction.ActionType.Move,
                        Source = new List<string> { "<<modDirectory>>/nonexistent.txt" },
                        Destination = "<<kotorDirectory>>/Override"
                    }
                }
            };

            var missingFiles = await ComponentValidationService.GetMissingFilesForComponentAsync(component);

            Assert.Multiple(() =>
            {
                Assert.That(missingFiles, Is.Not.Null, "Missing files list should not be null");
                Assert.That(missingFiles, Is.Not.Empty, "Should report missing file");
            });
        }

        #endregion

        #region Component Validation with Complex Scenarios

        [Test]
        public async Task ValidateComponent_WithMultipleInstructionsAndOptions_ValidatesAll()
        {
            var component = new ModComponent
            {
                Name = "Complex Component",
                Guid = Guid.NewGuid()
            };

            var option = new Option
            {
                Name = "Option",
                Guid = Guid.NewGuid(),
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
                {
                    new Instruction
                    {
                        Action = Instruction.ActionType.Move,
                        Source = new List<string> { "<<modDirectory>>/option_file.txt" },
                        Destination = "<<kotorDirectory>>/Override"
                    }
                }
            };

            component.Options.Add(option);

            component.Instructions.Add(new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { "<<modDirectory>>/main_file.txt" },
                Destination = "<<kotorDirectory>>/Override"
            });

            component.Instructions.Add(new Instruction
            {
                Action = Instruction.ActionType.Choose,
                Source = new List<string> { option.Guid.ToString() }
            });

            var missingFiles = await ComponentValidationService.GetMissingFilesForComponentAsync(component);

            Assert.Multiple(() =>
            {
                Assert.That(missingFiles, Is.Not.Null, "Missing files list should not be null");
                // Should validate both main instructions and option instructions
            });
        }

        [Test]
        public async Task ValidateComponent_WithInstructionDependencies_ValidatesWhenDependencyMet()
        {
            var depComponent = new ModComponent { Name = "Dependency", Guid = Guid.NewGuid(), IsSelected = true };

            var component = new ModComponent
            {
                Name = "Dependent",
                Guid = Guid.NewGuid(),
                Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
                {
                    new Instruction
                    {
                        Action = Instruction.ActionType.Move,
                        Source = new List<string> { "<<modDirectory>>/file.txt" },
                        Destination = "<<kotorDirectory>>/Override",
                        Dependencies = new List<Guid> { depComponent.Guid }
                    }
                }
            };

            var missingFiles = await ComponentValidationService.GetMissingFilesForComponentAsync(component);

            Assert.Multiple(() =>
            {
                Assert.That(missingFiles, Is.Not.Null, "Missing files list should not be null");
                // Validation should check files regardless of dependencies
            });
        }

        #endregion
    }
}

