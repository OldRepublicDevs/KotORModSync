// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using KOTORModSync.Core;
using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    internal class ComponentOrderTests
    {
        [Test]
        public void ConfirmComponentsInstallOrder_InstallBefore_ReturnsTrue()
        {

            var thisGuid = Guid.NewGuid();
            var componentsListExpectedOrder = new List<ModComponent>
            {
            new ModComponent
            {
                Name = "C1_InstallBefore_C2",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid>
                {
                    thisGuid,
                },
            },
            new ModComponent
            {
                Name = "C2", Guid = thisGuid,
            },
            new ModComponent
            {
                Name = "C3", Guid = Guid.NewGuid(),
            },
            };

            (bool isCorrectOrder, List<ModComponent> reorderedComponents) =
                ModComponent.ConfirmComponentsInstallOrder(componentsListExpectedOrder);

            Assert.Multiple(() =>
            {
                Assert.That(componentsListExpectedOrder, Is.Not.Null, "Expected order list should not be null");
                Assert.That(componentsListExpectedOrder, Is.Not.Empty, "Expected order list should not be empty");
                Assert.That(reorderedComponents, Is.Not.Null, "Reordered components list should not be null");
                Assert.That(reorderedComponents, Is.Not.Empty, "Reordered components list should not be empty");
            });

            foreach (ModComponent component in reorderedComponents)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(component, Is.Not.Null, $"Component should not be null: {component?.Name ?? "Unknown"}");
                    Assert.That(component.Guid, Is.Not.EqualTo(Guid.Empty), $"Component GUID should not be empty: {component.Name}");
                });

                int actualIndex = reorderedComponents.FindIndex(c => c.Guid == component.Guid);
                int expectedIndex = componentsListExpectedOrder.FindIndex(c => c.Guid == component.Guid);
                Assert.Multiple(() =>
                {
                    Assert.That(actualIndex, Is.GreaterThanOrEqualTo(0), $"Component should be found in reordered list: {component.Name}");
                    Assert.That(expectedIndex, Is.GreaterThanOrEqualTo(0), $"Component should be found in expected list: {component.Name}");
                    Assert.That(actualIndex, Is.EqualTo(expectedIndex), $"ModComponent {component.Name} is out of order.");
                });
            }

            Assert.Multiple(() =>
            {
                Assert.That(isCorrectOrder, Is.True, "Components should be in correct order");
                Assert.That(reorderedComponents, Is.Not.Empty, "Reordered components list should not be empty");
                Assert.That(reorderedComponents.Count, Is.EqualTo(componentsListExpectedOrder.Count), "Reordered components count should match expected");
            });
        }

        [Test]
        public void ConfirmComponentsInstallOrder_InstallBefore_ReturnsFalse()
        {

            var thisGuid = Guid.NewGuid();
            var unorderedList = new List<ModComponent>
            {
            new ModComponent
            {
                Name = "C2", Guid = thisGuid,
            },
            new ModComponent
            {
                Name = "C1_InstallBefore_C2",
                Guid = Guid.NewGuid(),
                InstallBefore = new List<Guid>
                {
                    thisGuid,
                },
            },
            new ModComponent
            {
                Name = "C3", Guid = Guid.NewGuid(),
            },
            };

            (bool isCorrectOrder, List<ModComponent> reorderedComponents) =
                ModComponent.ConfirmComponentsInstallOrder(unorderedList);

            var componentsListExpectedOrder = new List<ModComponent>(unorderedList);
            Swap(componentsListExpectedOrder, index1: 0, index2: 1);

            foreach (ModComponent component in reorderedComponents)
            {
                int actualIndex = reorderedComponents.FindIndex(c => c.Guid == component.Guid);
                int expectedIndex = componentsListExpectedOrder.FindIndex(c => c.Guid == component.Guid);
                Assert.That(actualIndex, Is.EqualTo(expectedIndex), $"ModComponent {component.Name} is out of order.");
            }

            Assert.Multiple(
                () =>
                {
                    Assert.That(isCorrectOrder, Is.False);
                    Assert.That(reorderedComponents, Is.Not.Empty);
                }
            );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_InstallAfter_ReturnsTrue()
        {

            var thisGuid = Guid.NewGuid();
            var componentsListExpectedOrder = new List<ModComponent>
            {
            new ModComponent
            {
                Name = "C1", Guid = thisGuid,
            },
            new ModComponent
            {
                Name = "C2_InstallAfter_C1",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid>
                {
                    thisGuid,
                },
            },
            new ModComponent
            {
                Name = "C3", Guid = Guid.NewGuid(),
            },
            };

            (bool isCorrectOrder, List<ModComponent> reorderedComponents) =
                ModComponent.ConfirmComponentsInstallOrder(componentsListExpectedOrder);

            Assert.Multiple(() =>
            {
                Assert.That(componentsListExpectedOrder, Is.Not.Null, "Expected order list should not be null");
                Assert.That(componentsListExpectedOrder, Is.Not.Empty, "Expected order list should not be empty");
                Assert.That(reorderedComponents, Is.Not.Null, "Reordered components list should not be null");
                Assert.That(reorderedComponents, Is.Not.Empty, "Reordered components list should not be empty");
            });

            foreach (ModComponent component in reorderedComponents)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(component, Is.Not.Null, $"Component should not be null: {component?.Name ?? "Unknown"}");
                    Assert.That(component.Guid, Is.Not.EqualTo(Guid.Empty), $"Component GUID should not be empty: {component.Name}");
                });

                int actualIndex = reorderedComponents.FindIndex(c => c.Guid == component.Guid);
                int expectedIndex = componentsListExpectedOrder.FindIndex(c => c.Guid == component.Guid);
                Assert.Multiple(() =>
                {
                    Assert.That(actualIndex, Is.GreaterThanOrEqualTo(0), $"Component should be found in reordered list: {component.Name}");
                    Assert.That(expectedIndex, Is.GreaterThanOrEqualTo(0), $"Component should be found in expected list: {component.Name}");
                    Assert.That(actualIndex, Is.EqualTo(expectedIndex), $"ModComponent {component.Name} is out of order.");
                });
            }

            Assert.Multiple(() =>
            {
                Assert.That(isCorrectOrder, Is.True, "Components should be in correct order");
                Assert.That(reorderedComponents, Is.Not.Empty, "Reordered components list should not be empty");
                Assert.That(reorderedComponents.Count, Is.EqualTo(componentsListExpectedOrder.Count), "Reordered components count should match expected");
            });
        }

        [Test]
        public void ConfirmComponentsInstallOrder_InstallAfter_ReturnsFalse()
        {

            var thisGuid = Guid.NewGuid();
            var unorderedList = new List<ModComponent>
            {
            new ModComponent
            {
                Name = "C1_InstallAfter_C2",
                Guid = Guid.NewGuid(),
                InstallAfter = new List<Guid>
                {
                    thisGuid,
                },
            },
            new ModComponent
            {
                Name = "C2", Guid = thisGuid,
            },
            new ModComponent
            {
                Name = "C3", Guid = Guid.NewGuid(),
            },
            };

            (bool isCorrectOrder, List<ModComponent> reorderedComponents) =
                ModComponent.ConfirmComponentsInstallOrder(unorderedList);

            var componentsListExpectedOrder = new List<ModComponent>(unorderedList);
            Swap(componentsListExpectedOrder, index1: 0, index2: 1);

            foreach (ModComponent component in reorderedComponents)
            {
                int actualIndex = reorderedComponents.FindIndex(c => c.Guid == component.Guid);
                int expectedIndex = componentsListExpectedOrder.FindIndex(c => c.Guid == component.Guid);
                Assert.That(actualIndex, Is.EqualTo(expectedIndex), $"ModComponent {component.Name} is out of order.");
            }

            Assert.Multiple(
                () =>
                {
                    Assert.That(isCorrectOrder, Is.False);
                    Assert.That(reorderedComponents, Is.Not.Empty);
                }
            );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_ComplexScenario_CorrectOrder()
        {

            var componentA = new ModComponent
            {
                Name = "A",
                Guid = Guid.NewGuid(),
            };
            var componentB = new ModComponent
            {
                Name = "B",
                Guid = Guid.NewGuid(),
                InstallAfter =
                new List<Guid>
                {
                    componentA.Guid,
                },
            };
            var componentC = new ModComponent
            {
                Name = "C",
                Guid = Guid.NewGuid(),
                InstallBefore =
                new List<Guid>
                {
                    componentA.Guid,
                },
            };
            var componentD = new ModComponent
            {
                Name = "D",
                Guid = Guid.NewGuid(),
                InstallBefore =
                new List<Guid>
                {
                    componentB.Guid,
                },
            };
            Guid componentFGuid = Guid.Empty;
            var componentE = new ModComponent
            {
                Name = "E",
                Guid = Guid.NewGuid(),
                InstallAfter =
                new List<Guid>
                {
                    componentB.Guid,
                },
                InstallBefore =
                new List<Guid>
                {
                    componentFGuid,
                },
            };
            var componentF = new ModComponent
            {
                Name = "F",
                Guid = componentFGuid,
                InstallAfter =
                new List<Guid>
                {
                    componentE.Guid, componentB.Guid,
                },
            };
            var componentG = new ModComponent
            {
                Name = "G",
                Guid = Guid.NewGuid(),
                InstallAfter =
                new List<Guid>
                {
                    componentD.Guid, componentF.Guid,
                },
            };
            var componentH = new ModComponent
            {
                Name = "H",
                Guid = Guid.NewGuid(),
                InstallBefore =
                new List<Guid>
                {
                    componentG.Guid,
                },
            };
            var componentI = new ModComponent
            {
                Name = "I",
                Guid = Guid.NewGuid(),
                InstallBefore =
                new List<Guid>
                {
                    componentG.Guid,
                },
            };
            var componentJ = new ModComponent
            {
                Name = "J",
                Guid = Guid.NewGuid(),
                InstallAfter =
                new List<Guid>
                {
                    componentH.Guid, componentI.Guid,
                },
            };

            var correctOrderedComponentsList = new List<ModComponent>
            {
                componentC,
                componentD,
                componentA,
                componentB,
                componentE,
                componentF,
                componentH,
                componentI,
                componentG,
                componentJ, };

            (bool isCorrectOrder, List<ModComponent> reorderedComponents) =
                ModComponent.ConfirmComponentsInstallOrder(correctOrderedComponentsList);

            foreach (ModComponent component in reorderedComponents)
            {
                int actualIndex = reorderedComponents.FindIndex(c => c.Guid == component.Guid);
                int expectedIndex = correctOrderedComponentsList.FindIndex(c => c.Guid == component.Guid);
                Assert.That(actualIndex, Is.EqualTo(expectedIndex), $"ModComponent {component.Name} is out of order.");
            }

            Assert.Multiple(
                () =>
                {
                    Assert.That(isCorrectOrder, Is.True);
                    Assert.That(reorderedComponents, Is.Not.Empty);
                }
            );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_ComplexScenario_Unordered()
        {

            var componentA = new ModComponent
            {
                Name = "A",
                Guid = Guid.NewGuid(),
            };
            var componentB = new ModComponent
            {
                Name = "B",
                Guid = Guid.NewGuid(),
                InstallAfter =
                new List<Guid>
                {
                    componentA.Guid,
                },
            };
            var componentC = new ModComponent
            {
                Name = "C",
                Guid = Guid.NewGuid(),
                InstallBefore =
                new List<Guid>
                {
                    componentA.Guid,
                },
            };
            var componentD = new ModComponent
            {
                Name = "D",
                Guid = Guid.NewGuid(),
                InstallBefore =
                new List<Guid>
                {
                    componentB.Guid,
                },
            };
            Guid componentFGuid = Guid.Empty;
            var componentE = new ModComponent
            {
                Name = "E",
                Guid = Guid.NewGuid(),
                InstallAfter =
                new List<Guid>
                {
                    componentB.Guid,
                },
                InstallBefore =
                new List<Guid>
                {
                    componentFGuid,
                },
            };
            var componentF = new ModComponent
            {
                Name = "F",
                Guid = componentFGuid,
                InstallAfter =
                new List<Guid>
                {
                    componentE.Guid, componentB.Guid,
                },
            };
            var componentG = new ModComponent
            {
                Name = "G",
                Guid = Guid.NewGuid(),
                InstallAfter =
                new List<Guid>
                {
                    componentD.Guid, componentF.Guid,
                },
            };
            var componentH = new ModComponent
            {
                Name = "H",
                Guid = Guid.NewGuid(),
                InstallBefore =
                new List<Guid>
                {
                    componentG.Guid,
                },
            };
            var componentI = new ModComponent
            {
                Name = "I",
                Guid = Guid.NewGuid(),
                InstallAfter =
                new List<Guid>
                {
                    componentH.Guid,
                },
                InstallBefore =
                new List<Guid>
                {
                    componentG.Guid,
                },
            };
            var componentJ = new ModComponent
            {
                Name = "J",
                Guid = Guid.NewGuid(),
                InstallAfter =
                new List<Guid>
                {
                    componentH.Guid, componentI.Guid,
                },
            };

            var unorderedComponentsList = new List<ModComponent>
            {
                componentA,
                componentB,
                componentC,
                componentD,
                componentE,
                componentF,
                componentG,
                componentH,
                componentI,
                componentJ, };
            var correctOrderedComponentsList = new List<ModComponent>
            {
                componentC,
                componentA,
                componentD,
                componentB,
                componentE,
                componentF,
                componentH,
                componentI,
                componentG,
                componentJ, };

            (bool isCorrectOrder, List<ModComponent> reorderedComponents) =
                ModComponent.ConfirmComponentsInstallOrder(unorderedComponentsList);

            foreach (ModComponent component in reorderedComponents)
            {
                int actualIndex = reorderedComponents.FindIndex(c => c.Guid == component.Guid);
                int expectedIndex = correctOrderedComponentsList.FindIndex(c => c.Guid == component.Guid);
                Assert.That(actualIndex, Is.EqualTo(expectedIndex), $"ModComponent '{component.Name}' is out of order.");
            }

            Assert.Multiple(
                () =>
                {
                    Assert.That(isCorrectOrder, Is.False);
                    Assert.That(reorderedComponents, Is.Not.Empty);
                }
            );
        }

        [Test]
        public void ConfirmComponentsInstallOrder_ImpossibleScenario_ReturnsFalse()
        {

            var componentA = new ModComponent
            {
                Name = "A",
                Guid = Guid.NewGuid(),
                InstallBefore =
                new List<Guid>
                {
                    Guid.NewGuid(),
                },
            };
            var componentB = new ModComponent
            {
                Name = "B",
                Guid = Guid.NewGuid(),
                InstallAfter =
                new List<Guid>
                {
                    componentA.Guid,
                },
            };
            var componentC = new ModComponent
            {
                Name = "C",
                Guid = Guid.NewGuid(),
                InstallAfter =
                new List<Guid>
                {
                    componentB.Guid,
                },
                InstallBefore =
                new List<Guid>
                {
                    componentA.Guid,
                },
            };

            var componentsList = new List<ModComponent>
            {
                componentA, componentB, componentC,
            };

            Assert.Multiple(() =>
            {
                Assert.That(componentsList, Is.Not.Null, "Components list should not be null");
                Assert.That(componentsList, Is.Not.Empty, "Components list should not be empty");
            });

            var exception = Assert.Throws<KeyNotFoundException>(
                () => { _ = ModComponent.ConfirmComponentsInstallOrder(componentsList); },
                message: "ConfirmComponentsInstallOrder should have raised a KeyNotFoundException for impossible scenario"
            );

            Assert.Multiple(() =>
            {
                Assert.That(exception, Is.Not.Null, "Exception should not be null");
                Assert.That(exception.Message, Is.Not.Null.And.Not.Empty, "Exception message should not be null or empty");
            });
        }

        private static void Swap<T>(IList<T> list, int index1, int index2) =>
            (list[index1], list[index2]) = (list[index2], list[index1]);
    }
}
