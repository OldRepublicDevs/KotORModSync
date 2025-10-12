// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Core.Services.Download;
using NUnit.Framework;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace KOTORModSync.Tests
{
	/// <summary>
	/// Integration tests for the unified ModLinkProcessingService pipeline.
	/// Tests the full flow: Download -> Extract instruction -> AutoInstructionGenerator
	/// </summary>
	[TestFixture]
	public class UnifiedPipelineIntegrationTests
	{
		private string _testDirectory;
		private string _downloadDirectory;

		[SetUp]
		public void SetUp()
		{
			_testDirectory = Path.Combine(Path.GetTempPath(), "KOTORModSync_PipelineTests_" + Guid.NewGuid());
			_downloadDirectory = Path.Combine(_testDirectory, "Downloads");
			Directory.CreateDirectory(_testDirectory);
			Directory.CreateDirectory(_downloadDirectory);

			// Initialize MainConfig for the tests
			var mainConfig = new MainConfig();
			mainConfig.sourcePath = new DirectoryInfo(_downloadDirectory);
			mainConfig.destinationPath = new DirectoryInfo(Path.Combine(_testDirectory, "KOTOR"));
			Directory.CreateDirectory(mainConfig.destinationPath.FullName);
		}

		[TearDown]
		public void TearDown()
		{
			try
			{
				if ( Directory.Exists(_testDirectory) )
					Directory.Delete(_testDirectory, recursive: true);
			}
			catch
			{
				// Ignore cleanup errors
			}
		}

		[Test]
		public void UnifiedPipeline_LocalArchive_GeneratesInstructions()
		{
			// Arrange - Create a local archive file
			string archivePath = CreateTestArchive("test_mod.zip", archive =>
			{
				AddTextFileToArchive(archive, "Override/appearance.2da", "2DA");
				AddTextFileToArchive(archive, "Override/dialog.dlg", "DLG");
			});

			var component = new ModComponent
			{
				Name = "Test Mod",
				Guid = Guid.NewGuid(),
				ModLink = new List<string> { "file:///" + archivePath.Replace("\\", "/") }
			};

			// Set up the unified pipeline
			var downloadCacheService = new DownloadCacheService();
			var httpClient = new System.Net.Http.HttpClient();
			var handlers = new List<IDownloadHandler>
			{
				new DirectDownloadHandler(httpClient)
			};
			var downloadManager = new DownloadManager(handlers);
			downloadCacheService.SetDownloadManager(downloadManager);
			var modLinkProcessor = new ModLinkProcessingService(downloadCacheService);

			// Act
			int result = modLinkProcessor.ProcessComponentModLinksSync(
				new List<ModComponent> { component },
				_downloadDirectory,
				null,
				CancellationToken.None);

			// Assert
			Assert.That(result, Is.GreaterThan(0), "Should process at least one component");
			Assert.That(component.Instructions, Is.Not.Empty, "Should have instructions");
			Assert.That(component.Instructions[0].Action, Is.EqualTo(Instruction.ActionType.Extract), "First instruction should be Extract");
		}

		[Test]
		public void UnifiedPipeline_MultipleModLinks_CreatesInstructionsForEach()
		{
			// Arrange - Create two archives
			string archive1 = CreateTestArchive("mod1.zip", archive =>
			{
				AddTextFileToArchive(archive, "file1.2da", "2DA");
			});

			string archive2 = CreateTestArchive("mod2.zip", archive =>
			{
				AddTextFileToArchive(archive, "file2.dlg", "DLG");
			});

			var component = new ModComponent
			{
				Name = "Multi Link Mod",
				Guid = Guid.NewGuid(),
				ModLink = new List<string>
				{
					"file:///" + archive1.Replace("\\", "/"),
					"file:///" + archive2.Replace("\\", "/")
				}
			};

			var downloadCacheService = new DownloadCacheService();
			var httpClient = new System.Net.Http.HttpClient();
			var handlers = new List<IDownloadHandler> { new DirectDownloadHandler(httpClient) };
			var downloadManager = new DownloadManager(handlers);
			downloadCacheService.SetDownloadManager(downloadManager);
			var modLinkProcessor = new ModLinkProcessingService(downloadCacheService);

			// Act
			modLinkProcessor.ProcessComponentModLinksSync(
				new List<ModComponent> { component },
				_downloadDirectory,
				null,
				CancellationToken.None);

			// Assert
			Assert.That(component.Instructions.Count, Is.EqualTo(4), "Should have 2 Extract + 2 Move instructions");

			// Check for both archives
			var mod1Instructions = component.Instructions.Where(i =>
				i.Source != null && i.Source.Any(s => s.Contains("mod1"))).ToList();
			var mod2Instructions = component.Instructions.Where(i =>
				i.Source != null && i.Source.Any(s => s.Contains("mod2"))).ToList();

			Assert.That(mod1Instructions.Count, Is.EqualTo(2), "Should have Extract + Move for mod1");
			Assert.That(mod2Instructions.Count, Is.EqualTo(2), "Should have Extract + Move for mod2");
		}

		[Test]
		public void UnifiedPipeline_ComponentWithExistingInstructions_AddsNewOnes()
		{
			// Arrange - Component already has an instruction
			string archivePath = CreateTestArchive("new_mod.zip", archive =>
			{
				AddTextFileToArchive(archive, "file.2da", "2DA");
			});

			var component = new ModComponent
			{
				Name = "Test Mod",
				Guid = Guid.NewGuid(),
				ModLink = new List<string> { "file:///" + archivePath.Replace("\\", "/") }
			};

			// Add existing instruction manually
			var existingInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.Copy,
				Source = new List<string> { "existing_file.txt" },
				Destination = "some_path"
			};
			existingInstruction.SetParentComponent(component);
			component.Instructions.Add(existingInstruction);

			var downloadCacheService = new DownloadCacheService();
			var httpClient = new System.Net.Http.HttpClient();
			var handlers = new List<IDownloadHandler> { new DirectDownloadHandler(httpClient) };
			var downloadManager = new DownloadManager(handlers);
			downloadCacheService.SetDownloadManager(downloadManager);
			var modLinkProcessor = new ModLinkProcessingService(downloadCacheService);

			// Act
			modLinkProcessor.ProcessComponentModLinksSync(
				new List<ModComponent> { component },
				_downloadDirectory,
				null,
				CancellationToken.None);

			// Assert
			Assert.That(component.Instructions.Count, Is.EqualTo(3), "Should have existing + Extract + Move");
			Assert.That(component.Instructions[0].Action, Is.EqualTo(Instruction.ActionType.Copy), "Existing instruction should remain");
		}

		[Test]
		public void UnifiedPipeline_ProcessSameArchiveTwice_DoesNotDuplicate()
		{
			// Arrange
			string archivePath = CreateTestArchive("same_mod.zip", archive =>
			{
				AddTextFileToArchive(archive, "file.2da", "2DA");
			});

			var component = new ModComponent
			{
				Name = "Test Mod",
				Guid = Guid.NewGuid(),
				ModLink = new List<string> { "file:///" + archivePath.Replace("\\", "/") }
			};

			var downloadCacheService = new DownloadCacheService();
			var httpClient = new System.Net.Http.HttpClient();
			var handlers = new List<IDownloadHandler> { new DirectDownloadHandler(httpClient) };
			var downloadManager = new DownloadManager(handlers);
			downloadCacheService.SetDownloadManager(downloadManager);
			var modLinkProcessor = new ModLinkProcessingService(downloadCacheService);

			// Act - Process twice
			modLinkProcessor.ProcessComponentModLinksSync(
				new List<ModComponent> { component },
				_downloadDirectory,
				null,
				CancellationToken.None);

			int instructionsAfterFirst = component.Instructions.Count;

			modLinkProcessor.ProcessComponentModLinksSync(
				new List<ModComponent> { component },
				_downloadDirectory,
				null,
				CancellationToken.None);

			int instructionsAfterSecond = component.Instructions.Count;

			// Assert
			Assert.That(instructionsAfterFirst, Is.EqualTo(instructionsAfterSecond),
				"Processing same archive twice should not duplicate instructions");
		}

		[Test]
		public void UnifiedPipeline_TSLPatcherArchive_CreatesPatcherInstructionBeforeMove()
		{
			// Arrange - Create hybrid archive
			string archivePath = CreateTestArchive("hybrid.zip", archive =>
			{
				AddTextFileToArchive(archive, "tslpatchdata/changes.ini", "[Settings]\nLookupGameFolder=1");
				AddTextFileToArchive(archive, "TSLPatcher.exe", "fake");
				AddTextFileToArchive(archive, "ExtraFolder/file.2da", "2DA");
			});

			var component = new ModComponent
			{
				Name = "Hybrid Mod",
				Guid = Guid.NewGuid(),
				ModLink = new List<string> { "file:///" + archivePath.Replace("\\", "/") }
			};

			var downloadCacheService = new DownloadCacheService();
			var httpClient = new System.Net.Http.HttpClient();
			var handlers = new List<IDownloadHandler> { new DirectDownloadHandler(httpClient) };
			var downloadManager = new DownloadManager(handlers);
			downloadCacheService.SetDownloadManager(downloadManager);
			var modLinkProcessor = new ModLinkProcessingService(downloadCacheService);

			// Act
			modLinkProcessor.ProcessComponentModLinksSync(
				new List<ModComponent> { component },
				_downloadDirectory,
				null,
				CancellationToken.None);

			// Assert
			Assert.That(component.Instructions.Count, Is.GreaterThanOrEqualTo(3), "Should have Extract + Patcher + Move");
			Assert.That(component.Instructions[0].Action, Is.EqualTo(Instruction.ActionType.Extract));
			Assert.That(component.Instructions[1].Action, Is.EqualTo(Instruction.ActionType.Patcher),
				"TSLPatcher instruction should come before Move");
			Assert.That(component.Instructions[2].Action, Is.EqualTo(Instruction.ActionType.Move));
		}

		#region Helper Methods

		private string CreateTestArchive(string fileName, Action<ZipArchive> populateArchive)
		{
			string archivePath = Path.Combine(_downloadDirectory, fileName);

			using ( var archive = ZipArchive.Create() )
			{
				populateArchive(archive);
				archive.SaveTo(archivePath, CompressionType.Deflate);
			}

			return archivePath;
		}

		private static void AddTextFileToArchive(ZipArchive archive, string path, string content)
		{
			var memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
			archive.AddEntry(path, memoryStream, closeStream: true);
		}

		#endregion
	}
}

