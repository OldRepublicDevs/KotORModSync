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
