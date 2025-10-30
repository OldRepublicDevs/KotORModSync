// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using JetBrains.Annotations;

using KOTORModSync.Core.FileSystemUtils;
using KOTORModSync.Core.Utility;

using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
namespace KOTORModSync.Core
{
	public sealed class ComponentValidation
	{
		public enum ArchivePathCode
		{
			NotAnArchive,
			PathMissingArchiveName,
			CouldNotOpenArchive,
			NotFoundInArchive,
			FoundSuccessfully,
			NeedsAppendedArchiveName,
			NoArchivesFound,
		}
		[CanBeNull] private readonly List<ModComponent> _componentsList;
		[NotNull] private readonly List<ValidationResult> _validationResults = new List<ValidationResult>();
		[NotNull] public readonly ModComponent ComponentToValidate;
		public ComponentValidation([NotNull] ModComponent component, [CanBeNull] List<ModComponent> componentsList = null)
		{
			ComponentToValidate = component ?? throw new ArgumentNullException(nameof(component));
			if (componentsList is null)
				return;
			_componentsList = new List<ModComponent>(componentsList);
		}
		public bool Run() =>
			VerifyExtractPaths()
			&& ParseDestinationWithAction();
		private void AddError([NotNull] string message, [NotNull] Instruction instruction) =>
			_validationResults.Add(new ValidationResult(this, instruction, message, isError: true));
		private void AddWarning([NotNull] string message, [NotNull] Instruction instruction) =>
			_validationResults.Add(new ValidationResult(this, instruction, message, isError: false));
		[NotNull]
		public List<string> GetErrors() =>
			_validationResults.Where(r => r.IsError).Select(r => r.Message).ToList();
		[NotNull]
		public List<string> GetErrors(int instructionIndex) =>
			_validationResults.Where(r => r.InstructionIndex == instructionIndex && r.IsError).Select(r => r.Message)
				.ToList();
		[NotNull]
		public List<string> GetErrors([CanBeNull] Instruction instruction) =>
			_validationResults.Where(r => r.Instruction == instruction && r.IsError).Select(r => r.Message).ToList();
		[NotNull]
		public List<string> GetWarnings() =>
			_validationResults.Where(r => !r.IsError).Select(r => r.Message).ToList();
		[NotNull]
		public List<string> GetWarnings(int instructionIndex) =>
			_validationResults.Where(r => r.InstructionIndex == instructionIndex && !r.IsError).Select(r => r.Message)
				.ToList();
		[NotNull]
		public List<string> GetWarnings([CanBeNull] Instruction instruction) =>
			_validationResults.Where(r => r.Instruction == instruction && !r.IsError).Select(r => r.Message).ToList();
		private bool VerifyExtractPaths()
		{
			try
			{
				bool success = true;
				IReadOnlyList<string> allArchives = GetAllArchivesFromInstructions();
				if (allArchives.IsNullOrEmptyCollection())
				{
					foreach (Instruction instruction in ComponentToValidate.Instructions)
					{
						if (instruction.Action == Instruction.ActionType.Extract)
						{
							AddError(
								$"Missing Required Archives: [{string.Join(separator: ",", instruction.Source)}]",
								instruction
							);
							success = false;
						}
					}
					return success;
				}
				List<Instruction> instructions = ComponentToValidate.Instructions.ToList();
				foreach (Option thisOption in ComponentToValidate.Options)
				{
					if (thisOption is null)
						continue;
					instructions.AddRange(thisOption.Instructions);
				}
				foreach (Instruction instruction in instructions)
				{
					switch (instruction.Action)
					{
						case Instruction.ActionType.Unset:
							AddError(message: "Action cannot be null", instruction);
							success = false;
							continue;
						case Instruction.ActionType.Extract:
						case Instruction.ActionType.Choose:
							continue;
						case Instruction.ActionType.Execute:
						case Instruction.ActionType.Patcher:
						case Instruction.ActionType.Move:
						case Instruction.ActionType.Copy:
						case Instruction.ActionType.Rename:
						case Instruction.ActionType.Delete:
						case Instruction.ActionType.DelDuplicate:
						case Instruction.ActionType.Run:
						default:
							break;
					}
					if (instruction.Source is null)
					{
						AddWarning(message: "Instruction does not have a 'Source' key defined", instruction);
						success = false;
						continue;
					}
					bool archiveNameFound = true;
					for (int index = 0; index < instruction.Source.Count; index++)
					{
						string sourcePath = PathHelper.FixPathFormatting(instruction.Source[index]);
						if (sourcePath.StartsWith(value: "<<kotorDirectory>>", StringComparison.OrdinalIgnoreCase))
							continue;
						if (sourcePath.EndsWith(value: "tslpatcher.exe", StringComparison.OrdinalIgnoreCase)
							&& instruction.Action != Instruction.ActionType.Patcher)
						{
							AddWarning(
								message:
								"'tslpatcher.exe' used in Source path without the ActionType 'Patcher', was this intentional?",
								instruction
							);
						}
						(bool, bool) result = IsSourcePathInArchives(sourcePath, allArchives, instruction);
						if (!result.Item1 && MainConfig.AttemptFixes)
						{
							string[] parts = sourcePath.Split(Path.DirectorySeparatorChar);
							string duplicatedPart = parts[1] + Path.DirectorySeparatorChar + parts[1];
							string[] remainingParts = parts.Skip(2).ToArray();
							string path = string.Join(
								Path.DirectorySeparatorChar.ToString(),
								new[]
								{
									parts[0], duplicatedPart,
								}.Concat(remainingParts)
							);
							result = IsSourcePathInArchives(path, allArchives, instruction);
							if (result.Item1)
							{
								_ = Logger.LogAsync("Fixing the above issue automatically...");
								var newSource = instruction.Source.ToList();
								newSource[index] = path;
								instruction.Source = newSource;
							}
							success &= result.Item1;
							archiveNameFound &= result.Item2;
						}
					}
					if (!archiveNameFound)
					{
						AddWarning(
							"'Source' path does not include the archive's name as part"
							+ " of the extraction folder, possible FileNotFound exception.",
							instruction
						);
					}
				}
				return success;
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
				return false;
			}
		}
		[NotNull]
		public IReadOnlyList<string> GetAllArchivesFromInstructions()
		{
			List<string> allArchives = new List<string>();
			List<Instruction> instructions = ComponentToValidate.Instructions.ToList();
			foreach (Option thisOption in ComponentToValidate.Options)
			{
				if (thisOption is null)
					continue;
				instructions.AddRange(thisOption.Instructions);
			}
			foreach (Instruction instruction in instructions)
			{
				if (!(_componentsList is null) && !ModComponent.ShouldRunInstruction(instruction, _componentsList))
					continue;
				if (instruction.Action != Instruction.ActionType.Extract)
					continue;
				Services.FileSystem.RealFileSystemProvider realFileSystem = new Services.FileSystem.RealFileSystemProvider();
				List<string> realPaths = PathHelper.EnumerateFilesWithWildcards(
					instruction.Source.Select(UtilityHelper.ReplaceCustomVariables).ToList(),  // DO NOT CHANGE THIS LINE FOR ANY REASON. IT MUST CALL ReplaceCustomVariables() ON THE SOURCE PATHS.
					realFileSystem,
					includeSubFolders: true
				);
				if (realPaths is null)
				{
					AddError(message: "Could not find real paths", instruction);
					continue;
				}
				foreach (string realSourcePath in realPaths)
				{
					if (Path.GetExtension(realSourcePath).Equals(value: ".exe", StringComparison.OrdinalIgnoreCase))
					{
						allArchives.Add(realSourcePath);
						continue;
					}
					if (File.Exists(realSourcePath))
					{
						allArchives.Add(realSourcePath);
						continue;
					}
					AddError("Missing required download:" + $" '{Path.GetFileName(realSourcePath)}'", instruction);
				}
			}
			return allArchives;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private bool ParseDestinationWithAction()
		{
			bool success = true;
			List<Instruction> instructions = ComponentToValidate.Instructions.ToList();
			foreach (Option thisOption in ComponentToValidate.Options)
			{
				if (thisOption is null)
					continue;
				instructions.AddRange(thisOption.Instructions);
			}
			foreach (Instruction instruction in instructions)
			{
				switch (instruction.Action)
				{
					case Instruction.ActionType.Unset:
						continue;
					case Instruction.ActionType.Patcher when string.IsNullOrEmpty(instruction.Destination):
						AddWarning(
							message:
							"Destination must be <<kotorDirectory>> with 'Patcher' action, setting it now automatically.",
							instruction
						);
						instruction.Destination = "<<kotorDirectory>>";
						break;
					case Instruction.ActionType.Patcher when !instruction.Destination.Equals(
						value: "<<kotorDirectory>>",
						StringComparison.OrdinalIgnoreCase
					):
						success = false;
						AddError(
							"'Destination' key must be either null or string literal '<<kotorDirectory>>'"
							+ $" for this action. Got '{instruction.Destination}'",
							instruction
						);
						if (MainConfig.AttemptFixes)
						{
							Logger.Log("Fixing the above issue automatically.");
							instruction.Destination = "<<kotorDirectory>>";
							success = true;
						}
						break;
					case Instruction.ActionType.Choose:
					case Instruction.ActionType.Extract:
					case Instruction.ActionType.Delete:
						if (string.IsNullOrEmpty(instruction.Destination))
							break;
						success = false;
						AddError(
							$"'Destination' key cannot be used with this action. Got '{instruction.Destination}'",
							instruction
						);
						if (MainConfig.AttemptFixes)
						{
							Logger.Log("Fixing the above issue automatically.");
							instruction.Destination = string.Empty;
							success = true;
						}
						break;
					case Instruction.ActionType.Rename:
						if (instruction.Destination.Equals(
								$"<<kotorDirectory>>{Path.DirectorySeparatorChar}Override",
								StringComparison.OrdinalIgnoreCase
							))
						{
							success = false;
							AddError(
								"Incorrect 'Destination' format."
								+ $" Got '{instruction.Destination}',"
								+ " expected a filename.",
								instruction
							);
						}
						break;
					case Instruction.ActionType.Run:
					case Instruction.ActionType.Execute:
						break;
					default:
						string destinationPath = null;
						if (!string.IsNullOrEmpty(instruction.Destination))
						{
							destinationPath = PathHelper.FixPathFormatting(
								UtilityHelper.ReplaceCustomVariables(instruction.Destination)
							);
							if (MainConfig.CaseInsensitivePathing && !Directory.Exists(destinationPath))
								destinationPath = PathHelper.GetCaseSensitivePath(destinationPath).Item1;
						}
						if (string.IsNullOrWhiteSpace(destinationPath)
							|| !PathValidator.IsValidPath(destinationPath)
							|| !Directory.Exists(destinationPath))
						{
							success = false;
							AddError($"Destination cannot be found! Got '{destinationPath}'", instruction);
							if (MainConfig.AttemptFixes && PathValidator.IsValidPath(destinationPath))
							{
								Logger.Log("Fixing the above error automatically...");
								try
								{
									_ = Directory.CreateDirectory(destinationPath);
									success = true;
								}
								catch (Exception e)
								{
									Logger.LogException(e);
									AddError(e.Message, instruction);
									success = false;
								}
							}
						}
						break;
				}
			}
			return success;
		}
		[NotNull]
		private static string GetErrorDescription(ArchivePathCode code)
		{
			switch (code)
			{
				case ArchivePathCode.FoundSuccessfully:
					return "File successfully found in archive.";
				case ArchivePathCode.NotAnArchive:
					return "Not an archive";
				case ArchivePathCode.PathMissingArchiveName:
					return "Missing archive name in path";
				case ArchivePathCode.CouldNotOpenArchive:
					return "Could not open archive";
				case ArchivePathCode.NotFoundInArchive:
					return "Not found in archive";
				case ArchivePathCode.NoArchivesFound:
					return "No archives found/no extract instructions created";
				case ArchivePathCode.NeedsAppendedArchiveName:
				default:
					return "Unknown error";
			}
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private (bool, bool) IsSourcePathInArchives(
			[NotNull] string sourcePath,
			[NotNull] IReadOnlyList<string> allArchives,
			[NotNull] Instruction instruction
		)
		{
			if (sourcePath is null)
				throw new ArgumentNullException(nameof(sourcePath));
			if (allArchives is null)
				throw new ArgumentNullException(nameof(allArchives));
			if (instruction is null)
				throw new ArgumentNullException(nameof(instruction));
			bool foundInAnyArchive = false;
			bool hasError = false;
			bool archiveNameFound = false;
			StringBuilder errorDescription = new StringBuilder();
			sourcePath = PathHelper.FixPathFormatting(sourcePath)
				.Replace($"<<modDirectory>>{Path.DirectorySeparatorChar}", newValue: "").Replace(
					$"<<kotorDirectory>>{Path.DirectorySeparatorChar}",
					newValue: ""
				);
			foreach (string archivePath in allArchives)
			{
				if (archivePath is null)
				{
					AddError(message: "Archive is not a valid file path", instruction);
					continue;
				}
				string archiveName = Path.GetFileNameWithoutExtension(archivePath);
				string[] pathParts = sourcePath.Split(Path.DirectorySeparatorChar);
				archiveNameFound = PathHelper.WildcardPathMatch(archiveName, pathParts[0]);
				ArchivePathCode code = IsPathInArchive(sourcePath, archivePath);
				if (code == ArchivePathCode.FoundSuccessfully)
				{
					foundInAnyArchive = true;
					break;
				}
				if (code == ArchivePathCode.NotFoundInArchive)
				{
					continue;
				}
				hasError = true;
				errorDescription.AppendLine(GetErrorDescription(code));
			}
			if (hasError)
			{
				AddError($"Invalid source path '{sourcePath}'. Reason: {errorDescription.ToString()}", instruction);
				return (false, archiveNameFound);
			}
			if (_componentsList is null)
			{
				AddError(new NullReferenceException(nameof(_componentsList)).Message, instruction);
				return (false, archiveNameFound);
			}
			if (foundInAnyArchive || !ModComponent.ShouldRunInstruction(instruction, _componentsList))
			{
				return (true, true);
			}
			if (ComponentToValidate.Name.Equals(value: "Improved AI", StringComparison.OrdinalIgnoreCase))
			{
				return (true, true);
			}
			if (!ModComponent.ShouldRunInstruction(instruction, _componentsList))
			{
				return (true, true);
			}
			AddError($"Failed to find '{sourcePath}' in any archives!", instruction);
			return (false, archiveNameFound);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private static ArchivePathCode IsPathInArchive([NotNull] string relativePath, [NotNull] string archivePath)
		{
			if (relativePath is null)
				throw new ArgumentNullException(nameof(relativePath));
			if (archivePath is null)
				throw new ArgumentNullException(nameof(archivePath));
			if (!ArchiveHelper.IsArchive(archivePath))
				return ArchivePathCode.NotAnArchive;
			if (Path.GetExtension(archivePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
				return ArchivePathCode.FoundSuccessfully;

			try
			{
				using (FileStream stream = File.OpenRead(archivePath))
				{
					IArchive archive = null;
					if (archivePath.EndsWith(value: ".zip", StringComparison.OrdinalIgnoreCase))
					{
						archive = ZipArchive.Open(stream);
					}
					else if (archivePath.EndsWith(value: ".rar", StringComparison.OrdinalIgnoreCase))
					{
						archive = RarArchive.Open(stream);
					}
					else if (archivePath.EndsWith(value: ".7z", StringComparison.OrdinalIgnoreCase))
					{
						archive = SevenZipArchive.Open(stream);
					}
					if (archive is null)
						return ArchivePathCode.CouldNotOpenArchive;
					string archiveNameAppend = Path.GetFileNameWithoutExtension(archivePath);
					if (PathHelper.WildcardPathMatch(archiveNameAppend, relativePath))
						return ArchivePathCode.FoundSuccessfully;

					HashSet<string> folderPaths = new HashSet<string>(StringComparer.Ordinal);
					StringBuilder pathBuilder = new StringBuilder();
					foreach (IArchiveEntry entry in archive.Entries)
					{
						pathBuilder.Clear();
						pathBuilder.Append(archiveNameAppend);
						pathBuilder.Append(Path.DirectorySeparatorChar);
						pathBuilder.Append(entry.Key.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
						string itemInArchivePath = pathBuilder.ToString();
						string folderName = Path.GetFileName(itemInArchivePath);
						if (entry.IsDirectory)
						{
							folderName = Path.GetDirectoryName(itemInArchivePath);
						}
						if (!string.IsNullOrEmpty(folderName))
						{
							_ = folderPaths.Add(
								folderName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
							);
						}
						if (PathHelper.WildcardPathMatch(itemInArchivePath, relativePath))
							return ArchivePathCode.FoundSuccessfully;
					}
					foreach (string folderPath in folderPaths)
					{
						if (!(folderPath is null) && PathHelper.WildcardPathMatch(folderPath, relativePath))
							return ArchivePathCode.FoundSuccessfully;
					}
				}
				return ArchivePathCode.NotFoundInArchive;
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, $"Failed to validate archive '{Path.GetFileName(archivePath)}'");
				return ArchivePathCode.CouldNotOpenArchive;
			}
		}
	}
}