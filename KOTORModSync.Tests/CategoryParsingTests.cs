// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using KOTORModSync.Core;

using NUnit.Framework;

namespace KOTORModSync.Tests
{
    [TestFixture]
    public class CategoryParsingTests
    {
        [Test]
        public void ComponentDeserialization_WithAmpersandInCategory_ShouldNotSplit()
        {

            string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Bugfix & Graphics Improvement""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component?.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Bugfix & Graphics Improvement"));
            });
        }

        [Test]
        public void ComponentDeserialization_WithMultipleAmpersandCategories_ShouldNotSplit()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Graphics Improvement & Bugfix""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component?.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Graphics Improvement & Bugfix"));
            });
        }

        [Test]
        public void ComponentDeserialization_WithCommaSeparatedCategories_ShouldSplitCorrectly()
        {

            string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Bugfix & Graphics Improvement, Immersion""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component?.Category, Has.Count.EqualTo(2));
                Assert.That(component.Category[0], Is.EqualTo("Bugfix & Graphics Improvement"));
                Assert.That(component.Category[1], Is.EqualTo("Immersion"));
            });
        }

        [Test]
        public void ComponentDeserialization_WithSemicolonSeparatedCategories_ShouldSplitCorrectly()
        {

            string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Graphics; Immersion""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component?.Category, Has.Count.EqualTo(2));
                Assert.That(component.Category[0], Is.EqualTo("Graphics"));
                Assert.That(component.Category[1], Is.EqualTo("Immersion"));
            });
        }

        [Test]
        public void ComponentDeserialization_WithMixedSeparators_ShouldSplitCorrectly()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Essential, Mechanics Change; Graphics Improvement""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component?.Category, Has.Count.EqualTo(3));
                Assert.That(component.Category[0], Is.EqualTo("Essential"));
                Assert.That(component.Category[1], Is.EqualTo("Mechanics Change"));
                Assert.That(component.Category[2], Is.EqualTo("Graphics Improvement"));
            });
        }

        [Test]
        public void ComponentDeserialization_WithSingleCategory_ShouldReturnSingleItem()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Essential""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component?.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Essential"));
            });
        }

        [Test]
        public void ComponentDeserialization_WithEmptyCategory_ShouldReturnEmptyList()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = """"
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.That(component?.Category, Is.Empty);
        }

        [Test]
        public void ComponentDeserialization_WithWhitespaceOnlyCategory_ShouldReturnEmptyList()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""   ""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.That(component?.Category, Is.Empty);
        }

        [Test]
        public void ComponentDeserialization_WithExtraWhitespace_ShouldTrimCorrectly()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""  Essential  ,  Mechanics Change  ;  Graphics Improvement  ""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(3));
                Assert.That(component.Category[0], Is.EqualTo("Essential"));
                Assert.That(component.Category[1], Is.EqualTo("Mechanics Change"));
                Assert.That(component.Category[2], Is.EqualTo("Graphics Improvement"));
            });
        }

        [Test]
        public void ComponentDeserialization_WithEmptyItems_ShouldFilterThemOut()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Essential,,Mechanics Change; ;Graphics Improvement""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(3));
                Assert.That(component.Category[0], Is.EqualTo("Essential"));
                Assert.That(component.Category[1], Is.EqualTo("Mechanics Change"));
                Assert.That(component.Category[2], Is.EqualTo("Graphics Improvement"));
            });
        }

        [Test]
        public void ComponentDeserialization_WithSlashInName_ShouldNotSplit()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Graphics/Visual Improvement""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Graphics/Visual Improvement"));
            });
        }

        [Test]
        public void ComponentDeserialization_WithRealWorldExamples_ShouldWorkCorrectly()
        {

            (string, string[])[] testCases = new (string, string[])[]
            {
                ("Essential", new string[] { "Essential" }),
                ("Mechanics Change", new string[] { "Mechanics Change" }),
                ("Graphics Improvement", new string[] { "Graphics Improvement" }),
                ("Graphics Improvement & Bugfix", new string[] { "Graphics Improvement & Bugfix" }),
                ("Bugfix & Graphics Improvement, Immersion", new string[] { "Bugfix & Graphics Improvement", "Immersion" }),
                ("Essential, Mechanics Change; Graphics Improvement", new string[] { "Essential", "Mechanics Change", "Graphics Improvement" }),
            };

            foreach ((string input, string[] expected) in testCases)
            {

                string tomlContent = $@"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{{12345678-1234-1234-1234-123456789012}}""
Category = ""{input}""
";

                var component = ModComponent.DeserializeTomlComponent(tomlContent);

                Assert.That(component, Is.Not.Null, $"Failed for input: '{input}'");
                Assert.That(component?.Category, Is.EqualTo(expected), $"Failed for input: '{input}'");
            }
        }

        [Test]
        public void ComponentDeserialization_WithListFormat_ShouldWorkCorrectly()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = [""Bugfix & Graphics Improvement"", ""Immersion""]
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.That(component?.Category, Has.Count.EqualTo(2), "Category should have 2 items");
            Assert.Multiple(() =>
            {
                Assert.That(component?.Category[0], Is.EqualTo("Bugfix & Graphics Improvement"));
                Assert.That(component?.Category[1], Is.EqualTo("Immersion"));
            });
        }

        [Test]
        public void ComponentDeserialization_WithMissingCategory_ShouldReturnEmptyList()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Test Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.That(component?.Category, Is.Empty, "Category should be empty");
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_BugfixAndGraphicsImprovement()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""JC's Minor Fixes""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Bugfix & Graphics Improvement""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Bugfix & Graphics Improvement"));
            });
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_BugfixGraphicsImmersion()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""KOTOR Community Patch""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Bugfix, Graphics Improvement & Immersion""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(2));
                Assert.That(component.Category[0], Is.EqualTo("Bugfix"));
                Assert.That(component.Category[1], Is.EqualTo("Graphics Improvement & Immersion"));
            });
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_AppearanceChangeAndGraphics()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Ajunta Pall Unique Appearance""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Appearance Change & Graphics Improvement""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Appearance Change & Graphics Improvement"));
            });
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_GraphicsAndAppearance()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Republic Soldier Fix""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Graphics Improvement & Appearance Change""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Graphics Improvement & Appearance Change"));
            });
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_AddedContentAndImmersion()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""New Leviathan Dialogue""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Added Content & Immersion""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Added Content & Immersion"));
            });
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_BugfixAndImmersion()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Leviathan Prison Break""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Bugfix & Immersion""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Bugfix & Immersion"));
            });
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_AppearanceChangeBugfixAndGraphics()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Taris Dueling Arena Adjustment""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Appearance Change, Bugfix & Graphics Improvement""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(2));
                Assert.That(component.Category[0], Is.EqualTo("Appearance Change"));
                Assert.That(component.Category[1], Is.EqualTo("Bugfix & Graphics Improvement"));
            });
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_AppearanceImmersionAndGraphics()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Juhani Appearance Overhaul""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Appearance Change, Immersion & Graphics Improvement""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(2));
                Assert.That(component.Category[0], Is.EqualTo("Appearance Change"));
                Assert.That(component.Category[1], Is.EqualTo("Immersion & Graphics Improvement"));
            });
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_AddedAndRestoredContent()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Senni Vek Mod""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Added & Restored Content""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Added & Restored Content"));
            });
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_MechanicsChangeAndImmersion()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Repair Affects Stun Droid""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Mechanics Change & Immersion""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Mechanics Change & Immersion"));
            });
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_ImmersionAndGraphics()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Ending Enhancement""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Immersion & Graphics Improvement""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Immersion & Graphics Improvement"));
            });
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_AppearanceChangeAndImmersion()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Loadscreens in Color""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Appearance Change & Immersion""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(1));
                Assert.That(component.Category[0], Is.EqualTo("Appearance Change & Immersion"));
            });
        }

        [Test]
        public void ComponentDeserialization_RealWorldExample_ReflectiveLightsaberBlades()
        {

            const string tomlContent = @"
[[thisMod]]
Name = ""Reflective Lightsaber Blades""
Guid = ""{12345678-1234-1234-1234-123456789012}""
Category = ""Appearance Change, Immersion & Graphics Improvement""
";

            var component = ModComponent.DeserializeTomlComponent(tomlContent);

            Assert.That(component, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(component.Category, Has.Count.EqualTo(2));
                Assert.That(component.Category[0], Is.EqualTo("Appearance Change"));
                Assert.That(component.Category[1], Is.EqualTo("Immersion & Graphics Improvement"));
            });
        }
    }
}
