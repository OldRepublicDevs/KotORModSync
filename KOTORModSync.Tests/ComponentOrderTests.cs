// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core;

namespace KOTORModSync.Tests
{
	[TestFixture]
	internal class ComponentOrderTests
	{
		[Test]
		public void ConfirmComponentsInstallOrder_InstallBefore_ReturnsTrue()
		{
			// Arrange
			var thisGuid = Guid.NewGuid();
			var componentsListExpectedOrder = new List<ModComponent>
			{
				new()
				{
					Name = "C1_InstallBefore_C2",
					Guid = Guid.NewGuid(),
					InstallBefore =
					[
						thisGuid,
					],
				},
				new()
				{
					Name = "C2", Guid = thisGuid,
				},
				new()
				{
					Name = "C3", Guid = Guid.NewGuid(),
				},
			};

			// Act
			(bool isCorrectOrder, List<ModComponent> reorderedComponents) =
				ModComponent.ConfirmComponentsInstallOrder(componentsListExpectedOrder);

			// Assert
			foreach ( ModComponent component in reorderedComponents )
			{
				int actualIndex = reorderedComponents.FindIndex(c => c.Guid == component.Guid);
				int expectedIndex = componentsListExpectedOrder.FindIndex(c => c.Guid == component.Guid);
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
		public void ConfirmComponentsInstallOrder_InstallBefore_ReturnsFalse()
		{
			// Arrange
			var thisGuid = Guid.NewGuid();
			var unorderedList = new List<ModComponent>
			{
				new()
				{
					Name = "C2", Guid = thisGuid,
				},
				new()
				{
					Name = "C1_InstallBefore_C2",
					Guid = Guid.NewGuid(),
					InstallBefore =
					[
						thisGuid,
					],
				},
				new()
				{
					Name = "C3", Guid = Guid.NewGuid(),
				},
			};

			// Act
			(bool isCorrectOrder, List<ModComponent> reorderedComponents) =
				ModComponent.ConfirmComponentsInstallOrder(unorderedList);

			// Create a copy of unorderedList with the expected order
			var componentsListExpectedOrder = new List<ModComponent>(unorderedList);
			Swap(componentsListExpectedOrder, index1: 0, index2: 1);

			// Assert
			foreach ( ModComponent component in reorderedComponents )
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
			// Arrange
			var thisGuid = Guid.NewGuid();
			var componentsListExpectedOrder = new List<ModComponent>
			{
				new()
				{
					Name = "C1", Guid = thisGuid,
				},
				new()
				{
					Name = "C2_InstallAfter_C1",
					Guid = Guid.NewGuid(),
					InstallAfter =
					[
						thisGuid,
					],
				},
				new()
				{
					Name = "C3", Guid = Guid.NewGuid(),
				},
			};

			// Act
			(bool isCorrectOrder, List<ModComponent> reorderedComponents) =
				ModComponent.ConfirmComponentsInstallOrder(componentsListExpectedOrder);

			// Assert
			foreach ( ModComponent component in reorderedComponents )
			{
				int actualIndex = reorderedComponents.FindIndex(c => c.Guid == component.Guid);
				int expectedIndex = componentsListExpectedOrder.FindIndex(c => c.Guid == component.Guid);
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
		public void ConfirmComponentsInstallOrder_InstallAfter_ReturnsFalse()
		{
			// Arrange
			var thisGuid = Guid.NewGuid();
			var unorderedList = new List<ModComponent>
			{
				new()
				{
					Name = "C1_InstallAfter_C2",
					Guid = Guid.NewGuid(),
					InstallAfter =
					[
						thisGuid,
					],
				},
				new()
				{
					Name = "C2", Guid = thisGuid,
				},
				new()
				{
					Name = "C3", Guid = Guid.NewGuid(),
				},
			};

			// Act
			(bool isCorrectOrder, List<ModComponent> reorderedComponents) =
				ModComponent.ConfirmComponentsInstallOrder(unorderedList);

			// Create a copy of unorderedList with the expected order
			var componentsListExpectedOrder = new List<ModComponent>(unorderedList);
			Swap(componentsListExpectedOrder, index1: 0, index2: 1);

			// Assert
			foreach ( ModComponent component in reorderedComponents )
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
			// Arrange
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
				[
					componentA.Guid,
				],
			};
			var componentC = new ModComponent
			{
				Name = "C",
				Guid = Guid.NewGuid(),
				InstallBefore =
				[
					componentA.Guid,
				],
			};
			var componentD = new ModComponent
			{
				Name = "D",
				Guid = Guid.NewGuid(),
				InstallBefore =
				[
					componentB.Guid,
				],
			};
			var componentFGuid = new Guid();
			var componentE = new ModComponent
			{
				Name = "E",
				Guid = Guid.NewGuid(),
				InstallAfter =
				[
					componentB.Guid,
				],
				InstallBefore =
				[
					componentFGuid,
				],
			};
			var componentF = new ModComponent
			{
				Name = "F",
				Guid = componentFGuid,
				InstallAfter =
				[
					componentE.Guid, componentB.Guid,
				],
			};
			var componentG = new ModComponent
			{
				Name = "G",
				Guid = Guid.NewGuid(),
				InstallAfter =
				[
					componentD.Guid, componentF.Guid,
				],
			};
			var componentH = new ModComponent
			{
				Name = "H",
				Guid = Guid.NewGuid(),
				InstallBefore =
				[
					componentG.Guid,
				],
			};
			var componentI = new ModComponent
			{
				Name = "I",
				Guid = Guid.NewGuid(),
				InstallBefore =
				[
					componentG.Guid,
				],
			};
			var componentJ = new ModComponent
			{
				Name = "J",
				Guid = Guid.NewGuid(),
				InstallAfter =
				[
					componentH.Guid, componentI.Guid,
				],
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
				componentJ };

			// Act
			(bool isCorrectOrder, List<ModComponent> reorderedComponents) =
				ModComponent.ConfirmComponentsInstallOrder(correctOrderedComponentsList);

			// Assert
			foreach ( ModComponent component in reorderedComponents )
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
			// Arrange
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
				[
					componentA.Guid,
				],
			};
			var componentC = new ModComponent
			{
				Name = "C",
				Guid = Guid.NewGuid(),
				InstallBefore =
				[
					componentA.Guid,
				],
			};
			var componentD = new ModComponent
			{
				Name = "D",
				Guid = Guid.NewGuid(),
				InstallBefore =
				[
					componentB.Guid,
				],
			};
			var componentFGuid = new Guid();
			var componentE = new ModComponent
			{
				Name = "E",
				Guid = Guid.NewGuid(),
				InstallAfter =
				[
					componentB.Guid,
				],
				InstallBefore =
				[
					componentFGuid,
				],
			};
			var componentF = new ModComponent
			{
				Name = "F",
				Guid = componentFGuid,
				InstallAfter =
				[
					componentE.Guid, componentB.Guid,
				],
			};
			var componentG = new ModComponent
			{
				Name = "G",
				Guid = Guid.NewGuid(),
				InstallAfter =
				[
					componentD.Guid, componentF.Guid,
				],
			};
			var componentH = new ModComponent
			{
				Name = "H",
				Guid = Guid.NewGuid(),
				InstallBefore =
				[
					componentG.Guid,
				],
			};
			var componentI = new ModComponent
			{
				Name = "I",
				Guid = Guid.NewGuid(),
				InstallAfter =
				[
					componentH.Guid,
				],
				InstallBefore =
				[
					componentG.Guid,
				],
			};
			var componentJ = new ModComponent
			{
				Name = "J",
				Guid = Guid.NewGuid(),
				InstallAfter =
				[
					componentH.Guid, componentI.Guid,
				],
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
				componentJ };
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
				componentJ };

			// Act
			(bool isCorrectOrder, List<ModComponent> reorderedComponents) =
				ModComponent.ConfirmComponentsInstallOrder(unorderedComponentsList);

			// Assert
			foreach ( ModComponent component in reorderedComponents )
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
			// Arrange
			var componentA = new ModComponent
			{
				Name = "A",
				Guid = Guid.NewGuid(),
				InstallBefore =
				[
					Guid.NewGuid(),
				],
			};
			var componentB = new ModComponent
			{
				Name = "B",
				Guid = Guid.NewGuid(),
				InstallAfter =
				[
					componentA.Guid,
				],
			};
			var componentC = new ModComponent
			{
				Name = "C",
				Guid = Guid.NewGuid(),
				InstallAfter =
				[
					componentB.Guid,
				],
				InstallBefore =
				[
					componentA.Guid,
				],
			};

			var componentsList = new List<ModComponent>
			{
				componentA, componentB, componentC,
			};

			// Act & Assert
			_ = Assert.Throws<KeyNotFoundException>(
				() => { _ = ModComponent.ConfirmComponentsInstallOrder(componentsList); },
				message: "ConfirmComponentsInstallOrder should have raised a KeyNotFoundException"
			);
		}

		private static void Swap<T>(IList<T> list, int index1, int index2) =>
			(list[index1], list[index2]) = (list[index2], list[index1]);
	}
}
