// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Linq;

namespace KOTORModSync
{

	public static class FuzzyMatcher
	{

		private static int LevenshteinDistance( string s1, string s2 )
		{
			if (string.IsNullOrEmpty( s1 ))
				return string.IsNullOrEmpty( s2 ) ? 0 : s2.Length;
			if (string.IsNullOrEmpty( s2 ))
				return s1.Length;

			int n = s1.Length;
			int m = s2.Length;
			int[,] d = new int[n + 1, m + 1];

			for (int i = 0; i <= n; i++)
				d[i, 0] = i;
			for (int j = 0; j <= m; j++)
				d[0, j] = j;

			for (int i = 1; i <= n; i++)
			{
				for (int j = 1; j <= m; j++)
				{
					int cost = s2[j - 1] == s1[i - 1] ? 0 : 1;
					d[i, j] = Math.Min(
						Math.Min( d[i - 1, j] + 1, d[i, j - 1] + 1 ),
						d[i - 1, j - 1] + cost );
				}
			}

			return d[n, m];
		}

		private static double SimilarityRatio( string s1, string s2 )
		{
			if (string.IsNullOrEmpty( s1 ) && string.IsNullOrEmpty( s2 ))
				return 1.0;
			if (string.IsNullOrEmpty( s1 ) || string.IsNullOrEmpty( s2 ))
				return 0.0;

			int maxLen = Math.Max( s1.Length, s2.Length );
			int distance = LevenshteinDistance( s1, s2 );
			return 1.0 - (double)distance / maxLen;
		}

		private static string Normalize( string input )
		{
			if (string.IsNullOrEmpty( input ))
				return string.Empty;

			return string.Join( " ", input.ToLowerInvariant().Split( new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries ) );
		}

		private static bool AreSimilar( string s1, string s2, double threshold = 0.75 )
		{
			if (string.IsNullOrEmpty( s1 ) || string.IsNullOrEmpty( s2 ))
				return false;

			string norm1 = Normalize( s1 );
			string norm2 = Normalize( s2 );

			if (string.Equals( norm1, norm2, StringComparison.Ordinal ))
				return true;

			string shorter = norm1.Length < norm2.Length ? norm1 : norm2;
			string longer = norm1.Length < norm2.Length ? norm2 : norm1;

			if (longer.StartsWith( shorter, StringComparison.Ordinal ))
			{

				double addedRatio = (double)(longer.Length - shorter.Length) / longer.Length;

				if (addedRatio <= 0.50)
					return true;

				double baseRatio = (double)shorter.Length / longer.Length;
				if (baseRatio >= 0.35)
					return true;
			}

			if (norm1.Contains( norm2 ) || norm2.Contains( norm1 ))
			{
				int minLen = Math.Min( norm1.Length, norm2.Length );
				int maxLen = Math.Max( norm1.Length, norm2.Length );
				double containmentRatio = (double)minLen / maxLen;

				return containmentRatio >= 0.40;
			}

			double ratio = SimilarityRatio( norm1, norm2 );
			if (ratio >= threshold)
				return true;

			string[] words1 = norm1.Split( ' ' );
			string[] words2 = norm2.Split( ' ' );

			string[] meaningfulWords1 = words1.Where( w => w.Length > 2 ).ToArray();
			string[] meaningfulWords2 = words2.Where( w => w.Length > 2 ).ToArray();


			string[] commonWords = meaningfulWords1.Where( w => meaningfulWords2.Contains( w, StringComparer.Ordinal ) ).ToArray();
			int totalUniqueWords = meaningfulWords1.Union( meaningfulWords2, StringComparer.Ordinal ).Distinct( StringComparer.Ordinal ).Count();

			if (totalUniqueWords > 0)
			{

				if (meaningfulWords1.Length >= 2 && meaningfulWords2.Length >= 2)
				{
					int commonPrefixLength = 0;
					int minWords = Math.Min( meaningfulWords1.Length, meaningfulWords2.Length );
					for (int i = 0; i < minWords; i++)
					{
						if (string.Equals( meaningfulWords1[i], meaningfulWords2[i], StringComparison.Ordinal ))
							commonPrefixLength++;
						else
							break;
					}

					if (commonPrefixLength >= 2)
						return true;
				}

				double wordOverlap = (double)commonWords.Length / totalUniqueWords;

				if (wordOverlap >= 0.5)
					return true;
			}

			return false;
		}

