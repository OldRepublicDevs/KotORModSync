// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Parsing
{
	public sealed class MarkdownParser
	{
		private readonly MarkdownImportProfile _profile;
		private readonly Action<string> _logInfo;
		private readonly Action<string> _logVerbose;

		public MarkdownParser([NotNull] MarkdownImportProfile profile, [CanBeNull] Action<string> logInfo = null, [CanBeNull] Action<string> logVerbose = null)
		{
			_profile = profile ?? throw new ArgumentNullException(nameof(profile));
			_logInfo = logInfo ?? (_ => { });
			_logVerbose = logVerbose ?? (_ => { });
		}

		[NotNull]
		public MarkdownParserResult Parse([NotNull] string markdown)
		{
			if ( markdown == null )
				throw new ArgumentNullException(nameof(markdown));

			_logInfo("Starting markdown parsing...");
			_logVerbose($"Markdown content length: {markdown.Length} characters");

			var components = new List<ModComponent>();
			var warnings = new List<string>();

			int originalLength = markdown.Length;
			markdown = MarkdownUtilities.ExtractModListSection(markdown);
			if ( markdown.Length < originalLength )
			{
				_logVerbose($"Found '## Mod List' marker, parsing content after it ({markdown.Length} characters)");
			}
			else
			{
				_logVerbose("No '## Mod List' marker found, parsing entire document");
			}

			_logVerbose($"Using outer pattern to split sections: {_profile.ComponentSectionPattern}");

			var outerRegex = new Regex(
				_profile.ComponentSectionPattern,
				_profile.ComponentSectionOptions
			);

			MatchCollection outerMatches = outerRegex.Matches(markdown);
			_logInfo($"Found {outerMatches.Count} component sections using outer pattern");

			int componentIndex = 0;

			foreach ( Match outerMatch in outerMatches )
			{
				componentIndex++;
				_logVerbose($"Processing component section {componentIndex}/{outerMatches.Count}");

				string sectionText = outerMatch.Value;
				_logVerbose($"ModComponent {componentIndex} section length: {sectionText.Length} characters");

				ModComponent component = ParseComponentFromText(sectionText, out string warning, componentIndex);

				if ( component != null )
				{
					components.Add(component);
					_logVerbose($"Successfully parsed component {componentIndex}: '{component.Name}' by {component.Author}");
				}
				else if ( !string.IsNullOrEmpty(warning) )
				{
					warnings.Add($"ModComponent {componentIndex}: {warning}");
					_logVerbose($"Warning for component {componentIndex}: {warning}");
				}
				else
				{
					_logVerbose($"ModComponent {componentIndex} resulted in null component with no warning");
				}
			}

			_logInfo($"Parsing completed. Successfully parsed {components.Count} components with {warnings.Count} warnings");
			foreach ( ModComponent component in components )
			{
				int linkCount = component.ModLink?.Count ?? 0;
				_logVerbose($"  - '{component.Name}' by {component.Author} ({component.Category}/{component.Tier}) with {linkCount} links");
			}

			return new MarkdownParserResult
			{
				Components = components,
				Warnings = warnings,
				Metadata = _profile.Metadata,
			};
		}

		[CanBeNull]
		private ModComponent ParseComponentFromText([NotNull] string componentText, out string warning, int componentIndex)
		{
			warning = string.Empty;
			if ( string.IsNullOrWhiteSpace(componentText) )
			{
				warning = "ModComponent text is null or whitespace";
				return null;
			}

			_logVerbose($"  Parsing component {componentIndex} text content...");

			var component = new ModComponent
			{
				Guid = Guid.NewGuid(),
			};

			string extractedName = ExtractValue(componentText, _profile.NamePattern, "name|name_plain|name_link");
			if ( extractedName != null )
			{
				component.Name = extractedName;
				_logVerbose($"  Extracted Name: '{extractedName}'");
			}
			else
			{
				_logVerbose($"  No name found using pattern: {_profile.NamePattern}");
			}

			string extractedAuthor = ExtractValue(componentText, _profile.AuthorPattern, "author");
			if ( extractedAuthor != null )
			{
				component.Author = extractedAuthor;
				_logVerbose($"  Extracted Author: '{extractedAuthor}'");
			}
			else
			{
				_logVerbose($"  No author found using pattern: {_profile.AuthorPattern}");
			}

			string extractedDescription = ExtractValue(componentText, _profile.DescriptionPattern, "description");
			if ( extractedDescription != null )
			{
				component.Description = extractedDescription;
				_logVerbose($"  Extracted Description: '{extractedDescription.Substring(0, Math.Min(100, extractedDescription.Length))}...'");
			}
			else
			{
				_logVerbose($"  No description found using pattern: {_profile.DescriptionPattern}");
			}

			string extractedMethod = ExtractValue(componentText, _profile.InstallationMethodPattern, "method");
			if ( extractedMethod != null )
			{
				component.InstallationMethod = extractedMethod;
				_logVerbose($"  Extracted Installation Method: '{extractedMethod}'");
			}
			else
			{
				_logVerbose($"  No installation method found using pattern: {_profile.InstallationMethodPattern}");
			}

			string extractedDirections = ExtractValue(componentText, _profile.InstallationInstructionsPattern, "directions");
			if ( extractedDirections != null )
			{
				component.Directions = extractedDirections;
				_logVerbose($"  Extracted Directions: '{extractedDirections.Substring(0, Math.Min(100, extractedDirections.Length))}...'");
			}
			else
			{
				_logVerbose($"  No directions found using pattern: {_profile.InstallationInstructionsPattern}");
			}

			MatchCollection modLinkMatches = string.IsNullOrWhiteSpace(_profile.ModLinkPattern)
				? null
				: Regex.Matches(componentText, _profile.ModLinkPattern, RegexOptions.Compiled | RegexOptions.Multiline);
			if ( !(modLinkMatches is null) && modLinkMatches.Count > 0 )
			{
				component.ModLink = modLinkMatches.Cast<Match>()
					.Select(m => m.Groups["link"].Value.Trim())
					.Where(l => !string.IsNullOrEmpty(l))
					.Distinct()
					.ToList();
				_logVerbose($"  Extracted {component.ModLink.Count} mod links");
			}
			else
			{
				_logVerbose($"  No mod links found using pattern: {_profile.ModLinkPattern}");
			}

			if ( !string.IsNullOrWhiteSpace(_profile.CategoryTierPattern) )
			{
				Match categoryTierMatch = Regex.Match(
					componentText,
					_profile.CategoryTierPattern,
					RegexOptions.Compiled | RegexOptions.Multiline
				);
				if ( categoryTierMatch.Success )
				{
					string categoryStr = categoryTierMatch.Groups["category"].Value.Trim();
					_logVerbose($"  [DEBUG] Raw categoryStr: '{categoryStr}' (type: {categoryStr?.GetType().Name})");

					string normalizedCategory = MarkdownUtilities.NormalizeCategoryFormat(categoryStr);
					_logVerbose($"  [DEBUG] Normalized: '{normalizedCategory}'");

					var splitResult = normalizedCategory.Split(
						new[] { "&" },
						StringSplitOptions.RemoveEmptyEntries
					).Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();

					_logVerbose($"  [DEBUG] Split result count: {splitResult.Count}");
					if ( splitResult.Count > 0 )
					{
						_logVerbose($"  [DEBUG] First element: '{splitResult[0]}' (type: {splitResult[0]?.GetType().FullName})");
					}

					component.Category = splitResult;

					component.Tier = categoryTierMatch.Groups["tier"].Value.Trim();
					_logVerbose($"  Extracted Category/Tier: '{string.Join(" & ", component.Category)}'/'{component.Tier}'");
				}
				else
				{
					_logVerbose($"  No category/tier found using pattern: {_profile.CategoryTierPattern}");
				}
			}

			if ( !string.IsNullOrWhiteSpace(_profile.NonEnglishPattern) )
			{
				string nonEnglish = ExtractValue(componentText, _profile.NonEnglishPattern, "value");
				if ( !string.IsNullOrWhiteSpace(nonEnglish) )
				{
					component.Language = new List<string> { nonEnglish };
					_logVerbose($"  Extracted Non-English value: '{nonEnglish}'");
				}
				else
				{
					_logVerbose($"  No non-English value found using pattern: {_profile.NonEnglishPattern}");
				}
			}

			if ( string.IsNullOrWhiteSpace(component.Name) )
			{
				warning = "ModComponent has no name";
				_logVerbose($"  ModComponent {componentIndex} rejected: no name found");
				return null;
			}

			string modSyncMetadata = ExtractModSyncMetadata(componentText);
			if ( !string.IsNullOrWhiteSpace(modSyncMetadata) )
			{
				_logVerbose($"  Found ModSync metadata block, parsing...");
				try
				{
					ParseModSyncMetadata(component, modSyncMetadata);
					_logVerbose($"  Successfully parsed ModSync metadata: {component.Instructions.Count} instructions, {component.Options.Count} options");
				}
				catch ( Exception ex )
				{
					_logVerbose($"  Warning: Failed to parse ModSync metadata: {ex.Message}");
					warning = $"Failed to parse ModSync metadata: {ex.Message}";
				}
			}

			return component;
		}

		[CanBeNull]
		private static string ExtractValue([NotNull] string source, [CanBeNull] string pattern, [NotNull] string groupNames)
		{
			if ( string.IsNullOrWhiteSpace(pattern) )
				return null;

			Match match = Regex.Match(source, pattern, RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
			if ( !match.Success )
				return null;

			foreach ( var groupName in groupNames.Split('|') )
			{
				var value = match.Groups[groupName.Trim()]?.Value;
				if ( !string.IsNullOrWhiteSpace(value) )
					return value.Trim();
			}
			return null;
		}

		[CanBeNull]
		private string ExtractModSyncMetadata([NotNull] string componentText)
		{
			if ( string.IsNullOrWhiteSpace(componentText) )
				return null;

			string pattern = !string.IsNullOrWhiteSpace(_profile.InstructionsBlockPattern)
				? _profile.InstructionsBlockPattern
				: @"<!--<<ModSync>>\s*(?<instructions>[\s\S]*?)-->";

			Match match = Regex.Match(
				componentText,
				pattern,
				RegexOptions.Singleline | RegexOptions.IgnoreCase
			);

			return match.Success ? match.Groups["instructions"].Value.Trim() : null;
		}

		private void ParseModSyncMetadata([NotNull] ModComponent component, [NotNull] string metadataText)
		{
			if ( string.IsNullOrWhiteSpace(metadataText) )
				return;

			bool isYaml = DetectYamlFormat(metadataText);
			bool isToml = DetectTomlFormat(metadataText);

			if ( isYaml )
			{
				_logVerbose($"    Detected YAML format, attempting to deserialize...");
				try
				{
					ModComponent yamlComponent = ModComponent.DeserializeYAMLComponent(metadataText);
					if ( yamlComponent != null )
					{

						MergeComponentMetadata(component, yamlComponent);
						_logVerbose($"    Successfully parsed YAML: {component.Instructions.Count} instructions, {component.Options.Count} options");
						return;
					}
				}
				catch ( Exception ex )
				{
					_logVerbose($"    Warning: YAML parsing failed, falling back to legacy parser: {ex.Message}");
				}
			}
			else if ( isToml )
			{
				_logVerbose($"    Detected TOML format, attempting to deserialize...");
				try
				{

					string tomlString = metadataText;
					if ( !tomlString.Contains("[[thisMod]]") && !tomlString.Contains("[thisMod]") )
					{
						tomlString = "[[thisMod]]\n" + metadataText;
					}

					ModComponent tomlComponent = ModComponent.DeserializeTomlComponent(tomlString);
					if ( tomlComponent != null )
					{

						MergeComponentMetadata(component, tomlComponent);
						_logVerbose($"    Successfully parsed TOML: {component.Instructions.Count} instructions, {component.Options.Count} options");
						return;
					}
				}
				catch ( Exception ex )
				{
					_logVerbose($"    Warning: TOML parsing failed, falling back to legacy parser: {ex.Message}");
				}
			}

			_logVerbose($"    Using legacy markdown format parser");
			ParseLegacyMarkdownMetadata(component, metadataText);
		}

		private static bool DetectYamlFormat([NotNull] string text)
		{

			return Regex.IsMatch(text, @"^\s*Guid:\s*[a-f0-9\-]+", RegexOptions.Multiline | RegexOptions.IgnoreCase)
				   && Regex.IsMatch(text, @"^\s*Instructions:\s*$", RegexOptions.Multiline)
				   && !text.Contains("**");
		}

		private static bool DetectTomlFormat([NotNull] string text)
		{

			return text.Contains("[[thisMod]]") || Regex.IsMatch(text, @"^\s*\w+\s*=", RegexOptions.Multiline);
		}

		private static void MergeComponentMetadata([NotNull] ModComponent target, [NotNull] ModComponent source)
		{

			if ( source.Guid != Guid.Empty )
			{
				target.Guid = source.Guid;
			}

			if ( source.Instructions.Count > 0 )
			{
				target.Instructions = source.Instructions;
			}

			if ( source.Options.Count > 0 )
			{
				target.Options = source.Options;
			}

			if ( source.Dependencies.Count > 0 )
			{
				target.Dependencies = source.Dependencies;
			}

			if ( source.Restrictions.Count > 0 )
			{
				target.Restrictions = source.Restrictions;
			}
		}

		private void ParseLegacyMarkdownMetadata([NotNull] ModComponent component, [NotNull] string metadataText)
		{
			string[] lines = metadataText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			int instructionsStart = FindSectionStart(lines, "#### Instructions");
			int optionsStart = FindSectionStart(lines, "#### Options");

			int headerEnd = instructionsStart >= 0 ? instructionsStart : lines.Length;
			for ( int i = 0; i < headerEnd; i++ )
			{
				string line = lines[i].Trim();
				if ( line.StartsWith("- **GUID:**") )
				{
					string guidStr = ExtractMetadataValue(line, "GUID");
					if ( Guid.TryParse(guidStr, out Guid guid) )
					{
						component.Guid = guid;
						_logVerbose($"    Parsed component GUID: {guid}");
						break;
					}
				}
			}

			if ( instructionsStart >= 0 )
			{
				int instructionsEnd = optionsStart >= 0 ? optionsStart : lines.Length;
				component.Instructions = ParseInstructions(lines, instructionsStart + 1, instructionsEnd, component);
				_logVerbose($"    Parsed {component.Instructions.Count} instructions");
			}

			if ( optionsStart >= 0 )
			{
				component.Options = ParseOptions(lines, optionsStart + 1, lines.Length);
				_logVerbose($"    Parsed {component.Options.Count} options");
			}
		}

		private static int FindSectionStart([NotNull] string[] lines, [NotNull] string sectionHeader)
		{
			for ( int i = 0; i < lines.Length; i++ )
			{
				if ( lines[i].Trim() == sectionHeader )
					return i;
			}
			return -1;
		}

		[NotNull]
		private ObservableCollection<Instruction> ParseInstructions([NotNull] string[] lines, int startIdx, int endIdx, [NotNull] ModComponent parentComponent)
		{
			var instructions = new ObservableCollection<Instruction>();

			for ( int i = startIdx; i < endIdx; i++ )
			{
				string line = lines[i].Trim();

				if ( Regex.IsMatch(line, @"^\d+\.\s+\*\*GUID:\*\*") )
				{
					var instruction = new Instruction();

					string guidStr = ExtractMetadataValue(line, "GUID");
					if ( Guid.TryParse(guidStr, out Guid guid) )
					{
						instruction.Guid = guid;
					}

					for ( int j = i + 1; j < endIdx; j++ )
					{
						string propLine = lines[j].Trim();

						if ( Regex.IsMatch(propLine, @"^\d+\.\s+\*\*GUID:\*\*") || propLine.StartsWith("#") )
							break;

						if ( propLine.StartsWith("**Action:**") )
						{
							string actionStr = ExtractMetadataValue(propLine, "Action");
							if ( Enum.TryParse<Instruction.ActionType>(actionStr, true, out var action) )
							{
								instruction.Action = action;
							}
						}
						else if ( propLine.StartsWith("**Overwrite:**") )
						{
							string overwriteStr = ExtractMetadataValue(propLine, "Overwrite");
							if ( bool.TryParse(overwriteStr, out bool overwrite) )
							{
								instruction.Overwrite = overwrite;
							}
						}
						else if ( propLine.StartsWith("**Source:**") )
						{
							string sourceStr = ExtractMetadataValue(propLine, "Source");
							instruction.Source = sourceStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
								.Select(s => s.Trim())
								.ToList();
						}
						else if ( propLine.StartsWith("**Destination:**") )
						{
							instruction.Destination = ExtractMetadataValue(propLine, "Destination");
						}

						i = j;
					}

					instruction.SetParentComponent(parentComponent);
					instructions.Add(instruction);
					_logVerbose($"      Parsed instruction: Action={instruction.Action}, GUID={instruction.Guid}");
				}
			}

			return instructions;
		}

		[NotNull]
		private ObservableCollection<Option> ParseOptions([NotNull] string[] lines, int startIdx, int endIdx)
		{
			var options = new ObservableCollection<Option>();
			Option currentOption = null;

			for ( int i = startIdx; i < endIdx; i++ )
			{
				string line = lines[i].Trim();

				if ( Regex.IsMatch(line, @"^#+\s+Option\s+\d+", RegexOptions.IgnoreCase) )
				{

					if ( currentOption != null )
					{
						options.Add(currentOption);
						_logVerbose($"      Parsed option: Name={currentOption.Name}, GUID={currentOption.Guid}");
					}

					currentOption = new Option
					{
						Guid = Guid.NewGuid()
					};
					continue;
				}

				if ( currentOption == null )
					continue;

				if ( line.StartsWith("- **GUID:**") )
				{
					string guidStr = ExtractMetadataValue(line, "GUID");
					if ( Guid.TryParse(guidStr, out Guid guid) )
					{
						currentOption.Guid = guid;
					}
				}
				else if ( line.StartsWith("- **Name:**") )
				{
					currentOption.Name = ExtractMetadataValue(line, "Name");
				}
				else if ( line.StartsWith("- **Description:**") )
				{
					currentOption.Description = ExtractMetadataValue(line, "Description");
				}
				else if ( line.StartsWith("- **Is Selected:**") )
				{
					string selectedStr = ExtractMetadataValue(line, "Is Selected");
					if ( bool.TryParse(selectedStr, out bool isSelected) )
					{
						currentOption.IsSelected = isSelected;
					}
				}
				else if ( line.StartsWith("- **Install State:**") )
				{
					string stateStr = ExtractMetadataValue(line, "Install State");
					if ( int.TryParse(stateStr, out int state) )
					{
						currentOption.InstallState = (ModComponent.ComponentInstallState)state;
					}
				}
				else if ( line.StartsWith("- **Is Downloaded:**") )
				{
					string downloadedStr = ExtractMetadataValue(line, "Is Downloaded");
					if ( bool.TryParse(downloadedStr, out bool isDownloaded) )
					{
						currentOption.IsDownloaded = isDownloaded;
					}
				}
				else if ( line.StartsWith("- **Restrictions:**") )
				{
					string restrictionsStr = ExtractMetadataValue(line, "Restrictions");
					currentOption.Restrictions = restrictionsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(s => s.Trim())
						.Where(s => Guid.TryParse(s, out _))
						.Select(s => Guid.Parse(s))
						.ToList();
				}
				else if ( line.Contains("- **Instruction:**") )
				{

					var optionInstructions = ParseOptionsInstructions(lines, ref i, endIdx, currentOption);
					foreach ( var inst in optionInstructions )
					{
						currentOption.Instructions.Add(inst);
					}
				}
			}

			if ( currentOption != null )
			{
				options.Add(currentOption);
				_logVerbose($"      Parsed option: Name={currentOption.Name}, GUID={currentOption.Guid}");
			}

			return options;
		}

		[NotNull]
		private static List<Instruction> ParseOptionsInstructions([NotNull] string[] lines, ref int currentIdx, int endIdx, [NotNull] Option parentOption)
		{
			var instructions = new List<Instruction>();
			var currentInstruction = new Instruction();

			for ( int i = currentIdx + 1; i < endIdx; i++ )
			{
				string line = lines[i].Trim();

				if ( Regex.IsMatch(line, @"^#+\s+Option\s+\d+", RegexOptions.IgnoreCase) ||
					 (line.StartsWith("- **") && !line.Contains("**GUID:**") && !line.Contains("**Action:**") &&
					  !line.Contains("**Destination:**") && !line.Contains("**Overwrite:**") && !line.Contains("**Source:**")) )
				{
					currentIdx = i - 1;
					break;
				}

				if ( line.StartsWith("- **GUID:**") )
				{
					string guidStr = ExtractMetadataValue(line, "GUID");
					if ( Guid.TryParse(guidStr, out Guid guid) )
					{
						currentInstruction.Guid = guid;
					}
				}
				else if ( line.StartsWith("- **Action:**") )
				{
					string actionStr = ExtractMetadataValue(line, "Action");
					if ( Enum.TryParse<Instruction.ActionType>(actionStr, true, out var action) )
					{
						currentInstruction.Action = action;
					}
				}
				else if ( line.StartsWith("- **Destination:**") )
				{
					currentInstruction.Destination = ExtractMetadataValue(line, "Destination");
				}
				else if ( line.StartsWith("- **Overwrite:**") )
				{
					string overwriteStr = ExtractMetadataValue(line, "Overwrite");
					if ( bool.TryParse(overwriteStr, out bool overwrite) )
					{
						currentInstruction.Overwrite = overwrite;
					}
				}
				else if ( line.StartsWith("- **Source:**") )
				{
					string sourceStr = ExtractMetadataValue(line, "Source");
					currentInstruction.Source = sourceStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(s => s.Trim())
						.ToList();
				}

				currentIdx = i;
			}

			if ( currentInstruction.Guid != Guid.Empty )
			{
				currentInstruction.SetParentComponent(parentOption);
				instructions.Add(currentInstruction);
			}

			return instructions;
		}

		[NotNull]
		private static string ExtractMetadataValue([NotNull] string line, [NotNull] string key)
		{

			string pattern = $@"\*\*{Regex.Escape(key)}:\*\*\s*(.+)$";
			Match match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
			return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
		}
	}
}

