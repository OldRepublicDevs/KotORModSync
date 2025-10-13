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
	public class XmlFileTests
	{
		[SetUp]
		public void SetUp()
		{
			_filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");
			File.WriteAllText(_filePath, _exampleXml);
		}

		[TearDown]
		public void TearDown()
		{
			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " != null");
			if ( File.Exists(_filePath) )
				File.Delete(_filePath);
		}

		private string _filePath = string.Empty;

		private readonly string _exampleXml = @"<?xml version=""2.0"" encoding=""utf-8""?>
<ModComponents>
  <ModComponent>
    <Name>Ultimate Dantooine</Name>
    <Guid>{B3525945-BDBD-45D8-A324-AAF328A5E13E}</Guid>
    <Dependencies>
      <Dependency>{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}</Dependency>
      <Dependency>{D0F371DA-5C69-4A26-8A37-76E3A6A2A50D}</Dependency>
    </Dependencies>
    <InstallOrder>3</InstallOrder>
    <Instructions>
      <Instruction>
        <Action>extract</Action>
        <Source>Ultimate Dantooine High Resolution - TPC Version-1103-2-1-1670680013.rar</Source>
        <Destination>%temp%\mod_files\Dantooine HR</Destination>
        <Overwrite>true</Overwrite>
      </Instruction>
      <Instruction>
        <Action>delete</Action>
        <Paths>
          <Path>%temp%\mod_files\Dantooine HR\DAN_wall03.tpc</Path>
          <Path>%temp%\mod_files\Dantooine HR\DAN_NEW1.tpc</Path>
          <Path>%temp%\mod_files\Dantooine HR\DAN_MWFl.tpc</Path>
        </Paths>
      </Instruction>
      <Instruction>
        <Action>move</Action>
        <Source>%temp%\mod_files\Dantooine HR\</Source>
        <Destination>%temp%\Override</Destination>
      </Instruction>
    </Instructions>
  </ModComponent>
  <ModComponent>
    <Name>TSLRCM Tweak Pack</Name>
    <Guid>{C5418549-6B7E-4A8C-8B8E-4AA1BC63C732}</Guid>
    <InstallOrder>1</InstallOrder>
    <Dependencies />
    <Instructions>
      <Instruction>
        <Action>extract</Action>
        <Source>URCMTP 1.3.rar</Source>
        <Destination>%temp%\mod_files\TSLRCM Tweak Pack</Destination>
        <Overwrite>true</Overwrite>
      </Instruction>
      <Instruction>
        <Action>run</Action>
        <Path>%temp%\mod_files\TSLPatcher.exe</Path>
      </Instruction>
    </Instructions>
  </ModComponent>
</ModComponents>";

		[Test]
		public void SaveAndLoadXMLFile_MatchingComponents()
		{
			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " is null");

			List<ModComponent> originalComponents = FileLoadingService.LoadFromFile(_filePath);

			string modifiedFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");
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

		[Test]
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

			string xmlString = ModComponentSerializationService.SaveToXmlString([newComponent]);
			ModComponent duplicateComponent = ModComponentSerializationService.LoadFromXmlString(xmlString)[0];

			Assert.That(duplicateComponent, Is.Not.Null);
			AssertComponentEquality(newComponent, duplicateComponent);
		}

		[Test]
		public void SaveAndLoadXMLFile_WhitespaceTests()
		{
			List<ModComponent> originalComponents = FileLoadingService.LoadFromFile(_filePath);

			Assert.That(_filePath, Is.Not.Null, nameof(_filePath) + " != null");
			string xmlContents = File.ReadAllText(_filePath);

			xmlContents = "    \r\n\t   \r\n\r\n\r\n" + xmlContents + "    \r\n\t   \r\n\r\n\r\n";

			string modifiedFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");
			File.WriteAllText(modifiedFilePath, xmlContents);

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
		public void SaveAndLoadXMLFile_EmptyComponentsList()
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
		public void SaveAndLoadXMLFile_DuplicateGuids()
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
		public void SaveAndLoadXMLFile_ModifyComponents()
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
		public void SaveAndLoadXMLFile_MultipleRounds()
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
					},
				}
			};
			string extractXml = ModComponentSerializationService.SaveToXmlString([extractComponent]);
			Assert.Multiple(() =>
			{
				Assert.That(extractXml.Contains("Overwrite"), Is.False, "Extract should not serialize Overwrite");
				Assert.That(extractXml.Contains("Destination"), Is.False, "Extract should not serialize Destination");
				Assert.That(extractXml.Contains("Arguments"), Is.False, "Extract should not serialize Arguments");
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

			string xmlString = ModComponentSerializationService.SaveToXmlString([component]);
			Assert.Multiple(() =>
			{
				Assert.That(xmlString, Does.Not.Contain("IsDownloaded"), "XML should not contain IsDownloaded");
				Assert.That(xmlString, Does.Not.Contain("InstallState"), "XML should not contain InstallState");
				Assert.That(xmlString, Does.Not.Contain("LastStartedUtc"), "XML should not contain LastStartedUtc");
				Assert.That(xmlString, Does.Not.Contain("LastCompletedUtc"), "XML should not contain LastCompletedUtc");
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

