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
	public sealed partial class MarkdownParser
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
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		public MarkdownParserResult Parse([NotNull] string markdown)
		{
			if (markdown is null)
				throw new ArgumentNullException(nameof(markdown));

			System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
			_logInfo("Starting markdown parsing...");
			_logVerbose($"Markdown content length: {markdown.Length} characters");

			List<ModComponent> components = new List<ModComponent>();
			List<string> warnings = new List<string>();
			string preambleContent = string.Empty;
			string epilogueContent = string.Empty;
			string widescreenWarningContent = string.Empty;
			string aspyrExclusiveWarningContent = string.Empty;


			string originalMarkdown = markdown;
			int modListIndex = markdown.IndexOf("## Mod List", StringComparison.Ordinal);


			int aspyrSectionIndex = -1;
			if (modListIndex >= 0)
			{

				string beforeModList = markdown.Substring(0, modListIndex);
				aspyrSectionIndex = beforeModList.IndexOf("## CRITICAL:", StringComparison.OrdinalIgnoreCase);
				if (aspyrSectionIndex >= 0)
				{
					_logVerbose($"Found Aspyr-exclusive section at index {aspyrSectionIndex}");
				}
			}


			int modListEndIndex = -1;
			if (modListIndex >= 0)
			{

				string afterModList = markdown.Substring(modListIndex + "## Mod List".Length);
				Match nextHeaderMatch = Regex.Match(afterModList, @"^##\s+(?!#)", RegexOptions.Multiline, TimeSpan.FromMilliseconds(250));
				if (nextHeaderMatch.Success)
				{
					modListEndIndex = modListIndex + "## Mod List".Length + nextHeaderMatch.Index;
					_logVerbose($"Found end of mod list section at index {modListEndIndex}");
				}
			}

			if (modListIndex >= 0)
			{
				preambleContent = markdown.Substring(0, modListIndex).TrimEnd();
				markdown = markdown.Substring(modListIndex);
				_logVerbose($"Captured {preambleContent.Length} characters before '## Mod List'");
				_logVerbose($"Parsing content after '## Mod List' ({markdown.Length} characters)");
			}
			else
			{
				_logVerbose("No '## Mod List' marker found, parsing entire document");
			}

			_logVerbose($"Using outer pattern to split sections: {_profile.ComponentSectionPattern}");


			int widescreenSectionIndex = markdown.IndexOf("## Optional Widescreen", StringComparison.OrdinalIgnoreCase);
			_logVerbose(widescreenSectionIndex >= 0
				? $"Found widescreen section at index {widescreenSectionIndex}"
				: "No widescreen section marker found");

			Regex outerRegex = new Regex(
				_profile.ComponentSectionPattern,
				_profile.ComponentSectionOptions,
				TimeSpan.FromMilliseconds(250)
			);


			MatchCollection outerMatches = outerRegex.Matches(originalMarkdown);
			_logInfo($"Found {outerMatches.Count} component sections using outer pattern");

			int componentIndex = 0;
			int lastValidComponentEndIndex = 0;
			int lastNonWidescreenComponentEndIndex = 0;
			int firstWidescreenComponentStartIndex = -1;
			int lastNonAspyrComponentEndIndex = 0;
			int firstAspyrComponentStartIndex = -1;

			foreach (Match outerMatch in outerMatches)
			{
				componentIndex++;
				_logVerbose($"Processing component section {componentIndex}/{outerMatches.Count}");

				if (modListIndex >= 0 && outerMatch.Index < modListIndex)
				{
					_logVerbose($"Skipping component {componentIndex} as it's before the mod list section (before index {modListIndex})");
					continue;
				}

				if (modListEndIndex >= 0 && outerMatch.Index >= modListEndIndex)
				{
					_logVerbose($"Skipping component {componentIndex} as it's outside the mod list section (after index {modListEndIndex})");
					continue;
				}

				string sectionText = outerMatch.Value;
				_logVerbose($"ModComponent {componentIndex} section length: {sectionText.Length} characters");


				bool isAspyrExclusive = false;
				bool isWidescreenOnly = false;


				if (aspyrSectionIndex >= 0 && modListIndex >= 0)
				{
					isAspyrExclusive = outerMatch.Index >= aspyrSectionIndex && outerMatch.Index < modListIndex;
				}


				if (modListIndex >= 0)
				{

					int absoluteWidescreenIndex = widescreenSectionIndex >= 0 ? modListIndex + widescreenSectionIndex : -1;
					isWidescreenOnly = absoluteWidescreenIndex >= 0 && outerMatch.Index >= absoluteWidescreenIndex;
				}

				ModComponent component = ParseComponentFromText(sectionText, out string warning, componentIndex);

				if (component != null)
				{
					component.AspyrExclusive = isAspyrExclusive ? true : (bool?)null;
					component.WidescreenOnly = isWidescreenOnly;
					components.Add(component);
					_logVerbose($"Successfully parsed component {componentIndex}: '{component.Name}' by {component.Author} (AspyrExclusive: {isAspyrExclusive}, WidescreenOnly: {isWidescreenOnly})");


					lastValidComponentEndIndex = Math.Max(lastValidComponentEndIndex, outerMatch.Index + outerMatch.Length);


					if (!isAspyrExclusive && aspyrSectionIndex >= 0)
					{
						lastNonAspyrComponentEndIndex = outerMatch.Index + outerMatch.Length;
					}
					else if (isAspyrExclusive && firstAspyrComponentStartIndex == -1)
					{
						firstAspyrComponentStartIndex = outerMatch.Index;
					}


					if (!isWidescreenOnly)
					{
						lastNonWidescreenComponentEndIndex = outerMatch.Index + outerMatch.Length;
					}
					else if (firstWidescreenComponentStartIndex == -1)
					{
						firstWidescreenComponentStartIndex = outerMatch.Index;
					}
				}
				else if (!string.IsNullOrEmpty(warning))
				{
					warnings.Add($"ModComponent {componentIndex}: {warning}");
					_logVerbose($"Warning for component {componentIndex}: {warning}");
				}
				else
				{
					_logVerbose($"ModComponent {componentIndex} resulted in null component with no warning");
				}
			}


			if (lastNonAspyrComponentEndIndex > 0 && firstAspyrComponentStartIndex > lastNonAspyrComponentEndIndex)
			{
				aspyrExclusiveWarningContent = originalMarkdown.Substring(
					lastNonAspyrComponentEndIndex,
					firstAspyrComponentStartIndex - lastNonAspyrComponentEndIndex
				).Trim();
				_logVerbose($"Captured {aspyrExclusiveWarningContent.Length} characters for Aspyr exclusive warning content");
			}


			if (lastNonWidescreenComponentEndIndex > 0 && firstWidescreenComponentStartIndex > lastNonWidescreenComponentEndIndex)
			{
				widescreenWarningContent = originalMarkdown.Substring(
					lastNonWidescreenComponentEndIndex,
					firstWidescreenComponentStartIndex - lastNonWidescreenComponentEndIndex
				).Trim();
				_logVerbose($"Captured {widescreenWarningContent.Length} characters for widescreen warning content");
			}


			if (modListEndIndex >= 0 && modListEndIndex < originalMarkdown.Length)
			{

				epilogueContent = originalMarkdown.Substring(modListEndIndex).TrimStart();
				_logVerbose($"Captured {epilogueContent.Length} characters after mod list section (from index {modListEndIndex})");
			}
			else if (lastValidComponentEndIndex > 0 && lastValidComponentEndIndex < originalMarkdown.Length)
			{

				epilogueContent = originalMarkdown.Substring(lastValidComponentEndIndex).TrimStart();
				_logVerbose($"Captured {epilogueContent.Length} characters after last valid component (from index {lastValidComponentEndIndex})");
			}

			_logInfo($"Parsing completed. Successfully parsed {components.Count} components with {warnings.Count} warnings");
			foreach (ModComponent component in components)
			{
				int linkCount = component.ModLinkFilenames?.Count ?? 0;
				_logVerbose($"  - '{component.Name}' by {component.Author} ({component.Category}/{component.Tier}) with {linkCount} links");
			}


			ResolveDependencies(components);

			sw.Stop();
			Services.TelemetryService.Instance.RecordParsingOperation(
				fileType: "markdown",
				success: true,
				componentCount: components.Count,
				durationMs: sw.Elapsed.TotalMilliseconds
			);

			return new MarkdownParserResult
			{
				Components = components,
				Warnings = warnings,
				Metadata = _profile.Metadata,
				PreambleContent = preambleContent,
				EpilogueContent = epilogueContent,
				WidescreenWarningContent = widescreenWarningContent,
				AspyrExclusiveWarningContent = aspyrExclusiveWarningContent,
			};
		}


		private readonly Dictionary<Guid, string> _tempMasterNames = new Dictionary<Guid, string>();


		private static readonly Dictionary<string, string> s_authorAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			{ "JC", "JCarter426" },
			{ "JCarter426", "JC" },
		};
		private static readonly string[] separatorArray = new[] { "&" };

		[CanBeNull]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private ModComponent ParseComponentFromText([NotNull] string componentText, out string warning, int componentIndex)
		{
			warning = string.Empty;
			if (string.IsNullOrWhiteSpace(componentText))
			{
				warning = "ModComponent text is null or whitespace";
				return null;
			}

			_logVerbose($"  Parsing component {componentIndex} text content...");

			ModComponent component = new ModComponent
			{
				Guid = Guid.NewGuid(),
			};

			// Extract and remove ModSync metadata block BEFORE extracting other fields
			// This prevents the metadata from being included in fields like Directions/Installation Instructions
			string modSyncMetadata = ExtractModSyncMetadata(componentText);
			string componentTextWithoutMetadata = componentText;
			if (!string.IsNullOrWhiteSpace(modSyncMetadata))
			{
				_logVerbose($"  Found ModSync metadata block, removing from text before field extraction...");
				// Remove the entire <!--<<ModSync>> ... --> block from the component text
				string pattern = !string.IsNullOrWhiteSpace(_profile.InstructionsBlockPattern)
					? _profile.InstructionsBlockPattern
					: @"<!--<<ModSync>>\s*[\s\S]*?-->";
				componentTextWithoutMetadata = Regex.Replace(componentText, pattern, "", RegexOptions.Singleline | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250)).Trim();
				_logVerbose($"  Removed ModSync metadata block (reduced text from {componentText.Length} to {componentTextWithoutMetadata.Length} chars)");
			}

			string extractedHeading = null;
			if (!string.IsNullOrWhiteSpace(_profile.HeadingPattern))
			{
				extractedHeading = ExtractValue(componentTextWithoutMetadata, _profile.HeadingPattern, "heading");
				if (extractedHeading != null)
				{
					component.Heading = extractedHeading;
					_logVerbose($"  Extracted Heading: '{extractedHeading}'");
				}
			}


			string extractedName = ExtractValue(componentTextWithoutMetadata, _profile.NamePattern, "name|name_plain|name_link");
			if (extractedName != null)
			{
				component.Name = extractedName;
				_logVerbose($"  Extracted Name from field: '{extractedName}'");


				Match nameFieldMatch = Regex.Match(componentTextWithoutMetadata, _profile.NamePattern, RegexOptions.Compiled | RegexOptions.Multiline, TimeSpan.FromMilliseconds(250));
				if (nameFieldMatch.Success)
				{

					component.NameFieldContent = nameFieldMatch.Value.Replace("**Name:**", "").Trim();
					_logVerbose($"  Captured full Name field content: '{component.NameFieldContent.Substring(0, Math.Min(100, component.NameFieldContent.Length))}...'");
				}
			}
			else if (extractedHeading != null)
			{

				component.Name = extractedHeading;
				_logVerbose($"  Name field not found, using heading as name: '{extractedHeading}'");
			}
			else
			{
				_logVerbose($"  No name found using pattern: {_profile.NamePattern}");
			}

			string extractedAuthor = ExtractValue(componentTextWithoutMetadata, _profile.AuthorPattern, "author");
			if (extractedAuthor != null)
			{
				component.Author = extractedAuthor;
				_logVerbose($"  Extracted Author: '{extractedAuthor}'");
			}
			else
			{
				_logVerbose($"  No author found using pattern: {_profile.AuthorPattern}");
			}

			string extractedDescription = ExtractValue(componentTextWithoutMetadata, _profile.DescriptionPattern, "description");
			if (extractedDescription != null)
			{
				component.Description = extractedDescription;
				_logVerbose($"  Extracted Description: '{extractedDescription.Substring(0, Math.Min(100, extractedDescription.Length))}...'");
			}
			else
			{
				_logVerbose($"  No description found using pattern: {_profile.DescriptionPattern}");
			}

			string extractedMethod = ExtractValue(componentTextWithoutMetadata, _profile.InstallationMethodPattern, "method");
			if (extractedMethod != null)
			{
				component.InstallationMethod = extractedMethod;
				_logVerbose($"  Extracted Installation Method: '{extractedMethod}'");
			}
			else
			{
				_logVerbose($"  No installation method found using pattern: {_profile.InstallationMethodPattern}");
			}

			string extractedDownloadInstructions = ExtractValue(componentTextWithoutMetadata, _profile.DownloadInstructionsPattern, "download");
			if (extractedDownloadInstructions != null)
			{
				component.DownloadInstructions = extractedDownloadInstructions;
				_logVerbose($"  Extracted Download Instructions: '{extractedDownloadInstructions.Substring(0, Math.Min(100, extractedDownloadInstructions.Length))}...'");
			}
			else
			{
				_logVerbose($"  No download instructions found using pattern: {_profile.DownloadInstructionsPattern}");
			}

			string extractedDirections = ExtractValue(componentTextWithoutMetadata, _profile.InstallationInstructionsPattern, "directions");
			if (extractedDirections != null)
			{
				component.Directions = extractedDirections;
				_logVerbose($"  Extracted Directions: '{extractedDirections.Substring(0, Math.Min(100, extractedDirections.Length))}...'");
			}
			else
			{
				_logVerbose($"  No directions found using pattern: {_profile.InstallationInstructionsPattern}");
			}

			string extractedWarning = ExtractValue(componentTextWithoutMetadata, _profile.UsageWarningPattern, "warning");
			if (extractedWarning != null)
			{
				component.UsageWarning = extractedWarning;
				_logVerbose($"  Extracted Usage Warning: '{extractedWarning.Substring(0, Math.Min(100, extractedWarning.Length))}...'");
			}
			else
			{
				_logVerbose($"  No usage warning found using pattern: {_profile.UsageWarningPattern}");
			}

			string extractedScreenshots = ExtractValue(componentTextWithoutMetadata, _profile.ScreenshotsPattern, "screenshots");
			if (extractedScreenshots != null)
			{
				component.Screenshots = extractedScreenshots;
				_logVerbose($"  Extracted Screenshots: '{extractedScreenshots.Substring(0, Math.Min(100, extractedScreenshots.Length))}...'");
			}
			else
			{
				_logVerbose($"  No screenshots found using pattern: {_profile.ScreenshotsPattern}");
			}

			string extractedKnownBugs = ExtractValue(componentTextWithoutMetadata, _profile.KnownBugsPattern, "bugs");
			if (extractedKnownBugs != null)
			{
				component.KnownBugs = extractedKnownBugs;
				_logVerbose($"  Extracted Known Bugs: '{extractedKnownBugs.Substring(0, Math.Min(100, extractedKnownBugs.Length))}...'");
			}
			else
			{
				_logVerbose($"  No known bugs found using pattern: {_profile.KnownBugsPattern}");
			}

			string extractedInstallationWarning = ExtractValue(componentTextWithoutMetadata, _profile.InstallationWarningPattern, "installwarning");
			if (extractedInstallationWarning != null)
			{
				component.InstallationWarning = extractedInstallationWarning;
				_logVerbose($"  Extracted Installation Warning: '{extractedInstallationWarning.Substring(0, Math.Min(100, extractedInstallationWarning.Length))}...'");
			}
			else
			{
				_logVerbose($"  No installation warning found using pattern: {_profile.InstallationWarningPattern}");
			}

			string extractedCompatibilityWarning = ExtractValue(componentTextWithoutMetadata, _profile.CompatibilityWarningPattern, "compatwarning");
			if (extractedCompatibilityWarning != null)
			{
				component.CompatibilityWarning = extractedCompatibilityWarning;
				_logVerbose($"  Extracted Compatibility Warning: '{extractedCompatibilityWarning.Substring(0, Math.Min(100, extractedCompatibilityWarning.Length))}...'");
			}
			else
			{
				_logVerbose($"  No compatibility warning found using pattern: {_profile.CompatibilityWarningPattern}");
			}

			string extractedSteamNotes = ExtractValue(componentTextWithoutMetadata, _profile.SteamNotesPattern, "steamnotes");
			if (extractedSteamNotes != null)
			{
				component.SteamNotes = extractedSteamNotes;
				_logVerbose($"  Extracted Steam Notes: '{extractedSteamNotes.Substring(0, Math.Min(100, extractedSteamNotes.Length))}...'");
			}
			else
			{
				_logVerbose($"  No steam notes found using pattern: {_profile.SteamNotesPattern}");
			}


			string extractedMasters = ExtractValue(componentTextWithoutMetadata, _profile.DependenciesPattern, "masters");
			if (extractedMasters != null)
			{
				_tempMasterNames[component.Guid] = extractedMasters;
				_logVerbose($"  Extracted Masters: '{extractedMasters}'");
			}
			else
			{
				_logVerbose($"  No masters found using pattern: {_profile.DependenciesPattern}");
			}


			if (!string.IsNullOrWhiteSpace(_profile.ModLinkPattern) && !string.IsNullOrWhiteSpace(_profile.NamePattern))
			{

				Match nameFieldMatch = Regex.Match(componentTextWithoutMetadata, _profile.NamePattern, RegexOptions.Compiled | RegexOptions.Multiline, TimeSpan.FromMilliseconds(250));
				if (nameFieldMatch.Success)
				{

					string nameFieldText = nameFieldMatch.Value;


					MatchCollection modLinkMatches = Regex.Matches(nameFieldText, _profile.ModLinkPattern, RegexOptions.Compiled | RegexOptions.Multiline, TimeSpan.FromMilliseconds(250));
					if (modLinkMatches.Count > 0)
					{
						// Fix: ModLinkFilenames expects a Dictionary<string, Dictionary<string, bool?>>
						// We'll store each mod link as a key with an empty dictionary as value
						List<string> links = modLinkMatches.Cast<Match>()
							.Select(m => m.Groups["link"].Value.Trim())
							.Where(l => !string.IsNullOrEmpty(l))
							.Distinct(StringComparer.OrdinalIgnoreCase)
							.ToList();

						component.ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>(StringComparer.OrdinalIgnoreCase);
						foreach (string link in links)
						{
							component.ModLinkFilenames[link] = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
						}

						_logVerbose($"  Extracted {component.ModLinkFilenames.Count} mod links from Name field");
					}
					else
					{
						_logVerbose($"  No mod links found in Name field");
					}
				}
				else
				{
					_logVerbose($"  Could not find Name field to extract mod links from");
				}
			}
			else
			{
				_logVerbose($"  ModLinkFilenames or Name pattern not configured");
			}

			if (!string.IsNullOrWhiteSpace(_profile.CategoryTierPattern))
			{
				Match categoryTierMatch = Regex.Match(
					componentTextWithoutMetadata,
					_profile.CategoryTierPattern,
					RegexOptions.Compiled | RegexOptions.Multiline,
					TimeSpan.FromMilliseconds(250)
				);
				if (categoryTierMatch.Success)
				{
					string categoryStr = categoryTierMatch.Groups["category"].Value.Trim();
					_logVerbose($"  [DEBUG] Raw categoryStr: '{categoryStr}' (type: {categoryStr?.GetType().Name})");

					string normalizedCategory = MarkdownUtilities.NormalizeCategoryFormat(categoryStr);
					_logVerbose($"  [DEBUG] Normalized: '{normalizedCategory}'");

					List<string> splitResult = normalizedCategory.Split(
						separatorArray,
						StringSplitOptions.RemoveEmptyEntries
					).Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();

					_logVerbose($"  [DEBUG] Split result count: {splitResult.Count}");
					if (splitResult.Count > 0)
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

			if (!string.IsNullOrWhiteSpace(_profile.NonEnglishPattern))
			{
				string nonEnglish = ExtractValue(componentTextWithoutMetadata, _profile.NonEnglishPattern, "value");
				if (!string.IsNullOrWhiteSpace(nonEnglish))
				{
					component.Language = new List<string> { nonEnglish };
					_logVerbose($"  Extracted Non-English value: '{nonEnglish}'");
				}
				else
				{
					_logVerbose($"  No non-English value found using pattern: {_profile.NonEnglishPattern}");
				}
			}

			if (string.IsNullOrWhiteSpace(component.Name))
			{

				bool hasComponentFields = componentTextWithoutMetadata.Contains("**name:**", StringComparison.CurrentCultureIgnoreCase) ||
										  componentTextWithoutMetadata.Contains("**author:**", StringComparison.CurrentCultureIgnoreCase) ||
										  componentTextWithoutMetadata.Contains("**description:**", StringComparison.CurrentCultureIgnoreCase) ||
										  componentTextWithoutMetadata.Contains("**category:**", StringComparison.CurrentCultureIgnoreCase) ||
										  componentTextWithoutMetadata.Contains("**installation:**", StringComparison.CurrentCultureIgnoreCase);

				if (hasComponentFields)
				{

					string excerpt = componentTextWithoutMetadata.Length > 180
						? componentTextWithoutMetadata.Substring(0, 180).Replace('\n', ' ').Replace('\r', ' ') + "..."
						: componentTextWithoutMetadata.Replace('\n', ' ').Replace('\r', ' ');
					warning = $"ModComponent entry at section {componentIndex} is missing a **Name:** field or name value.";
					_logVerbose(
						$"  [REJECT MOD] Section {componentIndex} appears to be a mod entry (fields present: Name/Author/Description/etc) but does not have a valid **Name:** field or name was left blank. Section excerpt: \"{excerpt}\"");
				}
				else
				{

					string preview = componentTextWithoutMetadata.Length > 120
						? componentTextWithoutMetadata.Substring(0, 120).Replace('\n', ' ').Replace('\r', ' ') + "..."
						: componentTextWithoutMetadata.Replace('\n', ' ').Replace('\r', ' ');
					_logVerbose(
						$"  [SKIP NON-MOD] Section {componentIndex} does not contain any expected mod entry fields (Name/Author/Description/Category/Installation). Skipping as likely non-mod content. Preview: \"{preview}\"");
				}
				return null;
			}

			// ModSync metadata was already extracted and removed earlier, now just parse it
			if (!string.IsNullOrWhiteSpace(modSyncMetadata))
			{
				_logVerbose($"  Parsing extracted ModSync metadata block...");
				try
				{
					ParseModSyncMetadata(component, modSyncMetadata);
					_logVerbose($"  Successfully parsed ModSync metadata: {component.Instructions.Count} instructions, {component.Options.Count} options");
				}
				catch (Exception ex)
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
			if (string.IsNullOrWhiteSpace(pattern))
				return null;

			Match match = Regex.Match(
				source,
				pattern,
				RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase,
				TimeSpan.FromMilliseconds(250) // Limit regex execution time to reduce DoS risk
			);
			if (!match.Success)
				return null;

			foreach (string groupName in groupNames.Split('|'))
			{
				string value = match.Groups[groupName.Trim()]?.Value;
				if (!string.IsNullOrWhiteSpace(value))
					return value.Trim();
			}
			return null;
		}

		[CanBeNull]
		private string ExtractModSyncMetadata([NotNull] string componentText)
		{
			if (string.IsNullOrWhiteSpace(componentText))
				return null;

			string pattern = !string.IsNullOrWhiteSpace(_profile.InstructionsBlockPattern)
				? _profile.InstructionsBlockPattern
				: @"<!--<<ModSync>>\s*(?<instructions>[\s\S]*?)-->";

			Match match = Regex.Match(
				componentText,
				pattern,
				RegexOptions.Singleline | RegexOptions.IgnoreCase,
				TimeSpan.FromMilliseconds(250) // Limit regex execution time to reduce DoS risk
			);

			return match.Success ? match.Groups["instructions"].Value.Trim() : null;
		}

		private void ParseModSyncMetadata([NotNull] ModComponent component, [NotNull] string metadataText)
		{
			if (string.IsNullOrWhiteSpace(metadataText))
				return;

			bool isYaml = DetectYamlFormat(metadataText);
			bool isToml = DetectTomlFormat(metadataText);

			if (isYaml)
			{
				_logVerbose($"    Detected YAML format, attempting to deserialize...");
				try
				{
					ModComponent yamlComponent = Services.ModComponentSerializationService.DeserializeYamlComponent(metadataText);
					if (yamlComponent != null)
					{

						MergeComponentMetadata(component, yamlComponent);
						_logVerbose($"    Successfully parsed YAML: {component.Instructions.Count} instructions, {component.Options.Count} options");
						return;
					}
				}
				catch (Exception ex)
				{
					_logVerbose($"    Warning: YAML parsing failed, falling back to legacy parser: {ex.Message}");
				}
			}
			else if (isToml)
			{
				_logVerbose($"    Detected TOML format, attempting to deserialize...");
				try
				{

					string tomlString = metadataText;
					if (!tomlString.Contains("[[thisMod]]") && !tomlString.Contains("[thisMod]"))
					{
						tomlString = "[[thisMod]]\n" + metadataText;
					}

					ModComponent tomlComponent = ModComponent.DeserializeTomlComponent(tomlString);
					if (tomlComponent != null)
					{

						MergeComponentMetadata(component, tomlComponent);
						_logVerbose($"    Successfully parsed TOML: {component.Instructions.Count} instructions, {component.Options.Count} options");
						return;
					}
				}
				catch (Exception ex)
				{
					_logVerbose($"    Warning: TOML parsing failed, falling back to legacy parser: {ex.Message}");
				}
			}

			_logVerbose($"    Using legacy markdown format parser");
			ParseLegacyMarkdownMetadata(component, metadataText);
		}

		private static bool DetectYamlFormat([NotNull] string text)
		{

			return Regex.IsMatch(text, @"^\s*Guid:\s*[a-f0-9\-]+", RegexOptions.Multiline | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250))
				   && Regex.IsMatch(text, @"^\s*Instructions:\s*$", RegexOptions.Multiline, TimeSpan.FromMilliseconds(250))
				   && !text.Contains("**");
		}

		private static bool DetectTomlFormat([NotNull] string text)
		{

			if (text.Contains("[[thisMod]]") || text.Contains("[thisMod]"))
				return true;


			if (!text.Contains("Guid") || !text.Contains("Instructions"))
				return false;


			return Regex.IsMatch(text, @"^[a-zA-Z_][a-zA-Z0-9_]*\s*=\s*[""'\[\d]", RegexOptions.Multiline, TimeSpan.FromMilliseconds(250));
		}

		private static void MergeComponentMetadata([NotNull] ModComponent target, [NotNull] ModComponent source)
		{

			if (source.Guid != Guid.Empty)
			{
				target.Guid = source.Guid;
			}

			// Copy IsSelected property from source to target
			target.IsSelected = source.IsSelected;

			if (source.Instructions.Count > 0)
			{
				target.Instructions = source.Instructions;
			}

			if (source.Options.Count > 0)
			{
				target.Options = source.Options;
			}

			if (source.Dependencies.Count > 0)
			{
				target.Dependencies = source.Dependencies;
			}

			if (source.Restrictions.Count > 0)
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
			for (int i = 0; i < headerEnd; i++)
			{
				string line = lines[i].Trim();
				if (line.StartsWith("- **GUID:**", StringComparison.Ordinal))
				{
					string guidStr = ExtractMetadataValue(line, "GUID");
					if (Guid.TryParse(guidStr, out Guid guid))
					{
						component.Guid = guid;
						_logVerbose($"    Parsed component GUID: {guid}");
						break;
					}
				}
			}

			if (instructionsStart >= 0)
			{
				int instructionsEnd = optionsStart >= 0 ? optionsStart : lines.Length;
				component.Instructions = ParseInstructions(lines, instructionsStart + 1, instructionsEnd, component);
				_logVerbose($"    Parsed {component.Instructions.Count} instructions");
			}

			if (optionsStart >= 0)
			{
				component.Options = ParseOptions(lines, optionsStart + 1, lines.Length);
				_logVerbose($"    Parsed {component.Options.Count} options");
			}
		}

		private static int FindSectionStart([NotNull] string[] lines, [NotNull] string sectionHeader)
		{
			for (int i = 0; i < lines.Length; i++)


			{
				if (string.Equals(lines[i].Trim(), sectionHeader, StringComparison.Ordinal))
					return i;
			}
			return -1;
		}

		[NotNull]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private ObservableCollection<Instruction> ParseInstructions([NotNull] string[] lines, int startIdx, int endIdx, [NotNull] ModComponent parentComponent)
		{
			ObservableCollection<Instruction> instructions = new ObservableCollection<Instruction>();

			int i = startIdx;
			while (i < endIdx)
			{
				string line = lines[i].Trim();

				if (Regex.IsMatch(line, @"^\d+\.\s+\*\*GUID:\*\*", RegexOptions.None, TimeSpan.FromMilliseconds(250)))
				{
					Instruction instruction = new Instruction();

					string guidStr = ExtractMetadataValue(line, "GUID");
					if (Guid.TryParse(guidStr, out Guid guid))
					{
						instruction.Guid = guid;
					}

					int j = i + 1;
					for (; j < endIdx; j++)
					{
						string propLine = lines[j].Trim();

						if (Regex.IsMatch(propLine, @"^\d+\.\s+\*\*GUID:\*\*", RegexOptions.None, TimeSpan.FromMilliseconds(250)) || propLine.StartsWith("#", StringComparison.Ordinal))
							break;

						if (propLine.StartsWith("**Action:**", StringComparison.Ordinal))
						{
							string actionStr = ExtractMetadataValue(propLine, "Action");
							if (Enum.TryParse<Instruction.ActionType>(actionStr, true, out Instruction.ActionType action))
							{
								instruction.Action = action;
							}
						}
						else if (propLine.StartsWith("**Overwrite:**", StringComparison.Ordinal))
						{
							string overwriteStr = ExtractMetadataValue(propLine, "Overwrite");
							if (bool.TryParse(overwriteStr, out bool overwrite))
							{
								instruction.Overwrite = overwrite;
							}
						}
						else if (propLine.StartsWith("**Source:**", StringComparison.Ordinal))
						{
							string sourceStr = ExtractMetadataValue(propLine, "Source");
							instruction.Source = sourceStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
								.Select(s => s.Trim())
								.ToList();
						}
						else if (propLine.StartsWith("**Destination:**", StringComparison.Ordinal))
						{
							instruction.Destination = ExtractMetadataValue(propLine, "Destination");
						}
					}

					instruction.SetParentComponent(parentComponent);
					instructions.Add(instruction);
					_logVerbose($"      Parsed instruction: Action={instruction.Action}, GUID={instruction.Guid}");

					i = j;
					continue;
				}
				i++;
			}

			return instructions;
		}

		[NotNull]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private ObservableCollection<Option> ParseOptions([NotNull] string[] lines, int startIdx, int endIdx)
		{
			ObservableCollection<Option> options = new ObservableCollection<Option>();
			Option currentOption = null;

			for (int i = startIdx; i < endIdx; i++)
			{
				string line = lines[i].Trim();

				if (Regex.IsMatch(line, @"^#+\s+Option\s+\d+", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250)))
				{

					if (currentOption != null)
					{
						options.Add(currentOption);
						_logVerbose($"      Parsed option: Name={currentOption.Name}, GUID={currentOption.Guid}");
					}

					currentOption = new Option
					{
						Guid = Guid.NewGuid(),
					};
					continue;
				}

				if (currentOption is null)
					continue;

				if (line.StartsWith("- **GUID:**", StringComparison.Ordinal))
				{
					string guidStr = ExtractMetadataValue(line, "GUID");
					if (Guid.TryParse(guidStr, out Guid guid))
					{
						currentOption.Guid = guid;
					}
				}
				else if (line.StartsWith("- **Name:**", StringComparison.Ordinal))
				{
					currentOption.Name = ExtractMetadataValue(line, "Name");
				}
				else if (line.StartsWith("- **Description:**", StringComparison.Ordinal))
				{
					currentOption.Description = ExtractMetadataValue(line, "Description");
				}
				else if (line.StartsWith("- **Is Selected:**", StringComparison.Ordinal))
				{
					string selectedStr = ExtractMetadataValue(line, "Is Selected");
					if (bool.TryParse(selectedStr, out bool isSelected))
					{
						currentOption.IsSelected = isSelected;
					}
				}
				else if (line.StartsWith("- **Install State:**", StringComparison.Ordinal))
				{
					string stateStr = ExtractMetadataValue(line, "Install State");
					if (int.TryParse(stateStr, System.Globalization.CultureInfo.InvariantCulture, out int state))
					{
						currentOption.InstallState = (ModComponent.ComponentInstallState)state;
					}
				}
				else if (line.StartsWith("- **Is Downloaded:**", StringComparison.Ordinal))
				{
					string downloadedStr = ExtractMetadataValue(line, "Is Downloaded");
					if (bool.TryParse(downloadedStr, out bool isDownloaded))
					{
						currentOption.IsDownloaded = isDownloaded;
					}
				}
				else if (line.StartsWith("- **Restrictions:**", StringComparison.Ordinal))
				{
					string restrictionsStr = ExtractMetadataValue(line, "Restrictions");
					currentOption.Restrictions = restrictionsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(s => s.Trim())
						.Where(s => Guid.TryParse(s, out _))
						.Select(s => Guid.Parse(s))
						.ToList();
				}
				else if (line.Contains("- **Instruction:**"))
				{

					List<Instruction> optionInstructions = ParseOptionsInstructions(lines, ref i, endIdx, currentOption);
					foreach (Instruction inst in optionInstructions)
					{
						currentOption.Instructions.Add(inst);
					}
				}
			}

			if (currentOption != null)
			{
				options.Add(currentOption);
				_logVerbose($"      Parsed option: Name={currentOption.Name}, GUID={currentOption.Guid}");
			}

			return options;
		}

		[NotNull]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private static List<Instruction> ParseOptionsInstructions([NotNull] string[] lines, ref int currentIdx, int endIdx, [NotNull] Option parentOption)
		{
			List<Instruction> instructions = new List<Instruction>();
			Instruction currentInstruction = new Instruction();

			for (int i = currentIdx + 1; i < endIdx; i++)
			{
				string line = lines[i].Trim();

				if (MyRegex().IsMatch(line) ||
					 (line.StartsWith("- **", StringComparison.Ordinal) && !line.Contains("**GUID:**", StringComparison.Ordinal) && !line.Contains("**Action:**", StringComparison.Ordinal) &&
					  !line.Contains("**Destination:**", StringComparison.Ordinal) && !line.Contains("**Overwrite:**", StringComparison.Ordinal) && !line.Contains("**Source:**", StringComparison.Ordinal)))
				{
					currentIdx = i - 1;
					break;
				}

				if (line.StartsWith("- **GUID:**", StringComparison.Ordinal))
				{
					string guidStr = ExtractMetadataValue(line, "GUID");
					if (Guid.TryParse(guidStr, out Guid guid))
					{
						currentInstruction.Guid = guid;
					}
				}
				else if (line.StartsWith("- **Action:**", StringComparison.Ordinal))
				{
					string actionStr = ExtractMetadataValue(line, "Action");
					if (Enum.TryParse(actionStr, true, out Instruction.ActionType action))
					{
						currentInstruction.Action = action;
					}
				}
				else if (line.StartsWith("- **Destination:**", StringComparison.Ordinal))
				{
					currentInstruction.Destination = ExtractMetadataValue(line, "Destination");
				}
				else if (line.StartsWith("- **Overwrite:**", StringComparison.Ordinal))
				{
					string overwriteStr = ExtractMetadataValue(line, "Overwrite");
					if (bool.TryParse(overwriteStr, out bool overwrite))
					{
						currentInstruction.Overwrite = overwrite;
					}
				}
				else if (line.StartsWith("- **Source:**", StringComparison.Ordinal))
				{
					string sourceStr = ExtractMetadataValue(line, "Source");
					currentInstruction.Source = sourceStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(s => s.Trim())
						.ToList();
				}

				currentIdx = i;
			}

			if (currentInstruction.Guid != Guid.Empty)
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
			Match match = Regex.Match(line, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
			return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private void ResolveDependencies([NotNull] List<ModComponent> components)
		{

			Dictionary<string, Guid> nameToGuid = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
			foreach (ModComponent component in components)
			{
				if (!string.IsNullOrWhiteSpace(component.Name))
				{
					nameToGuid[component.Name] = component.Guid;
				}
				if (!string.IsNullOrWhiteSpace(component.Heading) && !nameToGuid.ContainsKey(component.Heading))
				{
					nameToGuid[component.Heading] = component.Guid;
				}
			}


			foreach (ModComponent component in components)
			{
				if (_tempMasterNames.TryGetValue(component.Guid, out string masterNames))
				{

					string[] dependencyNames;
					if (!string.IsNullOrWhiteSpace(_profile.DependenciesSeparatorPattern))
					{
						dependencyNames = Regex.Split(masterNames, _profile.DependenciesSeparatorPattern, RegexOptions.None, TimeSpan.FromMilliseconds(250));
					}
					else
					{

						dependencyNames = masterNames.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
					}

					foreach (string depName in dependencyNames)
					{
						string trimmedName = depName.Trim();
						if (string.IsNullOrWhiteSpace(trimmedName))
							continue;

						bool resolved = false;


						if (nameToGuid.TryGetValue(trimmedName, out Guid dependencyGuid))
						{
							resolved = true;
							_logVerbose($"  Resolved dependency '{trimmedName}' to GUID {dependencyGuid} (exact match) for component '{component.Name}'");
						}
						else if (trimmedName.Contains("'s ", StringComparison.Ordinal) || trimmedName.Contains("'s ", StringComparison.OrdinalIgnoreCase))
						{

							string[] parts = trimmedName.Split(new[] { "'s ", "'s " }, 2, StringSplitOptions.None);
							if (parts.Length == 2)
							{
								string authorPrefix = parts[0].Trim();
								string componentName = parts[1].Trim();


								ModComponent matchedComponent = components.Find(c =>
									string.Equals(c.Name, componentName, StringComparison.OrdinalIgnoreCase) &&
									AuthorMatches(c.Author, authorPrefix));

								if (matchedComponent != null)
								{
									dependencyGuid = matchedComponent.Guid;
									resolved = true;
									_logVerbose($"  Resolved dependency '{trimmedName}' to GUID {dependencyGuid} (author's name match) for component '{component.Name}'");
								}
							}
						}

						if (resolved && dependencyGuid != Guid.Empty)
						{
							component.Dependencies.Add(dependencyGuid);
							component.DependencyNames.Add(trimmedName);
							component.DependencyGuidToOriginalName[dependencyGuid] = trimmedName;
						}
						else
						{
							_logInfo($"Warning: Could not resolve dependency '{trimmedName}' for component '{component.Name}' - no matching component found");
						}
					}
				}
			}


			_tempMasterNames.Clear();
		}

		private static bool AuthorMatches(string componentAuthor, string searchAuthor)
		{
			if (string.IsNullOrWhiteSpace(componentAuthor) || string.IsNullOrWhiteSpace(searchAuthor))
				return false;


			if (componentAuthor.Contains(searchAuthor, StringComparison.OrdinalIgnoreCase))
				return true;


			if (s_authorAliases.TryGetValue(searchAuthor, out string alias) &&
				componentAuthor.Contains(alias, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return false;
		}

		[GeneratedRegex(@"^#+\s+Option\s+\d+", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 250)]
		private static partial Regex MyRegex();
	}
}