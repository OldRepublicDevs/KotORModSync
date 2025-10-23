// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Text;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;
using Tomlyn;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class TomlFileTests
	{
		[SetUp]
		public void SetUp()
		{

			_filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".toml");

			File.WriteAllText(_filePath, _exampleToml);
		}

		[TearDown]
		public void TearDown()
		{

			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " != null");
			File.Delete(_filePath);
		}

		private string _filePath = string.Empty;

		private readonly string _exampleToml = @"[[thisMod]]
name = ""Ultimate Dantooine""
guid = ""{B3525945-BDBD-45D8-A324-AAF328A5E13E}""
dependencies = [
    ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}"",
    ""{D0F371DA-5C69-4A26-8A37-76E3A6A2A50D}""
]
installOrder = 3

[[thisMod.instructions]]
action = ""extract""
source = ""Ultimate Dantooine High Resolution - TPC Version-1103-2-1-1670680013.rar""
destination = ""%temp%\\mod_files\\Dantooine HR""
overwrite = true

[[thisMod.instructions]]
action = ""delete""
paths = [
    ""%temp%\\mod_files\\Dantooine HR\\DAN_wall03.tpc"",
    ""%temp%\\mod_files\\Dantooine HR\\DAN_NEW1.tpc"",
    ""%temp%\\mod_files\\Dantooine HR\\DAN_MWFl.tpc""
]

[[thisMod.instructions]]
action = ""move""
source = ""%temp%\\mod_files\\Dantooine HR\\""
destination = ""%temp%\\Override""

[[thisMod]]
name = ""TSLRCM Tweak Pack""
guid = ""{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}""
installOrder = 1
dependencies = []

[[thisMod.instructions]]
action = ""extract""
source = ""URCMTP 1.3.rar""
destination = ""%temp%\\mod_files\\TSLRCM Tweak Pack""
overwrite = true

