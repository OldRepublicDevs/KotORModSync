



using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Utility;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;

namespace KOTORModSync.Core.Services.FileSystem
{
	
	
	
	public class RealFileSystemProvider : IFileSystemProvider
	{
		public bool IsDryRun => false;

		public bool FileExists(string path) => File.Exists(path);

		public bool DirectoryExists(string path) => Directory.Exists(path);

		public Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite)
		{
			string directoryName = Path.GetDirectoryName(destinationPath);
			if ( directoryName != null && !Directory.Exists(directoryName) )
			{
				Directory.CreateDirectory(directoryName);
			}

			File.Copy(sourcePath, destinationPath, overwrite);
			return Task.CompletedTask;
		}

		public Task MoveFileAsync(string sourcePath, string destinationPath, bool overwrite)
		{
			string directoryName = Path.GetDirectoryName(destinationPath);
			if ( directoryName != null && !Directory.Exists(directoryName) )
			{
				Directory.CreateDirectory(directoryName);
			}

			if ( File.Exists(destinationPath) )
			{
				if ( overwrite )
					File.Delete(destinationPath);
			}

			File.Move(sourcePath, destinationPath);
			return Task.CompletedTask;
		}

		public Task DeleteFileAsync(string path)
		{
			File.Delete(path);
			return Task.CompletedTask;
		}

		public Task RenameFileAsync(string sourcePath, string newFileName, bool overwrite)
		{
			string directory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
			string destinationPath = Path.Combine(directory, newFileName);

			if ( overwrite && File.Exists(destinationPath) ) File.Delete(destinationPath);

			File.Move(sourcePath, destinationPath);
			return Task.CompletedTask;
		}

		public Task<string> ReadFileAsync(string path)
		{
			string content = File.ReadAllText(path);
			return Task.FromResult(content);
		}

		public Task WriteFileAsync(string path, string contents)
		{
			File.WriteAllText(path, contents);
			return Task.CompletedTask;
		}

		public Task CreateDirectoryAsync(string path)
		{
			_ = Directory.CreateDirectory(path);
			return Task.CompletedTask;
		}

		public async Task<List<string>> ExtractArchiveAsync(string archivePath, string destinationPath)
		{
			var extractedFiles = new List<string>();
			int maxCount = MainConfig.UseMultiThreadedIO ? 16 : 1;

			using ( var semaphore = new SemaphoreSlim(initialCount: 1, maxCount) )
			{
				using ( var cts = new CancellationTokenSource() )
				{
					try
					{
						await InnerExtractFileAsync(archivePath, destinationPath, extractedFiles, semaphore, cts.Token);
					}
					catch ( IndexOutOfRangeException )
					{
						await Logger.LogWarningAsync("Falling back to 7-Zip and restarting entire archive extraction due to the above error.");
						cts.Cancel();
						throw new OperationCanceledException("Falling back to 7-Zip extraction");
					}
					catch ( OperationCanceledException ex )
					{
						await Logger.LogWarningAsync(ex.Message);
						throw;
					}
					catch ( IOException ex )
					{
						await Logger.LogExceptionAsync(ex);
						throw;
					}
					catch ( Exception ex )
					{
						await Logger.LogExceptionAsync(ex);
						throw;
					}
				}
			}

			return extractedFiles;

			async Task InnerExtractFileAsync(string sourcePath, string destPath, List<string> extracted, SemaphoreSlim sem, CancellationToken token)
			{
				if ( token.IsCancellationRequested )
					return;

				await sem.WaitAsync(token);

				try
				{
					var archive = new FileInfo(sourcePath);
					string sourceRelDirPath = MainConfig.SourcePath is null ? sourcePath : PathHelper.GetRelativePath(MainConfig.SourcePath.FullName, sourcePath);

					await Logger.LogAsync($"Extracting archive '{sourcePath}'...");

					
					if ( archive.Extension.Equals(value: ".exe", StringComparison.OrdinalIgnoreCase) )
					{
						(int exitCode, string _, string _) = await PlatformAgnosticMethods.ExecuteProcessAsync(archive.FullName, $" -o\"{archive.DirectoryName}\" -y");

						if ( exitCode == 0 )
							return;

						throw new InvalidOperationException($"'{sourceRelDirPath}' is not a self-extracting executable as previously assumed. Cannot extract.");
					}

					using ( FileStream stream = File.OpenRead(archive.FullName) )
					{
						IArchive arch = GetArchiveByExtension(archive.Extension, stream);

						using ( arch )
						using ( IReader reader = arch.ExtractAllEntries() )
						{
							while ( reader.MoveToNextEntry() )
							{
								if ( reader.Entry.IsDirectory )
									continue;

								string extractFolderName = Path.GetFileNameWithoutExtension(archive.Name);
								string destinationItemPath = Path.Combine(destPath, extractFolderName, reader.Entry.Key);
								string destinationDirectory = Path.GetDirectoryName(destinationItemPath) ?? throw new NullReferenceException($"Path.GetDirectoryName({destinationItemPath})");

								if ( MainConfig.CaseInsensitivePathing && !Directory.Exists(destinationDirectory) )
								{
									destinationDirectory = PathHelper.GetCaseSensitivePath(destinationDirectory, isFile: false).Item1;
								}

								string destinationRelDirPath = MainConfig.SourcePath is null ? destinationDirectory : PathHelper.GetRelativePath(MainConfig.SourcePath.FullName, destinationDirectory);

								if ( !Directory.Exists(destinationDirectory) )
								{
									await Logger.LogVerboseAsync($"Create directory '{destinationRelDirPath}'");
									_ = Directory.CreateDirectory(destinationDirectory);
								}

								await Logger.LogVerboseAsync($"Extract '{reader.Entry.Key}' to '{destinationRelDirPath}'");

								try
								{
									IReader localReader = reader;
									await Task.Run(() =>
									{
										if ( localReader.Cancelled )
											return;
										localReader.WriteEntryToDirectory(destinationDirectory, ArchiveHelper.DefaultExtractionOptions);
									}, token);

									extracted.Add(destinationItemPath);
								}
								catch ( ObjectDisposedException )
								{
									return;
								}
								catch ( UnauthorizedAccessException )
								{
									await Logger.LogWarningAsync($"Skipping file '{reader.Entry.Key}' due to lack of permissions.");
								}
							}
						}
					}
				}
				finally
				{
					_ = sem.Release();
				}
			}

			IArchive GetArchiveByExtension(string extension, Stream stream)
			{
				switch ( extension.ToLowerInvariant() )
				{
					case ".zip":
						return ZipArchive.Open(stream);
					case ".rar":
						return RarArchive.Open(stream);
					case ".7z":
						return SevenZipArchive.Open(stream);
					default:
						return ArchiveFactory.Open(stream);
				}
			}
		}

		public List<string> GetFilesInDirectory(string directoryPath, string searchPattern = "*.*", SearchOption searchOption = SearchOption.TopDirectoryOnly) => !Directory.Exists(directoryPath)
				? new List<string>()
				: Directory.GetFiles(directoryPath, searchPattern, searchOption).ToList();

		public List<string> GetDirectoriesInDirectory(string directoryPath) => !Directory.Exists(directoryPath) ? new List<string>() : Directory.GetDirectories(directoryPath).ToList();

		public string GetFileName(string path) => Path.GetFileName(path);

		public string GetDirectoryName(string path) => Path.GetDirectoryName(path);

		public async Task<(int exitCode, string output, string error)> ExecuteProcessAsync(string programPath, string arguments) => await PlatformAgnosticMethods.ExecuteProcessAsync(programPath, arguments);

		public string GetActualPath(string path) => path;
	}
}

