// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class CategoryParsingTests
	{
		[Test]
		public void ComponentDeserialization_WithAmpersandInCategory_ShouldNotSplit()
		{
			// Arrange
			string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Bugfix & Graphics Improvement""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component?.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Bugfix & Graphics Improvement"));
		}

		[Test]
		public void ComponentDeserialization_WithMultipleAmpersandCategories_ShouldNotSplit()
		{
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Graphics Improvement & Bugfix""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component?.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Graphics Improvement & Bugfix"));
		}

		[Test]
		public void ComponentDeserialization_WithCommaSeparatedCategories_ShouldSplitCorrectly()
		{
			// Arrange
			string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Bugfix & Graphics Improvement, Immersion""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component?.Category, Has.Count.EqualTo(2));
			Assert.That(component.Category[0], Is.EqualTo("Bugfix & Graphics Improvement"));
			Assert.That(component.Category[1], Is.EqualTo("Immersion"));
		}

		[Test]
		public void ComponentDeserialization_WithSemicolonSeparatedCategories_ShouldSplitCorrectly()
		{
			// Arrange
			string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Graphics; Immersion""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component?.Category, Has.Count.EqualTo(2));
			Assert.That(component.Category[0], Is.EqualTo("Graphics"));
			Assert.That(component.Category[1], Is.EqualTo("Immersion"));
		}

		[Test]
		public void ComponentDeserialization_WithMixedSeparators_ShouldSplitCorrectly()
		{
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Essential, Mechanics Change; Graphics Improvement""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component?.Category, Has.Count.EqualTo(3));
			Assert.That(component.Category[0], Is.EqualTo("Essential"));
			Assert.That(component.Category[1], Is.EqualTo("Mechanics Change"));
			Assert.That(component.Category[2], Is.EqualTo("Graphics Improvement"));
		}

		[Test]
		public void ComponentDeserialization_WithSingleCategory_ShouldReturnSingleItem()
		{
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Essential""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component?.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Essential"));
		}

		[Test]
		public void ComponentDeserialization_WithEmptyCategory_ShouldReturnEmptyList()
		{
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = """"
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component?.Category, Is.Empty);
		}

		[Test]
		public void ComponentDeserialization_WithWhitespaceOnlyCategory_ShouldReturnEmptyList()
		{
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""   ""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component?.Category, Is.Empty);
		}

		[Test]
		public void ComponentDeserialization_WithExtraWhitespace_ShouldTrimCorrectly()
		{
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""  Essential  ,  Mechanics Change  ;  Graphics Improvement  ""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(3));
			Assert.That(component.Category[0], Is.EqualTo("Essential"));
			Assert.That(component.Category[1], Is.EqualTo("Mechanics Change"));
			Assert.That(component.Category[2], Is.EqualTo("Graphics Improvement"));
		}

		[Test]
		public void ComponentDeserialization_WithEmptyItems_ShouldFilterThemOut()
		{
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Essential,,Mechanics Change; ;Graphics Improvement""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(3));
			Assert.That(component.Category[0], Is.EqualTo("Essential"));
			Assert.That(component.Category[1], Is.EqualTo("Mechanics Change"));
			Assert.That(component.Category[2], Is.EqualTo("Graphics Improvement"));
		}

		[Test]
		public void ComponentDeserialization_WithSlashInName_ShouldNotSplit()
		{
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Graphics/Visual Improvement""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Graphics/Visual Improvement"));
		}

		[Test]
		public void ComponentDeserialization_WithRealWorldExamples_ShouldWorkCorrectly()
		{
			// Test cases based on actual TOML file examples
			(string, string[])[] testCases =
			[
				("Essential", ["Essential"]),
				("Mechanics Change", ["Mechanics Change"]),
				("Graphics Improvement", ["Graphics Improvement"]),
				("Graphics Improvement & Bugfix", ["Graphics Improvement & Bugfix"]),
				("Bugfix & Graphics Improvement, Immersion", ["Bugfix & Graphics Improvement", "Immersion"]),
				("Essential, Mechanics Change; Graphics Improvement", ["Essential", "Mechanics Change", "Graphics Improvement"])
			];

			foreach ((string input, string[] expected) in testCases)
			{
				// Arrange
				string tomlContent = $@"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{{12345678-1234-1234-1234-123456789012}}""
Category = ""{input}""
";

				// Act
				var component = ModComponent.DeserializeTomlComponent(tomlContent);

				// Assert
				Assert.That(component, Is.Not.Null, $"Failed for input: '{input}'");
				Assert.That(component?.Category, Is.EqualTo(expected), $"Failed for input: '{input}'");
			}
		}

		[Test]
		public void ComponentDeserialization_WithListFormat_ShouldWorkCorrectly()
		{
			// Test that the new List<string> format also works correctly
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = [""Bugfix & Graphics Improvement"", ""Immersion""]
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component?.Category, Has.Count.EqualTo(2), "Category should have 2 items");
			Assert.That(component?.Category[0], Is.EqualTo("Bugfix & Graphics Improvement"));
			Assert.That(component?.Category[1], Is.EqualTo("Immersion"));
		}

		[Test]
		public void ComponentDeserialization_WithMissingCategory_ShouldReturnEmptyList()
		{
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component?.Category, Is.Empty, "Category should be empty");
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_BugfixAndGraphicsImprovement()
		{
			// Test based on actual mod: "Bugfix & Graphics Improvement"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""JC's Minor Fixes""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Bugfix & Graphics Improvement""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Bugfix & Graphics Improvement"));
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_BugfixGraphicsImmersion()
		{
			// Test based on actual mod: "Bugfix, Graphics Improvement & Immersion"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""KOTOR Community Patch""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Bugfix, Graphics Improvement & Immersion""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(2));
			Assert.That(component.Category[0], Is.EqualTo("Bugfix"));
			Assert.That(component.Category[1], Is.EqualTo("Graphics Improvement & Immersion"));
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_AppearanceChangeAndGraphics()
		{
			// Test based on actual mod: "Appearance Change & Graphics Improvement"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Ajunta Pall Unique Appearance""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Appearance Change & Graphics Improvement""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Appearance Change & Graphics Improvement"));
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_GraphicsAndAppearance()
		{
			// Test based on actual mod: "Graphics Improvement & Appearance Change"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Republic Soldier Fix""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Graphics Improvement & Appearance Change""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Graphics Improvement & Appearance Change"));
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_AddedContentAndImmersion()
		{
			// Test based on actual mod: "Added Content & Immersion"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""New Leviathan Dialogue""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Added Content & Immersion""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Added Content & Immersion"));
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_BugfixAndImmersion()
		{
			// Test based on actual mod: "Bugfix & Immersion"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Leviathan Prison Break""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Bugfix & Immersion""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Bugfix & Immersion"));
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_AppearanceChangeBugfixAndGraphics()
		{
			// Test based on actual mod: "Appearance Change, Bugfix & Graphics Improvement"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Taris Dueling Arena Adjustment""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Appearance Change, Bugfix & Graphics Improvement""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(2));
			Assert.That(component.Category[0], Is.EqualTo("Appearance Change"));
			Assert.That(component.Category[1], Is.EqualTo("Bugfix & Graphics Improvement"));
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_AppearanceImmersionAndGraphics()
		{
			// Test based on actual mod: "Appearance Change, Immersion & Graphics Improvement"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Juhani Appearance Overhaul""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Appearance Change, Immersion & Graphics Improvement""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(2));
			Assert.That(component.Category[0], Is.EqualTo("Appearance Change"));
			Assert.That(component.Category[1], Is.EqualTo("Immersion & Graphics Improvement"));
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_AddedAndRestoredContent()
		{
			// Test based on actual mod: "Added & Restored Content"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Senni Vek Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Added & Restored Content""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Added & Restored Content"));
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_MechanicsChangeAndImmersion()
		{
			// Test based on actual mod: "Mechanics Change & Immersion"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Repair Affects Stun Droid""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Mechanics Change & Immersion""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Mechanics Change & Immersion"));
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_ImmersionAndGraphics()
		{
			// Test based on actual mod: "Immersion & Graphics Improvement"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Ending Enhancement""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Immersion & Graphics Improvement""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Immersion & Graphics Improvement"));
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_AppearanceChangeAndImmersion()
		{
			// Test based on actual mod: "Appearance Change & Immersion"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Loadscreens in Color""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Appearance Change & Immersion""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(1));
			Assert.That(component.Category[0], Is.EqualTo("Appearance Change & Immersion"));
		}

		[Test]
		public void ComponentDeserialization_RealWorldExample_ReflectiveLightsaberBlades()
		{
			// Test based on actual mod with three categories: "Appearance Change, Immersion & Graphics Improvement"
			// Arrange
			const string tomlContent = @"
[[thisMod]]
Name = ""Reflective Lightsaber Blades""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Appearance Change, Immersion & Graphics Improvement""
";

			// Act
			var component = ModComponent.DeserializeTomlComponent(tomlContent);

			// Assert
			Assert.That(component, Is.Not.Null);
			Assert.That(component!.Category, Has.Count.EqualTo(2));
			Assert.That(component.Category[0], Is.EqualTo("Appearance Change"));
			Assert.That(component.Category[1], Is.EqualTo("Immersion & Graphics Improvement"));
		}
	}
}