// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System.IO.Compression;
using KOTORModSync.Core;

namespace KOTORModSync.Tests
{
	[TestFixture]
	[Ignore("not finished yet")]
	public class FileExtractor
	{
		[SetUp]
		public void Setup()
		{

			_destinationPath = new DirectoryInfo("DestinationPath");
			_sourcePaths =
			[
				"SourcePath1", "SourcePath2", "SourcePath3",
			];
		}

		[TearDown]
		public void TearDown()
		{

			if ( _destinationPath is
				{
					Exists: true,
				} )
			{
				_destinationPath.Delete(true);
			}
		}

		private DirectoryInfo? _destinationPath;
		private List<string>? _sourcePaths;

		[Test]
		public async Task ExtractFileAsync_ValidArchive_Success()
		{

			string archivePath = CreateTemporaryArchive("validArchive.zip");
			_sourcePaths =
			[
				archivePath,
			];

			Instruction.ActionExitCode extractionResult =
				await new Instruction().ExtractFileAsync(_destinationPath, _sourcePaths);

			Assert.Multiple(
				() =>
				{
					Assert.That(extractionResult, Is.Zero);

					Assert.That(Directory.Exists(_destinationPath?.FullName), Is.True);
				}
			);
		}

		[Test]
		public async Task ExtractFileAsync_InvalidArchive_Failure()
		{

			string archivePath = CreateTemporaryArchive("invalidArchive.zip");
			_sourcePaths =
			[
				archivePath,
			];

			Instruction.ActionExitCode extractionResult =
				await new Instruction().ExtractFileAsync(_destinationPath, _sourcePaths);

			Assert.Multiple(
				() =>
				{
					Assert.That(extractionResult, Is.Zero);

					Assert.That(Directory.Exists(_destinationPath?.FullName), Is.False);
				}
			);
		}

		[Test]
		[Ignore("not finished yet")]
		public async Task ExtractFileAsync_SelfExtractingExe_Success()
		{


			if ( _sourcePaths is null )
				throw new NullReferenceException(nameof(_sourcePaths));

			Instruction.ActionExitCode extractionResult =
				await new Instruction().ExtractFileAsync(_destinationPath, _sourcePaths);

			Assert.Multiple(
				() =>
				{
					Assert.That(extractionResult, Is.Zero);

					Assert.That(Directory.Exists(_destinationPath?.FullName), Is.True);
				}
			);
		}

		[Test]
		public async Task ExtractFileAsync_PermissionDenied_SkipsFile()
		{

			string archivePath = CreateTemporaryArchive("archiveWithPermissionDenied.zip");
			_sourcePaths =
			[
				archivePath,
			];

			Instruction.ActionExitCode extractionResult =
				await new Instruction().ExtractFileAsync(_destinationPath, _sourcePaths);

			Assert.Multiple(
				() =>
				{
					Assert.That(extractionResult, Is.Zero);

					Assert.That(Directory.Exists(_destinationPath?.FullName), Is.True);
				}
			);
		}

		private static string CreateTemporaryArchive(string fileName)
		{
			string archivePath = Path.Combine(Path.GetTempPath(), fileName);
			ZipFile.CreateFromDirectory(sourceDirectoryName: "SourceDirectory", archivePath);
			return archivePath;
		}

	}
}