[[thisMod.instructions]]
action = ""run""
path = ""%temp%\\mod_files\\TSLPatcher.exe""";

		[Test]
		public void SaveAndLoadTOMLFile_MatchingComponents()
		{

			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " is null");
			string tomlContents = File.ReadAllText(_filePath);

			tomlContents = Serializer.FixWhitespaceIssues(tomlContents);

			string modifiedFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".toml");
			File.WriteAllText(modifiedFilePath, tomlContents);

			List<ModComponent> originalComponents = FileLoadingService.LoadFromFile(modifiedFilePath);

			FileLoadingService.SaveToFile(originalComponents, modifiedFilePath);

			List<ModComponent> loadedComponents = FileLoadingService.LoadFromFile(modifiedFilePath);

			Assert.That(loadedComponents, Has.Count.EqualTo(originalComponents.Count));

			for ( int i = 0; i < originalComponents.Count; i++ )
			{
				ModComponent originalComponent = originalComponents[i];
				ModComponent loadedComponent = loadedComponents[i];

				AssertComponentEquality(loadedComponent, originalComponent);
			}
		}

		[Test]
		public void SaveAndLoad_DefaultComponent()
		{

			ModComponent newComponent = ModComponent.DeserializeTomlComponent(_exampleToml)
				?? throw new InvalidOperationException();
			newComponent.Guid = Guid.NewGuid();
			newComponent.Name = "test_mod_" + Path.GetRandomFileName();

			string tomlString = newComponent.SerializeComponent();

			ModComponent duplicateComponent = ModComponent.DeserializeTomlComponent(tomlString)
				?? throw new InvalidOperationException();

			AssertComponentEquality(newComponent, duplicateComponent);
		}

		[Test]
		[Ignore("not sure if I want to support")]
		public void SaveAndLoadTOMLFile_CaseInsensitive()
		{

			List<ModComponent> originalComponents = FileLoadingService.LoadFromFile(_filePath);

			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " != null");
			string tomlContents = File.ReadAllText(_filePath);

			tomlContents = ConvertFieldNamesAndValuesToMixedCase(tomlContents);

			string modifiedFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".toml");
			File.WriteAllText(modifiedFilePath, tomlContents);

			List<ModComponent> loadedComponents = FileLoadingService.LoadFromFile(modifiedFilePath);

			Assert.That(loadedComponents, Has.Count.EqualTo(originalComponents.Count));

			for ( int i = 0; i < originalComponents.Count; i++ )
			{
				ModComponent originalComponent = originalComponents[i];
				ModComponent loadedComponent = loadedComponents[i];

				AssertComponentEquality(originalComponent, loadedComponent);
			}
		}

		[Test]
		public void SaveAndLoadTOMLFile_WhitespaceTests()
		{

			List<ModComponent> originalComponents = FileLoadingService.LoadFromFile(_filePath);

			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " != null");
			string tomlContents = File.ReadAllText(_filePath);

			tomlContents = "    \r\n\t   \r\n\r\n\r\n" + tomlContents + "    \r\n\t   \r\n\r\n\r\n";

			string modifiedFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".toml");
			File.WriteAllText(modifiedFilePath, tomlContents);

			List<ModComponent> loadedComponents = FileLoadingService.LoadFromFile(modifiedFilePath);

			Assert.That(loadedComponents, Has.Count.EqualTo(originalComponents.Count));

			for ( int i = 0; i < originalComponents.Count; i++ )
			{
				ModComponent originalComponent = originalComponents[i];
				ModComponent loadedComponent = loadedComponents[i];

				AssertComponentEquality(originalComponent, loadedComponent);
			}
		}

		private static string ConvertFieldNamesAndValuesToMixedCase(string tomlContents)
		{
			var convertedContents = new StringBuilder();
			var random = new Random();

			bool isFieldName = true;

			foreach ( char c in tomlContents )
			{
				char convertedChar = c;

				if ( isFieldName )
				{
					if ( char.IsLetter(c) )
					{

						convertedChar = random.Next(2) == 0
							? char.ToUpper(c)
							: char.ToLower(c);
					}
					else if ( c == ']' )
					{
						isFieldName = false;
					}
				}
				else
				{
					if ( char.IsLetter(c) )
					{

						convertedChar = random.Next(2) == 0
							? char.ToUpper(c)
							: char.ToLower(c);
					}
					else if ( c == '[' )
					{
						isFieldName = true;
					}
				}

				_ = convertedContents.Append(convertedChar);
			}

			return convertedContents.ToString();
		}

		[Test]
		public void SaveAndLoadTOMLFile_EmptyComponentsList()
		{

			List<ModComponent> originalComponents = [];

			FileLoadingService.SaveToFile(originalComponents, _filePath);

			try
			{
				List<ModComponent> loadedComponents = FileLoadingService.LoadFromFile(_filePath);

				Assert.That(loadedComponents, Is.Null.Or.Empty);
			}
			catch ( InvalidDataException ) { }
		}

		[Test]
		public void SaveAndLoadTOMLFile_DuplicateGuids()
		{

			List<ModComponent> originalComponents =
			[
				new ModComponent
				{
					Name = "ModComponent 1", Guid = Guid.Parse("{B3525945-BDBD-45D8-A324-AAF328A5E13E}"),
				},
				new ModComponent
				{
					Name = "ModComponent 2", Guid = Guid.Parse("{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}"),
				},
				new ModComponent
				{
					Name = "ModComponent 3", Guid = Guid.Parse("{B3525945-BDBD-45D8-A324-AAF328A5E13E}"),
				},
			];

			FileLoadingService.SaveToFile(originalComponents, _filePath);
			List<ModComponent> loadedComponents = FileLoadingService.LoadFromFile(_filePath);

			Assert.That(loadedComponents, Has.Count.EqualTo(originalComponents.Count));

			for ( int i = 0; i < originalComponents.Count; i++ )
			{
				ModComponent originalComponent = originalComponents[i];
				ModComponent loadedComponent = loadedComponents[i];

				AssertComponentEquality(originalComponent, loadedComponent);
			}
		}

		[Test]
		public void SaveAndLoadTOMLFile_ModifyComponents()
		{

			List<ModComponent> originalComponents = FileLoadingService.LoadFromFile(_filePath);

			originalComponents[0].Name = "Modified Name";

			FileLoadingService.SaveToFile(originalComponents, _filePath);
			List<ModComponent> loadedComponents = FileLoadingService.LoadFromFile(_filePath);

			Assert.That(loadedComponents, Has.Count.EqualTo(originalComponents.Count));

			for ( int i = 0; i < originalComponents.Count; i++ )
			{
				ModComponent originalComponent = originalComponents[i];
				ModComponent loadedComponent = loadedComponents[i];

				AssertComponentEquality(loadedComponent, originalComponent);
			}
		}

		[Test]
		public void SaveAndLoadTOMLFile_MultipleRounds()
		{

			List<List<ModComponent>> rounds =
			[
				[
					new ModComponent
					{
						Name = "ModComponent 1", Guid = Guid.Parse("{B3525945-BDBD-45D8-A324-AAF328A5E13E}"),
					},
					new ModComponent
					{
						Name = "ModComponent 2", Guid = Guid.Parse("{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}"),
					},
				],
				[
					new ModComponent
					{
						Name = "ModComponent 3", Guid = Guid.Parse("{D0F371DA-5C69-4A26-8A37-76E3A6A2A50D}"),
					},
					new ModComponent
					{
						Name = "ModComponent 4", Guid = Guid.Parse("{E7B27A19-9A81-4A20-B062-7D00F2603D5C}"),
					},
					new ModComponent
					{
						Name = "ModComponent 5", Guid = Guid.Parse("{F1B05F5D-3C06-4B64-8E39-8BEC8D22BB0A}"),
					},
				],
				[
					new ModComponent
					{
						Name = "ModComponent 6", Guid = Guid.Parse("{EF04A28E-5031-4A95-A85A-9A1B29A31710}"),
					},
					new ModComponent
					{
						Name = "ModComponent 7", Guid = Guid.Parse("{B0373F49-ED5A-43A1-91E0-5CEB85659282}"),
					},
					new ModComponent
					{
						Name = "ModComponent 8", Guid = Guid.Parse("{BBDB9C8D-DA44-4859-A641-0364D6F34D12}"),
					},
					new ModComponent
					{
						Name = "ModComponent 9", Guid = Guid.Parse("{D6B5C60F-26A7-4595-A0E2-2DE567A376DE}"),
					},
				],
			];

			foreach ( List<ModComponent> components in rounds )
			{
				FileLoadingService.SaveToFile(components, _filePath);
				List<ModComponent> loadedComponents = FileLoadingService.LoadFromFile(_filePath);

				Assert.That(loadedComponents, Has.Count.EqualTo(components.Count));

				for ( int i = 0; i < components.Count; i++ )
				{
					ModComponent originalComponent = components[i];
					ModComponent loadedComponent = loadedComponents[i];

					AssertComponentEquality(originalComponent, loadedComponent);
				}
			}
		}

		[Test]
		public void TomlWriteStringTest()
		{

			var innerDictionary1 = new Dictionary<string, object>
			{
				{
					"name", "John"
				},
				{
					"age", 30
				},

			};

			var innerDictionary2 = new Dictionary<string, object>
			{
				{
					"name", "Alice"
				},
				{
					"age", 25
				},

			};

			var rootTable = new Dictionary<string, object>
			{
				{
					"thisMod", new List<object>
					{
						innerDictionary1, innerDictionary2,

					}
				},
			};

			Logger.Log(TomlWriter.WriteString(rootTable));
			Logger.Log(Toml.FromModel(rootTable));
		}

		[Test]
		public void Instruction_ConditionalSerialization_OnlyRelevantFieldsAreIncluded()
		{

			var extractComponent = new ModComponent
			{
				Name = "Extract Test",
				Guid = Guid.NewGuid(),
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
			{
				new Instruction
				{
					Action = Instruction.ActionType.Extract,
					Source = new List<string> { "test.rar" },
					Overwrite = true,
					Destination = "some/path",
					Arguments = "some args"
				}
			}
			};
			string extractToml = extractComponent.SerializeComponent();
			Assert.Multiple(() =>
			{
				Assert.That(extractToml.Contains("Overwrite"), Is.False, "Extract should not serialize Overwrite");
				Assert.That(extractToml.Contains("Destination"), Is.False, "Extract should not serialize Destination");
				Assert.That(extractToml.Contains("Arguments"), Is.False, "Extract should not serialize Arguments");
			});

			var moveComponent = new ModComponent
			{
				Name = "Move Test",
				Guid = Guid.NewGuid(),
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
			{
				new Instruction
				{
					Action = Instruction.ActionType.Move,
					Source = new List<string> { "test.txt" },
					Destination = "<<kotorDirectory>>\\Override",
					Overwrite = true,
					Arguments = "should not appear"
				}
			}
			};
			string moveToml = moveComponent.SerializeComponent();
			Assert.Multiple(() =>
			{
				Assert.That(moveToml, Does.Contain("Overwrite"), "Move should serialize Overwrite");
				Assert.That(moveToml, Does.Contain("Destination"), "Move should serialize Destination");
				Assert.That(moveToml, Does.Not.Contain("Arguments"), "Move should not serialize Arguments");
			});

			var patcherComponent = new ModComponent
			{
				Name = "Patcher Test",
				Guid = Guid.NewGuid(),
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
			{
				new Instruction
				{
					Action = Instruction.ActionType.Patcher,
					Source = new List<string> { "tslpatchdata" },
					Destination = "<<kotorDirectory>>",
					Arguments = "0",
					Overwrite = true
				}
			}
			};
			string patcherToml = patcherComponent.SerializeComponent();
			Assert.Multiple(() =>
			{
				Assert.That(patcherToml, Does.Not.Contain("Overwrite"), "Patcher should not serialize Overwrite");
				Assert.That(patcherToml, Does.Contain("Destination"), "Patcher should serialize Destination");
				Assert.That(patcherToml, Does.Contain("Arguments"), "Patcher should serialize Arguments");
			});

			var executeComponent = new ModComponent
			{
				Name = "Execute Test",
				Guid = Guid.NewGuid(),
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
			{
				new Instruction
				{
					Action = Instruction.ActionType.Execute,
					Source = new List<string> { "setup.exe" },
					Arguments = "/silent",
					Overwrite = true,
					Destination = "some/path"
				}
			}
			};
			string executeToml = executeComponent.SerializeComponent();
			Assert.Multiple(() =>
			{
				Assert.That(executeToml, Does.Not.Contain("Overwrite"), "Execute should not serialize Overwrite");
				Assert.That(executeToml, Does.Not.Contain("Destination"), "Execute should not serialize Destination");
				Assert.That(executeToml, Does.Contain("Arguments"), "Execute should serialize Arguments");
			});
		}

		[Test]
		public void ModComponent_RuntimeFields_AreNotSerialized()
		{

			var component = new ModComponent
			{
				Name = "Test Mod",
				Guid = Guid.NewGuid(),
				IsDownloaded = true,
				InstallState = ModComponent.ComponentInstallState.Completed,
				IsSelected = true
			};

			string tomlString = component.SerializeComponent();
			Assert.Multiple(() =>
			{
				Assert.That(tomlString, Does.Not.Contain("IsDownloaded"), "TOML should not contain IsDownloaded");
				Assert.That(tomlString, Does.Not.Contain("InstallState"), "TOML should not contain InstallState");
				Assert.That(tomlString, Does.Not.Contain("LastStartedUtc"), "TOML should not contain LastStartedUtc");
				Assert.That(tomlString, Does.Not.Contain("LastCompletedUtc"), "TOML should not contain LastCompletedUtc");

				Assert.That(tomlString, Does.Contain("IsSelected"), "TOML should contain IsSelected");
			});
		}

		[Test]
		public void SaveAndLoadTOMLFile_ComplexComponentWithOptionsAndInstructions()
		{
			// Test the complex scenario with ModLinkFilenames, Options, and Instructions
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

			// Write the expected TOML to a temporary file
			string tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".toml");
			File.WriteAllText(tempFilePath, expectedToml);

			try
			{
				// Load the components from the TOML
				List<ModComponent> loadedComponents = FileLoadingService.LoadFromFile(tempFilePath);

				// Verify we loaded exactly one component
				Assert.That(loadedComponents, Has.Count.EqualTo(1));

				ModComponent component = loadedComponents[0];

				// Verify basic properties
				Assert.That(component.Guid.ToString(), Is.EqualTo("987a0d17-c596-49af-ba28-851232455253"));
				Assert.That(component.Name, Is.EqualTo("KOTOR Dialogue Fixes"));
				Assert.That(component.Author, Is.EqualTo("Salk & Kainzorus Prime"));
				Assert.That(component.Tier, Is.EqualTo("1 - Essential"));
				Assert.That(component.IsSelected, Is.True);

				// Verify ModLinkFilenames
				Assert.That(component.ModLinkFilenames, Is.Not.Null);
				Assert.That(component.ModLinkFilenames.Count, Is.EqualTo(1));
				Assert.That(component.ModLinkFilenames.ContainsKey("https://deadlystream.com/files/file/1313-kotor-dialogue-fixes/"), Is.True);

				// Verify Instructions
				Assert.That(component.Instructions, Has.Count.EqualTo(2));

				var extractInstruction = component.Instructions.FirstOrDefault(i => i.Guid.ToString() == "e6d0dbb7-75f7-4886-a4a5-e7eea85dac1c");
				Assert.That(extractInstruction, Is.Not.Null);
				Assert.That(extractInstruction.Action, Is.EqualTo(Instruction.ActionType.Extract));
				Assert.That(extractInstruction.Source, Contains.Item("<<modDirectory>>\\KotOR_Dialogue_Fixes*.7z"));

				var chooseInstruction = component.Instructions.FirstOrDefault(i => i.Guid.ToString() == "b201d6e8-3d07-4de5-a937-47ba9952afac");
				Assert.That(chooseInstruction, Is.Not.Null);
				Assert.That(chooseInstruction.Action, Is.EqualTo(Instruction.ActionType.Choose));
				Assert.That(chooseInstruction.Source, Has.Count.EqualTo(2));
				Assert.That(chooseInstruction.Source, Contains.Item("cf2a12ec-3932-42f8-996d-b1b1bdfdbb48"));
				Assert.That(chooseInstruction.Source, Contains.Item("6d593186-e356-4994-b6a8-f71445869937"));

				// Verify Options
				Assert.That(component.Options, Has.Count.EqualTo(2));

				var standardOption = component.Options.FirstOrDefault(o => o.Guid.ToString() == "cf2a12ec-3932-42f8-996d-b1b1bdfdbb48");
				Assert.That(standardOption, Is.Not.Null);
				Assert.That(standardOption.Name, Is.EqualTo("Standard"));
				Assert.That(standardOption.Description, Is.EqualTo("Straight fixes to spelling errors/punctuation/grammar"));
				Assert.That(standardOption.Restrictions, Contains.Item(Guid.Parse("6d593186-e356-4994-b6a8-f71445869937")));

				var revisedOption = component.Options.FirstOrDefault(o => o.Guid.ToString() == "6d593186-e356-4994-b6a8-f71445869937");
				Assert.That(revisedOption, Is.Not.Null);
				Assert.That(revisedOption.Name, Is.EqualTo("Revised"));
				Assert.That(revisedOption.Description, Is.EqualTo("Everything in Straight Fixes, but also has changes from the PC Moderation changes."));
				Assert.That(revisedOption.IsSelected, Is.True);
				Assert.That(revisedOption.Restrictions, Contains.Item(Guid.Parse("cf2a12ec-3932-42f8-996d-b1b1bdfdbb48")));

				// Verify Option Instructions
				Assert.That(standardOption.Instructions, Has.Count.EqualTo(1));
				var standardInstruction = standardOption.Instructions[0];
				Assert.That(standardInstruction.Guid.ToString(), Is.EqualTo("9521423e-e617-474c-bcbb-a15563a516fc"));
				Assert.That(standardInstruction.Action, Is.EqualTo(Instruction.ActionType.Move));
				Assert.That(standardInstruction.Destination, Is.EqualTo("<<kotorDirectory>>"));
				Assert.That(standardInstruction.Source, Contains.Item("<<modDirectory>>\\KotOR_Dialogue_Fixes*\\Corrections only\\dialog.tlk"));

				Assert.That(revisedOption.Instructions, Has.Count.EqualTo(1));
				var revisedInstruction = revisedOption.Instructions[0];
				Assert.That(revisedInstruction.Guid.ToString(), Is.EqualTo("80fba038-4a24-4716-a0cc-1d4051e952a0"));
				Assert.That(revisedInstruction.Action, Is.EqualTo(Instruction.ActionType.Move));
				Assert.That(revisedInstruction.Destination, Is.EqualTo("<<kotorDirectory>>"));
				Assert.That(revisedInstruction.Source, Contains.Item("<<modDirectory>>\\KotOR_Dialogue_Fixes*\\PC Response Moderation version\\dialog.tlk"));

				// Now test round-trip: save and reload
				FileLoadingService.SaveToFile(loadedComponents, tempFilePath);
				List<ModComponent> reloadedComponents = FileLoadingService.LoadFromFile(tempFilePath);

				// Verify round-trip worked
				Assert.That(reloadedComponents, Has.Count.EqualTo(1));
				AssertComponentEquality(loadedComponents[0], reloadedComponents[0]);
			}
			finally
			{
				// Clean up
				if (File.Exists(tempFilePath))
					File.Delete(tempFilePath);
			}
		}

		private static void AssertComponentEquality(object? obj, object? another)
		{
			if ( ReferenceEquals(obj, another) )
				return;
			if ( obj is null || another is null )
				return;
			if ( obj.GetType() != another.GetType() )
				return;

			if ( obj is ModComponent comp1 && another is ModComponent comp2 )
			{

				string json1 = JsonConvert.SerializeObject(comp1);
				string json2 = JsonConvert.SerializeObject(comp2);

				ModComponent copy1 = JsonConvert.DeserializeObject<ModComponent>(json1)!;
				ModComponent copy2 = JsonConvert.DeserializeObject<ModComponent>(json2)!;

				var fixedGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
				foreach ( Instruction instruction in copy1.Instructions )
				{
					instruction.Guid = fixedGuid;
				}
				foreach ( Instruction instruction in copy2.Instructions )
				{
					instruction.Guid = fixedGuid;
				}

				string normalizedJson1 = JsonConvert.SerializeObject(copy1);
				string normalizedJson2 = JsonConvert.SerializeObject(copy2);

				Assert.That(normalizedJson1, Is.EqualTo(normalizedJson2));
			}
			else
			{
				string objJson = JsonConvert.SerializeObject(obj);
				string anotherJson = JsonConvert.SerializeObject(another);

				Assert.That(objJson, Is.EqualTo(anotherJson));
			}
		}
	}
}
