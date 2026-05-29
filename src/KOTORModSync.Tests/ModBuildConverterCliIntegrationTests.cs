// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.Linq;

using KOTORModSync.Core;
using KOTORModSync.Core.CLI;
using KOTORModSync.Core.Services;

using NUnit.Framework;

using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers.Zip;

namespace KOTORModSync.Tests
{
    [TestFixture]
    [Category("Integration")]
    public sealed class ModBuildConverterCliIntegrationTests
    {
        private string _testDirectory;
        private MainConfig _previousMainConfig;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "KOTORModSync_CliMerge_" + Guid.NewGuid());
            Directory.CreateDirectory(_testDirectory);
            _previousMainConfig = MainConfig.Instance;
            MainConfig.Instance = new MainConfig();
        }

        [TearDown]
        public void TearDown()
        {
            MainConfig.Instance = _previousMainConfig;

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

        [Test]
        public void Merge_AutoGenerateLocal_ViaCliEntry_AddsInstructions()
        {
            const string archiveName = "cli-auto-gen-mod.zip";
            string sourceDir = Path.Combine(_testDirectory, "mods");
            Directory.CreateDirectory(sourceDir);
            CreateTslPatcherArchive(Path.Combine(sourceDir, archiveName));

            string existingToml = Path.Combine(_testDirectory, "existing.toml");
            string incomingMd = Path.Combine(_testDirectory, "incoming.md");
            string outputToml = Path.Combine(_testDirectory, "merged.toml");

            File.WriteAllText(existingToml, @"[[thisMod]]
Guid = ""a1b2c3d4-e5f6-7890-abcd-ef1234567890""
Name = ""CLI Auto Gen Mod""
IsSelected = true
ModLinkFilenames = { ""https://example.com/cli-auto-gen-mod.zip"" = {  } }
");

            File.WriteAllText(incomingMd, @"### CLI Auto Gen Mod

**Name:** [CLI Auto Gen Mod](https://example.com/cli-auto-gen-mod.zip)

**Author:** Test Author

**Description:** Synthetic mod for CLI merge auto-generate integration.

**Category & Tier:** Immersion / 1 - Essential

**Installation Method:** TSLPatcher Mod

___
");

            string[] args =
            {
                "merge",
                "--existing", existingToml,
                "--incoming", incomingMd,
                "--use-existing-order",
                "--prefer-existing-instructions",
                "-f", "toml",
                "-o", outputToml,
                "--source-path", sourceDir,
                "--auto-generate-local",
            };

            int exitCode = ModBuildConverter.Run(args);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(0), "merge --auto-generate-local should succeed");
                Assert.That(File.Exists(outputToml), Is.True);
            });

            var merged = ModComponentSerializationService
                .DeserializeModComponentFromString(File.ReadAllText(outputToml), "toml")
                .ToList();

            Assert.That(merged, Has.Count.EqualTo(1));
            Assert.That(merged[0].Instructions.Count, Is.GreaterThan(0),
                "CLI merge path should auto-generate instructions from local archive");
        }

        private static void CreateTslPatcherArchive(string archivePath)
        {
            using (var archive = ZipArchive.CreateArchive())
            {
                var exeStream = new MemoryStream();
                var exeWriter = new BinaryWriter(exeStream);
                exeWriter.Write(new byte[] { 0x4D, 0x5A });
                exeWriter.Flush();
                exeStream.Position = 0;
                archive.AddEntry("TSLPatcher.exe", exeStream, closeStream: true);

                var changesStream = new MemoryStream();
                var changesWriter = new StreamWriter(changesStream);
                changesWriter.WriteLine("[Settings]");
                changesWriter.WriteLine("Version=1.0");
                changesWriter.Flush();
                changesStream.Position = 0;
                archive.AddEntry("tslpatchdata/changes.ini", changesStream, closeStream: true);

                using (FileStream fileStream = File.Create(archivePath))
                {
                    archive.SaveTo(fileStream, new ZipWriterOptions(CompressionType.Deflate));
                }
            }
        }
    }
}
