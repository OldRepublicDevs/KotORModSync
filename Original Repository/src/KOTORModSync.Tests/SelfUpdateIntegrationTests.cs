// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

using KOTORModSync.Services;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class SelfUpdateIntegrationTests
    {
        private string _tempRoot = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "KOTORModSync_SelfUpdateTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
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
        public async Task ApplyUpdateAsync_ZipArchive_GeneratesReplacementScriptAndLaunchInfo()
        {
            string archivePath = Path.Combine(_tempRoot, "update.zip");
            string currentProcessPath = Path.Combine(_tempRoot, "current", "KOTORModSync.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(currentProcessPath) ?? _tempRoot);
            File.WriteAllText(currentProcessPath, "old-binary");

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("payload/KOTORModSync.dll");
                using (StreamWriter writer = new StreamWriter(entry.Open()))
                {
                    writer.Write("new-binary");
                }
            }

            var coordinator = new AutoUpdateCoordinator();
            AutoUpdateExecutionResult result = await coordinator.ApplyUpdateAsync(
                new AutoUpdatePackage
                {
                    ArchivePath = archivePath,
                },
                currentProcessPath);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Not.Null);
                Assert.That(Directory.Exists(result.PayloadRoot), Is.True, "Payload directory should exist");
                Assert.That(File.Exists(Path.Combine(result.PayloadRoot, "KOTORModSync.dll")), Is.True, "Extracted payload should contain application binary");
                Assert.That(File.Exists(result.ScriptPath), Is.True, "Update script should be created");
                Assert.That(result.LaunchInfo, Is.Not.Null);
                Assert.That(result.ExitCode, Is.EqualTo(10));
            });
        }

        [Test]
        public void BuildWindowsScript_ContainsCopyAndRestartSteps()
        {
            string script = AutoUpdateCoordinator.BuildWindowsScript(
                sourceDir: @"C:\temp\payload",
                targetDir: @"C:\app",
                executablePath: @"C:\app\KOTORModSync.exe",
                parentProcessId: 1234);

            Assert.That(script, Does.Contain("robocopy"));
            Assert.That(script, Does.Contain("Start-Process -FilePath"));
            Assert.That(script, Does.Contain("$parentPid = 1234"));
        }

        [Test]
        public void BuildUnixScript_ContainsCopyAndRestartSteps()
        {
            string script = AutoUpdateCoordinator.BuildUnixScript(
                sourceDir: "/tmp/payload",
                targetDir: "/opt/app",
                executablePath: "/opt/app/KOTORModSync",
                parentProcessId: 4321);

            Assert.That(script, Does.Contain("rsync -a --delete"));
            Assert.That(script, Does.Contain("chmod +x"));
            Assert.That(script, Does.Contain("while kill -0 4321"));
        }
    }
}
