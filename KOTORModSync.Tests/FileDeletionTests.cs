





using System.Runtime.InteropServices;
using KOTORModSync.Core;
using KOTORModSync.Core.Services.FileSystem;
using KOTORModSync.Core.Utility;

#pragma warning disable U2U1000, CS8618, RCS1118 

namespace KOTORModSync.Tests
{
	[TestFixture]
	public class FileDeletionTests
	{
		private string _testRootDir;
		private string _sourceDir;
		private string _destinationDir;
		private string _realTestDir;
		private string _virtualTestDir;
		private MainConfig _originalConfig;
		private string _sevenZipPath;

		[OneTimeSetUp]
		public void OneTimeSetUp()
		{
			_sevenZipPath = VirtualFileSystemTests.Find7Zip();
		}

		[SetUp]
		public void Setup()
		{
			_testRootDir = Path.Combine(Path.GetTempPath(), "KOTORModSync_FileDeletion_Tests_" + Guid.NewGuid().ToString("N"));
			_realTestDir = Path.Combine(_testRootDir, "Real");
			_virtualTestDir = Path.Combine(_testRootDir, "Virtual");
			_sourceDir = Path.Combine(_testRootDir, "TestFiles", "source");
			_destinationDir = Path.Combine(_testRootDir, "TestFiles", "dest");

			Directory.CreateDirectory(_sourceDir);
			Directory.CreateDirectory(_destinationDir);

			_originalConfig = new MainConfig();
			_ = new MainConfig
			{
				sourcePath = new DirectoryInfo(_sourceDir),
				destinationPath = new DirectoryInfo(_destinationDir),
			};
		}

		[TearDown]
		public void Teardown()
		{
			try
			{
				if ( Directory.Exists(_testRootDir) )
					Directory.Delete(_testRootDir, recursive: true);
			}
			catch ( Exception ex )
			{
				TestContext.WriteLine($"Warning: Could not delete test directory: {ex.Message}");
			}
			
			_ = new MainConfig
			{
				sourcePath = _originalConfig.sourcePath,
				destinationPath = _originalConfig.destinationPath
			};
		}
		private async Task<(VirtualFileSystemProvider virtualProvider, string realSource, string realDest)> RunBothProviders(List<Instruction> instructions, string sourceDir, string destDir)
		{
			
			var virtualInstructions = new List<Instruction>();
			var realInstructions = new List<Instruction>();
			foreach ( Instruction instruction in instructions )
			{
				virtualInstructions.Add(new Instruction
				{
					Action = instruction.Action,
					Source = instruction.Source.ToList(),
					Destination = instruction.Destination,
					Overwrite = instruction.Overwrite,
					Arguments = instruction.Arguments
				});
				realInstructions.Add(new Instruction
				{
					Action = instruction.Action,
					Source = instruction.Source.ToList(),
					Destination = instruction.Destination,
					Overwrite = instruction.Overwrite,
					Arguments = instruction.Arguments
				});
			}

			
			string virtualRoot = Path.Combine(_testRootDir, "Virtual");
			string virtualSource = Path.Combine(virtualRoot, "source");
			string virtualDest = Path.Combine(virtualRoot, "dest");
			_ = Directory.CreateDirectory(virtualSource);
			_ = Directory.CreateDirectory(virtualDest);
			VirtualFileSystemTests.CopyDirectory(sourceDir, virtualSource);

			var virtualProvider = new VirtualFileSystemProvider();
			await virtualProvider.InitializeFromRealFileSystemAsync(virtualSource);
			var virtualComponent = new ModComponent { Name = "TestComponent", Instructions = new(virtualInstructions) };

			_ = new MainConfig
			{
				sourcePath = new DirectoryInfo(virtualSource),
				destinationPath = new DirectoryInfo(virtualDest)
			};

			_ = await virtualComponent.ExecuteInstructionsAsync(virtualComponent.Instructions, [virtualComponent], CancellationToken.None, virtualProvider);


			
			string realRoot = Path.Combine(_testRootDir, "Real");
			string realSource = Path.Combine(realRoot, "source");
			string realDest = Path.Combine(realRoot, "dest");
			_ = Directory.CreateDirectory(realSource);
			_ = Directory.CreateDirectory(realDest);
			VirtualFileSystemTests.CopyDirectory(sourceDir, realSource);

			var realProvider = new RealFileSystemProvider();
			var realComponent = new ModComponent { Name = "TestComponent", Instructions = new(realInstructions) };

			_ = new MainConfig
			{
				sourcePath = new DirectoryInfo(realSource),
				destinationPath = new DirectoryInfo(realDest)
			};

			_ = await realComponent.ExecuteInstructionsAsync(realComponent.Instructions, [realComponent], CancellationToken.None, realProvider);

			return (virtualProvider, realSource, realDest);
		}

