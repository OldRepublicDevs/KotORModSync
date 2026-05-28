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
    public sealed class CheckpointAndResumeEdgeCasesTests
    {
        private DirectoryInfo _workingDirectory;
        private MainConfig _mainConfigInstance;

        [SetUp]
        public void SetUp()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "KOTORModSync_CheckpointTests_" + Guid.NewGuid().ToString("N"));
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

        #region Checkpoint Edge Cases

        [Test]
        public async Task Checkpoint_WithNoComponents_HandlesGracefully()
        {
            _mainConfigInstance.allComponents = new List<ModComponent>();

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(resume, Is.Not.Null, "Resume result should not be null");
                Assert.That(resume.OrderedComponents, Is.Not.Null, "Ordered components should not be null");
                Assert.That(resume.OrderedComponents, Is.Empty, "Ordered components should be empty");
            });
        }

        [Test]
        public async Task Checkpoint_WithAllCompletedComponents_ResumesCorrectly()
        {
            var mod1 = TestComponentFactory.CreateComponent("Mod 1", _workingDirectory);
            mod1.InstallState = ModComponent.ComponentInstallState.Completed;

            var mod2 = TestComponentFactory.CreateComponent("Mod 2", _workingDirectory);
            mod2.InstallState = ModComponent.ComponentInstallState.Completed;

            _mainConfigInstance.allComponents = new List<ModComponent> { mod1, mod2 };

            var coordinator1 = new InstallCoordinator();
            _ = await coordinator1.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);
            coordinator1.CheckpointManager.UpdateComponentState(mod1);
            coordinator1.CheckpointManager.UpdateComponentState(mod2);
            await coordinator1.CheckpointManager.SaveAsync();

            var coordinator2 = new InstallCoordinator();
            ResumeResult resume = await coordinator2.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var restored1 = resume.OrderedComponents.First(c => c.Guid == mod1.Guid);
            var restored2 = resume.OrderedComponents.First(c => c.Guid == mod2.Guid);

            Assert.Multiple(() =>
            {
                Assert.That(restored1.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod 1 should be completed");
                Assert.That(restored2.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod 2 should be completed");
            });
        }

        [Test]
        public async Task Checkpoint_WithMixedStates_ResumesCorrectly()
        {
            var mod1 = TestComponentFactory.CreateComponent("Mod 1", _workingDirectory);
            mod1.InstallState = ModComponent.ComponentInstallState.Completed;

            var mod2 = TestComponentFactory.CreateComponent("Mod 2", _workingDirectory);
            mod2.InstallState = ModComponent.ComponentInstallState.Running;

            var mod3 = TestComponentFactory.CreateComponent("Mod 3", _workingDirectory);
            mod3.InstallState = ModComponent.ComponentInstallState.Pending;

            var mod4 = TestComponentFactory.CreateComponent("Mod 4", _workingDirectory);
            mod4.InstallState = ModComponent.ComponentInstallState.Failed;

            _mainConfigInstance.allComponents = new List<ModComponent> { mod1, mod2, mod3, mod4 };

            var coordinator1 = new InstallCoordinator();
            _ = await coordinator1.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);
            coordinator1.CheckpointManager.UpdateComponentState(mod1);
            coordinator1.CheckpointManager.UpdateComponentState(mod2);
            coordinator1.CheckpointManager.UpdateComponentState(mod3);
            coordinator1.CheckpointManager.UpdateComponentState(mod4);
            await coordinator1.CheckpointManager.SaveAsync();

            var coordinator2 = new InstallCoordinator();
            ResumeResult resume = await coordinator2.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var restored1 = resume.OrderedComponents.First(c => c.Guid == mod1.Guid);
            var restored2 = resume.OrderedComponents.First(c => c.Guid == mod2.Guid);
            var restored3 = resume.OrderedComponents.First(c => c.Guid == mod3.Guid);
            var restored4 = resume.OrderedComponents.First(c => c.Guid == mod4.Guid);

            Assert.Multiple(() =>
            {
                Assert.That(restored1.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod 1 should be completed");
                Assert.That(restored2.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Running), "Mod 2 should be running");
                Assert.That(restored3.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Pending), "Mod 3 should be pending");
                Assert.That(restored4.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Failed), "Mod 4 should be failed");
            });
        }

        [Test]
        public async Task Checkpoint_WithComponentStateChanges_UpdatesCorrectly()
        {
            var mod = TestComponentFactory.CreateComponent("Mod", _workingDirectory);
            mod.InstallState = ModComponent.ComponentInstallState.Pending;

            _mainConfigInstance.allComponents = new List<ModComponent> { mod };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            // Change state
            mod.InstallState = ModComponent.ComponentInstallState.Running;
            coordinator.CheckpointManager.UpdateComponentState(mod);
            await coordinator.CheckpointManager.SaveAsync();

            // Resume and verify
            var coordinator2 = new InstallCoordinator();
            ResumeResult resume2 = await coordinator2.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var restored = resume2.OrderedComponents.First(c => c.Guid == mod.Guid);

            Assert.Multiple(() =>
            {
                Assert.That(restored.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Running), "State should be updated");
            });
        }

        [Test]
        public async Task Checkpoint_WithDependencyChain_ResumesCorrectly()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            modA.InstallState = ModComponent.ComponentInstallState.Completed;

            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.InstallState = ModComponent.ComponentInstallState.Completed;
            modB.Dependencies = new List<Guid> { modA.Guid };

            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            modC.InstallState = ModComponent.ComponentInstallState.Pending;
            modC.Dependencies = new List<Guid> { modB.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC };

            var coordinator1 = new InstallCoordinator();
            _ = await coordinator1.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);
            coordinator1.CheckpointManager.UpdateComponentState(modA);
            coordinator1.CheckpointManager.UpdateComponentState(modB);
            coordinator1.CheckpointManager.UpdateComponentState(modC);
            await coordinator1.CheckpointManager.SaveAsync();

            var coordinator2 = new InstallCoordinator();
            ResumeResult resume = await coordinator2.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var restoredA = resume.OrderedComponents.First(c => c.Guid == modA.Guid);
            var restoredB = resume.OrderedComponents.First(c => c.Guid == modB.Guid);
            var restoredC = resume.OrderedComponents.First(c => c.Guid == modC.Guid);

            Assert.Multiple(() =>
            {
                Assert.That(restoredA.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod A should be completed");
                Assert.That(restoredB.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod B should be completed");
                Assert.That(restoredC.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Pending), "Mod C should be pending");
            });
        }

        [Test]
        public async Task Checkpoint_WithFailedDependency_BlocksDescendants()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            modA.InstallState = ModComponent.ComponentInstallState.Failed;
            // Make Mod A fail
            string archivePath = Path.Combine(_workingDirectory.FullName, "Mod A.zip");
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.Dependencies = new List<Guid> { modA.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB };

            var coordinator1 = new InstallCoordinator();
            _ = await coordinator1.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);
            coordinator1.CheckpointManager.UpdateComponentState(modA);
            coordinator1.CheckpointManager.UpdateComponentState(modB);
            await coordinator1.CheckpointManager.SaveAsync();

            var coordinator2 = new InstallCoordinator();
            ResumeResult resume = await coordinator2.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var restoredA = resume.OrderedComponents.First(c => c.Guid == modA.Guid);
            var restoredB = resume.OrderedComponents.First(c => c.Guid == modB.Guid);

            Assert.Multiple(() =>
            {
                Assert.That(restoredA.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Failed), "Mod A should be failed");
                Assert.That(restoredB.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Blocked), "Mod B should be blocked");
            });
        }

        [Test]
        public async Task Checkpoint_WithMultipleSessions_ResumesLatest()
        {
            var mod = TestComponentFactory.CreateComponent("Mod", _workingDirectory);
            mod.InstallState = ModComponent.ComponentInstallState.Pending;

            _mainConfigInstance.allComponents = new List<ModComponent> { mod };

            // First session
            var coordinator1 = new InstallCoordinator();
            _ = await coordinator1.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);
            mod.InstallState = ModComponent.ComponentInstallState.Running;
            coordinator1.CheckpointManager.UpdateComponentState(mod);
            await coordinator1.CheckpointManager.SaveAsync();

            // Second session
            var coordinator2 = new InstallCoordinator();
            _ = await coordinator2.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);
            mod.InstallState = ModComponent.ComponentInstallState.Completed;
            coordinator2.CheckpointManager.UpdateComponentState(mod);
            await coordinator2.CheckpointManager.SaveAsync();

            // Resume and verify latest state
            var coordinator3 = new InstallCoordinator();
            ResumeResult resume = await coordinator3.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var restored = resume.OrderedComponents.First(c => c.Guid == mod.Guid);

            Assert.Multiple(() =>
            {
                Assert.That(restored.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Should resume latest state");
            });
        }

        #endregion

        #region Resume Edge Cases

        [Test]
        public async Task Resume_WithNewComponents_AddsToOrder()
        {
            var mod1 = TestComponentFactory.CreateComponent("Mod 1", _workingDirectory);
            mod1.InstallState = ModComponent.ComponentInstallState.Completed;

            _mainConfigInstance.allComponents = new List<ModComponent> { mod1 };

            var coordinator1 = new InstallCoordinator();
            _ = await coordinator1.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);
            coordinator1.CheckpointManager.UpdateComponentState(mod1);
            await coordinator1.CheckpointManager.SaveAsync();

            // Add new component
            var mod2 = TestComponentFactory.CreateComponent("Mod 2", _workingDirectory);
            _mainConfigInstance.allComponents.Add(mod2);

            var coordinator2 = new InstallCoordinator();
            ResumeResult resume = await coordinator2.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(resume.OrderedComponents, Has.Count.EqualTo(2), "Should contain both components");
                Assert.That(resume.OrderedComponents.Any(c => c.Guid == mod1.Guid), Is.True, "Should contain Mod 1");
                Assert.That(resume.OrderedComponents.Any(c => c.Guid == mod2.Guid), Is.True, "Should contain Mod 2");
            });
        }

        [Test]
        public async Task Resume_WithRemovedComponents_HandlesGracefully()
        {
            var mod1 = TestComponentFactory.CreateComponent("Mod 1", _workingDirectory);
            mod1.InstallState = ModComponent.ComponentInstallState.Completed;

            var mod2 = TestComponentFactory.CreateComponent("Mod 2", _workingDirectory);
            mod2.InstallState = ModComponent.ComponentInstallState.Pending;

            _mainConfigInstance.allComponents = new List<ModComponent> { mod1, mod2 };

            var coordinator1 = new InstallCoordinator();
            _ = await coordinator1.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);
            coordinator1.CheckpointManager.UpdateComponentState(mod1);
            coordinator1.CheckpointManager.UpdateComponentState(mod2);
            await coordinator1.CheckpointManager.SaveAsync();

            // Remove mod2
            _mainConfigInstance.allComponents.Remove(mod2);

            var coordinator2 = new InstallCoordinator();
            ResumeResult resume = await coordinator2.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(resume.OrderedComponents, Has.Count.EqualTo(1), "Should contain only remaining component");
                Assert.That(resume.OrderedComponents.Any(c => c.Guid == mod1.Guid), Is.True, "Should contain Mod 1");
            });
        }

        #endregion
    }
}

