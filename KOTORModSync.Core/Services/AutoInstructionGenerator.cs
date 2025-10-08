// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using KOTORModSync.Core.TSLPatcher;
using KOTORModSync.Core.Utility;
using SharpCompress.Archives;

namespace KOTORModSync.Core.Services
{
	/// <summary>
	/// Automatically generates default instructions for components based on archive contents.
	/// </summary>
	public static class AutoInstructionGenerator
	{
		/// <summary>
		/// Analyzes an archive and generates appropriate default instructions.
		/// </summary>
		/// <param name="component">The component to generate instructions for</param>
		/// <param name="archivePath">Path to the mod archive</param>
		/// <returns>True if instructions were generated successfully</returns>
		public static bool GenerateInstructions([NotNull] Component component, [NotNull] string archivePath)
		{
			if ( component is null )
				throw new ArgumentNullException(nameof(component));
			if ( string.IsNullOrWhiteSpace(archivePath) )
				throw new ArgumentException("Archive path cannot be null or empty", nameof(archivePath));
			if ( !File.Exists(archivePath) )
				return false;

			try
			{
				(IArchive archive, FileStream stream) = ArchiveHelper.OpenArchive(archivePath);
				if ( archive is null || stream is null )
					return false;

				using ( stream )
				using ( archive )
				{
					ArchiveAnalysis analysis = AnalyzeArchive(archive);
					return GenerateAllInstructions(component, archivePath, analysis);
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Failed to generate instructions for {archivePath}");
				return false;
			}
		}

		/// <summary>
		/// Generates all appropriate instructions based on what's detected in the archive.
		/// Handles hybrid scenarios (e.g., TSLPatcher + loose files).
		/// </summary>
		private static bool GenerateAllInstructions(Component component, string archivePath, ArchiveAnalysis analysis)
		{
			string archiveFileName = Path.GetFileName(archivePath);
			string extractedPath = archiveFileName.Replace(Path.GetExtension(archiveFileName), "");
			component.Instructions.Clear();

			// Always start with Extract if we have any content
			if ( analysis.HasTslPatchData || analysis.HasSimpleOverrideFiles )
			{
				var extractInstruction = new Instruction
				{
					Guid = Guid.NewGuid(),
					Action = Instruction.ActionType.Extract,
					Source = new List<string> { $@"<<modDirectory>>\{archiveFileName}" },
					Overwrite = true
				};
				extractInstruction.SetParentComponent(component);
				component.Instructions.Add(extractInstruction);
			}
			else
			{
				// Nothing to install
				return false;
			}

			// Handle TSLPatcher instructions
			if ( analysis.HasTslPatchData )
			{
				if ( analysis.HasNamespacesIni )
					AddNamespacesChooseInstructions(component, archivePath, analysis, extractedPath);
				else if ( analysis.HasChangesIni )
					AddSimplePatcherInstruction(component, analysis, extractedPath);
			}

			// Handle loose Override files
			if ( analysis.HasSimpleOverrideFiles )
			{
				// Exclude folders that are part of TSLPatcher
				var overrideFolders = analysis.FoldersWithFiles
					.Where(f => !IsTslPatcherFolder(f, analysis))
					.ToList();

				if ( overrideFolders.Count > 1 )
					// Multiple folders - create Choose instruction
					AddMultiFolderChooseInstructions(component, extractedPath, overrideFolders);
				else if ( overrideFolders.Count == 1 )
					// Single folder - simple Move
					AddSimpleMoveInstruction(component, extractedPath, overrideFolders[0]);
				else if ( analysis.HasFlatFiles )
					// Flat files in root
					AddSimpleMoveInstruction(component, extractedPath, null);
			}

			// Set installation method based on what we found
			if ( analysis.HasTslPatchData && analysis.HasSimpleOverrideFiles )
				component.InstallationMethod = "Hybrid (TSLPatcher + Loose Files)";
			else if ( analysis.HasTslPatchData )
				component.InstallationMethod = "TSLPatcher";
			else if ( analysis.HasSimpleOverrideFiles )
				component.InstallationMethod = "Loose-File Mod";

			return component.Instructions.Count > 0;
		}

		/// <summary>
		/// Checks if a folder is part of the TSLPatcher structure and should be excluded from Override moves.
		/// </summary>
		private static bool IsTslPatcherFolder(string folderName, ArchiveAnalysis analysis)
		{
			if ( string.IsNullOrEmpty(folderName) )
				return false;

			// Check if this folder is the TSLPatcher path or contains tslpatchdata
			if ( folderName.Equals("tslpatchdata", StringComparison.OrdinalIgnoreCase) )
				return true;

			if ( string.IsNullOrEmpty(analysis.TslPatcherPath) )
			    return false;
			string[] pathParts = analysis.TslPatcherPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			if ( pathParts.Length > 0 && pathParts[0].Equals(folderName, StringComparison.OrdinalIgnoreCase) )
				return true;

			return false;
		}

		/// <summary>
		/// Adds Choose instruction with options from namespaces.ini.
		/// </summary>
		private static void AddNamespacesChooseInstructions(Component component, string archivePath, ArchiveAnalysis analysis, string extractedPath)
		{
			Dictionary<string, Dictionary<string, string>> namespaces =
				IniHelper.ReadNamespacesIniFromArchive(archivePath);

			if ( namespaces == null ||
			     !namespaces.TryGetValue("Namespaces", out Dictionary<string, string> value) )
			{
				return;
			}

			var optionGuids = new List<string>();

			// Create an option for each namespace
			foreach ( string ns in value.Values )
			{
				if ( !namespaces.TryGetValue(ns, out Dictionary<string, string> namespaceData) )
					continue;
				var optionGuid = Guid.NewGuid();
				optionGuids.Add(optionGuid.ToString());

				var option = new Option
				{
					Guid = optionGuid,
					Name = namespaceData.TryGetValue("Name", out string value2) ? value2 : ns,
					Description = namespaceData.TryGetValue("Description", out string value3) ? value3 : string.Empty,
					IsSelected = false
				};

				// Add patcher instruction for this namespace
				string iniName = namespaceData.TryGetValue("IniName", out string value4) ? value4 : "changes.ini";
				string patcherPath = string.IsNullOrEmpty(analysis.TslPatcherPath)
					? extractedPath
					: analysis.TslPatcherPath;

				string executableName = string.IsNullOrEmpty(analysis.PatcherExecutable)
					? "TSLPatcher.exe"
					: Path.GetFileName(analysis.PatcherExecutable);

				var patcherInstruction = new Instruction
				{
					Guid = Guid.NewGuid(),
					Action = Instruction.ActionType.Patcher,
					Source = new List<string> { $@"<<modDirectory>>\{patcherPath}\{executableName}" },
					Destination = "<<kotorDirectory>>",
					Arguments = iniName,
					Overwrite = true
				};
				patcherInstruction.SetParentComponent(option);
				option.Instructions.Add(patcherInstruction);
				component.Options.Add(option);
			}

			// Add Choose instruction
			var chooseInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.Choose,
				Source = optionGuids,
				Overwrite = true
			};
			chooseInstruction.SetParentComponent(component);
			component.Instructions.Add(chooseInstruction);
		}

