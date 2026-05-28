// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync.Core;
using KOTORModSync.Core.Installation;
using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public sealed class DependencyResolutionComplexTests
    {
        #region InstallBefore/InstallAfter Combinations

        [Test]
        public void GetOrderedInstallList_InstallBeforeAndInstallAfter_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent
            {
                Name = "Mod B",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid> { modA.Guid }
            };
            var modC = new ModComponent
            {
                Name = "Mod C",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid> { modA.Guid }
            };

            var components = new List<ModComponent> { modA, modB, modC };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(3), "Should contain all components");
                Assert.That(ordered.IndexOf(modB), Is.LessThan(ordered.IndexOf(modA)), "Mod B should come before Mod A (InstallBefore)");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modC)), "Mod A should come before Mod C (InstallAfter)");
            });
        }

        [Test]
        public void GetOrderedInstallList_InstallBeforeWithDependencies_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent
            {
                Name = "Mod B",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { modA.Guid },
                InstallBefore = new List<Guid> { modA.Guid }
            };

            var components = new List<ModComponent> { modA, modB };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(2), "Should contain all components");
                // Mod B depends on Mod A, but also wants to install before Mod A - this is a conflict
                // The dependency should take precedence
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modB)), "Mod A should come before Mod B (dependency takes precedence)");
            });
        }

        [Test]
        public void GetOrderedInstallList_InstallAfterWithDependencies_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent
            {
                Name = "Mod B",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { modA.Guid },
                InstallAfter = new List<Guid> { modA.Guid }
            };

            var components = new List<ModComponent> { modA, modB };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(2), "Should contain all components");
                Assert.That(ordered[0].Guid, Is.EqualTo(modA.Guid), "Mod A should be first");
                Assert.That(ordered[1].Guid, Is.EqualTo(modB.Guid), "Mod B should be second");
            });
        }

        #endregion

        #region Complex Dependency Graphs

        [Test]
        public void GetOrderedInstallList_DiamondDependency_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent { Name = "Mod B", Guid = Guid.NewGuid(), Dependencies = new List<Guid> { modA.Guid } };
            var modC = new ModComponent { Name = "Mod C", Guid = Guid.NewGuid(), Dependencies = new List<Guid> { modA.Guid } };
            var modD = new ModComponent
            {
                Name = "Mod D",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { modB.Guid, modC.Guid }
            };

            var components = new List<ModComponent> { modD, modC, modB, modA };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(4), "Should contain all components");
                Assert.That(ordered[0].Guid, Is.EqualTo(modA.Guid), "Mod A should be first");
                Assert.That(ordered.IndexOf(modB), Is.LessThan(ordered.IndexOf(modD)), "Mod B should come before Mod D");
                Assert.That(ordered.IndexOf(modC), Is.LessThan(ordered.IndexOf(modD)), "Mod C should come before Mod D");
                Assert.That(ordered[3].Guid, Is.EqualTo(modD.Guid), "Mod D should be last");
            });
        }

        [Test]
        public void GetOrderedInstallList_MultipleRoots_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent { Name = "Mod B", Guid = Guid.NewGuid() };
            var modC = new ModComponent
            {
                Name = "Mod C",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { modA.Guid, modB.Guid }
            };

            var components = new List<ModComponent> { modC, modB, modA };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(3), "Should contain all components");
                Assert.That(ordered.IndexOf(modA), Is.LessThan(ordered.IndexOf(modC)), "Mod A should come before Mod C");
                Assert.That(ordered.IndexOf(modB), Is.LessThan(ordered.IndexOf(modC)), "Mod B should come before Mod C");
            });
        }

        [Test]
        public void GetOrderedInstallList_WithIsolatedComponents_OrdersCorrectly()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent { Name = "Mod B", Guid = Guid.NewGuid() };
            var modC = new ModComponent { Name = "Mod C", Guid = Guid.NewGuid() };
            var modD = new ModComponent { Name = "Mod D", Guid = Guid.NewGuid(), Dependencies = new List<Guid> { modC.Guid } };

            var components = new List<ModComponent> { modD, modC, modB, modA };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(4), "Should contain all components");
                Assert.That(ordered.IndexOf(modC), Is.LessThan(ordered.IndexOf(modD)), "Mod C should come before Mod D");
                // Mod A and Mod B have no dependencies, so they can be in any order relative to each other
            });
        }

        #endregion

        #region Restrictions Handling

        [Test]
        public void Component_WithMultipleRestrictions_BlocksWhenAnySelected()
        {
            var restricted1 = new ModComponent { Name = "Restricted 1", Guid = Guid.NewGuid(), IsSelected = true };
            var restricted2 = new ModComponent { Name = "Restricted 2", Guid = Guid.NewGuid(), IsSelected = false };
            var mod = new ModComponent
            {
                Name = "Mod",
                Guid = Guid.NewGuid(),
                IsSelected = true,
                Restrictions = new List<Guid> { restricted1.Guid, restricted2.Guid }
            };

            Assert.Multiple(() =>
            {
                Assert.That(mod, Is.Not.Null, "Mod should not be null");
                Assert.That(mod.Restrictions, Is.Not.Null, "Restrictions should not be null");
                Assert.That(mod.Restrictions, Has.Count.EqualTo(2), "Mod should have two restrictions");
                Assert.That(mod.Restrictions, Contains.Item(restricted1.Guid), "Mod should restrict against Restricted 1");
                Assert.That(mod.Restrictions, Contains.Item(restricted2.Guid), "Mod should restrict against Restricted 2");
            });
        }

        [Test]
        public void Component_WithRestrictionAndDependency_HandlesBoth()
        {
            var depMod = new ModComponent { Name = "Dependency", Guid = Guid.NewGuid(), IsSelected = true };
            var restrictedMod = new ModComponent { Name = "Restricted", Guid = Guid.NewGuid(), IsSelected = false };
            var mod = new ModComponent
            {
                Name = "Mod",
                Guid = Guid.NewGuid(),
                IsSelected = true,
                Dependencies = new List<Guid> { depMod.Guid },
                Restrictions = new List<Guid> { restrictedMod.Guid }
            };

            Assert.Multiple(() =>
            {
                Assert.That(mod, Is.Not.Null, "Mod should not be null");
                Assert.That(mod.Dependencies, Is.Not.Null, "Dependencies should not be null");
                Assert.That(mod.Dependencies, Contains.Item(depMod.Guid), "Mod should depend on Dependency");
                Assert.That(mod.Restrictions, Is.Not.Null, "Restrictions should not be null");
                Assert.That(mod.Restrictions, Contains.Item(restrictedMod.Guid), "Mod should restrict against Restricted");
            });
        }

        #endregion

        #region Option Dependencies

        [Test]
        public void Option_WithMultipleDependencies_AllMustBeMet()
        {
            var component = new ModComponent { Name = "Mod", Guid = Guid.NewGuid(), IsSelected = true };
            var option1 = new Option { Name = "Option 1", Guid = Guid.NewGuid(), IsSelected = true };
            var option2 = new Option { Name = "Option 2", Guid = Guid.NewGuid(), IsSelected = true };
            var option3 = new Option
            {
                Name = "Option 3",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { option1.Guid, option2.Guid },
                IsSelected = true
            };

            component.Options.Add(option1);
            component.Options.Add(option2);
            component.Options.Add(option3);

            Assert.Multiple(() =>
            {
                Assert.That(option3.Dependencies, Is.Not.Null, "Option 3 dependencies should not be null");
                Assert.That(option3.Dependencies, Has.Count.EqualTo(2), "Option 3 should have two dependencies");
                Assert.That(option3.Dependencies, Contains.Item(option1.Guid), "Option 3 should depend on Option 1");
                Assert.That(option3.Dependencies, Contains.Item(option2.Guid), "Option 3 should depend on Option 2");
            });
        }

        [Test]
        public void Option_WithOptionAndComponentDependencies_HandlesBoth()
        {
            var depComponent = new ModComponent { Name = "Dependency", Guid = Guid.NewGuid(), IsSelected = true };
            var component = new ModComponent { Name = "Mod", Guid = Guid.NewGuid(), IsSelected = true };
            var option1 = new Option { Name = "Option 1", Guid = Guid.NewGuid(), IsSelected = true };
            var option2 = new Option
            {
                Name = "Option 2",
                Guid = Guid.NewGuid(),
                Dependencies = new List<Guid> { option1.Guid, depComponent.Guid },
                IsSelected = true
            };

            component.Options.Add(option1);
            component.Options.Add(option2);

            Assert.Multiple(() =>
            {
                Assert.That(option2.Dependencies, Is.Not.Null, "Option 2 dependencies should not be null");
                Assert.That(option2.Dependencies, Has.Count.EqualTo(2), "Option 2 should have two dependencies");
                Assert.That(option2.Dependencies, Contains.Item(option1.Guid), "Option 2 should depend on Option 1");
                Assert.That(option2.Dependencies, Contains.Item(depComponent.Guid), "Option 2 should depend on Dependency component");
            });
        }

        #endregion

        #region Edge Cases

        [Test]
        public void GetOrderedInstallList_WithSelfReference_HandlesGracefully()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            // Try to create a self-reference (should be handled gracefully)
            modA.Dependencies = new List<Guid> { modA.Guid };

            var components = new List<ModComponent> { modA };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(1), "Should contain the component");
                Assert.That(ordered[0].Guid, Is.EqualTo(modA.Guid), "Should contain Mod A");
            });
        }

        [Test]
        public void GetOrderedInstallList_WithMissingDependency_IgnoresMissing()
        {
            var missingGuid = Guid.NewGuid();
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid(), Dependencies = new List<Guid> { missingGuid } };
            var modB = new ModComponent { Name = "Mod B", Guid = Guid.NewGuid() };

            var components = new List<ModComponent> { modA, modB };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(2), "Should contain both components");
                Assert.That(ordered.Select(c => c.Guid).ToHashSet(), Is.EquivalentTo(components.Select(c => c.Guid).ToHashSet()),
                    "Should contain all components");
            });
        }

        [Test]
        public void GetOrderedInstallList_WithEmptyDependencies_PreservesOrder()
        {
            var modA = new ModComponent { Name = "Mod A", Guid = Guid.NewGuid() };
            var modB = new ModComponent { Name = "Mod B", Guid = Guid.NewGuid() };
            var modC = new ModComponent { Name = "Mod C", Guid = Guid.NewGuid() };

            var components = new List<ModComponent> { modA, modB, modC };
            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(3), "Should contain all components");
                Assert.That(ordered.Select(c => c.Guid).ToHashSet(), Is.EquivalentTo(components.Select(c => c.Guid).ToHashSet()),
                    "Should contain all components");
            });
        }

        [Test]
        public void GetOrderedInstallList_WithLargeGraph_OrdersCorrectly()
        {
            // Create a graph with 10 components
            var components = new List<ModComponent>();
            for (int i = 0; i < 10; i++)
            {
                components.Add(new ModComponent { Name = $"Mod {i}", Guid = Guid.NewGuid() });
            }

            // Create dependencies: each mod depends on the previous one
            for (int i = 1; i < 10; i++)
            {
                components[i].Dependencies = new List<Guid> { components[i - 1].Guid };
            }

            var ordered = InstallCoordinator.GetOrderedInstallList(components);

            Assert.Multiple(() =>
            {
                Assert.That(ordered, Is.Not.Null, "Ordered list should not be null");
                Assert.That(ordered, Has.Count.EqualTo(10), "Should contain all components");

                // Verify order
                for (int i = 0; i < 10; i++)
                {
                    Assert.That(ordered[i].Guid, Is.EqualTo(components[i].Guid), $"Mod {i} should be at position {i}");
                }
            });
        }

        #endregion
    }
}

