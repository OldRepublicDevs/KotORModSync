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
    public sealed class InstallCoordinatorComplexTests
    {
        private DirectoryInfo _workingDirectory;
        private MainConfig _mainConfigInstance;

        [SetUp]
        public void SetUp()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "KOTORModSync_CoordinatorTests_" + Guid.NewGuid().ToString("N"));
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
                try
                {
                    InstallCoordinator.ClearSessionForTests(_workingDirectory);
                    Directory.Delete(_workingDirectory.FullName, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region Complex Ordering Scenarios

        [Test]
        public void GetOrderedInstallList_WithDependenciesAndInstallAfter_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent
            {
                Name = "Mod B",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { modA.Guid }
            };
            var modC = new ModComponent
            {
                Name = "Mod C",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { modB.Guid }
            };

            var components = new List<ModComponent> { modC, modB, modA };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(3), "Should contain all components");
                Assert.That(ordered[0].Guid, Is.EqualTo(modA.Guid), "Mod A should be first (dependency)");
                Assert.That(ordered[1].Guid, Is.EqualTo(modB.Guid), "Mod B should be second (depends on A)");
                Assert.That(ordered[2].Guid, Is.EqualTo(modC.Guid), "Mod C should be third (InstallAfter B)");
            });
        }

        [Test]
        public void GetOrderedInstallList_WithDependenciesAndInstallBefore_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent
            {
                Name = "Mod B",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { modA.Guid }
            };
            var modC = new ModComponent
            {
                Name = "Mod C",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid> { modB.Guid }
            };

            var components = new List<ModComponent> { modC, modB, modA };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(3), "Should contain all components");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modB)), "Mod A should come before Mod B");
                Assert.That(ordered.IndexOf(modC), Is.LessThan(ordered.IndexOf(modB)), "Mod C should come before Mod B (InstallBefore)");
            });
        }

        [Test]
        public void GetOrderedInstallList_WithMultipleDependencies_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent { Name = "Mod B", Guid = Guid.NewGuid() };
            var modC = new ModComponent
            {
                Name = "Mod C",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { modA.Guid, modB.Guid }
            };
            var modD = new ModComponent
            {
                Name = "Mod D",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { modC.Guid }
            };

            var components = new List<ModComponent> { modD, modC, modB, modA };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(4), "Should contain all components");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modC)), "Mod A should come before Mod C");
                Assert.That(ordered.IndexOf(modB), Is.LessThan(ordered.IndexOf(modC)), "Mod B should come before Mod C");
                Assert.That(ordered.IndexOf(modC), Is.LessThan(ordered.IndexOf(modD)), "Mod C should come before Mod D");
            });
        }

        [Test]
        public void GetOrderedInstallList_WithMixedDependencies_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent
            {
                Name = "Mod B",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { modA.Guid },
                InstallAfter = new List<Guid> { modA.Guid }
            };
            var modC = new ModComponent
            {
                Name = "Mod C",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid> { modB.Guid }
            };

            var components = new List<ModComponent> { modC, modB, modA };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(3), "Should contain all components");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modB)), "Mod A should come before Mod B");
                Assert.That(ordered.IndexOf(modC), Is.LessThan(ordered.IndexOf(modB)), "Mod C should come before Mod B");
            });
        }

        #endregion

        #region Component State Management

        [Test]
        public async Task InstallCoordinator_WithMixedStates_RespectsState()
        {
            var component1 = TestComponentFactory.CreateComponent("Completed", _workingDirectory);
            component1.InstallState = ModComponent.ComponentInstallState.Completed;

            var component2 = TestComponentFactory.CreateComponent("Pending", _workingDirectory);
            component2.InstallState = ModComponent.ComponentInstallState.Pending;

            var component3 = TestComponentFactory.CreateComponent("Blocked", _workingDirectory);
            component3.InstallState = ModComponent.ComponentInstallState.Blocked;

            _mainConfigInstance.allComponents = new List<ModComponent> { component1, component2, component3 };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(resume.OrderedComponents, Is.Not.Null, "Ordered components should not be null");
                Assert.That(resume.OrderedComponents, Has.Count.EqualTo(3), "Should contain all components");

                var completed = resume.OrderedComponents.First(c => c.Guid == component1.Guid);
                Assert.That(completed.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Completed component should remain completed");

                var pending = resume.OrderedComponents.First(c => c.Guid == component2.Guid);
                Assert.That(pending.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Pending), "Pending component should remain pending");

                var blocked = resume.OrderedComponents.First(c => c.Guid == component3.Guid);
                Assert.That(blocked.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Blocked), "Blocked component should remain blocked");
            });
        }

        [Test]
        public async Task InstallCoordinator_WithFailedComponent_BlocksDescendants()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.Dependencies = new List<Guid> { modA.Guid };
            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            modC.Dependencies = new List<Guid> { modB.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC };

            // Simulate Mod A failing
            modA.InstallState = ModComponent.ComponentInstallState.Failed;

            InstallCoordinator.MarkBlockedDescendants(_mainConfigInstance.allComponents, modA.Guid);

            Assert.Multiple(() =>
            {
                Assert.That(modA.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Failed), "Mod A should be failed");
                Assert.That(modB.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Blocked), "Mod B should be blocked");
                Assert.That(modC.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Blocked), "Mod C should be blocked");
            });
        }

        [Test]
        public async Task InstallCoordinator_WithPartialCompletion_ResumesCorrectly()
        {
            var component1 = TestComponentFactory.CreateComponent("Completed", _workingDirectory);
            component1.InstallState = ModComponent.ComponentInstallState.Completed;

            var component2 = TestComponentFactory.CreateComponent("Pending", _workingDirectory);
            component2.InstallState = ModComponent.ComponentInstallState.Pending;

            _mainConfigInstance.allComponents = new List<ModComponent> { component1, component2 };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            // Simulate installation
            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(resume.OrderedComponents, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(ModComponent.InstallExitCode.Success), "Installation should succeed");
                Assert.That(component1.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Completed component should remain completed");
                Assert.That(component2.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Pending component should be completed");
            });
        }

        #endregion

        #region Checkpoint Persistence

        [Test]
        public async Task CheckpointManager_WithMultipleComponents_PersistsAllStates()
        {
            var component1 = TestComponentFactory.CreateComponent("Component1", _workingDirectory);
            var component2 = TestComponentFactory.CreateComponent("Component2", _workingDirectory);
            var component3 = TestComponentFactory.CreateComponent("Component3", _workingDirectory);

            _mainConfigInstance.allComponents = new List<ModComponent> { component1, component2, component3 };

            var coordinator = new InstallCoordinator();
            _ = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            component1.InstallState = ModComponent.ComponentInstallState.Completed;
            component2.InstallState = ModComponent.ComponentInstallState.Running;
            component3.InstallState = ModComponent.ComponentInstallState.Pending;

            coordinator.CheckpointManager.UpdateComponentState(component1);
            coordinator.CheckpointManager.UpdateComponentState(component2);
            coordinator.CheckpointManager.UpdateComponentState(component3);
            await coordinator.CheckpointManager.SaveAsync();

            var secondCoordinator = new InstallCoordinator();
            ResumeResult resume = await secondCoordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(resume.OrderedComponents, Has.Count.EqualTo(3), "Should restore all components");

                var restored1 = resume.OrderedComponents.First(c => c.Guid == component1.Guid);
                Assert.That(restored1.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Component1 should be completed");

                var restored2 = resume.OrderedComponents.First(c => c.Guid == component2.Guid);
                Assert.That(restored2.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Running), "Component2 should be running");

                var restored3 = resume.OrderedComponents.First(c => c.Guid == component3.Guid);
                Assert.That(restored3.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Pending), "Component3 should be pending");
            });
        }

        [Test]
        public async Task CheckpointManager_WithComponentOrder_PersistsOrder()
        {
            var modA = TestComponentFactory.CreateComponent("Mod A", _workingDirectory);
            var modB = TestComponentFactory.CreateComponent("Mod B", _workingDirectory);
            modB.Dependencies = new List<Guid> { modA.Guid };
            var modC = TestComponentFactory.CreateComponent("Mod C", _workingDirectory);
            modC.Dependencies = new List<Guid> { modB.Guid };

            _mainConfigInstance.allComponents = new List<ModComponent> { modA, modB, modC };

            var coordinator = new InstallCoordinator();
            ResumeResult resume1 = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var originalOrder = resume1.OrderedComponents.Select(c => c.Guid).ToList();
            await coordinator.CheckpointManager.SaveAsync();

            var secondCoordinator = new InstallCoordinator();
            ResumeResult resume2 = await secondCoordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var restoredOrder = resume2.OrderedComponents.Select(c => c.Guid).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(restoredOrder, Is.EqualTo(originalOrder), "Component order should be preserved");
                Assert.That(restoredOrder[0], Is.EqualTo(modA.Guid), "Mod A should be first");
                Assert.That(restoredOrder[1], Is.EqualTo(modB.Guid), "Mod B should be second");
                Assert.That(restoredOrder[2], Is.EqualTo(modC.Guid), "Mod C should be third");
            });
        }

        #endregion

        #region Complex Dependency Chains

        [Test]
        public void GetOrderedInstallList_WithDeepDependencyChain_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent { Name = "Mod B", Guid = Guid.NewGuid(), Dependencies = new List<Guid> { modA.Guid } };
            var modC = new ModComponent { Name = "Mod C", Guid = Guid.NewGuid(), Dependencies = new List<Guid> { modB.Guid } };
            var modD = new ModComponent { Name = "Mod D", Guid = Guid.NewGuid(), Dependencies = new List<Guid> { modC.Guid } };
            var modE = new ModComponent { Name = "Mod E", Guid = Guid.NewGuid(), Dependencies = new List<Guid> { modD.Guid } };

            var components = new List<ModComponent> { modE, modD, modC, modB, modA };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(5), "Should contain all components");
                Assert.That(ordered[0].Guid, Is.EqualTo(modA.Guid), "Mod A should be first");
                Assert.That(ordered[1].Guid, Is.EqualTo(modB.Guid), "Mod B should be second");
                Assert.That(ordered[2].Guid, Is.EqualTo(modC.Guid), "Mod C should be third");
                Assert.That(ordered[3].Guid, Is.EqualTo(modD.Guid), "Mod D should be fourth");
                Assert.That(ordered[4].Guid, Is.EqualTo(modE.Guid), "Mod E should be fifth");
            });
        }

        [Test]
        public void GetOrderedInstallList_WithBranchingDependencies_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent { Name = "Mod B", Guid = Guid.NewGuid() };
            var modC = new ModComponent
            {
                Name = "Mod C",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { modA.Guid, modB.Guid }
            };
            var modD = new ModComponent
            {
                Name = "Mod D",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { modC.Guid }
            };

            var components = new List<ModComponent> { modD, modC, modB, modA };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(4), "Should contain all components");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modC)), "Mod A should come before Mod C");
                Assert.That(ordered.IndexOf(modB), Is.LessThan(ordered.IndexOf(modC)), "Mod B should come before Mod C");
                Assert.That(ordered.IndexOf(modC), Is.LessThan(ordered.IndexOf(modD)), "Mod C should come before Mod D");
            });
        }

        [Test]
        public void GetOrderedInstallList_WithInstallAfterChain_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent
            {
                Name = "Mod B",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { modA.Guid }
            };
            var modC = new ModComponent
            {
                Name = "Mod C",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { modB.Guid }
            };

            var components = new List<ModComponent> { modC, modB, modA };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(3), "Should contain all components");
                Assert.That(ordered[0].Guid, Is.EqualTo(modA.Guid), "Mod A should be first");
                Assert.That(ordered[1].Guid, Is.EqualTo(modB.Guid), "Mod B should be second");
                Assert.That(ordered[2].Guid, Is.EqualTo(modC.Guid), "Mod C should be third");
            });
        }

        #endregion

        #region Error Recovery

        [Test]
        public async Task InstallCoordinator_WithFailedComponent_ContinuesWithOthers()
        {
            var component1 = TestComponentFactory.CreateComponent("Success", _workingDirectory);
            var component2 = TestComponentFactory.CreateComponent("WillFail", _workingDirectory);
            // Make component2 fail by removing its archive
            string archivePath = Path.Combine(_workingDirectory.FullName, "WillFail.zip");
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            _mainConfigInstance.allComponents = new List<ModComponent> { component1, component2 };

            var coordinator = new InstallCoordinator();
            ResumeResult resume = await coordinator.InitializeAsync(MainConfig.AllComponents, MainConfig.DestinationPath, CancellationToken.None);

            var exitCode = await InstallationService.InstallAllSelectedComponentsAsync(resume.OrderedComponents, progressCallback: null, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(component1.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Completed), "Component1 should complete");
                Assert.That(component2.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Failed), "Component2 should fail");
            });
        }

        [Test]
        public void MarkBlockedDescendants_WithMultipleDependents_BlocksAll()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent { Name = "Mod B", Guid = Guid.NewGuid(), Dependencies = new List<Guid> { modA.Guid } };
            var modC = new ModComponent { Name = "Mod C", Guid = Guid.NewGuid(), Dependencies = new List<Guid> { modA.Guid } };
            var modD = new ModComponent { Name = "Mod D", Guid = Guid.NewGuid(), Dependencies = new List<Guid> { modB.Guid } };

            var components = new List<ModComponent> { modA, modB, modC, modD };
            modA.InstallState = ModComponent.ComponentInstallState.Failed;

            InstallCoordinator.MarkBlockedDescendants(components, modA.Guid);

            Assert.Multiple(() =>
            {
                Assert.That(modA.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Failed), "Mod A should be failed");
                Assert.That(modB.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Blocked), "Mod B should be blocked");
                Assert.That(modC.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Blocked), "Mod C should be blocked");
                Assert.That(modD.InstallState, Is.EqualTo(ModComponent.ComponentInstallState.Blocked), "Mod D should be blocked (transitive)");
            });
        }

        #endregion
    }
}

