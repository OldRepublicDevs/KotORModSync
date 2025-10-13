// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
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

	public static class AutoInstructionGenerator
	{



		public static bool GenerateInstructions([NotNull] ModComponent component, [NotNull] string archivePath)
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



		private static bool GenerateAllInstructions(ModComponent component, string archivePath, ArchiveAnalysis analysis)
		{
			string archiveFileName = Path.GetFileName(archivePath);
			string extractedPath = archiveFileName.Replace(Path.GetExtension(archiveFileName), "");

			var instructionsToRemove = component.Instructions
				.Where(i => i.Source != null && i.Source.Any(s =>
					s.IndexOf(archiveFileName, StringComparison.OrdinalIgnoreCase) >= 0))
				.ToList();

			foreach ( var instr in instructionsToRemove )
			{
				component.Instructions.Remove(instr);
			}

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

				return false;
			}

			if ( analysis.HasTslPatchData )
			{
				if ( analysis.HasNamespacesIni )
					AddNamespacesChooseInstructions(component, archivePath, analysis, extractedPath);
				else if ( analysis.HasChangesIni )
					AddSimplePatcherInstruction(component, analysis, extractedPath);
			}

			if ( analysis.HasSimpleOverrideFiles )
			{

				var overrideFolders = analysis.FoldersWithFiles
					.Where(f => !IsTslPatcherFolder(f, analysis))
					.ToList();

				if ( overrideFolders.Count > 1 )

					AddMultiFolderChooseInstructions(component, extractedPath, overrideFolders);
				else if ( overrideFolders.Count == 1 )

					AddSimpleMoveInstruction(component, extractedPath, overrideFolders[0]);
				else if ( analysis.HasFlatFiles )

					AddSimpleMoveInstruction(component, extractedPath, null);
			}

			if ( analysis.HasTslPatchData && analysis.HasSimpleOverrideFiles )
				component.InstallationMethod = "Hybrid (TSLPatcher + Loose Files)";
			else if ( analysis.HasTslPatchData )
				component.InstallationMethod = "TSLPatcher";
			else if ( analysis.HasSimpleOverrideFiles )
				component.InstallationMethod = "Loose-File Mod";

			return component.Instructions.Count > 0;
		}

		private static bool IsTslPatcherFolder(string folderName, ArchiveAnalysis analysis)
		{
			if ( string.IsNullOrEmpty(folderName) )
				return false;

			if ( folderName.Equals("tslpatchdata", StringComparison.OrdinalIgnoreCase) )
				return true;

			if ( string.IsNullOrEmpty(analysis.TslPatcherPath) )
				return false;
			string[] pathParts = analysis.TslPatcherPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			if ( pathParts.Length > 0 && pathParts[0].Equals(folderName, StringComparison.OrdinalIgnoreCase) )
				return true;

			return false;
		}

		private static void AddNamespacesChooseInstructions(ModComponent component, string archivePath, ArchiveAnalysis analysis, string extractedPath)
		{
			Dictionary<string, Dictionary<string, string>> namespaces =
				IniHelper.ReadNamespacesIniFromArchive(archivePath);

			if ( namespaces == null ||
				 !namespaces.TryGetValue("Namespaces", out Dictionary<string, string> value) )
			{
				return;
			}

			var optionGuids = new List<string>();

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

		private static void AddSimplePatcherInstruction(ModComponent component, ArchiveAnalysis analysis, string extractedPath)
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

		private static void AddMultiFolderChooseInstructions(ModComponent component, string extractedPath, List<string> folders)
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


		private static void AddSimpleMoveInstruction(ModComponent component, string extractedPath, string folderName)
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

					string extension = Path.GetExtension(path).ToLowerInvariant();
					if ( !IsGameFile(extension) )
						continue;
					analysis.HasSimpleOverrideFiles = true;

					if ( pathParts.Length == 1 )
					{

						analysis.HasFlatFiles = true;
					}
					else if ( pathParts.Length >= 2 )
					{

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

			string[] parts = iniPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			for ( int i = 0; i < parts.Length - 1; i++ )
			{
				if ( parts[i].Equals("tslpatchdata", StringComparison.OrdinalIgnoreCase) )
				{

					return string.Join("/", parts.Take(i));
				}
			}
			return string.Empty;
		}

		private static bool IsGameFile(string extension)
		{

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

