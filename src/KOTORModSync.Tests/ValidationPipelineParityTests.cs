// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using KOTORModSync.Core;
using KOTORModSync.Core.CLI;
using KOTORModSync.Core.Services.Validation;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class ValidationPipelineParityTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "KOTORModSync_PipelineParity_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            string gameDir = Path.Combine(_tempDir, "game");
            string modDir = Path.Combine(_tempDir, "mods");
            Directory.CreateDirectory(gameDir);
            Directory.CreateDirectory(modDir);
            File.WriteAllText(Path.Combine(gameDir, "swkotor.exe"), string.Empty);
            File.WriteAllText(Path.Combine(gameDir, "swkotor2.exe"), string.Empty);

            MainConfig.Instance = new MainConfig
            {
                destinationPath = new DirectoryInfo(gameDir),
                sourcePath = new DirectoryInfo(modDir),
            };

            EnsureHolopatcherInTestResources();
        }

        private static void EnsureHolopatcherInTestResources()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string resourcesDir = Path.Combine(baseDir, "Resources");
            Directory.CreateDirectory(resourcesDir);
            string targetPath = Path.Combine(resourcesDir, "holopatcher");
            if (File.Exists(targetPath))
            {
                return;
            }

            string vendorHolopatcher = Path.GetFullPath(Path.Combine(
                baseDir,
                "..", "..", "..", "..", "..",
                "vendor", "bin", "HoloPatcher_linux"));
            if (!File.Exists(vendorHolopatcher))
            {
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.Copy(vendorHolopatcher, targetPath, overwrite: true);
            }
            else
            {
                File.CreateSymbolicLink(targetPath, vendorHolopatcher);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Test]
        public async Task Pipeline_WizardPreset_And_CliPreset_AgreeOnRestrictionConflict()
        {
            var dependency = new ModComponent
            {
                Guid = Guid.NewGuid(),
                Name = "DependencyMod",
                IsSelected = true,
                Instructions = new ObservableCollection<Instruction>
                {
                    new Instruction { Action = Instruction.ActionType.Move, Source = new List<string> { "<<modDirectory>>/a.txt" }, Destination = "<<kotorDirectory>>/Override" },
                },
            };

            var restricted = new ModComponent
            {
                Guid = Guid.NewGuid(),
                Name = "RestrictedMod",
                IsSelected = true,
                Restrictions = new List<Guid> { dependency.Guid },
                Instructions = new ObservableCollection<Instruction>
                {
                    new Instruction { Action = Instruction.ActionType.Move, Source = new List<string> { "<<modDirectory>>/b.txt" }, Destination = "<<kotorDirectory>>/Override" },
                },
            };

            var components = new List<ModComponent> { dependency, restricted };

            var wizardOptions = ValidationPipelineOptions.WizardFull;
            wizardOptions.SkipEnvironmentValidation = true;
            wizardOptions.SkipComponentArchiveValidation = true;
            wizardOptions.DryRun = false;

            var cliOptions = ValidationPipelineOptions.CliFullWithDryRun;
            cliOptions.SkipEnvironmentValidation = true;
            cliOptions.SkipComponentArchiveValidation = true;
            cliOptions.DryRun = false;

            ValidationPipelineResult wizardResult = await InstallationValidationPipeline.RunAsync(
                components,
                wizardOptions).ConfigureAwait(false);

            ValidationPipelineResult cliResult = await InstallationValidationPipeline.RunAsync(
                components,
                cliOptions).ConfigureAwait(false);

            Assert.That(wizardResult.IsSuccess, Is.False);
            Assert.That(cliResult.IsSuccess, Is.False);
            Assert.That(wizardResult.HasCriticalErrors, Is.True);
            Assert.That(cliResult.HasCriticalErrors, Is.True);
        }

        [Test]
        public async Task Pipeline_EnvironmentFailure_IsNotSuccess_AndSkipsDryRun()
        {
            MainConfig.Instance = new MainConfig
            {
                destinationPath = null,
                sourcePath = null,
            };

            var components = new List<ModComponent>
            {
                new ModComponent
                {
                    Guid = Guid.NewGuid(),
                    Name = "AnyMod",
                    IsSelected = true,
                    Instructions = new ObservableCollection<Instruction>(),
                },
            };

            var options = ValidationPipelineOptions.WizardFull;
            options.SkipComponentArchiveValidation = true;
            options.DryRun = false;

            ValidationPipelineResult result = await InstallationValidationPipeline.RunAsync(
                components,
                options).ConfigureAwait(false);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.HasCriticalErrors, Is.True);
            Assert.That(result.DryRunResult, Is.Null);
            Assert.That(
                result.Stages.Exists(s => s.Stage == ValidationPipelineStage.Environment && !s.Passed),
                Is.True);
        }

        [Test]
        public void Install_WithoutSkipValidation_BlocksOnMissingArchives()
        {
            string gameDir = MainConfig.DestinationPath.FullName;
            string modDir = MainConfig.SourcePath.FullName;
            File.WriteAllText(Path.Combine(gameDir, "swkotor.exe"), string.Empty);

            string componentGuid = Guid.NewGuid().ToString();
            string instructionGuid = Guid.NewGuid().ToString();
            string tomlPath = Path.Combine(_tempDir, "install_missing_archive.toml");
            File.WriteAllText(
                tomlPath,
                $@"[[thisMod]]
Guid = ""{componentGuid}""
Name = ""MissingArchiveMod""
IsSelected = true
Tier = ""1 - Essential""
Category = [""Test""]

[[thisMod.Instructions]]
Guid = ""{instructionGuid}""
Action = ""Extract""
Source = [""<<modDirectory>>\\definitely_missing.zip""]
Destination = ""<<kotorDirectory>>\\Override""
");

            int exitCode = ModBuildConverter.Run(new[]
            {
                "install",
                "-i", tomlPath,
                "-g", gameDir,
                "-s", modDir,
                "--use-file-selection",
                "-y",
            });

            Assert.That(exitCode, Is.EqualTo(1));
        }

        [Test]
        public void ModBuildConverter_Run_validate_FullDryRunWithMissingArchive_ExitsNonZero()
        {
            string componentGuid = Guid.NewGuid().ToString();
            string instructionGuid = Guid.NewGuid().ToString();
            string tomlPath = Path.Combine(_tempDir, "validate_missing_archive.toml");
            File.WriteAllText(
                tomlPath,
                $@"[[thisMod]]
Guid = ""{componentGuid}""
Name = ""MissingArchiveMod""
IsSelected = true
Tier = ""1 - Essential""
Category = [""Test""]

[[thisMod.Instructions]]
Guid = ""{instructionGuid}""
Action = ""Extract""
Source = [""<<modDirectory>>\\definitely_missing.zip""]
Destination = ""<<kotorDirectory>>\\Override""
");

            string gameDir = MainConfig.DestinationPath.FullName;
            string modDir = MainConfig.SourcePath.FullName;

            int exitCode = ModBuildConverter.Run(new[]
            {
                "validate",
                "-i", tomlPath,
                "-g", gameDir,
                "-s", modDir,
                "--full",
                "--dry-run",
                "--use-file-selection",
                "--errors-only",
            });

            Assert.That(exitCode, Is.EqualTo(1));
        }

        [Test]
        public void Install_WithRestrictionConflict_AutoDeselectsConflictingMod()
        {
            string gameDir = MainConfig.DestinationPath.FullName;
            string modDir = MainConfig.SourcePath.FullName;
            File.WriteAllText(Path.Combine(gameDir, "swkotor.exe"), string.Empty);
            File.WriteAllText(Path.Combine(modDir, "b.txt"), "payload");

            var dependencyGuid = Guid.NewGuid();
            var restrictedGuid = Guid.NewGuid();
            string depInstructionGuid = Guid.NewGuid().ToString();
            string resInstructionGuid = Guid.NewGuid().ToString();

            string tomlPath = Path.Combine(_tempDir, "install_restriction_autofix.toml");
            File.WriteAllText(
                tomlPath,
                $@"[[thisMod]]
Guid = ""{dependencyGuid}""
Name = ""DependencyMod""
IsSelected = true
Tier = ""1 - Essential""
Category = [""Test""]

[[thisMod.Instructions]]
Guid = ""{depInstructionGuid}""
Action = ""Move""
Source = [""<<modDirectory>>\\a.txt""]
Destination = ""<<kotorDirectory>>\\Override""

[[thisMod]]
Guid = ""{restrictedGuid}""
Name = ""RestrictedMod""
IsSelected = true
Restrictions = [""{dependencyGuid}""]
Tier = ""1 - Essential""
Category = [""Test""]

[[thisMod.Instructions]]
Guid = ""{resInstructionGuid}""
Action = ""Move""
Source = [""<<modDirectory>>\\b.txt""]
Destination = ""<<kotorDirectory>>\\Override""
");

            int exitCode = ModBuildConverter.Run(new[]
            {
                "install",
                "-i", tomlPath,
                "-g", gameDir,
                "-s", modDir,
                "--use-file-selection",
                "--skip-validation",
                "-y",
            });

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(File.Exists(Path.Combine(gameDir, "Override", "b.txt")), Is.True);
        }
    }
}
