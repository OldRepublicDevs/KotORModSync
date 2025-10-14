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
			// These bytes represent a typical signature for Windows executables.
			byte[] sfxSignature =
			{
				0x4D, 0x5A,
			}; // 'MZ' header

			byte[] fileHeader = new byte[sfxSignature.Length];

			using ( var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read) )
			{
				_ = fs.Read(fileHeader, offset: 0, sfxSignature.Length);
			}

			return sfxSignature.SequenceEqual(fileHeader);
		}

		/// <summary>
		/// Attempts to extract a 7z SFX executable by finding and extracting the embedded 7z payload.
		/// </summary>
		/// <param name="sfxPath">Path to the SFX .exe file</param>
		/// <param name="destinationPath">Base destination directory for extraction</param>
		/// <param name="extractedFiles">List to populate with extracted file paths</param>
		/// <returns>True if extraction succeeded, false otherwise</returns>
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

			// 7z signature: 37 7A BC AF 27 1C
			byte[] sevenZipSignature = { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C };
			long signatureOffset = -1;

			try
			{
				using ( var fs = new FileStream(sfxPath, FileMode.Open, FileAccess.Read) )
				{
					// Search for 7z signature in the file
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
						// Move back a bit to catch signatures that span buffer boundaries
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

				// Create a temporary .7z file with the embedded payload
				string tempSevenZipPath = Path.Combine(Path.GetTempPath(), $"sfx_extract_{Guid.NewGuid()}.7z");

				try
				{
					using ( var sourceStream = new FileStream(sfxPath, FileMode.Open, FileAccess.Read) )
					using ( var destStream = new FileStream(tempSevenZipPath, FileMode.Create, FileAccess.Write) )
					{
						sourceStream.Seek(signatureOffset, SeekOrigin.Begin);
						sourceStream.CopyTo(destStream);
					}

					// Extract the temp .7z file using SharpCompress
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
					// Clean up temp file
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

			using ( IReader reader = archive.ExtractAllEntries() )
			{
				while ( reader.MoveToNextEntry() )
				{
					if ( reader.Entry.IsDirectory )
						continue;

					string fileName = Path.GetFileName(reader.Entry.Key);
					string directory = Path.GetDirectoryName(reader.Entry.Key);

					// Check for exe file
					if ( fileName.EndsWith(value: ".exe", StringComparison.OrdinalIgnoreCase) )
					{
						if ( exePath != null )
							return null;  // Multiple exe files found in the archive.

						exePath = reader.Entry.Key;
					}

					// Check for 'tslpatchdata' folder
					if ( !(directory is null) && directory.Contains("tslpatchdata") )
					{
						tslPatchDataFolderExists = true;
					}
				}
			}

			if (
				exePath != null
				&& tslPatchDataFolderExists
				// ReSharper disable once PossibleNullReferenceException
				&& Path.GetDirectoryName(exePath).Contains("tslpatchdata")
			)
			{
				return exePath;
			}

			return null;
		}

		/// <summary>
		/// Attempts to extract using the 7z command-line tool if available.
		/// </summary>
		/// <param name="archivePath">Path to the archive file</param>
		/// <param name="destinationPath">Base destination directory for extraction</param>
		/// <param name="extractedFiles">List to populate with extracted file paths</param>
		/// <returns>True if extraction succeeded via CLI, false if 7z CLI not available or failed</returns>
		public static async System.Threading.Tasks.Task<bool> TryExtractWithSevenZipCliAsync([NotNull] string archivePath, [NotNull] string destinationPath, [NotNull] List<string> extractedFiles)
		{
			if ( archivePath is null )
				throw new ArgumentNullException(nameof(archivePath));
			if ( destinationPath is null )
				throw new ArgumentNullException(nameof(destinationPath));
			if ( extractedFiles is null )
				throw new ArgumentNullException(nameof(extractedFiles));

			// Try to find 7z executable
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
					// Continue to next path
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

				// Execute 7z extraction
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

				// Enumerate extracted files
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

			SevenZipBase.SetLibraryPath(Path.Combine(Utility.GetResourcesDirectory(), "7z.dll")); // Path to 7z.dll
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

				/*foreach (DirectoryInfo subdirectory in directory.EnumerateDirectoriesSafely())
                {
                    var subdirectoryInfo = new Dictionary<string, object>
                    {
                        { "Name", subdirectory.Name },
                        { "Type", "directory" },
                        { "Contents", GenerateArchiveTreeJson(subdirectory) }
                    };

                    (root["Contents"] as List<object>).Add(subdirectoryInfo);
                }*/
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

				archiveEntries.AddRange(
					from entry in archive.Entries.Where(e => !e.IsDirectory)
					let pathParts = entry.Key.Split(
						archivePath.EndsWith(value: ".rar", StringComparison.OrdinalIgnoreCase)
							? '\\' // Use backslash as separator for RAR files
							: '/'  // Use forward slash for other archive types
					)
					select new ModDirectory.ArchiveEntry
					{
						Name = pathParts[pathParts.Length - 1],
						Path = entry.Key,
					}
				);

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
