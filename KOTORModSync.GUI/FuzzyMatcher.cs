// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Linq;

namespace KOTORModSync
{
	/// <summary>
	/// Provides fuzzy matching utilities for comparing component names and authors.
	/// </summary>
	public static class FuzzyMatcher
	{
		/// <summary>
		/// Calculates the Levenshtein distance between two strings.
		/// This represents the minimum number of single-character edits required to change one string into another.
		/// </summary>
		private static int LevenshteinDistance(string s1, string s2)
		{
			if ( string.IsNullOrEmpty(s1) )
				return string.IsNullOrEmpty(s2) ? 0 : s2.Length;
			if ( string.IsNullOrEmpty(s2) )
				return s1.Length;

			int n = s1.Length;
			int m = s2.Length;
			int[,] d = new int[n + 1, m + 1];

			for ( int i = 0; i <= n; i++ )
				d[i, 0] = i;
			for ( int j = 0; j <= m; j++ )
				d[0, j] = j;

			for ( int i = 1; i <= n; i++ )
			{
				for ( int j = 1; j <= m; j++ )
				{
					int cost = s2[j - 1] == s1[i - 1] ? 0 : 1;
					d[i, j] = Math.Min(
						Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
						d[i - 1, j - 1] + cost);
				}
			}

			return d[n, m];
		}

		/// <summary>
		/// Calculates a similarity ratio between 0 and 1, where 1 is an exact match.
		/// </summary>
		private static double SimilarityRatio(string s1, string s2)
		{
			if ( string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2) )
				return 1.0;
			if ( string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2) )
				return 0.0;

			int maxLen = Math.Max(s1.Length, s2.Length);
			int distance = LevenshteinDistance(s1, s2);
			return 1.0 - (double)distance / maxLen;
		}

		/// <summary>
		/// Normalizes a string for comparison by converting to lowercase and removing extra whitespace.
		/// </summary>
		private static string Normalize(string input)
		{
			if ( string.IsNullOrEmpty(input) )
				return string.Empty;

			// Convert to lowercase and collapse multiple spaces
			return string.Join(" ", input.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
		}

		/// <summary>
		/// Checks if two strings are similar enough based on multiple criteria:
		/// 1. Exact match
		/// 2. One contains the other
		/// 3. High similarity ratio (>= threshold)
		/// 4. Significant word overlap
		/// </summary>
		private static bool AreSimilar(string s1, string s2, double threshold = 0.75)
		{
			if ( string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2) )
				return false;

			string norm1 = Normalize(s1);
			string norm2 = Normalize(s2);

			// Exact match after normalization
			if ( norm1 == norm2 )
				return true;

			// One contains the other (for cases like "Ajunta Pall Appearance" vs "Ajunta Pall Unique Appearance")
			if ( norm1.Contains(norm2) || norm2.Contains(norm1) )
			{
				// But only if they share a significant portion (at least 60% of shorter string)
				int minLen = Math.Min(norm1.Length, norm2.Length);
				int maxLen = Math.Max(norm1.Length, norm2.Length);
				return (double)minLen / maxLen >= 0.6;
			}

			// Check similarity ratio
			double ratio = SimilarityRatio(norm1, norm2);
			if ( ratio >= threshold )
				return true;

			// Check word overlap (for cases with reordered words)
			string[] words1 = norm1.Split(' ');
			string[] words2 = norm2.Split(' ');

			// Count common words (excluding very short words like "a", "of", etc.)
			string[] commonWords = words1.Where(w => w.Length > 2 && words2.Contains(w)).ToArray();
			int totalUniqueWords = words1.Union(words2).Count(w => w.Length > 2);

			if ( totalUniqueWords > 0 )
			{
				double wordOverlap = (double)commonWords.Length / totalUniqueWords;
				// If most meaningful words match, consider it a match
				return wordOverlap >= 0.7;
			}

			return false;
		}

		/// <summary>
		/// Performs fuzzy matching between two components based on their name and author.
		/// Returns true if the components are likely the same mod.
		/// </summary>
		/// <param name="existingName">Name of the existing component</param>
		/// <param name="existingAuthor">Author of the existing component</param>
		/// <param name="incomingName">Name of the incoming component</param>
		/// <param name="incomingAuthor">Author of the incoming component</param>
		/// <returns>True if components are likely a match</returns>
		public static bool FuzzyMatch(string existingName, string existingAuthor, string incomingName, string incomingAuthor)
		{
			// Normalize inputs
			string normExistingName = Normalize(existingName);
			string normExistingAuthor = Normalize(existingAuthor);
			string normIncomingName = Normalize(incomingName);
			string normIncomingAuthor = Normalize(incomingAuthor);

			// If authors are very different, probably not a match (unless one is empty/unknown)
			bool authorMatches = string.IsNullOrWhiteSpace(normExistingAuthor) ||
			                     string.IsNullOrWhiteSpace(normIncomingAuthor) ||
			                     normExistingAuthor == "unknown author" ||
			                     normIncomingAuthor == "unknown author" ||
			                     AreSimilar(normExistingAuthor, normIncomingAuthor, threshold: 0.8);

			if ( !authorMatches )
				return false;

			// Check if names are similar
			// Use a slightly lower threshold for names since they might have variations like "Unique", "Enhanced", etc.
			return AreSimilar(normExistingName, normIncomingName, threshold: 0.70);
		}

		/// <summary>
		/// Wrapper for Component objects.
		/// </summary>
		public static bool FuzzyMatchComponents(Core.Component existing, Core.Component incoming) => FuzzyMatch(existing.Name, existing.Author, incoming.Name, incoming.Author);
	}
}

