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
	/// 5. Common prefix (base name)
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
			// Lowered threshold from 0.7 to 0.6 for better matching
			// If most meaningful words match, consider it a match
			if ( wordOverlap >= 0.6 )
				return true;
		}

		return false;
	}

	/// <summary>
	/// Checks if two author strings match, accounting for cases like "Quanon" vs "Quanon, patch by JCarter426".
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

		// Check if one author name is a prefix of the other (handles "Quanon" vs "Quanon, patch by...")
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
	/// Higher score means more similar.
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

		// Calculate base similarity ratio
		double baseScore = SimilarityRatio(norm1, norm2);

		// Boost score if one contains the other
		if ( norm1.Contains(norm2) || norm2.Contains(norm1) )
		{
			int minLen = Math.Min(norm1.Length, norm2.Length);
			int maxLen = Math.Max(norm1.Length, norm2.Length);
			double containmentScore = (double)minLen / maxLen;
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
	/// Wrapper for Component objects - returns true if components match.
	/// </summary>
	public static bool FuzzyMatchComponents(Core.Component existing, Core.Component incoming) => FuzzyMatch(existing.Name, existing.Author, incoming.Name, incoming.Author);

	/// <summary>
	/// Wrapper for Component objects - returns similarity score.
	/// </summary>
	public static double GetComponentMatchScore(Core.Component existing, Core.Component incoming) => GetMatchScore(existing.Name, existing.Author, incoming.Name, incoming.Author);
	}
}

