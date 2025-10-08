// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Parsing
{
	/// <summary>
	/// Utility methods for parsing and processing markdown content
	/// </summary>
	public static class MarkdownUtilities
	{
		/// <summary>
		/// Extracts the mod list section from markdown content
		/// </summary>
		/// <param name="markdown">The full markdown content</param>
		/// <returns>The content starting from "## Mod List" marker</returns>
		[NotNull]
		public static string ExtractModListSection([NotNull] string markdown)
		{
			if ( markdown == null )
				throw new ArgumentNullException(nameof(markdown));

			int modListIndex = markdown.IndexOf("## Mod List", StringComparison.Ordinal);
			if ( modListIndex >= 0 )
			{
				return markdown.Substring(modListIndex);
			}
			return markdown;
		}

		/// <summary>
		/// Splits markdown into individual mod component sections using separator "___"
		/// </summary>
		/// <param name="markdown">The markdown content to split</param>
		/// <returns>List of mod sections that contain both ### heading and **Name:** field</returns>
		[NotNull]
		[ItemNotNull]
		public static List<string> ExtractModSections([NotNull] string markdown)
		{
			if ( markdown == null )
				throw new ArgumentNullException(nameof(markdown));

			// Split by the separator "___"
			var sections = new List<string>();
			string[] lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
			var currentSection = new List<string>();

			foreach ( string line in lines )
			{
				if ( line.Trim() == "___" )
				{
					if ( currentSection.Count > 0 )
					{
						string sectionText = string.Join(Environment.NewLine, currentSection).Trim();
						// Only count sections that have both ### heading AND **Name:** field (actual mod components)
						if ( !string.IsNullOrWhiteSpace(sectionText)
							&& sectionText.Contains("###")
							&& Regex.IsMatch(sectionText, @"\*\*Name:\*\*", RegexOptions.Multiline) )
						{
							sections.Add(sectionText);
						}
					}
					currentSection.Clear();
				}
				else
				{
					currentSection.Add(line);
				}
			}

			// Add the last section
			if ( currentSection.Count > 0 )
			{
				string sectionText = string.Join(Environment.NewLine, currentSection).Trim();
				// Only count sections that have both ### heading AND **Name:** field (actual mod components)
				if ( !string.IsNullOrWhiteSpace(sectionText)
					&& sectionText.Contains("###")
					&& Regex.IsMatch(sectionText, @"\*\*Name:\*\*", RegexOptions.Multiline) )
				{
					sections.Add(sectionText);
				}
			}

			return sections;
		}

		/// <summary>
		/// Extracts a single field value from text using a regex pattern
		/// </summary>
		/// <param name="text">The text to search</param>
		/// <param name="pattern">The regex pattern to use</param>
		/// <returns>The first captured group value, or empty string if no match</returns>
		[NotNull]
		public static string ExtractFieldValue([NotNull] string text, [NotNull] string pattern)
		{
			if ( text == null )
				throw new ArgumentNullException(nameof(text));
			if ( pattern == null )
				throw new ArgumentNullException(nameof(pattern));

			Match match = Regex.Match(text, pattern, RegexOptions.Multiline);
			if ( match.Success && match.Groups.Count > 1 )
			{
				return match.Groups[1].Value.Trim();
			}
			return string.Empty;
		}

		/// <summary>
		/// Extracts all field values from text using a regex pattern
		/// </summary>
		/// <param name="text">The text to search</param>
		/// <param name="pattern">The regex pattern to use</param>
		/// <returns>List of all matched values (from groups 1 and 2)</returns>
		[NotNull]
		[ItemNotNull]
		public static List<string> ExtractAllFieldValues([NotNull] string text, [NotNull] string pattern)
		{
			if ( text == null )
				throw new ArgumentNullException(nameof(text));
			if ( pattern == null )
				throw new ArgumentNullException(nameof(pattern));

			MatchCollection matches = Regex.Matches(text, pattern, RegexOptions.Multiline);
			var values = new List<string>();

			foreach ( Match match in matches )
			{
				if ( match.Success && match.Groups.Count > 1 )
				{
					// Try group 1 first, then group 2 (for optional groups in the pattern)
					string value = match.Groups[1].Value.Trim();
					if ( string.IsNullOrWhiteSpace(value) && match.Groups.Count > 2 )
					{
						value = match.Groups[2].Value.Trim();
					}
					if ( !string.IsNullOrWhiteSpace(value) )
					{
						values.Add(value);
					}
				}
			}

			return values;
		}

		/// <summary>
		/// Normalizes whitespace in text by replacing multiple spaces with a single space
		/// </summary>
		/// <param name="text">The text to normalize</param>
		/// <returns>Text with normalized whitespace</returns>
		[NotNull]
		public static string NormalizeWhitespace([CanBeNull] string text)
		{
			if ( string.IsNullOrEmpty(text) )
				return string.Empty;

			// Replace multiple spaces with single space
			return Regex.Replace(text.Trim(), @"\s+", " ");
		}

		/// <summary>
		/// Normalizes category format by replacing comma separators with ampersands and normalizing whitespace
		/// </summary>
		/// <param name="category">The category string to normalize</param>
		/// <returns>Normalized category string with ampersands as separators</returns>
		[NotNull]
		public static string NormalizeCategoryFormat([CanBeNull] string category)
		{
			if ( string.IsNullOrEmpty(category) )
				return string.Empty;

			// Normalize whitespace first
			category = NormalizeWhitespace(category);

			// Replace comma separators with ampersand for consistency
			// "Bugfix, Graphics & Immersion" becomes "Bugfix & Graphics & Immersion"
			category = Regex.Replace(category, @",\s*", " & ");

			// Normalize multiple ampersands to single
			category = Regex.Replace(category, @"\s*&\s*", " & ");

			return category.Trim();
		}
	}
}


