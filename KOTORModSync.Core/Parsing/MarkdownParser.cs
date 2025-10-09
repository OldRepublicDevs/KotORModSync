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

			// Extract the mod list section using utility method
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

			// ALWAYS use outer pattern first to split into sections, then parse each section
			_logVerbose($"Using outer pattern to split sections: {_profile.ComponentSectionPattern}");

			var outerRegex = new Regex(
				_profile.ComponentSectionPattern,
				_profile.ComponentSectionOptions
			);

			MatchCollection outerMatches = outerRegex.Matches(markdown);
			_logInfo($"Found {outerMatches.Count} component sections using outer pattern");

			int componentIndex = 0;

			// FOREACH over each outer section
			foreach ( Match outerMatch in outerMatches )
			{
				componentIndex++;
				_logVerbose($"Processing component section {componentIndex}/{outerMatches.Count}");

				// Get the text of this section
				string sectionText = outerMatch.Value;
				_logVerbose($"ModComponent {componentIndex} section length: {sectionText.Length} characters");

				// Parse this section using individual patterns
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

			// NOTE: Auto-generation of instructions from downloads should be done AFTER parsing
			// by calling DownloadCacheService.ResolveOrDownloadAsync() for each component.
			// MarkdownParser only parses the markdown structure - it doesn't download files.
			// The caller (FileLoadingService, tests, etc.) is responsible for calling
			// DownloadCacheService to download archives and auto-create instructions.

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

			// Extract Name (check both 'name' for linked names and 'name_plain' for plain text)
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

			// Extract Author
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

			// Extract Description
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

			// Extract Installation Method
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

			// Extract Installation Instructions
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

			// Extract ModLinks
			MatchCollection modLinkMatches = string.IsNullOrWhiteSpace(_profile.ModLinkPattern)
				? null
				: Regex.Matches(componentText, _profile.ModLinkPattern, RegexOptions.Compiled | RegexOptions.Multiline);
			if ( !(modLinkMatches is null) && modLinkMatches.Count > 0 )
			{
				component.ModLink = modLinkMatches.Cast<Match>()
					.Select(m => m.Groups["link"].Value.Trim())
					.Where(l => !string.IsNullOrEmpty(l))
					.Select(link =>
					{
						//HACK: Prepend base URL for relative paths starting with /
						if ( link.StartsWith("/") )
							return "https://kotor.neocities.org" + link;
						return link;
					})
					.Distinct()
					.ToList();
				_logVerbose($"  Extracted {component.ModLink.Count} mod links");
			}
			else
			{
				_logVerbose($"  No mod links found using pattern: {_profile.ModLinkPattern}");
			}

			// Extract Category and Tier
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

					// Normalize category format first (replaces commas with ampersands, normalizes whitespace)
					string normalizedCategory = MarkdownUtilities.NormalizeCategoryFormat(categoryStr);
					_logVerbose($"  [DEBUG] Normalized: '{normalizedCategory}'");

					// Split categories by ampersand (now that commas are replaced)
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

			// Extract Non-English Functionality
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

			// Validation
			if ( string.IsNullOrWhiteSpace(component.Name) )
			{
				warning = "ModComponent has no name";
				_logVerbose($"  ModComponent {componentIndex} rejected: no name found");
				return null;
			}

			// Check for and parse ModSync metadata
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

			// Support multiple group names separated by | (pipe)
			foreach ( var groupName in groupNames.Split('|') )
			{
				var value = match.Groups[groupName.Trim()]?.Value;
				if ( !string.IsNullOrWhiteSpace(value) )
					return value.Trim();
			}
			return null;
		}

		/// <summary>
		/// Extracts the ModSync metadata block from the component text.
		/// Format: &lt;!--&lt;&lt;ModSync&gt;&gt; ... --&gt;
		/// </summary>
		[CanBeNull]
		private static string ExtractModSyncMetadata([NotNull] string componentText)
		{
			if ( string.IsNullOrWhiteSpace(componentText) )
				return null;

			// Match HTML comment with ModSync marker
			Match match = Regex.Match(
				componentText,
				@"<!--\s*<<ModSync>>\s*\n(.*?)\n\s*-->",
				RegexOptions.Singleline | RegexOptions.IgnoreCase
			);

			return match.Success ? match.Groups[1].Value : null;
		}

		/// <summary>
		/// Parses the ModSync metadata block and populates the component's instructions and options.
		/// </summary>
		private void ParseModSyncMetadata([NotNull] ModComponent component, [NotNull] string metadataText)
		{
			if ( string.IsNullOrWhiteSpace(metadataText) )
				return;

			string[] lines = metadataText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

			// Find section boundaries first
			int instructionsStart = FindSectionStart(lines, "#### Instructions");
			int optionsStart = FindSectionStart(lines, "#### Options");

			// Parse GUID at component level (only in header before Instructions section)
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
						break; // Only take the first GUID in the header
					}
				}
			}

			// Parse Instructions section
			if ( instructionsStart >= 0 )
			{
				int instructionsEnd = optionsStart >= 0 ? optionsStart : lines.Length;
				component.Instructions = ParseInstructions(lines, instructionsStart + 1, instructionsEnd, component);
				_logVerbose($"    Parsed {component.Instructions.Count} instructions");
			}

			// Parse Options section
			if ( optionsStart >= 0 )
			{
				component.Options = ParseOptions(lines, optionsStart + 1, lines.Length);
				_logVerbose($"    Parsed {component.Options.Count} options");
			}
		}

		/// <summary>
		/// Finds the starting line index of a section header.
		/// </summary>
		private static int FindSectionStart([NotNull] string[] lines, [NotNull] string sectionHeader)
		{
			for ( int i = 0; i < lines.Length; i++ )
			{
				if ( lines[i].Trim() == sectionHeader )
					return i;
			}
			return -1;
		}

		/// <summary>
		/// Parses instructions from the metadata block.
		/// </summary>
		[NotNull]
		private ObservableCollection<Instruction> ParseInstructions([NotNull] string[] lines, int startIdx, int endIdx, [NotNull] ModComponent parentComponent)
		{
			var instructions = new ObservableCollection<Instruction>();

			for ( int i = startIdx; i < endIdx; i++ )
			{
				string line = lines[i].Trim();

				// Look for numbered instruction lines like "1. **GUID:**"
				if ( Regex.IsMatch(line, @"^\d+\.\s+\*\*GUID:\*\*") )
				{
					var instruction = new Instruction();

					// Parse instruction properties on subsequent lines
					string guidStr = ExtractMetadataValue(line, "GUID");
					if ( Guid.TryParse(guidStr, out Guid guid) )
					{
						instruction.Guid = guid;
					}

					// Look ahead for more properties until we hit the next instruction or section
					for ( int j = i + 1; j < endIdx; j++ )
					{
						string propLine = lines[j].Trim();

						// Stop if we hit another numbered instruction or section
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

						i = j; // Move outer loop forward
					}

					instruction.SetParentComponent(parentComponent);
					instructions.Add(instruction);
					_logVerbose($"      Parsed instruction: Action={instruction.Action}, GUID={instruction.Guid}");
				}
			}

			return instructions;
		}

		/// <summary>
		/// Parses options from the metadata block.
		/// </summary>
		[NotNull]
		private ObservableCollection<Option> ParseOptions([NotNull] string[] lines, int startIdx, int endIdx)
		{
			var options = new ObservableCollection<Option>();
			Option currentOption = null;

			for ( int i = startIdx; i < endIdx; i++ )
			{
				string line = lines[i].Trim();

				// Look for option headers like "##### Option 1"
				if ( Regex.IsMatch(line, @"^#+\s+Option\s+\d+", RegexOptions.IgnoreCase) )
				{
					// Save previous option if exists
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

				// Parse option properties
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
					// Parse nested instruction for this option
					var optionInstructions = ParseOptionsInstructions(lines, ref i, endIdx, currentOption);
					foreach ( var inst in optionInstructions )
					{
						currentOption.Instructions.Add(inst);
					}
				}
			}

			// Add the last option
			if ( currentOption != null )
			{
				options.Add(currentOption);
				_logVerbose($"      Parsed option: Name={currentOption.Name}, GUID={currentOption.Guid}");
			}

			return options;
		}

		/// <summary>
		/// Parses instructions nested within an option.
		/// </summary>
		[NotNull]
		private List<Instruction> ParseOptionsInstructions([NotNull] string[] lines, ref int currentIdx, int endIdx, [NotNull] Option parentOption)
		{
			var instructions = new List<Instruction>();
			var currentInstruction = new Instruction();

			for ( int i = currentIdx + 1; i < endIdx; i++ )
			{
				string line = lines[i].Trim();

				// Stop if we hit another option or top-level property
				if ( Regex.IsMatch(line, @"^#+\s+Option\s+\d+", RegexOptions.IgnoreCase) ||
					 (line.StartsWith("- **") && !line.Contains("**GUID:**") && !line.Contains("**Action:**") &&
					  !line.Contains("**Destination:**") && !line.Contains("**Overwrite:**") && !line.Contains("**Source:**")) )
				{
					currentIdx = i - 1;
					break;
				}

				// Parse instruction properties (indented)
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

		/// <summary>
		/// Extracts a value from a metadata line like "- **Key:** value" or "**Key:** value"
		/// </summary>
		[NotNull]
		private static string ExtractMetadataValue([NotNull] string line, [NotNull] string key)
		{
			// Match patterns like "**Key:** value" or "- **Key:** value"
			string pattern = $@"\*\*{Regex.Escape(key)}:\*\*\s*(.+)$";
			Match match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
			return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
		}
	}
}

