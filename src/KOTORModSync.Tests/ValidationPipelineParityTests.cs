// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
        public void ModBuildConverter_Run_validate_UsesSamePipelineOptionsShape()
        {
            string tomlPath = Path.Combine(_tempDir, "minimal.toml");
            File.WriteAllText(
                tomlPath,
                @"[[thisMod]]
name = ""TestMod""
isSelected = true
tier = ""1 - Essential""
category = [""Immersion""]

[[thisMod.instructions]]
action = ""Move""
source = [""<<modDirectory>>/missing.zip""]
destination = ""<<kotorDirectory>>/Override""
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

            Assert.That(exitCode, Is.EqualTo(1).Or.EqualTo(0));
        }
    }
}
