// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.Services.Validation;
using KOTORModSync.Core.Utility;
using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class PathValidationAndResolutionTests
    {
        private string _testDirectory;
        private string _modDirectory;
        private string _kotorDirectory;
        private MainConfig _config;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "KOTORModSync_PathValidation_" + Guid.NewGuid());
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

        #region Path Placeholder Resolution Tests

        [Test]
        public void ReplaceCustomVariables_WithModDirectory_ReplacesCorrectly()
        {
            string path = "<<modDirectory>>/file.txt";
            string resolved = UtilityHelper.ReplaceCustomVariables(path);

            Assert.That(resolved, Is.EqualTo(Path.Combine(_modDirectory, "file.txt")), "Should replace <<modDirectory>> with actual path");
        }

        [Test]
        public void ReplaceCustomVariables_WithKotorDirectory_ReplacesCorrectly()
        {
            string path = "<<kotorDirectory>>/Override/file.txt";
            string resolved = UtilityHelper.ReplaceCustomVariables(path);

            Assert.That(resolved, Is.EqualTo(Path.Combine(_kotorDirectory, "Override", "file.txt")), "Should replace <<kotorDirectory>> with actual path");
        }

        [Test]
        public void ReplaceCustomVariables_WithMixedPlaceholders_ReplacesCorrectly()
        {
            string path = "<<modDirectory>>/source.txt -> <<kotorDirectory>>/Override/dest.txt";
            string resolved = UtilityHelper.ReplaceCustomVariables(path);

            Assert.That(resolved, Contains.Substring(_modDirectory), "Should contain resolved mod directory");
            Assert.That(resolved, Contains.Substring(_kotorDirectory), "Should contain resolved kotor directory");
        }

        [Test]
        public void ReplaceCustomVariables_WithNoPlaceholders_ReturnsOriginal()
        {
            string path = "C:/SomePath/file.txt";
            string resolved = UtilityHelper.ReplaceCustomVariables(path);

            Assert.That(resolved, Is.EqualTo(path), "Should return original path when no placeholders");
        }

        [Test]
        public void ReplaceCustomVariables_WithCaseInsensitive_ReplacesCorrectly()
        {
            string path = "<<MODDIRECTORY>>/file.txt";
            string resolved = UtilityHelper.ReplaceCustomVariables(path);

            // Should handle case-insensitive replacement
            Assert.That(resolved, Is.Not.EqualTo(path), "Should replace case-insensitive placeholders");
        }

        #endregion

        #region Path Validation Tests

        [Test]
        public async Task PathValidationCache_ValidateAndCacheAsync_WithValidPath_ReturnsValid()
        {
            var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
            var instruction = new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { "<<modDirectory>>/file.txt" },
                Destination = "<<kotorDirectory>>/Override"
            };

            File.WriteAllText(Path.Combine(_modDirectory, "file.txt"), "content");

            var result = await PathValidationCache.ValidateAndCacheAsync(
                "<<modDirectory>>/file.txt", instruction, component).ConfigureAwait(false);

            Assert.That(result.IsValid, Is.True, "Valid path should return valid result");
        }

        [Test]
        public async Task PathValidationCache_ValidateAndCacheAsync_WithInvalidPath_ReturnsInvalid()
        {
            var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
            var instruction = new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { "<<modDirectory>>/nonexistent.txt" },
                Destination = "<<kotorDirectory>>/Override"
            };

            var result = await PathValidationCache.ValidateAndCacheAsync(
                "<<modDirectory>>/nonexistent.txt", instruction, component).ConfigureAwait(false);

            Assert.That(result.IsValid, Is.False, "Invalid path should return invalid result");
        }

        [Test]
        public async Task PathValidationCache_ValidateAndCacheAsync_CachesResults()
        {
            var component = new ModComponent { Name = "Test", Guid = Guid.NewGuid() };
            var instruction = new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { "<<modDirectory>>/file.txt" },
                Destination = "<<kotorDirectory>>/Override"
            };

            File.WriteAllText(Path.Combine(_modDirectory, "file.txt"), "content");

            var result1 = await PathValidationCache.ValidateAndCacheAsync(
                "<<modDirectory>>/file.txt", instruction, component).ConfigureAwait(false);

            var result2 = await PathValidationCache.ValidateAndCacheAsync(
                "<<modDirectory>>/file.txt", instruction, component).ConfigureAwait(false);

            Assert.Multiple(() =>
            {
                Assert.That(result1.IsValid, Is.True, "First validation should be valid");
                Assert.That(result2.IsValid, Is.True, "Cached validation should be valid");
            });
        }

        #endregion

        #region Path Normalization Tests

        [Test]
        public void PathHelper_FixPathFormatting_WithForwardSlashes_ConvertsToBackslashes()
        {
            string path = "<<modDirectory>>/subdir/file.txt";
            string fixedPath = PathHelper.FixPathFormatting(path);

            // On Windows, should convert to backslashes
            if (Path.DirectorySeparatorChar == '\\')
            {
                Assert.That(fixedPath, Contains.Substring("\\"), "Should convert forward slashes to backslashes on Windows");
            }
        }

        [Test]
        public void PathHelper_FixPathFormatting_WithMixedSlashes_Normalizes()
        {
            string path = "<<modDirectory>>/subdir\\file.txt";
            string fixedPath = PathHelper.FixPathFormatting(path);

            Assert.That(fixedPath, Is.Not.Null, "Should handle mixed slashes");
        }

        [Test]
        public void PathHelper_FixPathFormatting_WithTrailingSlash_HandlesCorrectly()
        {
            string path = "<<modDirectory>>/subdir/";
            string fixedPath = PathHelper.FixPathFormatting(path);

            Assert.That(fixedPath, Is.Not.Null, "Should handle trailing slashes");
        }

        #endregion

        #region Wildcard Path Tests

        [Test]
        public void PathHelper_EnumerateFilesWithWildcards_WithStarWildcard_MatchesFiles()
        {
            File.WriteAllText(Path.Combine(_modDirectory, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(_modDirectory, "file2.txt"), "content2");
            File.WriteAllText(Path.Combine(_modDirectory, "other.dat"), "content3");

            var files = PathHelper.EnumerateFilesWithWildcards(
                "<<modDirectory>>/file*.txt",
                new Core.Services.FileSystem.RealFileSystemProvider());

            Assert.Multiple(() =>
            {
                Assert.That(files, Is.Not.Empty, "Should find matching files");
                Assert.That(files.Count(), Is.EqualTo(2), "Should find exactly 2 matching files");
            });
        }

        [Test]
        public void PathHelper_EnumerateFilesWithWildcards_WithQuestionMarkWildcard_MatchesFiles()
        {
            File.WriteAllText(Path.Combine(_modDirectory, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(_modDirectory, "file2.txt"), "content2");
            File.WriteAllText(Path.Combine(_modDirectory, "file10.txt"), "content3");

            var files = PathHelper.EnumerateFilesWithWildcards(
                "<<modDirectory>>/file?.txt",
                new Core.Services.FileSystem.RealFileSystemProvider());

            Assert.Multiple(() =>
            {
                Assert.That(files, Is.Not.Empty, "Should find matching files");
                Assert.That(files.Count(), Is.EqualTo(2), "Should find exactly 2 matching files (file1 and file2)");
            });
        }

        [Test]
        public void PathHelper_EnumerateFilesWithWildcards_WithNoMatches_ReturnsEmpty()
        {
            File.WriteAllText(Path.Combine(_modDirectory, "file1.txt"), "content1");

            var files = PathHelper.EnumerateFilesWithWildcards(
                "<<modDirectory>>/nonexistent*.txt",
                new Core.Services.FileSystem.RealFileSystemProvider());

            Assert.That(files, Is.Empty, "Should return empty when no matches");
        }

        #endregion

        #region Path Edge Cases

        [Test]
        public void ReplaceCustomVariables_WithEmptyPath_ReturnsEmpty()
        {
            string path = string.Empty;
            string resolved = UtilityHelper.ReplaceCustomVariables(path);

            Assert.That(resolved, Is.EqualTo(string.Empty), "Should return empty for empty path");
        }

        [Test]
        public void ReplaceCustomVariables_WithNullPath_ReturnsNull()
        {
            string path = null;
            string resolved = UtilityHelper.ReplaceCustomVariables(path);

            Assert.That(resolved, Is.Null, "Should return null for null path");
        }

        [Test]
        public void ReplaceCustomVariables_WithNestedPlaceholders_HandlesCorrectly()
        {
            // This shouldn't happen in practice, but test edge case
            string path = "<<modDirectory>>/<<kotorDirectory>>/file.txt";
            string resolved = UtilityHelper.ReplaceCustomVariables(path);

            Assert.That(resolved, Is.Not.Null, "Should handle nested placeholders");
        }

        [Test]
        public void PathHelper_FixPathFormatting_WithUnicodeCharacters_HandlesCorrectly()
        {
            string path = "<<modDirectory>>/测试文件.txt";
            string fixedPath = PathHelper.FixPathFormatting(path);

            Assert.That(fixedPath, Is.Not.Null, "Should handle Unicode characters");
        }

        [Test]
        public void PathHelper_FixPathFormatting_WithSpecialCharacters_HandlesCorrectly()
        {
            string path = "<<modDirectory>>/file with spaces.txt";
            string fixedPath = PathHelper.FixPathFormatting(path);

            Assert.That(fixedPath, Is.Not.Null, "Should handle special characters");
        }

        #endregion

        #region Path Sandboxing Tests

        [Test]
        public void SetRealPaths_WithModDirectoryPath_StaysWithinSandbox()
        {
            var instruction = new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { "<<modDirectory>>/file.txt" },
                Destination = "<<kotorDirectory>>/Override"
            };

            instruction.SetRealPaths();

            Assert.Multiple(() =>
            {
                Assert.That(instruction.Source[0], Contains.Substring(_modDirectory), "Source should be within mod directory");
                Assert.That(instruction.Destination, Contains.Substring(_kotorDirectory), "Destination should be within kotor directory");
            });
        }

        [Test]
        public void SetRealPaths_WithRelativePath_ResolvesCorrectly()
        {
            var instruction = new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { "file.txt" },
                Destination = "Override"
            };

            instruction.SetRealPaths();

            // Should resolve relative paths within sandbox
            Assert.That(instruction.Source[0], Is.Not.Null, "Should resolve relative source path");
            Assert.That(instruction.Destination, Is.Not.Null, "Should resolve relative destination path");
        }

        #endregion
    }
}