		private static bool AuthorsMatch( string author1, string author2 )
		{
			string norm1 = Normalize( author1 );
			string norm2 = Normalize( author2 );

			if (string.IsNullOrWhiteSpace( norm1 ) || string.IsNullOrWhiteSpace( norm2 ) ||
string.Equals( norm1, "unknown author", StringComparison.Ordinal ) || string.Equals( norm2, "unknown author", StringComparison.Ordinal ))
				return true;

			if (string.Equals( norm1, norm2, StringComparison.Ordinal ))
				return true;

			if (norm1.StartsWith( norm2 + ",", StringComparison.Ordinal ) || norm1.StartsWith( norm2 + " ", StringComparison.Ordinal ) ||
				 norm2.StartsWith( norm1 + ",", StringComparison.Ordinal ) || norm2.StartsWith( norm1 + " ", StringComparison.Ordinal ))
				return true;

			string shorter = norm1.Length < norm2.Length ? norm1 : norm2;
			string longer = norm1.Length < norm2.Length ? norm2 : norm1;
			if (longer.StartsWith( shorter, StringComparison.Ordinal ) && shorter.Length >= 3)
				return true;

			return AreSimilar( norm1, norm2, threshold: 0.8 );
		}

		public static bool FuzzyMatch( string existingName, string existingAuthor, string incomingName, string incomingAuthor )
		{

			string normExistingName = Normalize( existingName );
			string normIncomingName = Normalize( incomingName );

			return AuthorsMatch( existingAuthor, incomingAuthor ) &&

				   AreSimilar( normExistingName, normIncomingName, threshold: 0.70 );
		}


		public static double GetMatchScore( string existingName, string existingAuthor, string incomingName, string incomingAuthor )
		{

			if (!AuthorsMatch( existingAuthor, incomingAuthor ))
				return 0.0;

			string norm1 = Normalize( existingName );
			string norm2 = Normalize( incomingName );

			if (string.Equals( norm1, norm2, StringComparison.Ordinal ))
				return 1.0;

			string shorter = norm1.Length < norm2.Length ? norm1 : norm2;
			string longer = norm1.Length < norm2.Length ? norm2 : norm1;

			double baseScore = SimilarityRatio( norm1, norm2 );

			if (longer.StartsWith( shorter, StringComparison.Ordinal ))
			{

				double prefixRatio = (double)shorter.Length / longer.Length;

				if (prefixRatio >= 0.50)
					baseScore = Math.Max( baseScore, 0.90 + (prefixRatio - 0.50) * 0.20 );
				else if (prefixRatio >= 0.40)
					baseScore = Math.Max( baseScore, 0.80 + (prefixRatio - 0.40) * 1.0 );
				else
					baseScore = Math.Max( baseScore, prefixRatio * 2.0 );
			}
			else if (norm1.Contains( norm2 ) || norm2.Contains( norm1 ))
			{
				int minLen = Math.Min( norm1.Length, norm2.Length );
				int maxLen = Math.Max( norm1.Length, norm2.Length );
				double containmentScore = (double)minLen / maxLen;

				containmentScore = Math.Min( 1.0, containmentScore * 1.2 );
				baseScore = Math.Max( baseScore, containmentScore );
			}

			string[] words1 = norm1.Split( ' ' );
			string[] words2 = norm2.Split( ' ' );
			string[] meaningfulWords1 = words1.Where( w => w.Length > 2 ).ToArray();
			string[] meaningfulWords2 = words2.Where( w => w.Length > 2 ).ToArray();

			if (meaningfulWords1.Length > 0 && meaningfulWords2.Length > 0)


			{
				string[] commonWords = meaningfulWords1.Where( w => meaningfulWords2.Contains( w, StringComparer.Ordinal ) ).ToArray();
				int totalUniqueWords = meaningfulWords1.Union( meaningfulWords2, StringComparer.Ordinal ).Distinct( StringComparer.Ordinal ).Count();
				double wordOverlapScore = (double)commonWords.Length / totalUniqueWords;

				int commonPrefixLength = 0;
				int minWords = Math.Min( meaningfulWords1.Length, meaningfulWords2.Length );
				for (int i = 0; i < minWords; i++)
				{
					if (string.Equals( meaningfulWords1[i], meaningfulWords2[i], StringComparison.Ordinal ))
						commonPrefixLength++;
					else
						break;
				}
				double prefixScore = (double)commonPrefixLength / Math.Max( meaningfulWords1.Length, meaningfulWords2.Length );

				baseScore = Math.Max( baseScore, Math.Max( wordOverlapScore, prefixScore ) );
			}

			return baseScore;
		}

		public static bool FuzzyMatchComponents( Core.ModComponent existing, Core.ModComponent incoming ) => FuzzyMatch( existing.Name, existing.Author, incoming.Name, incoming.Author );

		public static double GetComponentMatchScore( Core.ModComponent existing, Core.ModComponent incoming ) => GetMatchScore( existing.Name, existing.Author, incoming.Name, incoming.Author );
	}
}