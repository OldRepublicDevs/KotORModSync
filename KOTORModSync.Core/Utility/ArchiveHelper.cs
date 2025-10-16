// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SevenZip;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace KOTORModSync.Core.Utility
{
	public static class ArchiveHelper
	{
		public static readonly ExtractionOptions DefaultExtractionOptions = new ExtractionOptions
		{
			ExtractFullPath = false,
			Overwrite = true,
			PreserveFileTime = true,
		};

		public static bool IsArchive([NotNull] string filePath) => IsArchive(
			new FileInfo(filePath ?? throw new ArgumentNullException(nameof(filePath)))
		);

		public static bool IsArchive([NotNull] FileInfo thisFile) =>
			thisFile.Extension.Equals(value: ".zip", StringComparison.OrdinalIgnoreCase)
			|| thisFile.Extension.Equals(value: ".7z", StringComparison.OrdinalIgnoreCase)
			|| thisFile.Extension.Equals(value: ".rar", StringComparison.OrdinalIgnoreCase)
			|| thisFile.Extension.Equals(value: ".exe", StringComparison.OrdinalIgnoreCase);

		public static (IArchive, FileStream) OpenArchive(string archivePath)
		{
			if ( archivePath is null || !File.Exists(archivePath) )
				throw new ArgumentException(message: "Path must be a valid file on disk.", nameof(archivePath));

			try
			{
				FileStream stream = File.OpenRead(archivePath);
				IArchive archive = null;

				if ( archivePath.EndsWith(value: ".zip", StringComparison.OrdinalIgnoreCase) )
				{
					archive = ZipArchive.Open(stream);
				}
				else if ( archivePath.EndsWith(value: ".rar", StringComparison.OrdinalIgnoreCase) )
				{
					archive = RarArchive.Open(stream);
				}
				else if ( archivePath.EndsWith(value: ".7z", StringComparison.OrdinalIgnoreCase) )
				{
					archive = SevenZipArchive.Open(stream);
				}

				return (archive, stream);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex);
				return (null, null);
			}
		}

		public static bool IsPotentialSevenZipSFX([NotNull] string filePath)
		{
			byte[] sfxSignature =
			{
				0x4D, 0x5A,
			};

			byte[] fileHeader = new byte[sfxSignature.Length];

			using ( var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read) )
			{
				_ = fs.Read(fileHeader, offset: 0, sfxSignature.Length);
			}

			return sfxSignature.SequenceEqual(fileHeader);
		}

		public static bool TryExtractSevenZipSfx([NotNull] string sfxPath, [NotNull] string destinationPath, [NotNull] List<string> extractedFiles)
		{
			if ( sfxPath is null )
				throw new ArgumentNullException(nameof(sfxPath));
			if ( destinationPath is null )
				throw new ArgumentNullException(nameof(destinationPath));
			if ( extractedFiles is null )
				throw new ArgumentNullException(nameof(extractedFiles));

			if ( !File.Exists(sfxPath) )
				return false;

			byte[] sevenZipSignature = { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C };
			long signatureOffset = -1;

			try
			{
				using ( var fs = new FileStream(sfxPath, FileMode.Open, FileAccess.Read) )
				{
					var buffer = new byte[8192];
					long position = 0;
					int bytesRead;

					while ( (bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0 )
					{
						for ( int i = 0; i < bytesRead - sevenZipSignature.Length + 1; i++ )
						{
							bool match = true;
							for ( int j = 0; j < sevenZipSignature.Length; j++ )
							{
								if ( buffer[i + j] != sevenZipSignature[j] )
								{
									match = false;
									break;
								}
							}

							if ( match )
							{
								signatureOffset = position + i;
								break;
							}
						}

						if ( signatureOffset != -1 )
							break;

						position += bytesRead;
						if ( bytesRead == buffer.Length )
						{
							fs.Seek(-sevenZipSignature.Length, SeekOrigin.Current);
							position -= sevenZipSignature.Length;
						}
					}
				}

				if ( signatureOffset == -1 )
				{
					Logger.LogVerbose($"No 7z signature found in SFX file: {sfxPath}");
					return false;
				}

				Logger.LogVerbose($"Found 7z signature at offset {signatureOffset} in {sfxPath}");

				string tempSevenZipPath = Path.Combine(Path.GetTempPath(), $"sfx_extract_{Guid.NewGuid()}.7z");

				try
				{
					using ( var sourceStream = new FileStream(sfxPath, FileMode.Open, FileAccess.Read) )
					using ( var destStream = new FileStream(tempSevenZipPath, FileMode.Create, FileAccess.Write) )
					{
						sourceStream.Seek(signatureOffset, SeekOrigin.Begin);
						sourceStream.CopyTo(destStream);
					}

					string extractFolderName = Path.GetFileNameWithoutExtension(sfxPath);
					string extractPath = Path.Combine(destinationPath, extractFolderName);

					using ( var stream = File.OpenRead(tempSevenZipPath) )
					using ( var archive = SevenZipArchive.Open(stream) )
					using ( var reader = archive.ExtractAllEntries() )
					{
						while ( reader.MoveToNextEntry() )
						{
							if ( reader.Entry.IsDirectory )
								continue;

							string destinationItemPath = Path.Combine(extractPath, reader.Entry.Key);
							string destinationDirectory = Path.GetDirectoryName(destinationItemPath);

							if ( MainConfig.CaseInsensitivePathing && !Directory.Exists(destinationDirectory) )
							{
								destinationDirectory = PathHelper.GetCaseSensitivePath(destinationDirectory, isFile: false).Item1;
							}

							if ( !Directory.Exists(destinationDirectory) )
							{
								_ = Directory.CreateDirectory(destinationDirectory);
								Logger.LogVerbose($"Create directory '{destinationDirectory}'");
							}

							Logger.LogVerbose($"Extract '{reader.Entry.Key}' to '{destinationDirectory}'");
							reader.WriteEntryToDirectory(destinationDirectory, DefaultExtractionOptions);
							extractedFiles.Add(destinationItemPath);
						}
					}

					Logger.Log($"Successfully extracted 7z SFX archive: {sfxPath}");
					return true;
				}
				finally
				{
					if ( File.Exists(tempSevenZipPath) )
					{
						try
						{
							File.Delete(tempSevenZipPath);
						}
						catch ( Exception ex )
						{
							Logger.LogVerbose($"Failed to delete temporary 7z file: {ex.Message}");
						}
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Failed to extract 7z SFX: {sfxPath}");
				return false;
			}
		}

		public static string AnalyzeArchiveForExe(FileStream fileStream, IArchive archive)
		{
			string exePath = null;
			bool tslPatchDataFolderExists = false;

			try
			{
				using ( IReader reader = archive.ExtractAllEntries() )
				{
					while ( reader.MoveToNextEntry() )
					{
						if ( reader.Entry.IsDirectory )
							continue;

						string fileName = Path.GetFileName(reader.Entry.Key);
						string directory = Path.GetDirectoryName(reader.Entry.Key);

						if ( fileName.EndsWith(value: ".exe", StringComparison.OrdinalIgnoreCase) )
						{
							if ( exePath != null )
								return null;

							exePath = reader.Entry.Key;
						}

						if ( !(directory is null) && directory.Contains("tslpatchdata") )
						{
							tslPatchDataFolderExists = true;
						}
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogWarning($"SharpCompress failed to analyze archive: {ex.Message}");
				Logger.LogVerbose("Archive may require 7zip for extraction.");
				return null;
			}

			if (
				exePath != null
				&& tslPatchDataFolderExists
				&& Path.GetDirectoryName(exePath).Contains("tslpatchdata")
			)
			{
				return exePath;
			}

			return null;
		}

		public static async System.Threading.Tasks.Task<List<string>> TryListArchiveWithSevenZipCliAsync([NotNull] string archivePath)
		{
			if ( archivePath is null )
				throw new ArgumentNullException(nameof(archivePath));

			var fileList = new List<string>();
			string sevenZipPath = null;
			string[] possiblePaths = {
				// Command-line accessible (PATH)
				"7z",
				"7za",
				"7zr",

				// Windows - Program Files locations
				@"C:\Program Files\7-Zip\7z.exe",
				@"C:\Program Files (x86)\7-Zip\7z.exe",
				@"C:\Program Files\7-Zip\7za.exe",
				@"C:\Program Files (x86)\7-Zip\7za.exe",
				@"C:\Program Files\7-Zip\7zr.exe",
				@"C:\Program Files (x86)\7-Zip\7zr.exe",

				// Windows - Chocolatey
				@"C:\ProgramData\chocolatey\bin\7z.exe",
				@"C:\ProgramData\chocolatey\lib\7zip\tools\7z.exe",

				// Windows - Scoop
				@"C:\Users\%USERNAME%\scoop\apps\7zip\current\7z.exe",

				// Windows - Portable installations
				@"C:\7-Zip\7z.exe",
				@"C:\Tools\7-Zip\7z.exe",
				@"C:\Portable\7-Zip\7z.exe",

				// macOS - Homebrew/MacPorts
				"/usr/local/bin/7z",
				"/usr/local/bin/7za",
				"/opt/homebrew/bin/7z",
				"/opt/homebrew/bin/7za",

				// macOS - Manual installations
				"/Applications/7zX.app/Contents/MacOS/7za",
				"/Applications/Keka.app/Contents/Resources/7za",

				// Linux - Common system paths
				"/usr/bin/7z",
				"/usr/bin/7za",
				"/usr/bin/7zr",
				"/usr/local/bin/7z",
				"/usr/local/bin/7za",
				"/usr/local/bin/7zr",

				// Linux - Snap
				"/snap/bin/7z",
				"/snap/p7zip/current/usr/bin/7z",

				// Linux - Flatpak
				"/var/lib/flatpak/exports/bin/7z",

				// Linux - AppImage
				"/opt/7-Zip/7z",

				// Cross-platform - Home directory installations
				"~/bin/7z",
				"~/.local/bin/7z"
			};

			foreach ( string path in possiblePaths )
			{
				try
				{
					var (exitCode, _, _) = await PlatformAgnosticMethods.ExecuteProcessAsync(
						path,
						"--help",
						timeout: 2000,
						hideProcess: true,
						noLogging: true
					);

					if ( exitCode == 0 )
					{
						sevenZipPath = path;
						Logger.LogVerbose($"Found 7z CLI at: {sevenZipPath}");
						break;
					}
				}
				catch
				{
				}
			}

			if ( sevenZipPath is null )
			{
				Logger.LogWarning("7z CLI not found in any standard location. Install 7-Zip to improve archive compatibility.");
				Logger.LogVerbose($"Searched {possiblePaths.Length} possible 7z locations without success.");
				return fileList;
			}

			try
			{
				string args = $"l -slt \"{archivePath}\"";
				var (exitCode, output, error) = await PlatformAgnosticMethods.ExecuteProcessAsync(
					sevenZipPath,
					args,
					timeout: 30000,
					hideProcess: true,
					noLogging: true
				);

				if ( exitCode != 0 )
				{
					Logger.LogVerbose($"7z CLI list failed with exit code {exitCode}");
					return fileList;
				}

				bool inFileSection = false;
				string currentPath = null;
				bool isDirectory = false;

				foreach ( string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries) )
				{
					string trimmedLine = line.Trim();

					if ( trimmedLine.StartsWith("Path = ") )
					{
						if ( currentPath != null && !isDirectory )
						{
							fileList.Add(currentPath);
						}
						currentPath = trimmedLine.Substring("Path = ".Length);
						isDirectory = false;
						inFileSection = true;
					}
					else if ( inFileSection && trimmedLine.StartsWith("Folder = ") )
					{
						string folderValue = trimmedLine.Substring("Folder = ".Length);
						isDirectory = folderValue.Equals("+", StringComparison.OrdinalIgnoreCase);
					}
					else if ( trimmedLine == "----------" )
					{
						if ( currentPath != null && !isDirectory )
						{
							fileList.Add(currentPath);
						}
						currentPath = null;
						isDirectory = false;
						inFileSection = false;
					}
				}

				if ( currentPath != null && !isDirectory )
				{
					fileList.Add(currentPath);
				}

				if ( fileList.Count > 0 && fileList[0] == Path.GetFileName(archivePath) )
				{
					fileList.RemoveAt(0);
				}

				Logger.LogVerbose($"7z CLI listed {fileList.Count} files in archive");
				return fileList;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Failed to list archive with 7z CLI: {archivePath}");
				return fileList;
			}
		}

		public static async System.Threading.Tasks.Task<bool> TryExtractWithSevenZipCliAsync([NotNull] string archivePath, [NotNull] string destinationPath, [NotNull] List<string> extractedFiles)
		{
			if ( archivePath is null )
				throw new ArgumentNullException(nameof(archivePath));
			if ( destinationPath is null )
				throw new ArgumentNullException(nameof(destinationPath));
			if ( extractedFiles is null )
				throw new ArgumentNullException(nameof(extractedFiles));

			string sevenZipPath = null;
			string[] possiblePaths = { "7z", "7za", "/usr/bin/7z", "/usr/local/bin/7z" };

			foreach ( string path in possiblePaths )
			{
				try
				{
					var (exitCode, _, _) = await PlatformAgnosticMethods.ExecuteProcessAsync(
						path,
						"--help",
						timeout: 2000,
						hideProcess: true,
						noLogging: true
					);

					if ( exitCode == 0 )
					{
						sevenZipPath = path;
						break;
					}
				}
				catch
				{
				}
			}

			if ( sevenZipPath is null )
			{
				Logger.LogVerbose("7z CLI not found on PATH");
				return false;
			}

			Logger.LogVerbose($"Found 7z CLI at: {sevenZipPath}");

			try
			{
				string extractFolderName = Path.GetFileNameWithoutExtension(archivePath);
				string extractPath = Path.Combine(destinationPath, extractFolderName);

				if ( !Directory.Exists(extractPath) )
					_ = Directory.CreateDirectory(extractPath);

				string args = $"x \"-o{extractPath}\" -y \"{archivePath}\"";
				var (exitCode, output, error) = await PlatformAgnosticMethods.ExecuteProcessAsync(
					sevenZipPath,
					args,
					timeout: 120000,
					hideProcess: true,
					noLogging: false
				);

				if ( exitCode != 0 )
				{
					await Logger.LogErrorAsync($"7z CLI extraction failed with exit code {exitCode}");
					return false;
				}

				if ( Directory.Exists(extractPath) )
				{
					var files = Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories);
					extractedFiles.AddRange(files);
					Logger.Log($"Successfully extracted archive using 7z CLI: {archivePath}");
					return true;
				}

				return false;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Failed to extract with 7z CLI: {archivePath}");
				return false;
			}
		}

		public static void ExtractWith7Zip(FileStream stream, string destinationDirectory)
		{
			if ( !(Utility.GetOperatingSystem() == OSPlatform.Windows) )
				throw new NotImplementedException("Non-windows OS's are not currently supported");

			SevenZipBase.SetLibraryPath(Path.Combine(Utility.GetResourcesDirectory(), "7z.dll"));
			var extractor = new SevenZipExtractor(stream);
			extractor.ExtractArchive(destinationDirectory);
		}

		public static void OutputModTree([NotNull] DirectoryInfo directory, [NotNull] string outputPath)
		{
			if ( directory == null )
				throw new ArgumentNullException(nameof(directory));
			if ( outputPath == null )
				throw new ArgumentNullException(nameof(outputPath));

			Dictionary<string, object> root = GenerateArchiveTreeJson(directory);
			try
			{
				string json = JsonConvert.SerializeObject(
					root,
					Formatting.Indented,
					new JsonSerializerSettings
					{
						ContractResolver = new CamelCasePropertyNamesContractResolver(),
					}
				);

				File.WriteAllText(outputPath, json);
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error writing output file '{outputPath}': {ex.Message}");
			}
		}

		[CanBeNull]
		public static Dictionary<string, object> GenerateArchiveTreeJson([NotNull] DirectoryInfo directory)
		{
			if ( directory == null )
				throw new ArgumentNullException(nameof(directory));

			var root = new Dictionary<string, object>
			{
				{
					"Name", directory.Name
				},
				{
					"Type", "directory"
				},
				{
					"Contents", new List<object>()
				},
			};

			try
			{
				foreach ( FileInfo file in directory.EnumerateFilesSafely(searchPattern: "*.*") )
				{
					if ( file == null || !IsArchive(file.Extension) )
						continue;

					var fileInfo = new Dictionary<string, object>
					{
						{
							"Name", file.Name
						},
						{
							"Type", "file"
						},
					};
					List<ModDirectory.ArchiveEntry> archiveEntries = TraverseArchiveEntries(file.FullName);
					var archiveRoot = new Dictionary<string, object>
					{
						{
							"Name", file.Name
						},
						{
							"Type", "directory"
						},
						{
							"Contents", archiveEntries
						},
					};

					fileInfo["Contents"] = archiveRoot["Contents"];

					(root["Contents"] as List<object>)?.Add(fileInfo);
				}

			}
			catch ( Exception ex )
			{
				Logger.Log($"Error generating archive tree for '{directory.FullName}': {ex.Message}");
				return null;
			}

			return root;
		}

		[NotNull]
		private static List<ModDirectory.ArchiveEntry> TraverseArchiveEntries([NotNull] string archivePath)
		{
			if ( archivePath == null )
				throw new ArgumentNullException(nameof(archivePath));

			var archiveEntries = new List<ModDirectory.ArchiveEntry>();

			try
			{
				(IArchive archive, FileStream stream) = OpenArchive(archivePath);
				if ( archive is null || stream is null )
				{
					Logger.Log($"Unsupported archive format: '{Path.GetExtension(archivePath)}'");
					stream?.Dispose();
					return archiveEntries;
				}

				try
				{
					archiveEntries.AddRange(
						from entry in archive.Entries.Where(e => !e.IsDirectory)
						let pathParts = entry.Key.Split(
							archivePath.EndsWith(value: ".rar", StringComparison.OrdinalIgnoreCase)
								? '\\'
								: '/'
						)
						select new ModDirectory.ArchiveEntry
						{
							Name = pathParts[pathParts.Length - 1],
							Path = entry.Key,
						}
					);
				}
				catch ( Exception enumEx )
				{
					Logger.LogWarning($"SharpCompress failed to enumerate archive entries for '{Path.GetFileName(archivePath)}': {enumEx.Message}");
					Logger.LogVerbose("This archive may require 7zip for extraction.");
				}

				stream.Dispose();
			}
			catch ( Exception ex )
			{
				Logger.Log($"Error reading archive '{archivePath}': {ex.Message}");
			}

			return archiveEntries;
		}

		public static void ProcessArchiveEntry(
			[NotNull] IArchiveEntry entry,
			[NotNull] Dictionary<string, object> currentDirectory
		)
		{
			if ( entry == null )
				throw new ArgumentNullException(nameof(entry));
			if ( currentDirectory == null )
				throw new ArgumentNullException(nameof(currentDirectory));

			string[] pathParts = entry.Key.Split('/');
			bool isFile = !entry.IsDirectory;

			foreach ( string name in pathParts )
			{
				List<object> existingDirectory = currentDirectory["Contents"] as List<object>
					?? throw new InvalidDataException(
						$"Unexpected data type for directory contents: '{currentDirectory["Contents"]?.GetType()}'"
					);

				object existingChild = existingDirectory.Find(
					c => c is Dictionary<string, object> dict
						&& dict.ContainsKey("Name")
						&& dict["Name"] is string directoryName
						&& directoryName.Equals(name, StringComparison.OrdinalIgnoreCase)
				);

				if ( existingChild != null )
				{
					if ( isFile )
						((Dictionary<string, object>)existingChild)["Type"] = "file";

					currentDirectory = (Dictionary<string, object>)existingChild;
				}
				else
				{
					var child = new Dictionary<string, object>
					{
						{
							"Name", name
						},
						{
							"Type", isFile
								? "file"
								: "directory"
						},
						{
							"Contents", new List<object>()
						},
					};
					existingDirectory.Add(child);
					currentDirectory = child;
				}
			}
		}
	}
}
