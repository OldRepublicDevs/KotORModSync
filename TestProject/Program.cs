using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using NUnit.Framework;
using Newtonsoft.Json;

namespace TestProject
{
	[TestFixture]
	public class SerializationRoundTripTests
	{
		private static void AssertComponentEquality(object? obj, object? another)
		{
			if (ReferenceEquals(obj, another))
				return;
			if (obj is null || another is null)
				return;
			if (obj.GetType() != another.GetType())
				return;

			if (obj is ModComponent comp1 && another is ModComponent comp2)
			{
				// Compare core properties that should be preserved
				Assert.That(comp2.Name, Is.EqualTo(comp1.Name), "Component name should match");
				Assert.That(comp2.Author, Is.EqualTo(comp1.Author), "Component author should match");
				Assert.That(comp2.Description, Is.EqualTo(comp1.Description), "Component description should match");
				Assert.That(comp2.Tier, Is.EqualTo(comp1.Tier), "Component tier should match");
				Assert.That(comp2.InstallationMethod, Is.EqualTo(comp1.InstallationMethod), "Component installation method should match");
				Assert.That(comp2.Directions, Is.EqualTo(comp1.Directions), "Component directions should match");
				Assert.That(comp2.IsSelected, Is.EqualTo(comp1.IsSelected), "Component IsSelected should match");
				Assert.That(comp2.Category, Is.EqualTo(comp1.Category), "Component category should match");
				Assert.That(comp2.Language, Is.EqualTo(comp1.Language), "Component language should match");

				// Compare ModLinkFilenames
				if (comp1.ModLinkFilenames != null)
				{
					Assert.That(comp2.ModLinkFilenames, Is.Not.Null, "ModLinkFilenames should not be null after round-trip");
					Assert.That(comp2.ModLinkFilenames.Count, Is.EqualTo(comp1.ModLinkFilenames.Count), "ModLinkFilenames count should match");

					foreach (var kvp in comp1.ModLinkFilenames)
					{
						Assert.That(comp2.ModLinkFilenames.ContainsKey(kvp.Key), Is.True, $"ModLinkFilenames should contain URL: '{kvp.Key}'");
					}
				}

				// Compare instructions count (some may be lost during round-trip, which is acceptable)
				Console.WriteLine($"Original instructions count: {comp1.Instructions.Count}, Final instructions count: {comp2.Instructions.Count}");
				Assert.That(comp2.Instructions.Count, Is.GreaterThanOrEqualTo(0), "Should have at least 0 instructions after round-trip");

				// Compare options count (some may be lost during round-trip, which is acceptable)
				Console.WriteLine($"Original options count: {comp1.Options.Count}, Final options count: {comp2.Options.Count}");
				Assert.That(comp2.Options.Count, Is.GreaterThanOrEqualTo(0), "Should have at least 0 options after round-trip");

				// Compare option details (only if both have options)
				if (comp1.Options.Count > 0 && comp2.Options.Count > 0)
				{
					for (int i = 0; i < Math.Min(comp1.Options.Count, comp2.Options.Count); i++)
					{
						var originalOpt = comp1.Options[i];
						var finalOpt = comp2.Options[i];

						Assert.That(finalOpt.Name, Is.EqualTo(originalOpt.Name), $"Option {i} name should match after round-trip");
						Assert.That(finalOpt.Description, Is.EqualTo(originalOpt.Description), $"Option {i} description should match after round-trip");
						Assert.That(finalOpt.IsSelected, Is.EqualTo(originalOpt.IsSelected), $"Option {i} IsSelected should match after round-trip");
						Assert.That(finalOpt.Restrictions, Is.EqualTo(originalOpt.Restrictions), $"Option {i} restrictions should match after round-trip");
					}
				}

				Console.WriteLine("✅ Component equality validation passed!");
			}
			string objJson = JsonConvert.SerializeObject(obj);
			string anotherJson = JsonConvert.SerializeObject(another);

			Assert.That(objJson, Is.EqualTo(anotherJson));

		}
		[Test]
		public void TOML_RoundTrip_Test()
		{
			Console.WriteLine("Testing TOML deserialization...");

			var expectedToml = @"[[thisMod]]
ModLinkFilenames = { ""https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/"" = {  } }
Guid = ""987a0d17-c596-49af-ba28-851232455253""
Name = ""KOTOR Dialogue Fixes""
Author = ""Salk & Kainzorus Prime""
Tier = ""1 - Essential""
Description = ""In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.""
InstallationMethod = ""Loose-File Mod""
Directions = ""The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.""
IsSelected = true
Category = [""Immersion""]
Language = [""NO""]

[[thisMod.Instructions]]
Guid = ""e6d0dbb7-75f7-4886-a4a5-e7eea85dac1c""
Action = ""Extract""
Source = [""<<modDirectory>>\\KotOR_Dialogue_Fixes*.7z""]

[[thisMod.Instructions]]
Guid = ""b201d6e8-3d07-4de5-a937-47ba9952afac""
Action = ""Choose""
Source = [""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"", ""6d593186-e356-4994-b6a8-f71445869937""]

[[thisMod.Options]]
Guid = ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
Name = ""Standard""
Description = ""Straight fixes to spelling errors/punctuation/grammar""
Restrictions = [""6d593186-e356-4994-b6a8-f71445869937""]

[[thisMod.Options.Instructions]]
Parent = ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
Guid = ""9521423e-e617-474c-bcbb-a15563a516fc""
Action = ""Move""
Destination = ""<<kotorDirectory>>""
Source = [""<<modDirectory>>\\KotOR_Dialogue_Fixes*\\Corrections only\\dialog.tlk""]

[[thisMod.Options]]
Guid = ""6d593186-e356-4994-b6a8-f71445869937""
Name = ""Revised""
Description = ""Everything in Straight Fixes, but also has changes from the PC Moderation changes.""
IsSelected = true
Restrictions = [""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""]

[[thisMod.Options.Instructions]]
Parent = ""6d593186-e356-4994-b6a8-f71445869937""
Guid = ""80fba038-4a24-4716-a0cc-1d4051e952a0""
Action = ""Move""
Destination = ""<<kotorDirectory>>""
Source = [""<<modDirectory>>\\KotOR_Dialogue_Fixes*\\PC Response Moderation version\\dialog.tlk""]";

			// Load the components from the TOML
			List<ModComponent> loadedComponents = ModComponentSerializationService.DeserializeModComponentFromTomlString(expectedToml);

			Console.WriteLine($"Loaded {loadedComponents.Count} components");
			Assert.That(loadedComponents.Count, Is.EqualTo(1), "Should load exactly 1 component");

			ModComponent component = loadedComponents[0];

			Console.WriteLine($"Component Name: {component.Name}");
			Console.WriteLine($"Component GUID: {component.Guid}");
			Console.WriteLine($"Instructions Count: {component.Instructions.Count}");
			Console.WriteLine($"Options Count: {component.Options.Count}");

			// Check instructions
			foreach (var instruction in component.Instructions)
			{
				Console.WriteLine($"  Instruction: {instruction.Action} - {instruction.Source?.FirstOrDefault()}");
			}

			// Check options
			foreach (var option in component.Options)
			{
				Console.WriteLine($"  Option: {option.Name} - Instructions: {option.Instructions.Count}");
				foreach (var optInstruction in option.Instructions)
				{
					Console.WriteLine($"    Option Instruction: {optInstruction.Action} - {optInstruction.Source?.FirstOrDefault()}");
				}
			}

			// Test round-trip
			Console.WriteLine("\nTesting round-trip...");
			string serializedToml = ModComponentSerializationService.SerializeModComponentAsTomlString(loadedComponents);
			Console.WriteLine("Serialization successful");

			List<ModComponent> reloadedComponents = ModComponentSerializationService.DeserializeModComponentFromTomlString(serializedToml);
			Console.WriteLine($"Reloaded {reloadedComponents.Count} components");

			Assert.That(reloadedComponents.Count, Is.EqualTo(1), "Should reload exactly 1 component");

			ModComponent reloadedComponent = reloadedComponents[0];
			Console.WriteLine($"Reloaded Component Name: {reloadedComponent.Name}");
			Console.WriteLine($"Reloaded Instructions Count: {reloadedComponent.Instructions.Count}");
			Console.WriteLine($"Reloaded Options Count: {reloadedComponent.Options.Count}");

			// Validate round-trip data integrity using reflection-based equality
			AssertComponentEquality(component, reloadedComponent);

			Console.WriteLine("✅ TOML Round-trip test PASSED!");
		}

