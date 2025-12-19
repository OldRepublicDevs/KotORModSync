// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Services.FileSystem;
using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class AdvancedWildcardAndPathTests
    {
        private string _testDirectory;
        private string _modDirectory;
        private string _kotorDirectory;
        private MainConfig _config;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "KOTORModSync_AdvancedWildcardTests_" + Guid.NewGuid());
            _modDirectory = Path.Combine(_testDirectory, "Mods");
            _kotorDirectory = Path.Combine(_testDirectory, "KOTOR");
            Directory.CreateDirectory(_modDirectory);
            Directory.CreateDirectory(_kotorDirectory);
            Directory.CreateDirectory(Path.Combine(_kotorDirectory, "Override"));

            _config = new MainConfig
            {
                sourcePath = new DirectoryInfo(_modDirectory),
                destinationPath = new DirectoryInfo(_kotorDirectory)
            };
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

        #region Complex Wildcard Scenarios

        [Test]
        public void EnumerateFilesWithWildcards_WithMultipleWildcardsInPath_EnumeratesCorrectly()
        {
            // Create files with complex paths
            string subdir1 = Path.Combine(_modDirectory, "Mod*Version*1", "Sub*Dir", "file1.txt");
            string subdir2 = Path.Combine(_modDirectory, "Mod*Version*2", "Sub*Dir", "file2.txt");
            string subdir3 = Path.Combine(_modDirectory, "Mod*Version*3", "Other*Dir", "file3.txt");

            Directory.CreateDirectory(Path.GetDirectoryName(subdir1));
            Directory.CreateDirectory(Path.GetDirectoryName(subdir2));
            Directory.CreateDirectory(Path.GetDirectoryName(subdir3));

            File.WriteAllText(subdir1, "content1");
            File.WriteAllText(subdir2, "content2");
            File.WriteAllText(subdir3, "content3");

            var fileSystemProvider = new RealFileSystemProvider();
            var paths = new List<string>
            {
                Path.Combine(_modDirectory, "Mod*Version*", "*", "file*.txt")
            };

            var files = PathHelper.EnumerateFilesWithWildcards(paths, fileSystemProvider);

            Assert.Multiple(() =>
            {
                Assert.That(files, Is.Not.Null, "Files list should not be null");
                Assert.That(files.Count, Is.GreaterThanOrEqualTo(3), "Should find at least 3 files");
                Assert.That(files.Any(f => f.EndsWith("file1.txt", StringComparison.Ordinal)), Is.True, "Should find file1.txt");
                Assert.That(files.Any(f => f.EndsWith("file2.txt", StringComparison.Ordinal)), Is.True, "Should find file2.txt");
                Assert.That(files.Any(f => f.EndsWith("file3.txt", StringComparison.Ordinal)), Is.True, "Should find file3.txt");
            });
        }

        [Test]
        public void EnumerateFilesWithWildcards_WithQuestionMarkWildcards_EnumeratesCorrectly()
        {
            // Create files with single character wildcards
            File.WriteAllText(Path.Combine(_modDirectory, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(_modDirectory, "file2.txt"), "content2");
            File.WriteAllText(Path.Combine(_modDirectory, "file3.txt"), "content3");
            File.WriteAllText(Path.Combine(_modDirectory, "fileA.txt"), "contentA");

            var fileSystemProvider = new RealFileSystemProvider();
            var paths = new List<string>
            {
                Path.Combine(_modDirectory, "file?.txt") // Should match file1, file2, file3, but not fileA
            };

            var files = PathHelper.EnumerateFilesWithWildcards(paths, fileSystemProvider);

            Assert.Multiple(() =>
            {
                Assert.That(files, Is.Not.Null, "Files list should not be null");
                Assert.That(files.Count, Is.GreaterThanOrEqualTo(3), "Should find at least 3 files");
                Assert.That(files.All(f => Path.GetFileName(f).StartsWith("file", StringComparison.Ordinal) &&
                    Path.GetFileName(f).Length == 8), Is.True, "All files should match pattern file?.txt");
            });
        }

        [Test]
        public void EnumerateFilesWithWildcards_WithNestedWildcards_EnumeratesCorrectly()
        {
            // Create nested structure
            string nested1 = Path.Combine(_modDirectory, "Level1", "Level2", "Level3", "file.txt");
            string nested2 = Path.Combine(_modDirectory, "Level1", "Level2", "Other", "file.txt");
            string nested3 = Path.Combine(_modDirectory, "Level1", "Different", "file.txt");

            Directory.CreateDirectory(Path.GetDirectoryName(nested1));
            Directory.CreateDirectory(Path.GetDirectoryName(nested2));
            Directory.CreateDirectory(Path.GetDirectoryName(nested3));

            File.WriteAllText(nested1, "content1");
            File.WriteAllText(nested2, "content2");
            File.WriteAllText(nested3, "content3");

            var fileSystemProvider = new RealFileSystemProvider();
            var paths = new List<string>
            {
                Path.Combine(_modDirectory, "*", "*", "file.txt")
            };

            var files = PathHelper.EnumerateFilesWithWildcards(paths, fileSystemProvider);

            Assert.Multiple(() =>
            {
                Assert.That(files, Is.Not.Null, "Files list should not be null");
                Assert.That(files.Count, Is.GreaterThanOrEqualTo(2), "Should find at least 2 files");
            });
        }

        [Test]
        public void EnumerateFilesWithWildcards_WithEmptyPath_ReturnsEmpty()
        {
            var fileSystemProvider = new RealFileSystemProvider();
            var paths = new List<string> { string.Empty };

            var files = PathHelper.EnumerateFilesWithWildcards(paths, fileSystemProvider);

            Assert.Multiple(() =>
            {
                Assert.That(files, Is.Not.Null, "Files list should not be null");
                Assert.That(files, Is.Empty, "Empty path should return empty list");
            });
        }

        [Test]
        public void EnumerateFilesWithWildcards_WithNonExistentPath_ReturnsEmpty()
        {
            var fileSystemProvider = new RealFileSystemProvider();
            var paths = new List<string>
            {
                Path.Combine(_modDirectory, "NonExistent", "*.txt")
            };

            var files = PathHelper.EnumerateFilesWithWildcards(paths, fileSystemProvider);

            Assert.Multiple(() =>
            {
                Assert.That(files, Is.Not.Null, "Files list should not be null");
                Assert.That(files, Is.Empty, "Non-existent path should return empty list");
            });
        }

        #endregion

        #region Path Edge Cases

        [Test]
        public async Task Instruction_WithTrailingSeparators_HandlesCorrectly()
        {
            var component = new ModComponent { Name = "Trailing Separators", Guid = Guid.NewGuid(), IsSelected = true };
            File.WriteAllText(Path.Combine(_modDirectory, "file.txt"), "content");

            var instruction = new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { $"<<modDirectory>>/file.txt" },
                Destination = $"<<kotorDirectory>>/Override/" // Trailing separator
            };

            component.Instructions.Add(instruction);

            var fileSystemProvider = new RealFileSystemProvider();
            instruction.SetFileSystemProvider(fileSystemProvider);
            instruction.SetParentComponent(component);

            var result = await component.ExecuteSingleInstructionAsync(instruction, 0, new List<ModComponent> { component }, fileSystemProvider);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(Instruction.ActionExitCode.Success), "Move should succeed");
                Assert.That(File.Exists(Path.Combine(_kotorDirectory, "Override", "file.txt")), Is.True, "File should exist");
            });
        }

        [Test]
        public async Task Instruction_WithMixedPathSeparators_HandlesCorrectly()
        {
            var component = new ModComponent { Name = "Mixed Separators", Guid = Guid.NewGuid(), IsSelected = true };
            File.WriteAllText(Path.Combine(_modDirectory, "file.txt"), "content");

            var instruction = new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { $"<<modDirectory>>\\file.txt" }, // Backslash
                Destination = $"<<kotorDirectory>>/Override" // Forward slash
            };

            component.Instructions.Add(instruction);

            var fileSystemProvider = new RealFileSystemProvider();
            instruction.SetFileSystemProvider(fileSystemProvider);
            instruction.SetParentComponent(component);

            var result = await component.ExecuteSingleInstructionAsync(instruction, 0, new List<ModComponent> { component }, fileSystemProvider);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(Instruction.ActionExitCode.Success), "Move should succeed");
                Assert.That(File.Exists(Path.Combine(_kotorDirectory, "Override", "file.txt")), Is.True, "File should exist");
            });
        }

        [Test]
        public async Task Instruction_WithRelativePathSegments_HandlesCorrectly()
        {
            var component = new ModComponent { Name = "Relative Segments", Guid = Guid.NewGuid(), IsSelected = true };
            string subdir = Path.Combine(_modDirectory, "subdir");
            Directory.CreateDirectory(subdir);
            File.WriteAllText(Path.Combine(subdir, "file.txt"), "content");

            var instruction = new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { $"<<modDirectory>>/subdir/../subdir/file.txt" }, // Relative segments
                Destination = $"<<kotorDirectory>>/Override"
            };

            component.Instructions.Add(instruction);

            var fileSystemProvider = new RealFileSystemProvider();
            instruction.SetFileSystemProvider(fileSystemProvider);
            instruction.SetParentComponent(component);

            var result = await component.ExecuteSingleInstructionAsync(instruction, 0, new List<ModComponent> { component }, fileSystemProvider);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(Instruction.ActionExitCode.Success), "Move should succeed");
                Assert.That(File.Exists(Path.Combine(_kotorDirectory, "Override", "file.txt")), Is.True, "File should exist");
            });
        }

        #endregion

        #region Wildcard in Instructions

        [Test]
        public async Task Instruction_WithWildcardInSource_ProcessesAllMatching()
        {
            var component = new ModComponent { Name = "Wildcard Source", Guid = Guid.NewGuid(), IsSelected = true };
            File.WriteAllText(Path.Combine(_modDirectory, "texture1.tga"), "tga1");
            File.WriteAllText(Path.Combine(_modDirectory, "texture2.tga"), "tga2");
            File.WriteAllText(Path.Combine(_modDirectory, "texture3.tga"), "tga3");
            File.WriteAllText(Path.Combine(_modDirectory, "other.txt"), "other");

            var instruction = new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { "<<modDirectory>>/*.tga" },
                Destination = "<<kotorDirectory>>/Override"
            };

            component.Instructions.Add(instruction);

            var fileSystemProvider = new RealFileSystemProvider();
            instruction.SetFileSystemProvider(fileSystemProvider);
            instruction.SetParentComponent(component);

            var result = await component.ExecuteSingleInstructionAsync(instruction, 0, new List<ModComponent> { component }, fileSystemProvider);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(Instruction.ActionExitCode.Success), "Move should succeed");
                Assert.That(File.Exists(Path.Combine(_kotorDirectory, "Override", "texture1.tga")), Is.True, "Texture1 should exist");
                Assert.That(File.Exists(Path.Combine(_kotorDirectory, "Override", "texture2.tga")), Is.True, "Texture2 should exist");
                Assert.That(File.Exists(Path.Combine(_kotorDirectory, "Override", "texture3.tga")), Is.True, "Texture3 should exist");
                Assert.That(File.Exists(Path.Combine(_kotorDirectory, "Override", "other.txt")), Is.False, "Other file should not be moved");
            });
        }

        [Test]
        public async Task Instruction_WithMultipleWildcardSources_ProcessesAll()
        {
            var component = new ModComponent { Name = "Multiple Wildcards", Guid = Guid.NewGuid(), IsSelected = true };
            File.WriteAllText(Path.Combine(_modDirectory, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(_modDirectory, "file2.txt"), "content2");
            File.WriteAllText(Path.Combine(_modDirectory, "data1.dat"), "data1");
            File.WriteAllText(Path.Combine(_modDirectory, "data2.dat"), "data2");

            var instruction = new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string>
                {
                    "<<modDirectory>>/*.txt",
                    "<<modDirectory>>/*.dat"
                },
                Destination = "<<kotorDirectory>>/Override"
            };

            component.Instructions.Add(instruction);

            var fileSystemProvider = new RealFileSystemProvider();
            instruction.SetFileSystemProvider(fileSystemProvider);
            instruction.SetParentComponent(component);

            var result = await component.ExecuteSingleInstructionAsync(instruction, 0, new List<ModComponent> { component }, fileSystemProvider);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(Instruction.ActionExitCode.Success), "Move should succeed");
                Assert.That(File.Exists(Path.Combine(_kotorDirectory, "Override", "file1.txt")), Is.True, "File1 should exist");
                Assert.That(File.Exists(Path.Combine(_kotorDirectory, "Override", "file2.txt")), Is.True, "File2 should exist");
                Assert.That(File.Exists(Path.Combine(_kotorDirectory, "Override", "data1.dat")), Is.True, "Data1 should exist");
                Assert.That(File.Exists(Path.Combine(_kotorDirectory, "Override", "data2.dat")), Is.True, "Data2 should exist");
            });
        }

        #endregion
    }
}

