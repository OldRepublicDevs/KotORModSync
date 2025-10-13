// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Text;
using KOTORModSync.Core;
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
			// Create a temporary file for testing
			_filePath = Path.GetTempFileName();

			// Write example TOMLIN content to the file
			File.WriteAllText(_filePath, _exampleToml);
		}

		[TearDown]
		public void TearDown()
		{
			// Delete the temporary file
			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " != null");
			File.Delete(_filePath);
		}

		private string _filePath = string.Empty;

		// ReSharper disable once ConvertToConstant.Local
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
			// Read the original TOMLIN file contents
			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " is null");
			string tomlContents = File.ReadAllText(_filePath);

			// Fix whitespace issues
			tomlContents = Serializer.FixWhitespaceIssues(tomlContents);

			// Save the modified TOMLIN file
			string modifiedFilePath = Path.GetTempFileName();
			File.WriteAllText(modifiedFilePath, tomlContents);

			// Arrange
			List<ModComponent> originalComponents = ModComponent.ReadComponentsFromFile(modifiedFilePath);

			// Act
			ModComponent.OutputConfigFile(originalComponents, modifiedFilePath);

			// Reload the modified TOMLIN file
			List<ModComponent> loadedComponents = ModComponent.ReadComponentsFromFile(modifiedFilePath);

			// Assert
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
			// Deserialize default component
			ModComponent newComponent = ModComponent.DeserializeTomlComponent(_exampleToml)
				?? throw new InvalidOperationException();
			newComponent.Guid = Guid.NewGuid();
			newComponent.Name = "test_mod_" + Path.GetRandomFileName();

			// Serialize
			string tomlString = newComponent.SerializeComponent();

			// Deserialize into new instance
			ModComponent duplicateComponent = ModComponent.DeserializeTomlComponent(tomlString)
				?? throw new InvalidOperationException();

			// Compare
			AssertComponentEquality(newComponent, duplicateComponent);
		}

		[Test]
		[Ignore("not sure if I want to support")]
		public void SaveAndLoadTOMLFile_CaseInsensitive()
		{
			// Arrange
			List<ModComponent> originalComponents = ModComponent.ReadComponentsFromFile(_filePath);

			// Modify the TOML file contents
			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " != null");
			string tomlContents = File.ReadAllText(_filePath);

			// Convert field names and values to mixed case
			tomlContents = ConvertFieldNamesAndValuesToMixedCase(tomlContents);

			// Act
			string modifiedFilePath = Path.GetTempFileName();
			File.WriteAllText(modifiedFilePath, tomlContents);

			List<ModComponent> loadedComponents = ModComponent.ReadComponentsFromFile(modifiedFilePath);

			// Assert
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
			// Arrange
			List<ModComponent> originalComponents = ModComponent.ReadComponentsFromFile(_filePath);

			// Modify the TOMLIN file contents
			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " != null");
			string tomlContents = File.ReadAllText(_filePath);

			// Add mixed line endings and extra whitespaces
			tomlContents = "    \r\n\t   \r\n\r\n\r\n" + tomlContents + "    \r\n\t   \r\n\r\n\r\n";

			// Save the modified TOMLIN file
			string modifiedFilePath = Path.GetTempFileName();
			File.WriteAllText(modifiedFilePath, tomlContents);

			// Act
			List<ModComponent> loadedComponents = ModComponent.ReadComponentsFromFile(modifiedFilePath);

			// Assert
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

			bool isFieldName = true; // Flag to determine if the current item is a field name or field value

			foreach ( char c in tomlContents )
			{
				char convertedChar = c;

				if ( isFieldName )
				{
					if ( char.IsLetter(c) )
					{
						// Convert field name character to mixed case
						convertedChar = random.Next(2) == 0
							? char.ToUpper(c)
							: char.ToLower(c);
					}
					else if ( c == ']' )
					{
						isFieldName = false; // Switch to field value mode after closing bracket
					}
				}
				else
				{
					if ( char.IsLetter(c) )
					{
						// Convert field value character to mixed case
						convertedChar = random.Next(2) == 0
							? char.ToUpper(c)
							: char.ToLower(c);
					}
					else if ( c == '[' )
					{
						isFieldName = true; // Switch to field name mode after opening bracket
					}
				}

				_ = convertedContents.Append(convertedChar);
			}

			return convertedContents.ToString();
		}

		[Test]
		public void SaveAndLoadTOMLFile_EmptyComponentsList()
		{
			// Arrange
			// ReSharper disable once CollectionNeverUpdated.Local
			List<ModComponent> originalComponents = [];
			// Act
			ModComponent.OutputConfigFile(originalComponents, _filePath);

			try
			{
				List<ModComponent> loadedComponents = ModComponent.ReadComponentsFromFile(_filePath);

				// Assert
				Assert.That(loadedComponents, Is.Null.Or.Empty);
			}
			catch ( InvalidDataException ) { }
		}

		[Test]
		public void SaveAndLoadTOMLFile_DuplicateGuids()
		{
			// Arrange
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

			// Act
			ModComponent.OutputConfigFile(originalComponents, _filePath);
			List<ModComponent> loadedComponents = ModComponent.ReadComponentsFromFile(_filePath);

			// Assert
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
			// Arrange
			List<ModComponent> originalComponents = ModComponent.ReadComponentsFromFile(_filePath);

			// Modify some component properties
			originalComponents[0].Name = "Modified Name";

			// Act
			ModComponent.OutputConfigFile(originalComponents, _filePath);
			List<ModComponent> loadedComponents = ModComponent.ReadComponentsFromFile(_filePath);

			// Assert
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
			// Arrange
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
			// Act and Assert
			foreach ( List<ModComponent> components in rounds )
			{
				ModComponent.OutputConfigFile(components, _filePath);
				List<ModComponent> loadedComponents = ModComponent.ReadComponentsFromFile(_filePath);

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
			// Sample nested Dictionary representing TOML data
			var innerDictionary1 = new Dictionary<string, object>
			{
				{
					"name", "John"
				},
				{
					"age", 30
				},
				// other key-value pairs for the first table
			};

			var innerDictionary2 = new Dictionary<string, object>
			{
				{
					"name", "Alice"
				},
				{
					"age", 25
				},
				// other key-value pairs for the second table
			};

			var rootTable = new Dictionary<string, object>
			{
				{
					"thisMod", new List<object>
					{
						innerDictionary1, innerDictionary2,
						// additional dictionaries in the list
					}
				},
			};

			Logger.Log(TomlWriter.WriteString(rootTable));
			Logger.Log(Toml.FromModel(rootTable));
		}

		[Test]
		public void Instruction_ConditionalSerialization_OnlyRelevantFieldsAreIncluded()
		{
			// Test Extract action - should NOT have Overwrite, Destination, or Arguments
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
					Overwrite = true, // Should NOT be serialized
					Destination = "some/path", // Should NOT be serialized
					Arguments = "some args" // Should NOT be serialized
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

			// Test Move action - SHOULD have Overwrite and Destination, but NOT Arguments
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

			// Test Patcher action - SHOULD have Destination and Arguments, but NOT Overwrite
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
					Overwrite = true // Should NOT be serialized
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

			// Test Execute action - SHOULD have Arguments, but NOT Overwrite or Destination
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
					Overwrite = true, // Should NOT be serialized
					Destination = "some/path" // Should NOT be serialized
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
			// Create a component with runtime state
			var component = new ModComponent
			{
				Name = "Test Mod",
				Guid = Guid.NewGuid(),
				IsDownloaded = true, // Runtime state - should NOT be serialized
				InstallState = ModComponent.ComponentInstallState.Completed, // Runtime state - should NOT be serialized
				IsSelected = true
			};

			// Verify it serializes correctly
			string tomlString = component.SerializeComponent();
			Assert.Multiple(() =>
			{
				Assert.That(tomlString, Does.Not.Contain("IsDownloaded"), "TOML should not contain IsDownloaded");
				Assert.That(tomlString, Does.Not.Contain("InstallState"), "TOML should not contain InstallState");
				Assert.That(tomlString, Does.Not.Contain("LastStartedUtc"), "TOML should not contain LastStartedUtc");
				Assert.That(tomlString, Does.Not.Contain("LastCompletedUtc"), "TOML should not contain LastCompletedUtc");
				// IsSelected should be included (it's part of the TOML definition)
				Assert.That(tomlString, Does.Contain("IsSelected"), "TOML should contain IsSelected");
			});
		}

		private static void AssertComponentEquality(object? obj, object? another)
		{
			if ( ReferenceEquals(obj, another) )
				return;
			if ( obj is null || another is null )
				return;
			if ( obj.GetType() != another.GetType() )
				return;

			// If comparing Components, normalize instruction GUIDs before comparing
			if ( obj is ModComponent comp1 && another is ModComponent comp2 )
			{
				// Create deep copies to avoid modifying the originals
				string json1 = JsonConvert.SerializeObject(comp1);
				string json2 = JsonConvert.SerializeObject(comp2);

				ModComponent copy1 = JsonConvert.DeserializeObject<ModComponent>(json1)!;
				ModComponent copy2 = JsonConvert.DeserializeObject<ModComponent>(json2)!;

				// Normalize instruction GUIDs - set ALL instruction GUIDs to a fixed value
				// We can't use Guid.Empty because the Instruction.Guid property auto-generates a new GUID when accessed if empty
				var fixedGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
				foreach ( Instruction instruction in copy1.Instructions )
				{
					instruction.Guid = fixedGuid;
				}
				foreach ( Instruction instruction in copy2.Instructions )
				{
					instruction.Guid = fixedGuid;
				}

				// Now compare the normalized copies
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
