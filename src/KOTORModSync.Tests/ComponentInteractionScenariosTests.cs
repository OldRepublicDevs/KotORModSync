// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core;
using KOTORModSync.Core.Installation;
using KOTORModSync.Core.Services;
using KOTORModSync.Tests.TestHelpers;
using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class ComponentInteractionScenariosTests
    {
        private DirectoryInfo _workingDirectory;
        private MainConfig _mainConfigInstance;

        [SetUp]
        public void SetUp()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "KOTORModSync_InteractionTests_" + Guid.NewGuid().ToString("N"));
            _workingDirectory = Directory.CreateDirectory(tempRoot);
            _ = Directory.CreateDirectory(Path.Combine(tempRoot, ModComponent.CheckpointFolderName));

            _mainConfigInstance = new MainConfig
            {
                destinationPath = _workingDirectory,
                sourcePath = _workingDirectory,
                allComponents = new List<ModComponent>(),
            };
            InstallCoordinator.ClearSessionForTests(_workingDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (_workingDirectory != null && _workingDirectory.Exists)
            {
                InstallCoordinatorTestsHelper.CleanupTestDirectory(_workingDirectory);
            }
        }

        #region Component Interaction Scenarios

        [Test]
        public async Task Install_MultipleModsModifyingSameFiles_HandlesOverwrites()
        {
            var mod1 = TestComponentFactory.CreateComponent("Mod 1", _workingDirectory);
            File.WriteAllText(Path.Combine(_workingDirectory.FullName, "shared_file.txt"), "mod1 version");
            mod1.Instructions.Add(new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { "<<modDirectory>>/shared_file.txt" },
                Destination = "<<kotorDirectory>>/Override",
                Overwrite = true
            });

            var mod2 = TestComponentFactory.CreateComponent("Mod 2", _workingDirectory);
            File.WriteAllText(Path.Combine(_workingDirectory.FullName, "shared_file.txt"), "mod2 version");
            mod2.Instructions.Add(new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { "<<modDirectory>>/shared_file.txt" },
                Destination = "<<kotorDirectory>>/Override",
                Overwrite = true
            });

            _mainConfigInstance.allComponents = new List<ModComponent> { mod1, mod2 };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(ModComponent.InstallExitCode.Success), "Installation should succeed");
                Assert.That(File.Exists(Path.Combine(_workingDirectory.FullName, "Override", "shared_file.txt")), Is.True, "Shared file should exist");
                // Last mod to install should have its version
            });
        }

        [Test]
        public async Task Install_ModsWithOverlappingWildcards_ProcessesCorrectly()
        {
            var mod1 = new ModComponent { Name = "Mod 1", Guid = Guid.NewGuid(), IsSelected = true };
            File.WriteAllText(Path.Combine(_workingDirectory.FullName, "texture1.tga"), "tga1");
            File.WriteAllText(Path.Combine(_workingDirectory.FullName, "texture2.tga"), "tga2");
            mod1.Instructions.Add(new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { "<<modDirectory>>/texture1.tga" },
                Destination = "<<kotorDirectory>>/Override"
            });

            var mod2 = new ModComponent { Name = "Mod 2", Guid = Guid.NewGuid(), IsSelected = true };
            mod2.Instructions.Add(new Instruction
            {
                Action = Instruction.ActionType.Move,
                Source = new List<string> { "<<modDirectory>>/*.tga" },
                Destination = "<<kotorDirectory>>/Override",
                Overwrite = true
            });

            _mainConfigInstance.allComponents = new List<ModComponent> { mod1, mod2 };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(ModComponent.InstallExitCode.Success), "Installation should succeed");
                Assert.That(File.Exists(Path.Combine(_workingDirectory.FullName, "Override", "texture1.tga")), Is.True, "Texture1 should exist");
                Assert.That(File.Exists(Path.Combine(_workingDirectory.FullName, "Override", "texture2.tga")), Is.True, "Texture2 should exist");
            });
        }

        [Test]
        public async Task Install_ModsWithSequentialDependencies_InstallsInOrder()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.Dependencies = new List<Guid> { modA.Guid };
            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            modC.Dependencies = new List<Guid> { modB.Guid };
            var modD = TestComponentFactory.CreateComponent("Mod D", _workingDirectory);
            modD.Dependencies = new List<Guid> { modC.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC, modD };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Has.Count.EqualTo(4), "Should contain all components");
                Assert.That(ordered[0].Guid, Is.EqualTo(modA.Guid), "Mod A should be first");
                Assert.That(ordered[1].Guid, Is.EqualTo(modB.Guid), "Mod B should be second");
                Assert.That(ordered[2].Guid, Is.EqualTo(modC.Guid), "Mod C should be third");
                Assert.That(ordered[3].Guid, Is.EqualTo(modD.Guid), "Mod D should be fourth");
            });
        }

        [Test]
        public async Task Install_ModsWithBidirectionalDependencies_HandlesCorrectly()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);

            // Mod A depends on Mod B, but Mod B has InstallAfter Mod A
            modA.Dependencies = new List<Guid> { modB.Guid };
            modB.InstallAfter = new List<Guid> { modA.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Has.Count.EqualTo(2), "Should contain both components");
                // Dependencies should take precedence over InstallAfter
                Assert.That(ordered.IndexOf(modB), Is.LessThan(ordered.IndexOf(modA)), "Mod B should come before Mod A (dependency takes precedence)");
            });
        }

        [Test]
        public async Task Install_ModsWithOptionDependencies_RespectsDependencies()
        {
            var baseMod = TestComponentFactory.CreateComponent("Base Mod", _workingDirectory);

            var optionMod = new ModComponent
            {
                Name = "Option Mod",
                Guid = Guid.NewGuid(),
                IsSelected = true,
                Dependencies = new List<Guid> { baseMod.Guid }
            };

            var option1 = new Option { Name = "Option 1", Guid = Guid.NewGuid(), IsSelected = true };
            var option2 = new Option
            {
                Name = "Option 2",
                Guid = Guid.NewGuid(),
                IsSelected = true,
                Dependencies = new List<Guid> { option1.Guid }
            };

            optionMod.Options.Add(option1);
            optionMod.Options.Add(option2);

            optionMod.Instructions.Add(new Instruction
            {
                Action = Instruction.ActionType.Choose,
                Source = new List<string> { option1.Guid.ToString(), option2.Guid.ToString() }
            });

            _mainConfigInstance.allComponents = new List<ModComponent> { baseMod, optionMod };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Has.Count.EqualTo(2), "Should contain both components");
                Assert.That(ordered.IndexOf(baseMod), Is.LessThan(ordered.IndexOf(optionMod)), "Base mod should come before option mod");
            });
        }

        [Test]
        public async Task Install_ModsWithPartialDependencyChain_HandlesCorrectly()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.Dependencies = new List<Guid> { modA.Guid };
            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            // Mod C has no dependencies

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Has.Count.EqualTo(3), "Should contain all components");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modB)), "Mod A should come before Mod B");
                // Mod C can be anywhere relative to A and B
            });
        }

        [Test]
        public async Task Install_ModsWithMixedSelectionStates_InstallsOnlySelected()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            modA.IsSelected = true;

            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.IsSelected = false; // Not selected
            modB.Dependencies = new List<Guid> { modA.Guid };

            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            modC.IsSelected = true;
            modC.Dependencies = new List<Guid> { modB.Guid }; // Depends on unselected mod

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Has.Count.EqualTo(2), "Should contain only selected components");
                Assert.That(ordered.Select(c => c.Guid), Contains.Item(modA.Guid), "Mod A should be selected");
                Assert.That(ordered.Select(c => c.Guid), Contains.Item(modC.Guid), "Mod C should be selected");
                Assert.That(ordered.Select(c => c.Guid), Does.Not.Contain(modB.Guid), "Mod B should not be selected");
            });
        }

        #endregion

        #region Complex State Scenarios

        [Test]
        public async Task Install_WithPartialCompletion_ResumesCorrectly()
        {
            var mod1 = TestComponentFactory.CreateComponent("Mod 1", _workingDirectory);
            mod1.InstallState = ModComponent.ComponentInstallState.Completed;

            var mod2 = TestComponentFactory.CreateComponent("Mod 2", _workingDirectory);
            mod2.InstallState = ModComponent.ComponentInstallState.Pending;

            var mod3 = TestComponentFactory.CreateComponent("Mod 3", _workingDirectory);
            mod3.InstallState = ModComponent.ComponentInstallState.Pending;

            _mainConfigInstance.allComponents = new List<ModComponent> { mod1, mod2, mod3 };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            coordinator.CheckpointManager.UpdateComponentState(mod1);
            await coordinator.CheckpointManager.SaveAsync();

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(ModComponent.InstallExitCode.Success), "Installation should succeed");
                Assert.That(mod1.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod 1 should remain completed");
                Assert.That(mod2.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod 2 should be completed");
                Assert.That(mod3.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod 3 should be completed");
            });
        }

        [Test]
        public async Task Install_WithFailedComponentInChain_BlocksDescendants()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            // Make Mod A fail
            string archivePath = Path.Combine(_workingDirectory.FullName, "Mod A.zip");
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.Dependencies = new List<Guid> { modA.Guid };

            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            modC.Dependencies = new List<Guid> { modB.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(modA.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Failed), "Mod A should fail");
                Assert.That(modB.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Blocked), "Mod B should be blocked");
                Assert.That(modC.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Blocked), "Mod C should be blocked");
            });
        }

        #endregion
    }
}

