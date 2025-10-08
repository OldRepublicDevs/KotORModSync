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
		/// 2. Prefix matching with dynamic length-based scoring
		/// 3. One contains the other (substring matching)
		/// 4. High similarity ratio (>= threshold)
		/// 5. Significant word overlap
		/// 6. Common prefix (base name)
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

			string shorter = norm1.Length < norm2.Length ? norm1 : norm2;
			string longer = norm1.Length < norm2.Length ? norm2 : norm1;

			// Strong prefix matching: if shorter name is complete prefix of longer
			if ( longer.StartsWith(shorter) )
			{
				// Calculate how much was added as a ratio
				double addedRatio = (double)(longer.Length - shorter.Length) / longer.Length;

				// If less than 50% of the total was added, likely same mod with variant suffix
				// This is dynamic based on actual string lengths, not hardcoded patterns
				if ( addedRatio <= 0.50 )
					return true;

				// Even if more was added, still match if the base is substantial (>35% of total)
				double baseRatio = (double)shorter.Length / longer.Length;
				if ( baseRatio >= 0.35 )
					return true;
			}

			// One contains the other (for cases like "Ajunta Pall Appearance" vs "Ajunta Pall Unique Appearance")
			if ( norm1.Contains(norm2) || norm2.Contains(norm1) )
			{
				int minLen = Math.Min(norm1.Length, norm2.Length);
				int maxLen = Math.Max(norm1.Length, norm2.Length);
				double containmentRatio = (double)minLen / maxLen;

				// Dynamic threshold based on containment - lowered from 0.50 to 0.40
				return containmentRatio >= 0.40;
			}

			// Check similarity ratio
			double ratio = SimilarityRatio(norm1, norm2);
			if ( ratio >= threshold )
				return true;

			// Word-based analysis
			string[] words1 = norm1.Split(' ');
			string[] words2 = norm2.Split(' ');

			// Count common words (excluding very short words like "a", "of", etc.)
			string[] meaningfulWords1 = words1.Where(w => w.Length > 2).ToArray();
			string[] meaningfulWords2 = words2.Where(w => w.Length > 2).ToArray();
			string[] commonWords = meaningfulWords1.Where(w => meaningfulWords2.Contains(w)).ToArray();
			int totalUniqueWords = meaningfulWords1.Union(meaningfulWords2).Distinct().Count();

			if ( totalUniqueWords > 0 )
			{
				// If they share a common prefix of 2+ words, likely the same mod
				// (e.g., "Senni Vek Restoration" vs "Senni Vek Mod")
				if ( meaningfulWords1.Length >= 2 && meaningfulWords2.Length >= 2 )
				{
					int commonPrefixLength = 0;
					int minWords = Math.Min(meaningfulWords1.Length, meaningfulWords2.Length);
					for ( int i = 0; i < minWords; i++ )
					{
						if ( meaningfulWords1[i] == meaningfulWords2[i] )
							commonPrefixLength++;
						else
							break;
					}

					// If at least 2 words match at the start, consider it a match
					// This handles "Senni Vek Restoration" vs "Senni Vek Mod"
					if ( commonPrefixLength >= 2 )
						return true;
				}

				double wordOverlap = (double)commonWords.Length / totalUniqueWords;
				// If most meaningful words match, consider it a match
				if ( wordOverlap >= 0.5 )
					return true;
			}

			return false;
		}

		/// <summary>
		/// Checks if two author strings match.
		/// </summary>
		private static bool AuthorsMatch(string author1, string author2)
		{
			string norm1 = Normalize(author1);
			string norm2 = Normalize(author2);

			// Empty or unknown authors always match
			if ( string.IsNullOrWhiteSpace(norm1) || string.IsNullOrWhiteSpace(norm2) ||
				 norm1 == "unknown author" || norm2 == "unknown author" )
				return true;

			// Exact match
			if ( norm1 == norm2 )
				return true;

			// Check if one author name is a prefix of the other
			// This is common when one entry has additional credits
			if ( norm1.StartsWith(norm2 + ",") || norm1.StartsWith(norm2 + " ") ||
				 norm2.StartsWith(norm1 + ",") || norm2.StartsWith(norm1 + " ") )
				return true;

			// Check if shorter author appears at the start of longer author
			string shorter = norm1.Length < norm2.Length ? norm1 : norm2;
			string longer = norm1.Length < norm2.Length ? norm2 : norm1;
			if ( longer.StartsWith(shorter) && shorter.Length >= 3 ) // At least 3 chars to avoid false positives
				return true;

			// Fall back to fuzzy matching for cases like typos
			return AreSimilar(norm1, norm2, threshold: 0.8);
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
			string normIncomingName = Normalize(incomingName);

			// If authors don't match, probably not the same mod
			return AuthorsMatch(existingAuthor, incomingAuthor) &&
				   // Check if names are similar
				   // Use a slightly lower threshold for names since they might have variations like "Unique", "Enhanced", etc.
				   AreSimilar(normExistingName, normIncomingName, threshold: 0.70);
		}

		/// <summary>
		/// Calculates a similarity score (0.0 to 1.0) between two components.
		/// Higher score means more similar. Uses dynamic algorithmic scoring.
		/// </summary>
		public static double GetMatchScore(string existingName, string existingAuthor, string incomingName, string incomingAuthor)
		{
			// If authors don't match, score is 0
			if ( !AuthorsMatch(existingAuthor, incomingAuthor) )
				return 0.0;

			string norm1 = Normalize(existingName);
			string norm2 = Normalize(incomingName);

			// Exact match = perfect score
			if ( norm1 == norm2 )
				return 1.0;

			string shorter = norm1.Length < norm2.Length ? norm1 : norm2;
			string longer = norm1.Length < norm2.Length ? norm2 : norm1;

			// Calculate base similarity ratio
			double baseScore = SimilarityRatio(norm1, norm2);

			// Strong boost for prefix matches (e.g., "Ultimate Korriban" vs "Ultimate Korriban High Resolution")
			if ( longer.StartsWith(shorter) )
			{
				// Score based on how much of the longer string is the shorter string
				double prefixRatio = (double)shorter.Length / longer.Length;

				// Very high score for strong prefix matches (>50% is original name)
				if ( prefixRatio >= 0.50 )
					baseScore = Math.Max(baseScore, 0.90 + (prefixRatio - 0.50) * 0.20); // 0.90 to 1.0 range
				else if ( prefixRatio >= 0.40 )
					baseScore = Math.Max(baseScore, 0.80 + (prefixRatio - 0.40) * 1.0); // 0.80 to 0.90 range
				else
					baseScore = Math.Max(baseScore, prefixRatio * 2.0); // Scale up smaller ratios
			}
			// Boost score if one contains the other (non-prefix substring)
			else if ( norm1.Contains(norm2) || norm2.Contains(norm1) )
			{
				int minLen = Math.Min(norm1.Length, norm2.Length);
				int maxLen = Math.Max(norm1.Length, norm2.Length);
				double containmentScore = (double)minLen / maxLen;

				// Boost containment score
				containmentScore = Math.Min(1.0, containmentScore * 1.2);
				baseScore = Math.Max(baseScore, containmentScore);
			}

			// Word-based scoring
			string[] words1 = norm1.Split(' ');
			string[] words2 = norm2.Split(' ');
			string[] meaningfulWords1 = words1.Where(w => w.Length > 2).ToArray();
			string[] meaningfulWords2 = words2.Where(w => w.Length > 2).ToArray();

			if ( meaningfulWords1.Length > 0 && meaningfulWords2.Length > 0 )
			{
				string[] commonWords = meaningfulWords1.Where(w => meaningfulWords2.Contains(w)).ToArray();
				int totalUniqueWords = meaningfulWords1.Union(meaningfulWords2).Distinct().Count();
				double wordOverlapScore = (double)commonWords.Length / totalUniqueWords;

				// Check common prefix
				int commonPrefixLength = 0;
				int minWords = Math.Min(meaningfulWords1.Length, meaningfulWords2.Length);
				for ( int i = 0; i < minWords; i++ )
				{
					if ( meaningfulWords1[i] == meaningfulWords2[i] )
						commonPrefixLength++;
					else
						break;
				}
				double prefixScore = (double)commonPrefixLength / Math.Max(meaningfulWords1.Length, meaningfulWords2.Length);

				// Use the best of these scores
				baseScore = Math.Max(baseScore, Math.Max(wordOverlapScore, prefixScore));
			}

			return baseScore;
		}

		/// <summary>
		/// Wrapper for ModComponent objects - returns true if components match.
		/// </summary>
		public static bool FuzzyMatchComponents(Core.ModComponent existing, Core.ModComponent incoming) => FuzzyMatch(existing.Name, existing.Author, incoming.Name, incoming.Author);

		/// <summary>
		/// Wrapper for ModComponent objects - returns similarity score.
		/// </summary>
		public static double GetComponentMatchScore(Core.ModComponent existing, Core.ModComponent incoming) => GetMatchScore(existing.Name, existing.Author, incoming.Name, incoming.Author);
	}
}

