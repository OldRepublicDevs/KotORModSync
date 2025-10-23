// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Converters
{
	public partial class SpoilerFreeContentConverter : IMultiValueConverter
	{
		public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			if ( values == null || values.Count < 4 )
				return string.Empty;

			// First value should be the regular property name (e.g., "Description")
			// Second value should be the ModComponent
			// Third value should be the SpoilerFreeMode boolean
			// Fourth value should be the spoiler-free property name (e.g., "DescriptionSpoilerFree")

			string regularPropertyName = values[0]?.ToString() ?? string.Empty;
			if ( string.IsNullOrWhiteSpace(regularPropertyName) )
				return string.Empty;

			if ( !(values[1] is ModComponent component) )
				return string.Empty;

			if ( !(values[2] is bool spoilerFreeMode) )
				return string.Empty;

			string spoilerFreePropertyName = values[3]?.ToString() ?? string.Empty;
			if ( string.IsNullOrWhiteSpace(spoilerFreePropertyName) )
				return string.Empty;

			// Use reflection to dynamically get the property value
			return GetPropertyValue(component, regularPropertyName, spoilerFreePropertyName, spoilerFreeMode);
		}

		/// <summary>
		/// Dynamically retrieves the appropriate property value based on spoiler-free mode.
		/// </summary>
		private static string GetPropertyValue(ModComponent component, string regularPropertyName, string spoilerFreePropertyName, bool spoilerFreeMode)
		{
			try
			{
				Type componentType = typeof(ModComponent);

				// If in spoiler-free mode, try to get the spoiler-free version first
				if ( spoilerFreeMode )
				{
					PropertyInfo spoilerFreeProperty = componentType.GetProperty(spoilerFreePropertyName);

					if ( spoilerFreeProperty != null )
					{
						object spoilerFreeValue = spoilerFreeProperty.GetValue(component);
						if ( spoilerFreeValue is string spoilerFreeString )
						{
							return spoilerFreeString ?? string.Empty;
						}
					}
				}

				// Get the regular property value
				PropertyInfo regularProperty = componentType.GetProperty(regularPropertyName);
				if ( regularProperty != null )
				{
					object regularValue = regularProperty.GetValue(component);
					if ( regularValue is string regularString )
					{
						return regularString ?? string.Empty;
					}
				}

				// If property doesn't exist or is not a string, return empty
				return string.Empty;
			}
			catch ( Exception )
			{
				// If anything goes wrong, return empty string
				return string.Empty;
			}
		}

		/// <summary>
		/// Generates spoiler-free content by intelligently filtering out spoiler content.
		/// </summary>
		private static string GenerateSpoilerFreeContent(string originalContent)
		{
			if ( string.IsNullOrWhiteSpace(originalContent) )
				return string.Empty;

			// Split into sentences and process each one
			var sentences = originalContent.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
			var result = new StringBuilder();

			foreach ( var sentence in sentences )
			{
				var trimmedSentence = sentence.Trim();
				if ( string.IsNullOrWhiteSpace(trimmedSentence) )
					continue;

				// Skip sentences that likely contain spoilers
				if ( ContainsSpoilerContent(trimmedSentence) )
					continue;

				// Process the sentence to remove spoiler words
				var processedSentence = ProcessSentenceForSpoilers(trimmedSentence);

				if ( !string.IsNullOrWhiteSpace(processedSentence) )
				{
					if ( result.Length > 0 )
						result.Append(". ");
					result.Append(processedSentence);
				}
			}

			var spoilerFreeContent = result.ToString().Trim();

			// If the result is too short, create a generic abbreviation
			if ( string.IsNullOrWhiteSpace(spoilerFreeContent) || spoilerFreeContent.Length < 10 )
			{
				spoilerFreeContent = GenerateGenericAbbreviation(originalContent);
			}

			return spoilerFreeContent;
		}

		/// <summary>
		/// Processes a sentence to remove spoiler content while preserving important information.
		/// </summary>
		private static string ProcessSentenceForSpoilers(string sentence)
		{
			var words = sentence.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			var result = new StringBuilder();

			foreach ( var word in words )
			{
				// Skip words that might contain spoilers
				if ( ContainsSpoilerWord(word) )
					continue;

				// Add the word to result
				if ( result.Length > 0 )
					result.Append(' ');
				result.Append(word);
			}

			return result.ToString().Trim();
		}

		/// <summary>
		/// Checks if a sentence likely contains spoiler content.
		/// </summary>
		private static bool ContainsSpoilerContent(string sentence)
		{
			var spoilerPatterns = new[]
			{
				@"\b(ending|conclusion|final|last|death|die|dies|died|killed|kills|murder|betray|betrayal|betrayed|reveal|reveals|revealed|secret|secrets|hidden|truth|true|lies|lie|lying|deceive|deception|deceived)\b",
				@"\b(plot|story|narrative|twist|surprise|shock|shocking|unexpected|spoiler|spoilers)\b",
				@"\b(character|characters|name|names|identity|identities|who|what|when|where|why|how)\b.*\b(is|was|are|were|will|would|should|could|might|may)\b",
				@"\b(he|she|they|him|her|them|his|hers|theirs)\b.*\b(is|was|are|were|will|would|should|could|might|may)\b"
			};

			foreach ( var pattern in spoilerPatterns )
			{
				if ( Regex.IsMatch(sentence, pattern, RegexOptions.IgnoreCase) )
					return true;
			}

			return false;
		}

		/// <summary>
		/// Checks if a word likely contains spoiler content.
		/// </summary>
		private static bool ContainsSpoilerWord(string word)
		{
			var spoilerWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				"ending", "conclusion", "final", "last", "death", "die", "dies", "died", "killed", "kills", "murder",
				"betray", "betrayal", "betrayed", "reveal", "reveals", "revealed", "secret", "secrets", "hidden",
				"truth", "true", "lies", "lie", "lying", "deceive", "deception", "deceived", "plot", "story",
				"narrative", "twist", "surprise", "shock", "shocking", "unexpected", "spoiler", "spoilers"
			};

			return spoilerWords.Contains(word);
		}

		/// <summary>
		/// Generates a spoiler-free abbreviation for a property name when no content is available.
		/// </summary>
		private static string GenerateSpoilerFreeAbbreviation(string propertyName)
		{
			if ( string.IsNullOrWhiteSpace(propertyName) )
				return "Content";

			// Convert property name to a readable format
			var words = Regex.Split(propertyName, @"(?<!^)(?=[A-Z])");
			var result = new StringBuilder();

			foreach ( var word in words )
			{
				if ( !string.IsNullOrWhiteSpace(word) )
				{
					if ( result.Length > 0 )
						result.Append(' ');
					result.Append(word);
				}
			}

			return result.ToString().Trim();
		}

		/// <summary>
		/// Generates a generic abbreviation when spoiler-free content is too short.
		/// </summary>
		private static string GenerateGenericAbbreviation(string originalText)
		{
			if ( string.IsNullOrWhiteSpace(originalText) )
				return "Content";

			// Extract first letter of each word, plus some key letters
			var words = originalText.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
			var abbreviation = new StringBuilder();

			foreach ( var word in words.Take(5) ) // Limit to first 5 words
			{
				if ( word.Length > 0 )
				{
					abbreviation.Append(char.ToUpper(word[0]));

					// Add a few more letters from longer words
					if ( word.Length > 3 )
					{
						for ( int i = 1; i < Math.Min(3, word.Length); i++ )
						{
							if ( char.IsLetter(word[i]) )
								abbreviation.Append(char.ToLower(word[i]));
						}
					}
				}
			}

			var result = abbreviation.ToString();
			return string.IsNullOrWhiteSpace(result) ? "Content" : result;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
