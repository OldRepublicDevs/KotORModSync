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
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class MultiComponentInstallationTests
    {
        private DirectoryInfo _workingDirectory;
        private MainConfig _mainConfigInstance;

        [SetUp]
        public void SetUp()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "KOTORModSync_MultiComponentTests_" + Guid.NewGuid().ToString("N"));
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

        #region Complex Multi-Component Scenarios

        [Test]
        public async Task InstallAllSelectedComponents_WithDependencyChain_InstallsInOrder()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
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
                Assert.That(exitCode, Is.EqualTo(ModComponent.InstallExitCode.Success), "Installation should succeed");
                Assert.That(modA.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod A should be completed");
                Assert.That(modB.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod B should be completed");
                Assert.That(modC.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod C should be completed");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modB)), "Mod A should install before Mod B");
                Assert.That(ordered.IndexOf(modB), Is.LessThan(ordered.IndexOf(modC)), "Mod B should install before Mod C");
            });
        }

        [Test]
        public async Task InstallAllSelectedComponents_WithPartialSelection_InstallsOnlySelected()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            modA.IsSelected = true;
            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.IsSelected = false; // Not selected
            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            modC.IsSelected = true;

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(ModComponent.InstallExitCode.Success), "Installation should succeed");
                Assert.That(modA.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod A should be completed");
                Assert.That(modB.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Pending), "Mod B should remain pending (not selected)");
                Assert.That(modC.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod C should be completed");
                Assert.That(ordered, Has.Count.EqualTo(2), "Only selected components should be in ordered list");
                Assert.That(ordered.Select(c => c.Guid), Contains.Item(modA.Guid), "Mod A should be in ordered list");
                Assert.That(ordered.Select(c => c.Guid), Contains.Item(modC.Guid), "Mod C should be in ordered list");
                Assert.That(ordered.Select(c => c.Guid), Does.Not.Contain(modB.Guid), "Mod B should not be in ordered list");
            });
        }

        [Test]
        public async Task InstallAllSelectedComponents_WithMixedStates_RespectsStates()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            modA.InstallState = ModComponent.ComponentInstallState.Completed;
            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.InstallState = ModComponent.ComponentInstallState.Pending;
            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            modC.InstallState = ModComponent.ComponentInstallState.Blocked;

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(ModComponent.InstallExitCode.Success), "Installation should succeed");
                Assert.That(modA.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod A should remain completed");
                Assert.That(modB.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Mod B should be completed");
                Assert.That(modC.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Blocked), "Mod C should remain blocked");
            });
        }

        [Test]
        public async Task InstallAllSelectedComponents_WithFailedDependency_BlocksDescendants()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            // Make Mod A fail by removing its archive
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

        #region InstallBefore/InstallAfter Scenarios

        [Test]
        public async Task InstallAllSelectedComponents_WithInstallAfter_RespectsOrder()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.InstallAfter = new List<Guid> { modA.Guid };
            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            modC.InstallAfter = new List<Guid> { modB.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Has.Count.EqualTo(3), "Should contain all components");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modB)), "Mod A should come before Mod B");
                Assert.That(ordered.IndexOf(modB), Is.LessThan(ordered.IndexOf(modC)), "Mod B should come before Mod C");
            });
        }

        [Test]
        public async Task InstallAllSelectedComponents_WithInstallBefore_RespectsOrder()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modA.InstallBefore = new List<Guid> { modB.Guid };
            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            modB.InstallBefore = new List<Guid> { modC.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Has.Count.EqualTo(3), "Should contain all components");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modB)), "Mod A should come before Mod B");
                Assert.That(ordered.IndexOf(modB), Is.LessThan(ordered.IndexOf(modC)), "Mod B should come before Mod C");
            });
        }

        #endregion

        #region Component State Transitions

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
        public async Task Component_StateTransition_WithFailure_TransitionsToFailed()
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

        #endregion

        #region Checkpoint and Resume Scenarios

        [Test]
        public async Task InstallAllSelectedComponents_WithCheckpoint_ResumesCorrectly()
        {
            var component1 = TestComponentFactory.CreateComponent("Component1", _workingDirectory);
            var component2 = TestComponentFactory.CreateComponent("Component2", _workingDirectory);

            _mainConfigInstance.allComponents = new List<ModComponent> { component1, component2 };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();

            // Install first component
            component1.InstallState = ModComponent.ComponentInstallState.Completed;
            coordinator.CheckpointManager.UpdateComponentState(component1);
            await coordinator.CheckpointManager.SaveAsync();

            // Create new coordinator and resume
            var secondCoordinator = new InstallCoordinator();
            ResumeResult resume2 = await secondCoordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var restored1 = resume2.OrderedComponents.First(c => c.Guid == component1.Guid);
            var restored2 = resume2.OrderedComponents.First(c => c.Guid == component2.Guid);

            Assert.Multiple(() =>
            {
                Assert.That(restored1.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Component1 should be restored as completed");
                Assert.That(restored2.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Pending), "Component2 should be restored as pending");
            });
        }

        #endregion

        #region Complex Dependency Scenarios

        [Test]
        public async Task InstallAllSelectedComponents_WithDiamondDependency_InstallsCorrectly()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.Dependencies = new List<Guid> { modA.Guid };
            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            modC.Dependencies = new List<Guid> { modA.Guid };
            var modD = TestComponentFactory.CreateComponent("Mod D", _workingDirectory);
            modD.Dependencies = new List<Guid> { modB.Guid, modC.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC, modD };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(ModComponent.InstallExitCode.Success), "Installation should succeed");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modB)), "Mod A should come before Mod B");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modC)), "Mod A should come before Mod C");
                Assert.That(ordered.IndexOf(modB), Is.LessThan(ordered.IndexOf(modD)), "Mod B should come before Mod D");
                Assert.That(ordered.IndexOf(modC), Is.LessThan(ordered.IndexOf(modD)), "Mod C should come before Mod D");
            });
        }

        [Test]
        public async Task InstallAllSelectedComponents_WithBranchingDependencies_InstallsCorrectly()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            modC.Dependencies = new List<Guid> { modA.Guid, modB.Guid };
            var modD = TestComponentFactory.CreateComponent("Mod D", _workingDirectory);
            modD.Dependencies = new List<Guid> { modC.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC, modD };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var ordered = resume.OrderedComponents.Where(c => c.IsSelected).ToList();
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(ordered, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(ModComponent.InstallExitCode.Success), "Installation should succeed");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modC)), "Mod A should come before Mod C");
                Assert.That(ordered.IndexOf(modB), Is.LessThan(ordered.IndexOf(modC)), "Mod B should come before Mod C");
                Assert.That(ordered.IndexOf(modC), Is.LessThan(ordered.IndexOf(modD)), "Mod C should come before Mod D");
            });
        }

        #endregion
    }
}