		/// <summary>
		/// Adds a simple Patcher instruction for changes.ini.
		/// </summary>
		private static void AddSimplePatcherInstruction(Component component, ArchiveAnalysis analysis, string extractedPath)
		{
			string patcherPath = string.IsNullOrEmpty(analysis.TslPatcherPath)
				? extractedPath
				: analysis.TslPatcherPath;

			string executableName = string.IsNullOrEmpty(analysis.PatcherExecutable)
				? "TSLPatcher.exe"
				: Path.GetFileName(analysis.PatcherExecutable);

			var patcherInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.Patcher,
				Source = new List<string> { $@"<<modDirectory>>\{patcherPath}\{executableName}" },
				Destination = "<<kotorDirectory>>",
				Overwrite = true
			};
			patcherInstruction.SetParentComponent(component);
			component.Instructions.Add(patcherInstruction);
		}

		/// <summary>
		/// Adds Choose instruction for multiple folders.
		/// </summary>
		private static void AddMultiFolderChooseInstructions(Component component, string extractedPath, List<string> folders)
		{
			var optionGuids = new List<string>();

			foreach ( string folder in folders )
			{
				var optionGuid = Guid.NewGuid();
				optionGuids.Add(optionGuid.ToString());

				var option = new Option
				{
					Guid = optionGuid,
					Name = folder,
					Description = $"Install files from {folder} folder",
					IsSelected = false
				};

				// Add move instruction for this folder
				var moveInstruction = new Instruction
				{
					Guid = Guid.NewGuid(),
					Action = Instruction.ActionType.Move,
					Source = new List<string> { $@"<<modDirectory>>\{extractedPath}\{folder}\*" },
					Destination = @"<<kotorDirectory>>\Override",
					Overwrite = true
				};
				moveInstruction.SetParentComponent(option);
				option.Instructions.Add(moveInstruction);
				component.Options.Add(option);
			}

			// Add Choose instruction
			var chooseInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.Choose,
				Source = optionGuids,
				Overwrite = true
			};
			chooseInstruction.SetParentComponent(component);
			component.Instructions.Add(chooseInstruction);
		}

		/// <summary>
		/// Adds a simple Move instruction to Override.
		/// </summary>
		/// <param name="component">The component to add the instruction to</param>
		/// <param name="extractedPath">The base extracted path of the archive</param>
		/// <param name="folderName">Folder to move from, or null for flat files</param>
		private static void AddSimpleMoveInstruction(Component component, string extractedPath, string folderName)
		{
			string sourcePath = string.IsNullOrEmpty(folderName)
				? $@"<<modDirectory>>\{extractedPath}\*"
				: $@"<<modDirectory>>\{extractedPath}\{folderName}\*";

			var moveInstruction = new Instruction
			{
				Guid = Guid.NewGuid(),
				Action = Instruction.ActionType.Move,
				Source = new List<string> { sourcePath },
				Destination = @"<<kotorDirectory>>\Override",
				Overwrite = true
			};
			moveInstruction.SetParentComponent(component);
			component.Instructions.Add(moveInstruction);
		}

		private static ArchiveAnalysis AnalyzeArchive(IArchive archive)
		{
			var analysis = new ArchiveAnalysis();

			foreach ( IArchiveEntry entry in archive.Entries )
			{
				if ( entry.IsDirectory )
					continue;

				string path = entry.Key.Replace('\\', '/');
				string[] pathParts = path.Split('/');

				// Check for TSLPatchData folder
				if ( pathParts.Any(p => p.Equals("tslpatchdata", StringComparison.OrdinalIgnoreCase)) )
				{
					analysis.HasTslPatchData = true;

					string fileName = Path.GetFileName(path);
					if ( fileName.Equals("namespaces.ini", StringComparison.OrdinalIgnoreCase) )
					{
						analysis.HasNamespacesIni = true;
						analysis.TslPatcherPath = GetTslPatcherPath(path);
					}
					else if ( fileName.Equals("changes.ini", StringComparison.OrdinalIgnoreCase) )
					{
						analysis.HasChangesIni = true;
						if ( string.IsNullOrEmpty(analysis.TslPatcherPath) )
							analysis.TslPatcherPath = GetTslPatcherPath(path);
					}
					else if ( fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) )
					{
						if ( string.IsNullOrEmpty(analysis.PatcherExecutable) )
							analysis.PatcherExecutable = path;
					}
				}
				else
				{
					// Check for loose files that would go to Override
					string extension = Path.GetExtension(path).ToLowerInvariant();
					if ( !IsGameFile(extension) )
						continue;
					analysis.HasSimpleOverrideFiles = true;

					// Track which folders contain game files
					if ( pathParts.Length == 1 )
					{
						// File is in root of archive (flat structure)
						analysis.HasFlatFiles = true;
					}
					else if ( pathParts.Length >= 2 )
					{
						// File is in a subfolder - track the folder
						string topLevelFolder = pathParts[0];
						if ( !analysis.FoldersWithFiles.Contains(topLevelFolder) )
							analysis.FoldersWithFiles.Add(topLevelFolder);
					}
				}
			}
			return analysis;
		}
		private static string GetTslPatcherPath(string iniPath)
		{
			// Extract the directory containing tslpatchdata
			string[] parts = iniPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			for ( int i = 0; i < parts.Length - 1; i++ )
			{
				if ( parts[i].Equals("tslpatchdata", StringComparison.OrdinalIgnoreCase) )
				{
					// Return the path up to (but not including) tslpatchdata
					return string.Join("/", parts.Take(i));
				}
			}
			return string.Empty;
		}

		private static bool IsGameFile(string extension)
		{
			// Common KOTOR file extensions
			var gameExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				".2da", ".are", ".bik", ".dds",
				".dlg", ".git", ".gui", ".ifo",
				".jrl", ".lip", ".lyt", ".mdl",
				".mdx", ".mp3", ".ncs", ".pth",
				".ssf", ".tga", ".tlk", ".txi",
				".tpc", ".utc", ".utd", ".ute",
				".uti", ".utm", ".utp", ".uts",
				".utw", ".vis", ".wav"
			};

			return gameExtensions.Contains(extension);
		}

		private class ArchiveAnalysis
		{
			public bool HasTslPatchData { get; set; }
			public bool HasNamespacesIni { get; set; }
			public bool HasChangesIni { get; set; }
			public bool HasSimpleOverrideFiles { get; set; }
			public bool HasFlatFiles { get; set; }
			public List<string> FoldersWithFiles { get; set; } = new List<string>();
			public string TslPatcherPath { get; set; } = string.Empty;
			public string PatcherExecutable { get; set; } = string.Empty;
		}
	}
}