		private async Task RunDeleteDuplicateFile(string directory, string fileExtension, List<string>? compatibleExtensions = null)
		{
			var instructions = new List<Instruction>
			{
				new() {
					Action = Instruction.ActionType.DelDuplicate,
					Destination = "<<modDirectory>>",
					Arguments = fileExtension,
					Source = compatibleExtensions
				}
			};

			
			var (virtualProvider, realSource, realDest) = await RunBothProviders(instructions, directory, directory);

			
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			Assert.That(Directory.GetFiles(directory).Count, Is.EqualTo(0));
		}


		[Test]
		public async Task DeleteDuplicateFile_NoDuplicateFiles_NoFilesDeleted()
		{
			
			string file1 = Path.Combine(_sourceDir, "file1.txt");
			string file2 = Path.Combine(_sourceDir, "file2.png");
			await File.WriteAllTextAsync(file1, "Content 1");
			await File.WriteAllTextAsync(file2, "Content 2");

			var instructions = new List<Instruction>
			{
				new() {
					Action = Instruction.ActionType.DelDuplicate,
					Destination = "<<modDirectory>>",
					Arguments = ".txt",
					Source = [".txt", ".png"]
				}
			};

			
			var (virtualProvider, realSource, realDest) = await RunBothProviders(instructions, _sourceDir, _destinationDir);


			
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			Assert.That(File.Exists(Path.Combine(_sourceDir, "file1.txt")), Is.True);
			Assert.That(File.Exists(Path.Combine(_sourceDir, "file2.png")), Is.True);
		}

		[Test]
		public async Task DeleteDuplicateFile_DuplicateFilesWithDifferentExtensions_AllDuplicatesDeleted()
		{
			
			string file1 = Path.Combine(_sourceDir, "file.txt");
			string file2 = Path.Combine(_sourceDir, "file.png");
			string file3 = Path.Combine(_sourceDir, "file.jpg");
			await File.WriteAllTextAsync(file1, "Content 1");
			await File.WriteAllTextAsync(file2, "Content 2");
			await File.WriteAllTextAsync(file3, "Content 3");

			var instructions = new List<Instruction>
			{
				new() {
					Action = Instruction.ActionType.DelDuplicate,
					Destination = "<<modDirectory>>",
					Arguments = ".txt",
					Source = [".txt", ".png", ".jpg"]
				}
			};

			
			var (virtualProvider, realSource, realDest) = await RunBothProviders(instructions, _sourceDir, _destinationDir);

			
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			Assert.That(File.Exists(Path.Combine(realSource, "file.txt")), Is.False);
			Assert.That(File.Exists(Path.Combine(realSource, "file.png")), Is.True);
			Assert.That(File.Exists(Path.Combine(realSource, "file.jpg")), Is.True);
		}

		[Test]
		public async Task DeleteDuplicateFile_CaseInsensitiveFileNames_DuplicatesDeleted()
		{
			
			string file1 = Path.Combine(_sourceDir, "FILE.tga");
			string file2 = Path.Combine(_sourceDir, "fIle.tpc");
			await File.WriteAllTextAsync(file1, "Content 1");
			await File.WriteAllTextAsync(file2, "Content 2");

			var instructions = new List<Instruction>
			{
				new() {
					Action = Instruction.ActionType.DelDuplicate,
					Destination = "<<modDirectory>>",
					Arguments = ".tga",
					Source = [".tga", ".tpc"]
				}
			};

			
			var (virtualProvider, realSource, realDest) = await RunBothProviders(instructions, _sourceDir, _destinationDir);

			
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			Assert.That(File.Exists(Path.Combine(realSource, "FILE.tga")), Is.False);
			Assert.That(File.Exists(Path.Combine(realSource, "fIle.tpc")), Is.True);
		}

