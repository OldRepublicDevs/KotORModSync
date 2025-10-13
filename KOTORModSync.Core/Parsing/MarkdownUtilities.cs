



using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Parsing
{
	
	
	
	public static class MarkdownUtilities
	{
		
		
		
		
		
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

		
		
		
		
		
		[NotNull]
		[ItemNotNull]
		public static List<string> ExtractModSections([NotNull] string markdown)
		{
			if ( markdown == null )
				throw new ArgumentNullException(nameof(markdown));

			
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

			
			if ( currentSection.Count > 0 )
			{
				string sectionText = string.Join(Environment.NewLine, currentSection).Trim();
				
				if ( !string.IsNullOrWhiteSpace(sectionText)
					&& sectionText.Contains("###")
					&& Regex.IsMatch(sectionText, @"\*\*Name:\*\*", RegexOptions.Multiline) )
				{
					sections.Add(sectionText);
				}
			}

			return sections;
		}

		
		
		
		
		
		
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

		
		
		
		
		
		[NotNull]
		public static string NormalizeWhitespace([CanBeNull] string text)
		{
			if ( string.IsNullOrEmpty(text) )
				return string.Empty;

			
			return Regex.Replace(text.Trim(), @"\s+", " ");
		}

		
		
		
		
		
		[NotNull]
		public static string NormalizeCategoryFormat([CanBeNull] string category)
		{
			if ( string.IsNullOrEmpty(category) )
				return string.Empty;

			
			category = NormalizeWhitespace(category);

			
			
			category = Regex.Replace(category, @",\s*", " & ");

			
			category = Regex.Replace(category, @"\s*&\s*", " & ");

			return category.Trim();
		}
	}
}


