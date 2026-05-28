// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Text;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class CliInstallIntegrationTests
    {
        private string _tempRoot = string.Empty;
        private string _modsDirectory = string.Empty;
        private string _gameDirectory = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "KOTORModSync_CliInstallTests", Guid.NewGuid().ToString("N"));
            _modsDirectory = Path.Combine(_tempRoot, "mods");
            _gameDirectory = Path.Combine(_tempRoot, "game");

            Directory.CreateDirectory(_modsDirectory);
            Directory.CreateDirectory(_gameDirectory);
            Directory.CreateDirectory(Path.Combine(_gameDirectory, "Override"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempRoot))
            {
                try
                {
                    Directory.Delete(_tempRoot, recursive: true);
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }

        [Test]
        public void CliInstall_UsesSharedPipelineAndExtractsArchiveIntoGameDirectory()
        {
            string archivePath = Path.Combine(_modsDirectory, "cli_mod.zip");
            string outputFilePath = Path.Combine(_gameDirectory, "Override", "hello.txt");
            string tomlPath = Path.Combine(_tempRoot, "cli_install.toml");

            using (var archive = System.IO.Compression.ZipFile.Open(archivePath, System.IO.Compression.ZipArchiveMode.Create))
            {
                System.IO.Compression.ZipArchiveEntry entry = archive.CreateEntry("hello.txt");
                using (StreamWriter writer = new StreamWriter(entry.Open()))
                {
                    writer.Write("cli install content");
                }
            }

            string componentGuid = Guid.NewGuid().ToString();
            string instructionGuid = Guid.NewGuid().ToString();
            string tomlContents = new StringBuilder()
                .AppendLine("[metadata]")
                .AppendLine("fileFormatVersion = \"2.0\"")
                .AppendLine()
                .AppendLine("[[thisMod]]")
                .AppendLine($"Guid = \"{componentGuid}\"")
                .AppendLine("Name = \"CLI Install Test Mod\"")
                .AppendLine("IsSelected = true")
                .AppendLine("Category = [\"Test\"]")
                .AppendLine("Language = [\"YES\"]")
                .AppendLine()
                .AppendLine("[[thisMod.Instructions]]")
                .AppendLine($"Guid = \"{instructionGuid}\"")
                .AppendLine("Action = \"Extract\"")
                .AppendLine("Source = [\"<<modDirectory>>\\\\cli_mod.zip\"]")
                .AppendLine("Destination = \"<<kotorDirectory>>\\\\Override\"")
                .ToString();

            File.WriteAllText(tomlPath, tomlContents);

            var loadedComponents = FileLoadingService.LoadFromFile(tomlPath);
            Assert.That(loadedComponents, Has.Count.EqualTo(1), "Generated TOML should deserialize into a single component before CLI execution");

            int exitCode = KOTORModSync.Core.Program.Main(new[]
            {
                "install",
                "-i", tomlPath,
                "-g", _gameDirectory,
                "-s", _modsDirectory,
                "--skip-validation",
                "--ignore-errors",
                "-y",
            });

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), "CLI install should exit successfully");
                Assert.That(File.Exists(outputFilePath), Is.True, "CLI install should extract archive contents into the game directory");
                Assert.That(File.ReadAllText(outputFilePath), Is.EqualTo("cli install content"));
            });
        }
    }
}
