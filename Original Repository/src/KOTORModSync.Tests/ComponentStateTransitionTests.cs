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
    public sealed class ComponentStateTransitionTests
    {
        private DirectoryInfo _workingDirectory;
        private MainConfig _mainConfigInstance;

        [SetUp]
        public void SetUp()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "KOTORModSync_StateTransitionTests_" + Guid.NewGuid().ToString("N"));
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

        #region State Transition Scenarios

        [Test]
        public async Task Component_StateTransition_PendingToRunningToCompleted()
        {
            var component = TestComponentFactory.CreateComponent("StateTest", _workingDirectory);
            component.InstallState = ModComponent.ComponentInstallState.Pending;

            Assert.That(component.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Pending), "Initial state should be Pending");

            _mainConfigInstance.allComponents = new List<ModComponent> { component };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(ModComponent.InstallExitCode.Success), "Installation should succeed");
                Assert.That(component.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Final state should be Completed");
            });
        }

        [Test]
        public async Task Component_StateTransition_PendingToFailed_OnError()
        {
            var component = TestComponentFactory.CreateComponent("FailureTest", _workingDirectory);
            // Make component fail by removing its archive
            string archivePath = Path.Combine(_workingDirectory.FullName, "FailureTest.zip");
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            component.InstallState = ModComponent.ComponentInstallState.Pending;

            _mainConfigInstance.allComponents = new List<ModComponent> { component };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(component.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Failed), "State should be Failed");
            });
        }

        [Test]
        public async Task Component_StateTransition_Blocked_WhenDependencyFails()
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

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(modA.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Failed), "Mod A should fail");
                Assert.That(modB.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Blocked), "Mod B should be blocked");
            });
        }

        [Test]
        public async Task Component_StateTransition_Completed_RemainsCompleted()
        {
            var component = TestComponentFactory.CreateComponent("Completed", _workingDirectory);
            component.InstallState = ModComponent.ComponentInstallState.Completed;

            _mainConfigInstance.allComponents = new List<ModComponent> { component };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(ModComponent.InstallExitCode.Success), "Installation should succeed");
                Assert.That(component.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Completed state should remain");
            });
        }

        [Test]
        public async Task Component_StateTransition_Skipped_WhenNotSelected()
        {
            var component = TestComponentFactory.CreateComponent("Skipped", _workingDirectory);
            component.IsSelected = false;
            component.InstallState = ModComponent.ComponentInstallState.Pending;

            _mainConfigInstance.allComponents = new List<ModComponent> { component };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Does.Not.Contain(component), "Component should not be in ordered list");
                Assert.That(component.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Pending), "State should remain Pending when not selected");
            });
        }

        #endregion

        #region State Persistence

        [Test]
        public async Task Component_StatePersistence_AcrossSessions()
        {
            var component = TestComponentFactory.CreateComponent("Persistence", _workingDirectory);
            component.InstallState = ModComponent.ComponentInstallState.Completed;

            _mainConfigInstance.allComponents = new List<ModComponent> { component };

            var coordinator1 = new InstallCoordinator();
            _ = await coordinator1.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);
            coordinator1.CheckpointManager.UpdateComponentState(component);
            await coordinator1.CheckpointManager.SaveAsync();

            var coordinator2 = new InstallCoordinator();
            ResumeResult resume = await coordinator2.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var restored = resume.OrderedComponents.First(c => c.Guid == component.Guid);

            Assert.Multiple(() =>
            {
                Assert.That(restored.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "State should be persisted");
            });
        }

        [Test]
        public async Task Component_StatePersistence_MultipleStates()
        {
            var component1 = TestComponentFactory.CreateComponent("Component1", _workingDirectory);
            component1.InstallState = ModComponent.ComponentInstallState.Completed;

            var component2 = TestComponentFactory.CreateComponent("Component2", _workingDirectory);
            component2.InstallState = ModComponent.ComponentInstallState.Running;

            var component3 = TestComponentFactory.CreateComponent("Component3", _workingDirectory);
            component3.InstallState = ModComponent.ComponentInstallState.Failed;

            _mainConfigInstance.allComponents = new List<ModComponent> { component1, component2, component3 };

            var coordinator1 = new InstallCoordinator();
            _ = await coordinator1.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);
            coordinator1.CheckpointManager.UpdateComponentState(component1);
            coordinator1.CheckpointManager.UpdateComponentState(component2);
            coordinator1.CheckpointManager.UpdateComponentState(component3);
            await coordinator1.CheckpointManager.SaveAsync();

            var coordinator2 = new InstallCoordinator();
            ResumeResult resume = await coordinator2.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var restored1 = resume.OrderedComponents.First(c => c.Guid == component1.Guid);
            var restored2 = resume.OrderedComponents.First(c => c.Guid == component2.Guid);
            var restored3 = resume.OrderedComponents.First(c => c.Guid == component3.Guid);

            Assert.Multiple(() =>
            {
                Assert.That(restored1.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Component1 state should be persisted");
                Assert.That(restored2.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Running), "Component2 state should be persisted");
                Assert.That(restored3.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Failed), "Component3 state should be persisted");
            });
        }

        #endregion

        #region State with Dependencies

        [Test]
        public async Task Component_StateWithDependency_BlocksWhenDependencyFails()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            string archivePath = Path.Combine(_workingDirectory.FullName, "Mod A.zip");
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.Dependencies = new List<Guid> { modA.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(modA.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Failed), "Mod A should fail");
                Assert.That(modB.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Blocked), "Mod B should be blocked");
            });
        }

        [Test]
        public async Task Component_StateWithDependency_CompletesWhenDependencySucceeds()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.Dependencies = new List<Guid> { modA.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(ModComponent.InstallExitCode.Success), "Installation should succeed");
                Assert.That(modA.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod A should complete");
                Assert.That(modB.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod B should complete");
            });
        }

        #endregion
    }
}

