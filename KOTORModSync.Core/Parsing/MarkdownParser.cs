using System;
using System.Collections.Generic;
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

			var components = new List<Component>();
			var warnings = new List<string>();

			// Find "## Mod List" and only parse content after it
			int modListIndex = markdown.IndexOf("## Mod List", StringComparison.Ordinal);
			if ( modListIndex >= 0 )
			{
				markdown = markdown.Substring(modListIndex);
				_logVerbose($"Found '## Mod List' marker at index {modListIndex}, parsing content after it ({markdown.Length} characters)");
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
				_logVerbose($"Component {componentIndex} section length: {sectionText.Length} characters");

				// Parse this section using individual patterns
				Component component = ParseComponentFromText(sectionText, out string warning, componentIndex);

				if ( component != null )
				{
					components.Add(component);
					_logVerbose($"Successfully parsed component {componentIndex}: '{component.Name}' by {component.Author}");
				}
				else if ( !string.IsNullOrEmpty(warning) )
				{
					warnings.Add($"Component {componentIndex}: {warning}");
					_logVerbose($"Warning for component {componentIndex}: {warning}");
				}
				else
				{
					_logVerbose($"Component {componentIndex} resulted in null component with no warning");
				}
			}

			_logInfo($"Parsing completed. Successfully parsed {components.Count} components with {warnings.Count} warnings");
			foreach ( Component component in components )
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
		private Component ParseComponentFromText([NotNull] string componentText, out string warning, int componentIndex)
		{
			warning = string.Empty;
			if ( string.IsNullOrWhiteSpace(componentText) )
			{
				warning = "Component text is null or whitespace";
				return null;
			}

			_logVerbose($"  Parsing component {componentIndex} text content...");

			var component = new Component
			{
				Guid = Guid.NewGuid(),
			};

			// Extract Name
			string extractedName = ExtractValue(componentText, _profile.NamePattern, "name");
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
					component.Category = categoryTierMatch.Groups["category"].Value.Trim();
					component.Tier = categoryTierMatch.Groups["tier"].Value.Trim();
					_logVerbose($"  Extracted Category/Tier: '{component.Category}'/'{component.Tier}'");
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
				warning = "Component has no name";
				_logVerbose($"  Component {componentIndex} rejected: no name found");
				return null;
			}

			return component;
		}

		[CanBeNull]
		private static string ExtractValue([NotNull] string source, [CanBeNull] string pattern, [NotNull] string groupName)
		{
			if ( string.IsNullOrWhiteSpace(pattern) )
				return null;

			Match match = Regex.Match(source, pattern, RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnoreCase);
			return match.Success ? match.Groups[groupName].Value.Trim() : null;
		}
	}
}