		[Test]
		public async Task DeleteDuplicateFile_InvalidFileExtension_NoFilesDeleted()
		{
			
			string file1 = Path.Combine(_sourceDir, "file1.txt");
			string file2 = Path.Combine(_sourceDir, "file2.png");
			await File.WriteAllTextAsync(file1, "Content 1");
			await File.WriteAllTextAsync(file2, "Content 2");

			var instructions = new List<Instruction>
			{
				new() {
					Action = Instruction.ActionType.DelDuplicate,
					Destination = "<<modDirectory>>",
					Arguments = ".jpg",
					Source = [".txt", ".png"]
				}
			};

			
			var (virtualProvider, realSource, realDest) = await RunBothProviders(instructions, _sourceDir, _destinationDir);

			
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			Assert.That(File.Exists(Path.Combine(_sourceDir, "file1.txt")), Is.True);
			Assert.That(File.Exists(Path.Combine(_sourceDir, "file2.png")), Is.True);
		}

		[Test]
		public async Task DeleteDuplicateFile_EmptyDirectory_NoFilesDeleted()
		{
			
			var instructions = new List<Instruction>
			{
				new() {
					Action = Instruction.ActionType.DelDuplicate,
					Destination = "<<modDirectory>>",
					Arguments = ".txt",
					Source = [".txt"]
				}
			};

			
			var (virtualProvider, realSource, realDest) = await RunBothProviders(instructions, _sourceDir, _destinationDir);

			
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			Assert.That(Directory.GetFiles(_sourceDir), Is.Empty);
		}

		[Test]
		public async Task DeleteDuplicateFile_DuplicateFilesInSubdirectories_NoFilesDeleted()
		{
			
			string subdirectory = Path.Combine(_sourceDir, "Subdirectory");
			Directory.CreateDirectory(subdirectory);
			string file1 = Path.Combine(_sourceDir, "file.txt");
			string file2 = Path.Combine(subdirectory, "file.png");
			await File.WriteAllTextAsync(file1, "Content 1");
			await File.WriteAllTextAsync(file2, "Content 2");

			var instructions = new List<Instruction>
			{
				new() {
					Action = Instruction.ActionType.DelDuplicate,
					Destination = "<<modDirectory>>",
					Arguments = ".txt",
					Source = [".txt", ".png"]
				}
			};

			
			var (virtualProvider, realSource, realDest) = await RunBothProviders(instructions, _sourceDir, _destinationDir);

			
			Assert.That(virtualProvider.GetValidationIssues(), Is.Empty);
			Assert.That(File.Exists(file1), Is.True);
			Assert.That(File.Exists(file2), Is.True);
		}

		
		[Test]
		public async Task DeleteDuplicateFile_CaseSensitiveExtensions_DuplicatesDeleted()
		{
			if ( Utility.GetOperatingSystem() == OSPlatform.Windows )
			{
				Console.WriteLine("Test is not possible on Windows.");
				return;
			}

			
			string directory = Path.Combine(_testRootDir, "DuplicatesWithCaseInsensitiveExtensions");
			_ = Directory.CreateDirectory(directory);
			string file1 = Path.Combine(directory, "file.tpc");
			string file2 = Path.Combine(directory, "file.TPC");
			string file3 = Path.Combine(directory, "file.tga");
			await File.WriteAllTextAsync(file1, "Content 1");
			await File.WriteAllTextAsync(file2, "Content 2");
			await File.WriteAllTextAsync(file3, "Content 3");

			var instructions = new List<Instruction>
			{
				new() {
					Action = Instruction.ActionType.DelDuplicate,
					Destination = "<<modDirectory>>",
					Arguments = ".tpc",
					Source = [".tpc", ".tga"]
				}
			};

			
			await RunDeleteDuplicateFile(directory, ".tpc");

			
			Assert.Multiple(() =>
			{
				Assert.That(!File.Exists(file1));
				Assert.That(!File.Exists(file2));
				Assert.That(File.Exists(file3));
			});
		}
	}
}
