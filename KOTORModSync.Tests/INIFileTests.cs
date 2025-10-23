// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Text;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Utility;
using Newtonsoft.Json;

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class IniFileTests
	{
		[SetUp]
		public void SetUp()
		{
			_filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ini");
			File.WriteAllText(_filePath, _exampleIni);
		}

		[TearDown]
		public void TearDown()
		{
			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " != null");
			if ( File.Exists(_filePath) )
				File.Delete(_filePath);
		}

		private string _filePath = string.Empty;

		private readonly string _exampleIni = @"; KOTORModSync Configuration File
[Component1]
Name=Ultimate Dantooine
Guid={B3525945-BDBD-45D8-A324-AAF328A5E13E}
Dependencies={C5418549-6B7E-4A8C-8B8E-4AA1BC63C732},{D0F371DA-5C69-4A26-8A37-76E3A6A2A50D}
InstallOrder=3

[Component1.Instruction1]
Action=extract
Source=Ultimate Dantooine High Resolution - TPC Version-1103-2-1-1670680013.rar
Destination=%temp%\mod_files\Dantooine HR
Overwrite=true

[Component1.Instruction2]
Action=delete
Paths=%temp%\mod_files\Dantooine HR\DAN_wall03.tpc,%temp%\mod_files\Dantooine HR\DAN_NEW1.tpc,%temp%\mod_files\Dantooine HR\DAN_MWFl.tpc

[Component1.Instruction3]
Action=move
Source=%temp%\mod_files\Dantooine HR\
Destination=%temp%\Override

[Component2]
Name=TSLRCM Tweak Pack
Guid={C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}
InstallOrder=1
Dependencies=

[Component2.Instruction1]
Action=extract
Source=URCMTP 1.3.rar
Destination=%temp%\mod_files\TSLRCM Tweak Pack
Overwrite=true