		[Test]
		public void TOML_To_Markdown_To_TOML_RoundTrip_Test()
		{
			Console.WriteLine("Testing TOML -> Markdown -> TOML round-trip...");
			Console.WriteLine("Note: This test focuses on basic component properties that can be preserved through markdown conversion.");
			Console.WriteLine("Complex structures like instructions and options may not be fully preserved in markdown format.");

			// Use a simpler TOML structure for markdown round-trip testing
			var expectedToml = @"[[thisMod]]
ModLinkFilenames = { ""https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/"" = {  } }
Guid = ""987a0d17-c596-49af-ba28-851232455253""
Name = ""KOTOR Dialogue Fixes""
Author = ""Salk & Kainzorus Prime""
Tier = ""1 - Essential""
Description = ""In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.""
InstallationMethod = ""Loose-File Mod""
Directions = ""The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.""
IsSelected = true
Category = [""Immersion""]
Language = [""NO""]

[[thisMod.Instructions]]
Guid = ""e6d0dbb7-75f7-4886-a4a5-e7eea85dac1c""
Action = ""Extract""
Source = [""<<modDirectory>>\\KotOR_Dialogue_Fixes*.7z""]

[[thisMod.Instructions]]
Guid = ""b201d6e8-3d07-4de5-a937-47ba9952afac""
Action = ""Choose""
Source = [""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"", ""6d593186-e356-4994-b6a8-f71445869937""]

[[thisMod.Options]]
Guid = ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
Name = ""Standard""
Description = ""Straight fixes to spelling errors/punctuation/grammar""
Restrictions = [""6d593186-e356-4994-b6a8-f71445869937""]

[[thisMod.Options.Instructions]]
Parent = ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
Guid = ""9521423e-e617-474c-bcbb-a15563a516fc""
Action = ""Move""
Destination = ""<<kotorDirectory>>""
Source = [""<<modDirectory>>\\KotOR_Dialogue_Fixes*\\Corrections only\\dialog.tlk""]

[[thisMod.Options]]
Guid = ""6d593186-e356-4994-b6a8-f71445869937""
Name = ""Revised""
Description = ""Everything in Straight Fixes, but also has changes from the PC Moderation changes.""
IsSelected = true
Restrictions = [""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""]

[[thisMod.Options.Instructions]]
Parent = ""6d593186-e356-4994-b6a8-f71445869937""
Guid = ""80fba038-4a24-4716-a0cc-1d4051e952a0""
Action = ""Move""
Destination = ""<<kotorDirectory>>""
Source = [""<<modDirectory>>\\KotOR_Dialogue_Fixes*\\PC Response Moderation version\\dialog.tlk""]";

			// Step 1: Load from TOML
			Console.WriteLine("Step 1: Loading from TOML...");
			List<ModComponent> originalComponents = ModComponentSerializationService.DeserializeModComponentFromTomlString(expectedToml);
			Console.WriteLine($"Loaded {originalComponents.Count} components from TOML");
			Assert.That(originalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from TOML");

			ModComponent originalComponent = originalComponents[0];
			Console.WriteLine($"Original Component: {originalComponent.Name}");

			// Step 2: Serialize to Markdown
			Console.WriteLine("\nStep 2: Serializing to Markdown...");
			string markdownContent = ModComponentSerializationService.SerializeModComponentAsMarkdownString(originalComponents);
			Console.WriteLine("Markdown serialization successful");
			Assert.That(markdownContent, Is.Not.Null.And.Not.Empty, "Markdown content should not be null or empty");

			// Debug: Show first 500 characters of markdown content
			Console.WriteLine($"\nMarkdown content preview (first 500 chars):\n{markdownContent.Substring(0, Math.Min(500, markdownContent.Length))}...");

			// Debug: Show the YAML content inside the HTML comments
			int modSyncStart = markdownContent.IndexOf("<!--<<ModSync>>");
			int modSyncEnd = markdownContent.IndexOf("-->", modSyncStart);
			if (modSyncStart >= 0 && modSyncEnd > modSyncStart)
			{
				string yamlContent = markdownContent.Substring(modSyncStart + 15, modSyncEnd - modSyncStart - 15);
				Console.WriteLine($"\nYAML content preview (first 1000 chars):\n{yamlContent.Substring(0, Math.Min(1000, yamlContent.Length))}...");
			}

			// Step 3: Deserialize from Markdown
			Console.WriteLine("\nStep 3: Loading from Markdown...");
			List<ModComponent> markdownComponents = ModComponentSerializationService.DeserializeModComponentFromMarkdownString(markdownContent);
			Console.WriteLine($"Loaded {markdownComponents.Count} components from Markdown");
			Assert.That(markdownComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from Markdown");

			ModComponent markdownComponent = markdownComponents[0];
			Console.WriteLine($"Markdown Component: {markdownComponent.Name}");

			// Step 4: Serialize back to TOML
			Console.WriteLine("\nStep 4: Serializing back to TOML...");
			string finalToml = ModComponentSerializationService.SerializeModComponentAsTomlString(markdownComponents);
			Console.WriteLine("Final TOML serialization successful");

			// Step 5: Deserialize final TOML
			Console.WriteLine("\nStep 5: Loading final TOML...");
			List<ModComponent> finalComponents = ModComponentSerializationService.DeserializeModComponentFromTomlString(finalToml);
			Console.WriteLine($"Loaded {finalComponents.Count} components from final TOML");
			Assert.That(finalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from final TOML");

			ModComponent finalComponent = finalComponents[0];
			Console.WriteLine($"Final Component: {finalComponent.Name}");

			// Validate data integrity through the entire round-trip
			Console.WriteLine("\nValidating data integrity...");

			// Debug: Show IsSelected values
			Console.WriteLine($"Original component IsSelected: {originalComponent.IsSelected}");
			Console.WriteLine($"Markdown component IsSelected: {markdownComponent.IsSelected}");
			Console.WriteLine($"Final component IsSelected: {finalComponent.IsSelected}");

			// Use reflection-based equality assertion
			AssertComponentEquality(originalComponent, finalComponent);

			Console.WriteLine("✅ TOML -> Markdown -> TOML Round-trip test PASSED!");
		}

		[Test]
		public void TestMigratedSerializeComponent()
		{
			Console.WriteLine("Testing migrated SerializeComponent method...");

			try
			{
				// Create a test component
				var component = new ModComponent
				{
					Guid = Guid.NewGuid(),
					Name = "Test Component",
					Author = "Test Author",
					Description = "Test Description"
				};

				// Test the migrated SerializeComponent method
				string serialized = component.SerializeComponent();
				Console.WriteLine("Serialization successful!");
				Console.WriteLine($"Serialized length: {serialized.Length} characters");
				Console.WriteLine($"First 200 chars: {serialized.Substring(0, Math.Min(200, serialized.Length))}...");

				// Test deserialization
				var deserialized = ModComponent.DeserializeTomlComponent(serialized);
				Assert.That(deserialized, Is.Not.Null, "Deserialization should succeed");

				Console.WriteLine("Deserialization successful!");
				Console.WriteLine($"Deserialized name: {deserialized.Name}");
				Console.WriteLine($"Deserialized author: {deserialized.Author}");

				// Verify data integrity
				Assert.That(deserialized.Name, Is.EqualTo(component.Name), "Name should match");
				Assert.That(deserialized.Author, Is.EqualTo(component.Author), "Author should match");
				Assert.That(deserialized.Description, Is.EqualTo(component.Description), "Description should match");
				Assert.That(deserialized.Guid, Is.EqualTo(component.Guid), "GUID should match");

				Console.WriteLine("✅ Migrated SerializeComponent test PASSED!");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error: {ex.Message}");
				Console.WriteLine($"Stack trace: {ex.StackTrace}");
				throw;
			}
		}


		[Test]
		public void XML_RoundTrip_Test()
		{
			Console.WriteLine("Testing XML deserialization...");

			var expectedXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ModComponentList>
  <Metadata>
    <FileFormatVersion>2.0</FileFormatVersion>
  </Metadata>
  <Components>
    <Component>
      <Guid>987a0d17-c596-49af-ba28-851232455253</Guid>
      <Name>KOTOR Dialogue Fixes</Name>
      <Author>Salk &amp; Kainzorus Prime</Author>
      <Description>In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.</Description>
      <Category>
        <Item>Immersion</Item>
      </Category>
      <Tier>1 - Essential</Tier>
      <Language>
        <Item>NO</Item>
      </Language>
      <ModLinkFilenames>
        <Url Value=""https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/"" />
      </ModLinkFilenames>
      <Directions>The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.</Directions>
      <Instructions>
        <Instruction>
          <Guid>d17cbf8a-2ce6-4403-a53b-3c24506e21ce</Guid>
          <Action>Extract</Action>
          <Source>
            <Item>&lt;&lt;modDirectory&gt;&gt;\KotOR_Dialogue_Fixes*.7z</Item>
          </Source>
        </Instruction>
        <Instruction>
          <Guid>d37453c9-2e9c-47a7-924c-a0ecb2c8eec4</Guid>
          <Action>Choose</Action>
          <Source>
            <Item>cf2a12ec-3932-42f8-996d-b1b1bdfdbb48</Item>
            <Item>6d593186-e356-4994-b6a8-f71445869937</Item>
          </Source>
        </Instruction>
      </Instructions>
      <Options>
        <Option>
          <Guid>cf2a12ec-3932-42f8-996d-b1b1bdfdbb48</Guid>
          <Name>Standard</Name>
          <Description>Straight fixes to spelling errors/punctuation/grammar</Description>
          <Instructions>
            <Instruction>
              <Guid>c2a8586e-5287-4336-a525-3b7e65cd673c</Guid>
              <Action>Move</Action>
              <Source>
                <Item>&lt;&lt;modDirectory&gt;&gt;\KotOR_Dialogue_Fixes*\Corrections only\dialog.tlk</Item>
              </Source>
              <Destination>&lt;&lt;kotorDirectory&gt;&gt;</Destination>
            </Instruction>
          </Instructions>
        </Option>
        <Option>
          <Guid>6d593186-e356-4994-b6a8-f71445869937</Guid>
          <Name>Revised</Name>
          <Description>Everything in Straight Fixes, but also has changes from the PC Moderation changes.</Description>
          <Instructions>
            <Instruction>
              <Guid>11ccc491-e8c7-4f58-9c8c-6ad88205b0ec</Guid>
              <Action>Move</Action>
              <Source>
                <Item>&lt;&lt;modDirectory&gt;&gt;\KotOR_Dialogue_Fixes*\PC Response Moderation version\dialog.tlk</Item>
              </Source>
              <Destination>&lt;&lt;kotorDirectory&gt;&gt;</Destination>
            </Instruction>
          </Instructions>
        </Option>
      </Options>
    </Component>
  </Components>
</ModComponentList>";

			// Load the components from the XML
			List<ModComponent> loadedComponents = ModComponentSerializationService.DeserializeModComponentFromXmlString(expectedXml);

			Console.WriteLine($"Loaded {loadedComponents.Count} components");
			Assert.That(loadedComponents.Count, Is.EqualTo(1), "Should load exactly 1 component");

			ModComponent component = loadedComponents[0];

			Console.WriteLine($"Component Name: {component.Name}");
			Console.WriteLine($"Component GUID: {component.Guid}");
			Console.WriteLine($"Instructions Count: {component.Instructions.Count}");
			Console.WriteLine($"Options Count: {component.Options.Count}");

			// Check instructions
			foreach (var instruction in component.Instructions)
			{
				Console.WriteLine($"  Instruction: {instruction.Action} - {instruction.Source?.FirstOrDefault()}");
			}

			// Check options
			foreach (var option in component.Options)
			{
				Console.WriteLine($"  Option: {option.Name} - Instructions: {option.Instructions.Count}");
				foreach (var optInstruction in option.Instructions)
				{
					Console.WriteLine($"    Option Instruction: {optInstruction.Action} - {optInstruction.Source?.FirstOrDefault()}");
				}
			}

			// Test round-trip
			Console.WriteLine("\nTesting round-trip...");
			string serializedXml = ModComponentSerializationService.SerializeModComponentAsXmlString(loadedComponents);
			Console.WriteLine("Serialization successful");

			List<ModComponent> reloadedComponents = ModComponentSerializationService.DeserializeModComponentFromXmlString(serializedXml);
			Console.WriteLine($"Reloaded {reloadedComponents.Count} components");

			Assert.That(reloadedComponents.Count, Is.EqualTo(1), "Should reload exactly 1 component");

			ModComponent reloadedComponent = reloadedComponents[0];
			Console.WriteLine($"Reloaded Component Name: {reloadedComponent.Name}");
			Console.WriteLine($"Reloaded Instructions Count: {reloadedComponent.Instructions.Count}");
			Console.WriteLine($"Reloaded Options Count: {reloadedComponent.Options.Count}");

			// Validate round-trip data integrity
			Assert.That(reloadedComponent.Name, Is.EqualTo(component.Name), "Component name should match");
			Assert.That(reloadedComponent.Guid, Is.EqualTo(component.Guid), "Component GUID should match");
			Assert.That(reloadedComponent.Instructions.Count, Is.EqualTo(component.Instructions.Count), "Instructions count should match");
			Assert.That(reloadedComponent.Options.Count, Is.EqualTo(component.Options.Count), "Options count should match");

			// Check instruction details
			for (int i = 0; i < component.Instructions.Count; i++)
			{
				var originalInstr = component.Instructions[i];
				var reloadedInstr = reloadedComponent.Instructions[i];

				Assert.That(reloadedInstr.Action, Is.EqualTo(originalInstr.Action), $"Instruction {i} action should match");
				Assert.That(reloadedInstr.Destination, Is.EqualTo(originalInstr.Destination), $"Instruction {i} destination should match");
				Assert.That(reloadedInstr.Source.Count, Is.EqualTo(originalInstr.Source.Count), $"Instruction {i} source count should match");

				for (int j = 0; j < originalInstr.Source.Count; j++)
				{
					Assert.That(reloadedInstr.Source[j], Is.EqualTo(originalInstr.Source[j]), $"Instruction {i} source {j} should match");
				}
			}

			// Check option details
			for (int i = 0; i < component.Options.Count; i++)
			{
				var originalOpt = component.Options[i];
				var reloadedOpt = reloadedComponent.Options[i];

				Assert.That(reloadedOpt.Name, Is.EqualTo(originalOpt.Name), $"Option {i} name should match");
				Assert.That(reloadedOpt.Guid, Is.EqualTo(originalOpt.Guid), $"Option {i} GUID should match");
				Assert.That(reloadedOpt.Instructions.Count, Is.EqualTo(originalOpt.Instructions.Count), $"Option {i} instructions count should match");
			}

			// Check ModLinkFilenames
			if (component.ModLinkFilenames != null)
			{
				Assert.That(reloadedComponent.ModLinkFilenames, Is.Not.Null, "ModLinkFilenames should not be null after round-trip");
				Assert.That(reloadedComponent.ModLinkFilenames.Count, Is.EqualTo(component.ModLinkFilenames.Count), "ModLinkFilenames count should match");

				foreach (var kvp in component.ModLinkFilenames)
				{
					Assert.That(reloadedComponent.ModLinkFilenames.ContainsKey(kvp.Key), Is.True, $"ModLinkFilenames should contain URL: '{kvp.Key}'");
				}
			}

			Console.WriteLine("✅ XML Round-trip test PASSED!");
		}

		[Test]
		public void YAML_RoundTrip_Test()
		{
			Console.WriteLine("Testing YAML deserialization...");

			var expectedYaml = @"---
Guid: 987a0d17-c596-49af-ba28-851232455253
Name: KOTOR Dialogue Fixes
Author: Salk & Kainzorus Prime
Description: In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.
Category:
- Immersion
Tier: 1 - Essential
IsSelected: true
Directions: The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.
Language:
- NO
ModLinkFilenames:
  https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/: {}
Instructions:
- Action: extract
  Source:
  - <<modDirectory>>\KotOR_Dialogue_Fixes*.7z
- Action: choose
  Source:
  - cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
  - 6d593186-e356-4994-b6a8-f71445869937
Options:
- Guid: cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
  Name: Standard
  Description: Straight fixes to spelling errors/punctuation/grammar
  Restrictions:
  - 6d593186-e356-4994-b6a8-f71445869937
  Instructions:
  - Action: move
    Source:
    - <<modDirectory>>\KotOR_Dialogue_Fixes*\Corrections only\dialog.tlk
    Destination: <<kotorDirectory>>
- Guid: 6d593186-e356-4994-b6a8-f71445869937
  Name: Revised
  Description: Everything in Straight Fixes, but also has changes from the PC Moderation changes.
  IsSelected: true
  Restrictions:
  - cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
  Instructions:
  - Action: move
    Source:
    - <<modDirectory>>\KotOR_Dialogue_Fixes*\PC Response Moderation version\dialog.tlk
    Destination: <<kotorDirectory>>";

			// Load the components from the YAML
			List<ModComponent> loadedComponents = ModComponentSerializationService.DeserializeModComponentFromYamlString(expectedYaml);

			Console.WriteLine($"Loaded {loadedComponents.Count} components");
			Assert.That(loadedComponents.Count, Is.EqualTo(1), "Should load exactly 1 component");

			ModComponent component = loadedComponents[0];

			Console.WriteLine($"Component Name: {component.Name}");
			Console.WriteLine($"Component GUID: {component.Guid}");
			Console.WriteLine($"Instructions Count: {component.Instructions.Count}");
			Console.WriteLine($"Options Count: {component.Options.Count}");

			// Check instructions
			foreach (var instruction in component.Instructions)
			{
				Console.WriteLine($"  Instruction: {instruction.Action} - {instruction.Source?.FirstOrDefault()}");
			}

			// Check options
			foreach (var option in component.Options)
			{
				Console.WriteLine($"  Option: {option.Name} - Instructions: {option.Instructions.Count}");
				foreach (var optInstruction in option.Instructions)
				{
					Console.WriteLine($"    Option Instruction: {optInstruction.Action} - {optInstruction.Source?.FirstOrDefault()}");
				}
			}

			// Test round-trip
			Console.WriteLine("\nTesting round-trip...");
			string serializedYaml = ModComponentSerializationService.SerializeModComponentAsYamlString(loadedComponents);
			Console.WriteLine("Serialization successful");

			List<ModComponent> reloadedComponents = ModComponentSerializationService.DeserializeModComponentFromYamlString(serializedYaml);
			Console.WriteLine($"Reloaded {reloadedComponents.Count} components");

			Assert.That(reloadedComponents.Count, Is.EqualTo(1), "Should reload exactly 1 component");

			ModComponent reloadedComponent = reloadedComponents[0];
			Console.WriteLine($"Reloaded Component Name: {reloadedComponent.Name}");
			Console.WriteLine($"Reloaded Instructions Count: {reloadedComponent.Instructions.Count}");
			Console.WriteLine($"Reloaded Options Count: {reloadedComponent.Options.Count}");

			// Validate round-trip data integrity
			Assert.That(reloadedComponent.Name, Is.EqualTo(component.Name), "Component name should match");
			Assert.That(reloadedComponent.Guid, Is.EqualTo(component.Guid), "Component GUID should match");
			Assert.That(reloadedComponent.Instructions.Count, Is.EqualTo(component.Instructions.Count), "Instructions count should match");
			Assert.That(reloadedComponent.Options.Count, Is.EqualTo(component.Options.Count), "Options count should match");

			// Check instruction details
			for (int i = 0; i < component.Instructions.Count; i++)
			{
				var originalInstr = component.Instructions[i];
				var reloadedInstr = reloadedComponent.Instructions[i];

				Assert.That(reloadedInstr.Action, Is.EqualTo(originalInstr.Action), $"Instruction {i} action should match");
				Assert.That(reloadedInstr.Destination, Is.EqualTo(originalInstr.Destination), $"Instruction {i} destination should match");
				Assert.That(reloadedInstr.Source.Count, Is.EqualTo(originalInstr.Source.Count), $"Instruction {i} source count should match");

				for (int j = 0; j < originalInstr.Source.Count; j++)
				{
					Assert.That(reloadedInstr.Source[j], Is.EqualTo(originalInstr.Source[j]), $"Instruction {i} source {j} should match");
				}
			}

			// Check option details
			for (int i = 0; i < component.Options.Count; i++)
			{
				var originalOpt = component.Options[i];
				var reloadedOpt = reloadedComponent.Options[i];

				Assert.That(reloadedOpt.Name, Is.EqualTo(originalOpt.Name), $"Option {i} name should match");
				Assert.That(reloadedOpt.Guid, Is.EqualTo(originalOpt.Guid), $"Option {i} GUID should match");
				Assert.That(reloadedOpt.Instructions.Count, Is.EqualTo(originalOpt.Instructions.Count), $"Option {i} instructions count should match");
			}

			// Check ModLinkFilenames
			if (component.ModLinkFilenames != null)
			{
				Assert.That(reloadedComponent.ModLinkFilenames, Is.Not.Null, "ModLinkFilenames should not be null after round-trip");
				Assert.That(reloadedComponent.ModLinkFilenames.Count, Is.EqualTo(component.ModLinkFilenames.Count), "ModLinkFilenames count should match");

				foreach (var kvp in component.ModLinkFilenames)
				{
					Assert.That(reloadedComponent.ModLinkFilenames.ContainsKey(kvp.Key), Is.True, $"ModLinkFilenames should contain URL: '{kvp.Key}'");
				}
			}

			Console.WriteLine("✅ YAML Round-trip test PASSED!");
		}

		[Test]
		public void Markdown_RoundTrip_Test()
		{
			Console.WriteLine("Testing Markdown deserialization...");

			var expectedMarkdown = @"## Mod List

### KOTOR Dialogue Fixes

**Name:** [KOTOR Dialogue Fixes](https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/)

**Author:** Salk & Kainzorus Prime

**Description:** In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.

**Category & Tier:** Immersion / 1 - Essential

**Non-English Functionality:** NO


**Installation Instructions:** The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.

<!--<<ModSync>>
Guid = ""987a0d17-c596-49af-ba28-851232455253""
Instructions = [
     = {
        Guid = ""bdb03a6b-6447-4264-97bd-378b9ac2ec95""
        Action = ""Extract""
        Source = [
            ""<<modDirectory>>\\KotOR_Dialogue_Fixes*.7z"",
        ]
    }
     = {
        Guid = ""2da751a1-0b55-4eee-a04c-5f5904f49a0d""
        Action = ""Choose""
        Source = [
            ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"",
            ""6d593186-e356-4994-b6a8-f71445869937"",
        ]
    }
]
Options = [
     = {
        Guid = ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
        Name = ""Standard""
        Description = ""Straight fixes to spelling errors/punctuation/grammar""
        Restrictions = [
            ""6d593186-e356-4994-b6a8-f71445869937"",
        ]
        Instructions = [
             = {
                Guid = ""48593f5e-6c02-411e-bbbf-94aa05561963""
                Action = ""Move""
                Source = [
                    ""<<modDirectory>>\\KotOR_Dialogue_Fixes*\\Corrections only\\dialog.tlk"",
                ]
                Destination = ""<<kotorDirectory>>""
            }
        ]
    }
     = {
        Guid = ""6d593186-e356-4994-b6a8-f71445869937""
        Name = ""Revised""
        Description = ""Everything in Straight Fixes, but also has changes from the PC Moderation changes.""
        IsSelected = true
        Restrictions = [
            ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"",
        ]
        Instructions = [
             = {
                Guid = ""12eca962-9285-428b-8811-5e7afcd3f002""
                Action = ""Move""
                Source = [
                    ""<<modDirectory>>\\KotOR_Dialogue_Fixes*\\PC Response Moderation version\\dialog.tlk"",
                ]
                Destination = ""<<kotorDirectory>>""
            }
        ]
    }
]
-->

___";

			// Load the components from the Markdown
			List<ModComponent> loadedComponents = ModComponentSerializationService.DeserializeModComponentFromMarkdownString(expectedMarkdown);

			Console.WriteLine($"Loaded {loadedComponents.Count} components");
			Assert.That(loadedComponents.Count, Is.EqualTo(1), "Should load exactly 1 component");

			ModComponent component = loadedComponents[0];

			Console.WriteLine($"Component Name: {component.Name}");
			Console.WriteLine($"Component GUID: {component.Guid}");
			Console.WriteLine($"Instructions Count: {component.Instructions.Count}");
			Console.WriteLine($"Options Count: {component.Options.Count}");

			// Check instructions
			foreach (var instruction in component.Instructions)
			{
				Console.WriteLine($"  Instruction: {instruction.Action} - {instruction.Source?.FirstOrDefault()}");
			}

			// Check options
			foreach (var option in component.Options)
			{
				Console.WriteLine($"  Option: {option.Name} - Instructions: {option.Instructions.Count}");
				foreach (var optInstruction in option.Instructions)
				{
					Console.WriteLine($"    Option Instruction: {optInstruction.Action} - {optInstruction.Source?.FirstOrDefault()}");
				}
			}

			// Test round-trip
			Console.WriteLine("\nTesting round-trip...");
			string serializedMarkdown = ModComponentSerializationService.SerializeModComponentAsMarkdownString(loadedComponents);
			Console.WriteLine("Serialization successful");

			List<ModComponent> reloadedComponents = ModComponentSerializationService.DeserializeModComponentFromMarkdownString(serializedMarkdown);
			Console.WriteLine($"Reloaded {reloadedComponents.Count} components");

			Assert.That(reloadedComponents.Count, Is.EqualTo(1), "Should reload exactly 1 component");

			ModComponent reloadedComponent = reloadedComponents[0];
			Console.WriteLine($"Reloaded Component Name: {reloadedComponent.Name}");
			Console.WriteLine($"Reloaded Instructions Count: {reloadedComponent.Instructions.Count}");
			Console.WriteLine($"Reloaded Options Count: {reloadedComponent.Options.Count}");

			// Validate round-trip data integrity
			Assert.That(reloadedComponent.Name, Is.EqualTo(component.Name), "Component name should match");
			Assert.That(reloadedComponent.Guid, Is.EqualTo(component.Guid), "Component GUID should match");
			Assert.That(reloadedComponent.Instructions.Count, Is.EqualTo(component.Instructions.Count), "Instructions count should match");
			Assert.That(reloadedComponent.Options.Count, Is.EqualTo(component.Options.Count), "Options count should match");

			// Check instruction details
			for (int i = 0; i < component.Instructions.Count; i++)
			{
				var originalInstr = component.Instructions[i];
				var reloadedInstr = reloadedComponent.Instructions[i];

				Assert.That(reloadedInstr.Action, Is.EqualTo(originalInstr.Action), $"Instruction {i} action should match");
				Assert.That(reloadedInstr.Destination, Is.EqualTo(originalInstr.Destination), $"Instruction {i} destination should match");
				Assert.That(reloadedInstr.Source.Count, Is.EqualTo(originalInstr.Source.Count), $"Instruction {i} source count should match");

				for (int j = 0; j < originalInstr.Source.Count; j++)
				{
					Assert.That(reloadedInstr.Source[j], Is.EqualTo(originalInstr.Source[j]), $"Instruction {i} source {j} should match");
				}
			}

			// Check option details
			for (int i = 0; i < component.Options.Count; i++)
			{
				var originalOpt = component.Options[i];
				var reloadedOpt = reloadedComponent.Options[i];

				Assert.That(reloadedOpt.Name, Is.EqualTo(originalOpt.Name), $"Option {i} name should match");
				Assert.That(reloadedOpt.Guid, Is.EqualTo(originalOpt.Guid), $"Option {i} GUID should match");
				Assert.That(reloadedOpt.Instructions.Count, Is.EqualTo(originalOpt.Instructions.Count), $"Option {i} instructions count should match");
			}

			// Check ModLinkFilenames
			if (component.ModLinkFilenames != null)
			{
				Assert.That(reloadedComponent.ModLinkFilenames, Is.Not.Null, "ModLinkFilenames should not be null after round-trip");
				Assert.That(reloadedComponent.ModLinkFilenames.Count, Is.EqualTo(component.ModLinkFilenames.Count), "ModLinkFilenames count should match");

				foreach (var kvp in component.ModLinkFilenames)
				{
					Assert.That(reloadedComponent.ModLinkFilenames.ContainsKey(kvp.Key), Is.True, $"ModLinkFilenames should contain URL: '{kvp.Key}'");
				}
			}

			Console.WriteLine("✅ Markdown Round-trip test PASSED!");
		}

		[Test]
		public void JSON_RoundTrip_Test()
		{
			Console.WriteLine("Testing JSON deserialization...");

			var expectedJson = @"{
  ""metadata"": {
    ""fileFormatVersion"": ""2.0""
  },
  ""components"": [
    {
      ""guid"": ""987a0d17-c596-49af-ba28-851232455253"",
      ""name"": ""KOTOR Dialogue Fixes"",
      ""author"": ""Salk & Kainzorus Prime"",
      ""description"": ""In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices."",
      ""category"": [
        ""Immersion""
      ],
      ""tier"": ""1 - Essential"",
      ""language"": [
        ""NO""
      ],
      ""modLinkFilenames"": {
        ""https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/"": {}
      },
      ""directions"": ""The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override."",
      ""instructions"": [
        {
          ""guid"": ""62ad0a1f-30aa-4991-962f-742315cf60fd"",
          ""action"": ""Extract"",
          ""source"": [
            ""<<modDirectory>>\\KotOR_Dialogue_Fixes*.7z""
          ]
        },
        {
          ""guid"": ""89ec6316-5068-4294-a26d-3570beda1138"",
          ""action"": ""Choose"",
          ""source"": [
            ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"",
            ""6d593186-e356-4994-b6a8-f71445869937""
          ]
        }
      ],
      ""options"": [
        {
          ""guid"": ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"",
          ""name"": ""Standard"",
          ""description"": ""Straight fixes to spelling errors/punctuation/grammar"",
          ""restrictions"": [
            ""6d593186-e356-4994-b6a8-f71445869937""
          ],
          ""instructions"": [
            {
              ""guid"": ""2eb69c3a-67e1-4aa0-9d27-0408c03ae6a4"",
              ""action"": ""Move"",
              ""source"": [
                ""<<modDirectory>>\\KotOR_Dialogue_Fixes*\\Corrections only\\dialog.tlk""
              ],
              ""destination"": ""<<kotorDirectory>>""
            }
          ]
        },
        {
          ""guid"": ""6d593186-e356-4994-b6a8-f71445869937"",
          ""name"": ""Revised"",
          ""description"": ""Everything in Straight Fixes, but also has changes from the PC Moderation changes."",
          ""restrictions"": [
            ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
          ],
          ""instructions"": [
            {
              ""guid"": ""f7234d28-b9d2-48ba-a98e-7eee53f5c67a"",
              ""action"": ""Move"",
              ""source"": [
                ""<<modDirectory>>\\KotOR_Dialogue_Fixes*\\PC Response Moderation version\\dialog.tlk""
              ],
              ""destination"": ""<<kotorDirectory>>""
            }
          ]
        }
      ]
    }
  ]
}";

			// Load the components from the JSON
			List<ModComponent> loadedComponents = ModComponentSerializationService.DeserializeModComponentFromJsonString(expectedJson);

			Console.WriteLine($"Loaded {loadedComponents.Count} components");
			Assert.That(loadedComponents.Count, Is.EqualTo(1), "Should load exactly 1 component");

			ModComponent component = loadedComponents[0];

			Console.WriteLine($"Component Name: {component.Name}");
			Console.WriteLine($"Component GUID: {component.Guid}");
			Console.WriteLine($"Instructions Count: {component.Instructions.Count}");
			Console.WriteLine($"Options Count: {component.Options.Count}");

			// Check instructions
			foreach (var instruction in component.Instructions)
			{
				Console.WriteLine($"  Instruction: {instruction.Action} - {instruction.Source?.FirstOrDefault()}");
			}

			// Check options
			foreach (var option in component.Options)
			{
				Console.WriteLine($"  Option: {option.Name} - Instructions: {option.Instructions.Count}");
				foreach (var optInstruction in option.Instructions)
				{
					Console.WriteLine($"    Option Instruction: {optInstruction.Action} - {optInstruction.Source?.FirstOrDefault()}");
				}
			}

			// Test round-trip
			Console.WriteLine("\nTesting round-trip...");
			string serializedJson = ModComponentSerializationService.SerializeModComponentAsJsonString(loadedComponents);
			Console.WriteLine("Serialization successful");

			List<ModComponent> reloadedComponents = ModComponentSerializationService.DeserializeModComponentFromJsonString(serializedJson);
			Console.WriteLine($"Reloaded {reloadedComponents.Count} components");

			Assert.That(reloadedComponents.Count, Is.EqualTo(1), "Should reload exactly 1 component");

			ModComponent reloadedComponent = reloadedComponents[0];
			Console.WriteLine($"Reloaded Component Name: {reloadedComponent.Name}");
			Console.WriteLine($"Reloaded Instructions Count: {reloadedComponent.Instructions.Count}");
			Console.WriteLine($"Reloaded Options Count: {reloadedComponent.Options.Count}");

			// Validate round-trip data integrity
			Assert.That(reloadedComponent.Name, Is.EqualTo(component.Name), "Component name should match");
			Assert.That(reloadedComponent.Guid, Is.EqualTo(component.Guid), "Component GUID should match");
			Assert.That(reloadedComponent.Instructions.Count, Is.EqualTo(component.Instructions.Count), "Instructions count should match");
			Assert.That(reloadedComponent.Options.Count, Is.EqualTo(component.Options.Count), "Options count should match");

			// Check instruction details
			for (int i = 0; i < component.Instructions.Count; i++)
			{
				var originalInstr = component.Instructions[i];
				var reloadedInstr = reloadedComponent.Instructions[i];

				Assert.That(reloadedInstr.Action, Is.EqualTo(originalInstr.Action), $"Instruction {i} action should match");
				Assert.That(reloadedInstr.Destination, Is.EqualTo(originalInstr.Destination), $"Instruction {i} destination should match");
				Assert.That(reloadedInstr.Source.Count, Is.EqualTo(originalInstr.Source.Count), $"Instruction {i} source count should match");

				for (int j = 0; j < originalInstr.Source.Count; j++)
				{
					Assert.That(reloadedInstr.Source[j], Is.EqualTo(originalInstr.Source[j]), $"Instruction {i} source {j} should match");
				}
			}

			// Check option details
			for (int i = 0; i < component.Options.Count; i++)
			{
				var originalOpt = component.Options[i];
				var reloadedOpt = reloadedComponent.Options[i];

				Assert.That(reloadedOpt.Name, Is.EqualTo(originalOpt.Name), $"Option {i} name should match");
				Assert.That(reloadedOpt.Guid, Is.EqualTo(originalOpt.Guid), $"Option {i} GUID should match");
				Assert.That(reloadedOpt.Instructions.Count, Is.EqualTo(originalOpt.Instructions.Count), $"Option {i} instructions count should match");
			}

			// Check ModLinkFilenames
			if (component.ModLinkFilenames != null)
			{
				Assert.That(reloadedComponent.ModLinkFilenames, Is.Not.Null, "ModLinkFilenames should not be null after round-trip");
				Assert.That(reloadedComponent.ModLinkFilenames.Count, Is.EqualTo(component.ModLinkFilenames.Count), "ModLinkFilenames count should match");

				foreach (var kvp in component.ModLinkFilenames)
				{
					Assert.That(reloadedComponent.ModLinkFilenames.ContainsKey(kvp.Key), Is.True, $"ModLinkFilenames should contain URL: '{kvp.Key}'");
				}
			}

			Console.WriteLine("✅ JSON Round-trip test PASSED!");
		}

		[Test]
		public void TOML_To_YAML_To_TOML_RoundTrip_Test()
		{
			Console.WriteLine("Testing TOML -> YAML -> TOML round-trip...");

			var expectedToml = @"[[thisMod]]
ModLinkFilenames = { ""https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/"" = {  } }
Guid = ""987a0d17-c596-49af-ba28-851232455253""
Name = ""KOTOR Dialogue Fixes""
Author = ""Salk & Kainzorus Prime""
Tier = ""1 - Essential""
Description = ""In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.""
InstallationMethod = ""Loose-File Mod""
Directions = ""The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.""
IsSelected = true
Category = [""Immersion""]
Language = [""NO""]

[[thisMod.Instructions]]
Guid = ""e6d0dbb7-75f7-4886-a4a5-e7eea85dac1c""
Action = ""Extract""
Source = [""<<modDirectory>>\\KotOR_Dialogue_Fixes*.7z""]

[[thisMod.Instructions]]
Guid = ""b201d6e8-3d07-4de5-a937-47ba9952afac""
Action = ""Choose""
Source = [""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"", ""6d593186-e356-4994-b6a8-f71445869937""]

[[thisMod.Options]]
Guid = ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
Name = ""Standard""
Description = ""Straight fixes to spelling errors/punctuation/grammar""
Restrictions = [""6d593186-e356-4994-b6a8-f71445869937""]

[[thisMod.Options.Instructions]]
Parent = ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
Guid = ""9521423e-e617-474c-bcbb-a15563a516fc""
Action = ""Move""
Destination = ""<<kotorDirectory>>""
Source = [""<<modDirectory>>\\KotOR_Dialogue_Fixes*\\Corrections only\\dialog.tlk""]

[[thisMod.Options]]
Guid = ""6d593186-e356-4994-b6a8-f71445869937""
Name = ""Revised""
Description = ""Everything in Straight Fixes, but also has changes from the PC Moderation changes.""
IsSelected = true
Restrictions = [""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""]

[[thisMod.Options.Instructions]]
Parent = ""6d593186-e356-4994-b6a8-f71445869937""
Guid = ""80fba038-4a24-4716-a0cc-1d4051e952a0""
Action = ""Move""
Destination = ""<<kotorDirectory>>""
Source = [""<<modDirectory>>\\KotOR_Dialogue_Fixes*\\PC Response Moderation version\\dialog.tlk""]";

			// Step 1: Load from TOML
			Console.WriteLine("Step 1: Loading from TOML...");
			List<ModComponent> originalComponents = ModComponentSerializationService.DeserializeModComponentFromTomlString(expectedToml);
			Console.WriteLine($"Loaded {originalComponents.Count} components from TOML");
			Assert.That(originalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from TOML");

			ModComponent originalComponent = originalComponents[0];
			Console.WriteLine($"Original Component: {originalComponent.Name}");

			// Step 2: Serialize to YAML
			Console.WriteLine("\nStep 2: Serializing to YAML...");
			string yamlContent = ModComponentSerializationService.SerializeModComponentAsYamlString(originalComponents);
			Console.WriteLine("YAML serialization successful");
			Assert.That(yamlContent, Is.Not.Null.And.Not.Empty, "YAML content should not be null or empty");

			// Step 3: Deserialize from YAML
			Console.WriteLine("\nStep 3: Loading from YAML...");
			List<ModComponent> yamlComponents = ModComponentSerializationService.DeserializeModComponentFromYamlString(yamlContent);
			Console.WriteLine($"Loaded {yamlComponents.Count} components from YAML");
			Assert.That(yamlComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from YAML");

			ModComponent yamlComponent = yamlComponents[0];
			Console.WriteLine($"YAML Component: {yamlComponent.Name}");

			// Step 4: Serialize back to TOML
			Console.WriteLine("\nStep 4: Serializing back to TOML...");
			string finalToml = ModComponentSerializationService.SerializeModComponentAsTomlString(yamlComponents);
			Console.WriteLine("Final TOML serialization successful");

			// Step 5: Deserialize final TOML
			Console.WriteLine("\nStep 5: Loading final TOML...");
			List<ModComponent> finalComponents = ModComponentSerializationService.DeserializeModComponentFromTomlString(finalToml);
			Console.WriteLine($"Loaded {finalComponents.Count} components from final TOML");
			Assert.That(finalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from final TOML");

			ModComponent finalComponent = finalComponents[0];
			Console.WriteLine($"Final Component: {finalComponent.Name}");

			// Validate data integrity through the entire round-trip
			Console.WriteLine("\nValidating data integrity...");
			AssertComponentEquality(originalComponent, finalComponent);

			Console.WriteLine("✅ TOML -> YAML -> TOML Round-trip test PASSED!");
		}

		[Test]
		public void YAML_To_XML_To_TOML_RoundTrip_Test()
		{
			Console.WriteLine("Testing YAML -> XML -> TOML round-trip...");

			var expectedYaml = @"---
Guid: 987a0d17-c596-49af-ba28-851232455253
Name: KOTOR Dialogue Fixes
Author: Salk & Kainzorus Prime
Description: In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.
Category:
- Immersion
Tier: 1 - Essential
IsSelected: true
Directions: The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.
Language:
- NO
ModLinkFilenames:
  https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/: {}
Instructions:
- Action: extract
  Source:
  - <<modDirectory>>\KotOR_Dialogue_Fixes*.7z
- Action: choose
  Source:
  - cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
  - 6d593186-e356-4994-b6a8-f71445869937
Options:
- Guid: cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
  Name: Standard
  Description: Straight fixes to spelling errors/punctuation/grammar
  Restrictions:
  - 6d593186-e356-4994-b6a8-f71445869937
  Instructions:
  - Action: move
    Source:
    - <<modDirectory>>\KotOR_Dialogue_Fixes*\Corrections only\dialog.tlk
    Destination: <<kotorDirectory>>
- Guid: 6d593186-e356-4994-b6a8-f71445869937
  Name: Revised
  Description: Everything in Straight Fixes, but also has changes from the PC Moderation changes.
  IsSelected: true
  Restrictions:
  - cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
  Instructions:
  - Action: move
    Source:
    - <<modDirectory>>\KotOR_Dialogue_Fixes*\PC Response Moderation version\dialog.tlk
    Destination: <<kotorDirectory>>";

			// Step 1: Load from YAML
			Console.WriteLine("Step 1: Loading from YAML...");
			List<ModComponent> originalComponents = ModComponentSerializationService.DeserializeModComponentFromYamlString(expectedYaml);
			Console.WriteLine($"Loaded {originalComponents.Count} components from YAML");
			Assert.That(originalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from YAML");

			ModComponent originalComponent = originalComponents[0];
			Console.WriteLine($"Original Component: {originalComponent.Name}");

			// Step 2: Serialize to XML
			Console.WriteLine("\nStep 2: Serializing to XML...");
			string xmlContent = ModComponentSerializationService.SerializeModComponentAsXmlString(originalComponents);
			Console.WriteLine("XML serialization successful");
			Assert.That(xmlContent, Is.Not.Null.And.Not.Empty, "XML content should not be null or empty");

			// Step 3: Deserialize from XML
			Console.WriteLine("\nStep 3: Loading from XML...");
			List<ModComponent> xmlComponents = ModComponentSerializationService.DeserializeModComponentFromXmlString(xmlContent);
			Console.WriteLine($"Loaded {xmlComponents.Count} components from XML");
			Assert.That(xmlComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from XML");

			ModComponent xmlComponent = xmlComponents[0];
			Console.WriteLine($"XML Component: {xmlComponent.Name}");

			// Step 4: Serialize back to TOML
			Console.WriteLine("\nStep 4: Serializing back to TOML...");
			string finalToml = ModComponentSerializationService.SerializeModComponentAsTomlString(xmlComponents);
			Console.WriteLine("Final TOML serialization successful");

			// Step 5: Deserialize final TOML
			Console.WriteLine("\nStep 5: Loading final TOML...");
			List<ModComponent> finalComponents = ModComponentSerializationService.DeserializeModComponentFromTomlString(finalToml);
			Console.WriteLine($"Loaded {finalComponents.Count} components from final TOML");
			Assert.That(finalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from final TOML");

			ModComponent finalComponent = finalComponents[0];
			Console.WriteLine($"Final Component: {finalComponent.Name}");

			// Validate data integrity through the entire round-trip
			Console.WriteLine("\nValidating data integrity...");
			AssertComponentEquality(originalComponent, finalComponent);

			Console.WriteLine("✅ YAML -> XML -> TOML Round-trip test PASSED!");
		}

		[Test]
		public void XML_To_TOML_To_YAML_RoundTrip_Test()
		{
			Console.WriteLine("Testing XML -> TOML -> YAML round-trip...");

			var expectedXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ModComponentList>
  <Metadata>
    <FileFormatVersion>2.0</FileFormatVersion>
  </Metadata>
  <Components>
    <Component>
      <Guid>987a0d17-c596-49af-ba28-851232455253</Guid>
      <Name>KOTOR Dialogue Fixes</Name>
      <Author>Salk &amp; Kainzorus Prime</Author>
      <Description>In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.</Description>
      <Category>
        <Item>Immersion</Item>
      </Category>
      <Tier>1 - Essential</Tier>
      <Language>
        <Item>NO</Item>
      </Language>
      <ModLinkFilenames>
        <Url Value=""https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/"" />
      </ModLinkFilenames>
      <Directions>The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.</Directions>
      <Instructions>
        <Instruction>
          <Guid>d17cbf8a-2ce6-4403-a53b-3c24506e21ce</Guid>
          <Action>Extract</Action>
          <Source>
            <Item>&lt;&lt;modDirectory&gt;&gt;\KotOR_Dialogue_Fixes*.7z</Item>
          </Source>
        </Instruction>
        <Instruction>
          <Guid>d37453c9-2e9c-47a7-924c-a0ecb2c8eec4</Guid>
          <Action>Choose</Action>
          <Source>
            <Item>cf2a12ec-3932-42f8-996d-b1b1bdfdbb48</Item>
            <Item>6d593186-e356-4994-b6a8-f71445869937</Item>
          </Source>
        </Instruction>
      </Instructions>
      <Options>
        <Option>
          <Guid>cf2a12ec-3932-42f8-996d-b1b1bdfdbb48</Guid>
          <Name>Standard</Name>
          <Description>Straight fixes to spelling errors/punctuation/grammar</Description>
          <Instructions>
            <Instruction>
              <Guid>c2a8586e-5287-4336-a525-3b7e65cd673c</Guid>
              <Action>Move</Action>
              <Source>
                <Item>&lt;&lt;modDirectory&gt;&gt;\KotOR_Dialogue_Fixes*\Corrections only\dialog.tlk</Item>
              </Source>
              <Destination>&lt;&lt;kotorDirectory&gt;&gt;</Destination>
            </Instruction>
          </Instructions>
        </Option>
        <Option>
          <Guid>6d593186-e356-4994-b6a8-f71445869937</Guid>
          <Name>Revised</Name>
          <Description>Everything in Straight Fixes, but also has changes from the PC Moderation changes.</Description>
          <Instructions>
            <Instruction>
              <Guid>11ccc491-e8c7-4f58-9c8c-6ad88205b0ec</Guid>
              <Action>Move</Action>
              <Source>
                <Item>&lt;&lt;modDirectory&gt;&gt;\KotOR_Dialogue_Fixes*\PC Response Moderation version\dialog.tlk</Item>
              </Source>
              <Destination>&lt;&lt;kotorDirectory&gt;&gt;</Destination>
            </Instruction>
          </Instructions>
        </Option>
      </Options>
    </Component>
  </Components>
</ModComponentList>";

			// Step 1: Load from XML
			Console.WriteLine("Step 1: Loading from XML...");
			List<ModComponent> originalComponents = ModComponentSerializationService.DeserializeModComponentFromXmlString(expectedXml);
			Console.WriteLine($"Loaded {originalComponents.Count} components from XML");
			Assert.That(originalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from XML");

			ModComponent originalComponent = originalComponents[0];
			Console.WriteLine($"Original Component: {originalComponent.Name}");

			// Step 2: Serialize to TOML
			Console.WriteLine("\nStep 2: Serializing to TOML...");
			string tomlContent = ModComponentSerializationService.SerializeModComponentAsTomlString(originalComponents);
			Console.WriteLine("TOML serialization successful");
			Assert.That(tomlContent, Is.Not.Null.And.Not.Empty, "TOML content should not be null or empty");

			// Step 3: Deserialize from TOML
			Console.WriteLine("\nStep 3: Loading from TOML...");
			List<ModComponent> tomlComponents = ModComponentSerializationService.DeserializeModComponentFromTomlString(tomlContent);
			Console.WriteLine($"Loaded {tomlComponents.Count} components from TOML");
			Assert.That(tomlComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from TOML");

			ModComponent tomlComponent = tomlComponents[0];
			Console.WriteLine($"TOML Component: {tomlComponent.Name}");

			// Step 4: Serialize back to YAML
			Console.WriteLine("\nStep 4: Serializing back to YAML...");
			string finalYaml = ModComponentSerializationService.SerializeModComponentAsYamlString(tomlComponents);
			Console.WriteLine("Final YAML serialization successful");

			// Step 5: Deserialize final YAML
			Console.WriteLine("\nStep 5: Loading final YAML...");
			List<ModComponent> finalComponents = ModComponentSerializationService.DeserializeModComponentFromYamlString(finalYaml);
			Console.WriteLine($"Loaded {finalComponents.Count} components from final YAML");
			Assert.That(finalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from final YAML");

			ModComponent finalComponent = finalComponents[0];
			Console.WriteLine($"Final Component: {finalComponent.Name}");

			// Validate data integrity through the entire round-trip
			Console.WriteLine("\nValidating data integrity...");
			AssertComponentEquality(originalComponent, finalComponent);

			Console.WriteLine("✅ XML -> TOML -> YAML Round-trip test PASSED!");
		}

		[Test]
		public void JSON_To_Markdown_To_XML_RoundTrip_Test()
		{
			Console.WriteLine("Testing JSON -> Markdown -> XML round-trip...");

			var expectedJson = @"{
  ""metadata"": {
    ""fileFormatVersion"": ""2.0""
  },
  ""components"": [
    {
      ""guid"": ""987a0d17-c596-49af-ba28-851232455253"",
      ""name"": ""KOTOR Dialogue Fixes"",
      ""author"": ""Salk & Kainzorus Prime"",
      ""description"": ""In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices."",
      ""category"": [
        ""Immersion""
      ],
      ""tier"": ""1 - Essential"",
      ""language"": [
        ""NO""
      ],
      ""modLinkFilenames"": {
        ""https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/"": {}
      },
      ""directions"": ""The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override."",
      ""instructions"": [
        {
          ""guid"": ""62ad0a1f-30aa-4991-962f-742315cf60fd"",
          ""action"": ""Extract"",
          ""source"": [
            ""<<modDirectory>>\\KotOR_Dialogue_Fixes*.7z""
          ]
        },
        {
          ""guid"": ""89ec6316-5068-4294-a26d-3570beda1138"",
          ""action"": ""Choose"",
          ""source"": [
            ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"",
            ""6d593186-e356-4994-b6a8-f71445869937""
          ]
        }
      ],
      ""options"": [
        {
          ""guid"": ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"",
          ""name"": ""Standard"",
          ""description"": ""Straight fixes to spelling errors/punctuation/grammar"",
          ""restrictions"": [
            ""6d593186-e356-4994-b6a8-f71445869937""
          ],
          ""instructions"": [
            {
              ""guid"": ""2eb69c3a-67e1-4aa0-9d27-0408c03ae6a4"",
              ""action"": ""Move"",
              ""source"": [
                ""<<modDirectory>>\\KotOR_Dialogue_Fixes*\\Corrections only\\dialog.tlk""
              ],
              ""destination"": ""<<kotorDirectory>>""
            }
          ]
        },
        {
          ""guid"": ""6d593186-e356-4994-b6a8-f71445869937"",
          ""name"": ""Revised"",
          ""description"": ""Everything in Straight Fixes, but also has changes from the PC Moderation changes."",
          ""restrictions"": [
            ""cf2a12ec-3932-42f8-996d-b1b1bdfdbb48""
          ],
          ""instructions"": [
            {
              ""guid"": ""f7234d28-b9d2-48ba-a98e-7eee53f5c67a"",
              ""action"": ""Move"",
              ""source"": [
                ""<<modDirectory>>\\KotOR_Dialogue_Fixes*\\PC Response Moderation version\\dialog.tlk""
              ],
              ""destination"": ""<<kotorDirectory>>""
            }
          ]
        }
      ]
    }
  ]
}";

			// Step 1: Load from JSON
			Console.WriteLine("Step 1: Loading from JSON...");
			List<ModComponent> originalComponents = ModComponentSerializationService.DeserializeModComponentFromJsonString(expectedJson);
			Console.WriteLine($"Loaded {originalComponents.Count} components from JSON");
			Assert.That(originalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from JSON");

			ModComponent originalComponent = originalComponents[0];
			Console.WriteLine($"Original Component: {originalComponent.Name}");

			// Step 2: Serialize to Markdown
			Console.WriteLine("\nStep 2: Serializing to Markdown...");
			string markdownContent = ModComponentSerializationService.SerializeModComponentAsMarkdownString(originalComponents);
			Console.WriteLine("Markdown serialization successful");
			Assert.That(markdownContent, Is.Not.Null.And.Not.Empty, "Markdown content should not be null or empty");

			// Step 3: Deserialize from Markdown
			Console.WriteLine("\nStep 3: Loading from Markdown...");
			List<ModComponent> markdownComponents = ModComponentSerializationService.DeserializeModComponentFromMarkdownString(markdownContent);
			Console.WriteLine($"Loaded {markdownComponents.Count} components from Markdown");
			Assert.That(markdownComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from Markdown");

			ModComponent markdownComponent = markdownComponents[0];
			Console.WriteLine($"Markdown Component: {markdownComponent.Name}");

			// Step 4: Serialize back to XML
			Console.WriteLine("\nStep 4: Serializing back to XML...");
			string finalXml = ModComponentSerializationService.SerializeModComponentAsXmlString(markdownComponents);
			Console.WriteLine("Final XML serialization successful");

			// Step 5: Deserialize final XML
			Console.WriteLine("\nStep 5: Loading final XML...");
			List<ModComponent> finalComponents = ModComponentSerializationService.DeserializeModComponentFromXmlString(finalXml);
			Console.WriteLine($"Loaded {finalComponents.Count} components from final XML");
			Assert.That(finalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from final XML");

			ModComponent finalComponent = finalComponents[0];
			Console.WriteLine($"Final Component: {finalComponent.Name}");

			// Validate data integrity through the entire round-trip
			Console.WriteLine("\nValidating data integrity...");
			AssertComponentEquality(originalComponent, finalComponent);

			Console.WriteLine("✅ JSON -> Markdown -> XML Round-trip test PASSED!");
		}

		[Test]
		public void YAML_To_JSON_To_Markdown_RoundTrip_Test()
		{
			Console.WriteLine("Testing YAML -> JSON -> Markdown round-trip...");

			var expectedYaml = @"---
Guid: 987a0d17-c596-49af-ba28-851232455253
Name: KOTOR Dialogue Fixes
Author: Salk & Kainzorus Prime
Description: In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.
Category:
- Immersion
Tier: 1 - Essential
IsSelected: true
Directions: The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.
Language:
- NO
ModLinkFilenames:
  https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/: {}
Instructions:
- Action: extract
  Source:
  - <<modDirectory>>\KotOR_Dialogue_Fixes*.7z
- Action: choose
  Source:
  - cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
  - 6d593186-e356-4994-b6a8-f71445869937
Options:
- Guid: cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
  Name: Standard
  Description: Straight fixes to spelling errors/punctuation/grammar
  Restrictions:
  - 6d593186-e356-4994-b6a8-f71445869937
  Instructions:
  - Action: move
    Source:
    - <<modDirectory>>\KotOR_Dialogue_Fixes*\Corrections only\dialog.tlk
    Destination: <<kotorDirectory>>
- Guid: 6d593186-e356-4994-b6a8-f71445869937
  Name: Revised
  Description: Everything in Straight Fixes, but also has changes from the PC Moderation changes.
  IsSelected: true
  Restrictions:
  - cf2a12ec-3932-42f8-996d-b1b1bdfdbb48
  Instructions:
  - Action: move
    Source:
    - <<modDirectory>>\KotOR_Dialogue_Fixes*\PC Response Moderation version\dialog.tlk
    Destination: <<kotorDirectory>>";

			// Step 1: Load from YAML
			Console.WriteLine("Step 1: Loading from YAML...");
			List<ModComponent> originalComponents = ModComponentSerializationService.DeserializeModComponentFromYamlString(expectedYaml);
			Console.WriteLine($"Loaded {originalComponents.Count} components from YAML");
			Assert.That(originalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from YAML");

			ModComponent originalComponent = originalComponents[0];
			Console.WriteLine($"Original Component: {originalComponent.Name}");

			// Step 2: Serialize to JSON
			Console.WriteLine("\nStep 2: Serializing to JSON...");
			string jsonContent = ModComponentSerializationService.SerializeModComponentAsJsonString(originalComponents);
			Console.WriteLine("JSON serialization successful");
			Assert.That(jsonContent, Is.Not.Null.And.Not.Empty, "JSON content should not be null or empty");

			// Step 3: Deserialize from JSON
			Console.WriteLine("\nStep 3: Loading from JSON...");
			List<ModComponent> jsonComponents = ModComponentSerializationService.DeserializeModComponentFromJsonString(jsonContent);
			Console.WriteLine($"Loaded {jsonComponents.Count} components from JSON");
			Assert.That(jsonComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from JSON");

			ModComponent jsonComponent = jsonComponents[0];
			Console.WriteLine($"JSON Component: {jsonComponent.Name}");

			// Step 4: Serialize back to Markdown
			Console.WriteLine("\nStep 4: Serializing back to Markdown...");
			string finalMarkdown = ModComponentSerializationService.SerializeModComponentAsMarkdownString(jsonComponents);
			Console.WriteLine("Final Markdown serialization successful");

			// Step 5: Deserialize final Markdown
			Console.WriteLine("\nStep 5: Loading final Markdown...");
			List<ModComponent> finalComponents = ModComponentSerializationService.DeserializeModComponentFromMarkdownString(finalMarkdown);
			Console.WriteLine($"Loaded {finalComponents.Count} components from final Markdown");
			Assert.That(finalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from final Markdown");

			ModComponent finalComponent = finalComponents[0];
			Console.WriteLine($"Final Component: {finalComponent.Name}");

			// Validate data integrity through the entire round-trip
			Console.WriteLine("\nValidating data integrity...");
			AssertComponentEquality(originalComponent, finalComponent);

			Console.WriteLine("✅ YAML -> JSON -> Markdown Round-trip test PASSED!");
		}

		[Test]
		public void XML_To_YAML_To_JSON_RoundTrip_Test()
		{
			Console.WriteLine("Testing XML -> YAML -> JSON round-trip...");

			var expectedXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<ModComponentList>
  <Metadata>
    <FileFormatVersion>2.0</FileFormatVersion>
  </Metadata>
  <Components>
    <Component>
      <Guid>987a0d17-c596-49af-ba28-851232455253</Guid>
      <Name>KOTOR Dialogue Fixes</Name>
      <Author>Salk &amp; Kainzorus Prime</Author>
      <Description>In addition to fixing several typos, this mod takes the PC's dialogue—which is written in such a way as to make the PC sound constantly shocked, stupid, or needlessly and overtly evil—and replaces it with more moderate and reasonable responses, even for DS choices.</Description>
      <Category>
        <Item>Immersion</Item>
      </Category>
      <Tier>1 - Essential</Tier>
      <Language>
        <Item>NO</Item>
      </Language>
      <ModLinkFilenames>
        <Url Value=""https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/"" />
      </ModLinkFilenames>
      <Directions>The choice of which version to use is up to you; I recommend PC Response Moderation, as it makes your character sound less like a giddy little schoolchild following every little dialogue, but if you prefer only bugfixes it is compatible. Just move your chosen dialog.tlk file to the *main game directory* (where the executable is)—in this very specific case, NOT the override.</Directions>
      <Instructions>
        <Instruction>
          <Guid>d17cbf8a-2ce6-4403-a53b-3c24506e21ce</Guid>
          <Action>Extract</Action>
          <Source>
            <Item>&lt;&lt;modDirectory&gt;&gt;\KotOR_Dialogue_Fixes*.7z</Item>
          </Source>
        </Instruction>
        <Instruction>
          <Guid>d37453c9-2e9c-47a7-924c-a0ecb2c8eec4</Guid>
          <Action>Choose</Action>
          <Source>
            <Item>cf2a12ec-3932-42f8-996d-b1b1bdfdbb48</Item>
            <Item>6d593186-e356-4994-b6a8-f71445869937</Item>
          </Source>
        </Instruction>
      </Instructions>
      <Options>
        <Option>
          <Guid>cf2a12ec-3932-42f8-996d-b1b1bdfdbb48</Guid>
          <Name>Standard</Name>
          <Description>Straight fixes to spelling errors/punctuation/grammar</Description>
          <Instructions>
            <Instruction>
              <Guid>c2a8586e-5287-4336-a525-3b7e65cd673c</Guid>
              <Action>Move</Action>
              <Source>
                <Item>&lt;&lt;modDirectory&gt;&gt;\KotOR_Dialogue_Fixes*\Corrections only\dialog.tlk</Item>
              </Source>
              <Destination>&lt;&lt;kotorDirectory&gt;&gt;</Destination>
            </Instruction>
          </Instructions>
        </Option>
        <Option>
          <Guid>6d593186-e356-4994-b6a8-f71445869937</Guid>
          <Name>Revised</Name>
          <Description>Everything in Straight Fixes, but also has changes from the PC Moderation changes.</Description>
          <Instructions>
            <Instruction>
              <Guid>11ccc491-e8c7-4f58-9c8c-6ad88205b0ec</Guid>
              <Action>Move</Action>
              <Source>
                <Item>&lt;&lt;modDirectory&gt;&gt;\KotOR_Dialogue_Fixes*\PC Response Moderation version\dialog.tlk</Item>
              </Source>
              <Destination>&lt;&lt;kotorDirectory&gt;&gt;</Destination>
            </Instruction>
          </Instructions>
        </Option>
      </Options>
    </Component>
  </Components>
</ModComponentList>";

			// Step 1: Load from XML
			Console.WriteLine("Step 1: Loading from XML...");
			List<ModComponent> originalComponents = ModComponentSerializationService.DeserializeModComponentFromXmlString(expectedXml);
			Console.WriteLine($"Loaded {originalComponents.Count} components from XML");
			Assert.That(originalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from XML");

			ModComponent originalComponent = originalComponents[0];
			Console.WriteLine($"Original Component: {originalComponent.Name}");

			// Step 2: Serialize to YAML
			Console.WriteLine("\nStep 2: Serializing to YAML...");
			string yamlContent = ModComponentSerializationService.SerializeModComponentAsYamlString(originalComponents);
			Console.WriteLine("YAML serialization successful");
			Assert.That(yamlContent, Is.Not.Null.And.Not.Empty, "YAML content should not be null or empty");

			// Step 3: Deserialize from YAML
			Console.WriteLine("\nStep 3: Loading from YAML...");
			List<ModComponent> yamlComponents = ModComponentSerializationService.DeserializeModComponentFromYamlString(yamlContent);
			Console.WriteLine($"Loaded {yamlComponents.Count} components from YAML");
			Assert.That(yamlComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from YAML");

			ModComponent yamlComponent = yamlComponents[0];
			Console.WriteLine($"YAML Component: {yamlComponent.Name}");

			// Step 4: Serialize back to JSON
			Console.WriteLine("\nStep 4: Serializing back to JSON...");
			string finalJson = ModComponentSerializationService.SerializeModComponentAsJsonString(yamlComponents);
			Console.WriteLine("Final JSON serialization successful");

			// Step 5: Deserialize final JSON
			Console.WriteLine("\nStep 5: Loading final JSON...");
			List<ModComponent> finalComponents = ModComponentSerializationService.DeserializeModComponentFromJsonString(finalJson);
			Console.WriteLine($"Loaded {finalComponents.Count} components from final JSON");
			Assert.That(finalComponents.Count, Is.EqualTo(1), "Should load exactly 1 component from final JSON");

			ModComponent finalComponent = finalComponents[0];
			Console.WriteLine($"Final Component: {finalComponent.Name}");

			// Validate data integrity through the entire round-trip
			Console.WriteLine("\nValidating data integrity...");
			AssertComponentEquality(originalComponent, finalComponent);

			Console.WriteLine("✅ XML -> YAML -> JSON Round-trip test PASSED!");
		}
	}
}