[Component2.Instruction2]
Action=run
Path=%temp%\mod_files\TSLPatcher.exe
";

		[Test]
		public void SaveAndLoadINIFile_MatchingComponents()
		{
			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " is null");

			List<ModComponent> originalComponents = FileLoadingService.LoadFromFile(_filePath);

			string modifiedFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ini");
			FileLoadingService.SaveToFile(originalComponents, modifiedFilePath);

			List<ModComponent> loadedComponents = FileLoadingService.LoadFromFile(modifiedFilePath);

			Assert.That(loadedComponents, Has.Count.EqualTo(originalComponents.Count));

			for ( int i = 0; i < originalComponents.Count; i++ )
			{
				ModComponent originalComponent = originalComponents[i];
				ModComponent loadedComponent = loadedComponents[i];

				AssertComponentEquality(loadedComponent, originalComponent);
			}

			if ( File.Exists(modifiedFilePath) )
				File.Delete(modifiedFilePath);
		}

		/*[Test]
		[Ignore("INI serialization methods not implemented")]
		public void SaveAndLoad_DefaultComponent()
		{
			var newComponent = new ModComponent
			{
				Name = "Test Component",
				Guid = Guid.NewGuid(),
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
				{
					new Instruction
					{
						Action = Instruction.ActionType.Extract,
						Source = new List<string> { "test.rar" },
						Destination = "%temp%\\test"
					}
				}
			};

			string iniString = ModComponentSerializationService.SerializeModComponentAsIniString([newComponent]);
			ModComponent duplicateComponent = ModComponentSerializationService.DeserializeModComponentFromIniString(iniString)[0];

			Assert.That(duplicateComponent, Is.Not.Null);
			AssertComponentEquality(newComponent, duplicateComponent);
		}*/

		[Test]
		public void SaveAndLoadINIFile_WhitespaceTests()
		{
			List<ModComponent> originalComponents = FileLoadingService.LoadFromFile(_filePath);

			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " != null");
			string iniContents = File.ReadAllText(_filePath);

			iniContents = "    \r\n\t   \r\n\r\n\r\n" + iniContents + "    \r\n\t   \r\n\r\n\r\n";

			string modifiedFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ini");
			File.WriteAllText(modifiedFilePath, iniContents);

			List<ModComponent> loadedComponents = FileLoadingService.LoadFromFile(modifiedFilePath);

			Assert.That(loadedComponents, Has.Count.EqualTo(originalComponents.Count));

			for ( int i = 0; i < originalComponents.Count; i++ )
			{
				ModComponent originalComponent = originalComponents[i];
				ModComponent loadedComponent = loadedComponents[i];

				AssertComponentEquality(originalComponent, loadedComponent);
			}

			if ( File.Exists(modifiedFilePath) )
				File.Delete(modifiedFilePath);
		}

		[Test]
		public void SaveAndLoadINIFile_EmptyComponentsList()
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
		public void SaveAndLoadINIFile_DuplicateGuids()
		{
			List<ModComponent> originalComponents =
			[
				new ModComponent
				{
					Name = "ModComponent 1",
					Guid = Guid.Parse("{B3525945-BDBD-45D8-A324-AAF328A5E13E}"),
				},
				new ModComponent
				{
					Name = "ModComponent 2",
					Guid = Guid.Parse("{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}"),
				},
				new ModComponent
				{
					Name = "ModComponent 3",
					Guid = Guid.Parse("{B3525945-BDBD-45D8-A324-AAF328A5E13E}"),
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
		public void SaveAndLoadINIFile_ModifyComponents()
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
		public void SaveAndLoadINIFile_MultipleRounds()
		{
			List<List<ModComponent>> rounds =
			[
				[
					new ModComponent
					{
						Name = "ModComponent 1",
						Guid = Guid.Parse("{B3525945-BDBD-45D8-A324-AAF328A5E13E}"),
					},
					new ModComponent
					{
						Name = "ModComponent 2",
						Guid = Guid.Parse("{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}"),
					},
				],
				[
					new ModComponent
					{
						Name = "ModComponent 3",
						Guid = Guid.Parse("{D0F371DA-5C69-4A26-8A37-76E3A6A2A50D}"),
					},
					new ModComponent
					{
						Name = "ModComponent 4",
						Guid = Guid.Parse("{E7B27A19-9A81-4A20-B062-7D00F2603D5C}"),
					},
					new ModComponent
					{
						Name = "ModComponent 5",
						Guid = Guid.Parse("{F1B05F5D-3C06-4B64-8E39-8BEC8D22BB0A}"),
					},
				]
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

		/*[Test]
		[Ignore("INI serialization methods not implemented")]
		public void INIFile_CommentsAndSectionsFormat()
		{
			var component = new ModComponent
			{
				Name = "Test Component",
				Guid = Guid.NewGuid(),
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>
				{
					new Instruction
					{
						Action = Instruction.ActionType.Extract,
						Source = new List<string> { "test.rar" }
					}
				}
			};

			string iniString = ModComponentSerializationService.SerializeModComponentAsIniString([component]);

			Assert.That(iniString, Does.Contain("[Component"), "INI should contain component sections");
			Assert.That(iniString, Does.Contain("Name="), "INI should contain key=value pairs");
		}*/

		/*[Test]
		[Ignore("INI serialization methods not implemented")]
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
			string extractIni = ModComponentSerializationService.SerializeModComponentAsIniString([extractComponent]);
			Assert.Multiple(() =>
			{
				Assert.That(extractIni.Contains("Overwrite"), Is.False, "Extract should not serialize Overwrite");
				Assert.That(extractIni.Contains("Destination"), Is.False, "Extract should not serialize Destination");
				Assert.That(extractIni.Contains("Arguments"), Is.False, "Extract should not serialize Arguments");
			});
		}*/

		/*[Test]
		[Ignore("INI serialization methods not implemented")]
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

			string iniString = ModComponentSerializationService.SerializeModComponentAsIniString([component]);
			Assert.Multiple(() =>
			{
				Assert.That(iniString, Does.Not.Contain("IsDownloaded"), "INI should not contain IsDownloaded");
				Assert.That(iniString, Does.Not.Contain("InstallState"), "INI should not contain InstallState");
				Assert.That(iniString, Does.Not.Contain("LastStartedUtc"), "INI should not contain LastStartedUtc");
				Assert.That(iniString, Does.Not.Contain("LastCompletedUtc"), "INI should not contain LastCompletedUtc");
			});
		}*/

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

