using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{
	public static class MarkdownMergeService
	{
		private static string Normalize([NotNull] string value, bool ignoreCase, bool ignorePunctuation, bool trim)
		{
			string s = value ?? string.Empty;
			if ( trim ) s = s.Trim();
			if ( ignorePunctuation )
			{
				char[] arr = s.Where(c => !char.IsPunctuation(c)).ToArray();
				s = new string(arr);
			}
			if ( ignoreCase ) s = s.ToLowerInvariant();
			return s;
		}
		private static readonly char[] s_separator = new[] { ' ' };
		private static readonly char[] s_separatorArray = new[] { ' ' };

		private static double JaccardSimilarity([NotNull] string a, [NotNull] string b)
		{
			if ( string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b) ) return 0.0;
			var setA = new HashSet<string>(a.Split(s_separatorArray, StringSplitOptions.RemoveEmptyEntries));
			var setB = new HashSet<string>(b.Split(s_separator, StringSplitOptions.RemoveEmptyEntries));
			int intersection = setA.Intersect(setB).Count();
			int union = setA.Union(setB).Count();
			return union == 0 ? 0.0 : (double)intersection / union;
		}

		private static bool IsBlank([CanBeNull] string v) => string.IsNullOrWhiteSpace(v);

		[NotNull]
		private static string NormalizeKey([NotNull] Component c)
		{
			string name = c?.Name ?? string.Empty;
			string author = c?.Author ?? string.Empty;
			return (name + "|" + author).Trim().ToLowerInvariant();
		}

		public static void MergeInto([NotNull] List<Component> existing, [NotNull] List<Component> parsed, [CanBeNull] MergeHeuristicsOptions options = null)
		{
			if ( existing == null )
				throw new ArgumentNullException(nameof(existing));
			if ( parsed == null )
				throw new ArgumentNullException(nameof(parsed));

			if ( options == null ) options = MergeHeuristicsOptions.CreateDefault();

			// Fast path exact matches first
			var map = existing.ToDictionary(NormalizeKey, c => c);

			foreach ( Component incoming in parsed )
			{
				string key = NormalizeKey(incoming);
				if ( options.UseNameExact && options.UseAuthorExact && map.TryGetValue(key, out Component match) )
				{
					UpdateComponent(match, incoming);
				}
				else
				{
					// Fallback to heuristics across the list
					Component heuristicMatch = FindHeuristicMatch(existing, incoming, options);
					if ( heuristicMatch != null )
					{
						UpdateComponent(heuristicMatch, incoming, options);
					}
					else if ( options.AddNewWhenNoMatchFound )
					{
						// New mod: add without touching others
						existing.Add(incoming);
						map[key] = incoming;
					}
				}
			}
		}


		/// <summary>
		/// Update-only merge: updates properties on existing components from parsed ones.
		/// Does NOT add new components and does NOT remove any existing components here.
		/// GUIDs on existing items are preserved.
		/// </summary>
		public static void UpdateExistingFromParsed(
			[NotNull] List<Component> existing,
			[NotNull] List<Component> parsed,
			[CanBeNull] MergeHeuristicsOptions options = null)
		{
			if ( existing == null )
				throw new ArgumentNullException(nameof(existing));
			if ( parsed == null )
				throw new ArgumentNullException(nameof(parsed));

			if ( options == null ) options = MergeHeuristicsOptions.CreateDefault();

			// Fast exact map for quick hits
			var map = existing.ToDictionary(NormalizeKey, c => c);

			foreach ( Component incoming in parsed )
			{
				string key = NormalizeKey(incoming);
				if ( options.UseNameExact && options.UseAuthorExact && map.TryGetValue(key, out Component match) )
				{
					UpdateComponent(match, incoming, options);
					continue;
				}

				Component heuristicMatch = FindHeuristicMatch(existing, incoming, options);
				if ( heuristicMatch != null )
				{
					UpdateComponent(heuristicMatch, incoming, options);
				}
				// else: no match found -> skip (do not add new)
			}
		}

		/// <summary>
		/// Reorders the existing list so that matched items follow the order of the parsed list,
		/// while keeping unmatched existing items in their original positions.
		/// This does not add or remove any components.
		/// </summary>
		public static void ReorderExistingToParsed(
			[NotNull] List<Component> existing,
			[NotNull] List<Component> parsed,
			[CanBeNull] MergeHeuristicsOptions options = null)
		{
			if ( existing == null )
				throw new ArgumentNullException(nameof(existing));
			if ( parsed == null )
				throw new ArgumentNullException(nameof(parsed));

			if ( options == null ) options = MergeHeuristicsOptions.CreateDefault();

			// Build mapping from parsed to existing components (matched items)
			var matchedInOrder = new List<Component>();
			var matchedSet = new HashSet<Component>();
			var exactMap = existing.ToDictionary(NormalizeKey, c => c);

			foreach ( Component p in parsed )
			{
				Component match = null;
				if ( options.UseNameExact && options.UseAuthorExact )
					_ = exactMap.TryGetValue(NormalizeKey(p), out match);
				if ( match == null )
					match = FindHeuristicMatch(existing, p, options);
				if ( match != null && matchedSet.Add(match) )
					matchedInOrder.Add(match);
			}

			if ( matchedInOrder.Count == 0 )
				return; // nothing to reorder

			// Collect slots (indices) of matched items within existing list
			var slots = existing
				.Select((c, i) => new { c, i })
				.Where(x => matchedSet.Contains(x.c))
				.Select(x => x.i)
				.ToList();

			if ( slots.Count != matchedInOrder.Count )
				return; // safety: should be equal

			// Assign matched items in parsed order into the existing slots
			for ( int k = 0; k < slots.Count; k++ )
			{
				int idx = slots[k];
				existing[idx] = matchedInOrder[k];
			}
		}

		private static Component FindHeuristicMatch([NotNull] List<Component> existing, [NotNull] Component incoming, [NotNull] MergeHeuristicsOptions opt)
		{
			string inName = Normalize(incoming.Name ?? string.Empty, opt.IgnoreCase, opt.IgnorePunctuation, opt.TrimWhitespace);
			string inAuthor = Normalize(incoming.Author ?? string.Empty, opt.IgnoreCase, opt.IgnorePunctuation, opt.TrimWhitespace);

			double bestScore = 0.0;
			Component best = null;
			foreach ( Component e in existing )
			{
				string exName = Normalize(e.Name ?? string.Empty, opt.IgnoreCase, opt.IgnorePunctuation, opt.TrimWhitespace);
				string exAuthor = Normalize(e.Author ?? string.Empty, opt.IgnoreCase, opt.IgnorePunctuation, opt.TrimWhitespace);

				double score = 0.0;
				if ( opt.UseNameExact && !string.IsNullOrEmpty(inName) && inName == exName ) score += 1.0;
				if ( opt.UseAuthorExact && !string.IsNullOrEmpty(inAuthor) && inAuthor == exAuthor ) score += 1.0;
				if ( opt.UseNameSimilarity && !string.IsNullOrEmpty(inName) ) score += JaccardSimilarity(inName, exName);
				if ( opt.UseAuthorSimilarity && !string.IsNullOrEmpty(inAuthor) ) score += JaccardSimilarity(inAuthor, exAuthor) * 0.5;

				// If nothing yet, optionally match by domain of first link
				if ( score < 0.5 && opt.MatchByDomainIfNoNameAuthorMatch )
				{
					string incomingDomain = GetPrimaryDomain(incoming);
					string existingDomain = GetPrimaryDomain(e);
					if ( !string.IsNullOrEmpty(incomingDomain) && incomingDomain == existingDomain )
						score += 0.6;
				}

				if ( score > bestScore && score >= opt.MinNameSimilarity )
				{
					bestScore = score;
					best = e;
				}
			}
			return best;
		}

		private static string GetPrimaryDomain([NotNull] Component c)
		{
			string url = c.ModLink?.FirstOrDefault();
			if ( string.IsNullOrWhiteSpace(url) ) return null;
			try
			{
				var uri = new Uri(url, UriKind.Absolute);
				return uri.Host.ToLowerInvariant();
			}
			catch
			{
				return null;
			}
		}

		private static void UpdateComponent([NotNull] Component target, [NotNull] Component source, [CanBeNull] MergeHeuristicsOptions options = null)
		{
			if ( options == null ) options = MergeHeuristicsOptions.CreateDefault();
			// Keep target.Guid and selection/state; update content properties
			if ( !(options.SkipBlankUpdates && IsBlank(source.Author)) ) target.Author = string.IsNullOrWhiteSpace(source.Author) ? target.Author : source.Author;
			if ( !(options.SkipBlankUpdates && IsBlank(source.Category)) ) target.Category = string.IsNullOrWhiteSpace(source.Category) ? target.Category : source.Category;
			if ( !(options.SkipBlankUpdates && IsBlank(source.Tier)) ) target.Tier = string.IsNullOrWhiteSpace(source.Tier) ? target.Tier : source.Tier;
			if ( !(options.SkipBlankUpdates && IsBlank(source.Description)) ) target.Description = string.IsNullOrWhiteSpace(source.Description) ? target.Description : source.Description;
			if ( !(options.SkipBlankUpdates && IsBlank(source.Directions)) ) target.Directions = string.IsNullOrWhiteSpace(source.Directions) ? target.Directions : source.Directions;
			if ( !(options.SkipBlankUpdates && IsBlank(source.InstallationMethod)) ) target.InstallationMethod = string.IsNullOrWhiteSpace(source.InstallationMethod) ? target.InstallationMethod : source.InstallationMethod;

			// Merge language and links (union)
			if ( source.Language?.Count > 0 )
			{
				var set = new HashSet<string>(target.Language ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
				foreach ( string lang in source.Language ) if ( !string.IsNullOrWhiteSpace(lang) ) _ = set.Add(lang);
				target.Language = set.ToList();
			}

			if ( source.ModLink?.Count > 0 )
			{
				var set = new HashSet<string>(target.ModLink ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
				foreach ( string link in source.ModLink )
				{
					if ( string.IsNullOrWhiteSpace(link) ) continue;
					_ = set.Add(link);
				}
				// Remove invalid or inaccessible links if requested
				if ( options.ValidateExistingLinksBeforeReplace )
				{
					set = new HashSet<string>(set.Where(IsLikelyAccessibleUrl), StringComparer.OrdinalIgnoreCase);
				}
				target.ModLink = set.ToList();
			}

			// Do not remove existing Dependencies/Restrictions; union new GUIDs
			if ( source.Dependencies?.Count > 0 )
			{
				var set = new HashSet<Guid>(target.Dependencies ?? new List<Guid>());
				foreach ( Guid g in source.Dependencies ) _ = set.Add(g);
				target.Dependencies = set.ToList();
			}
			if ( source.Restrictions?.Count > 0 )
			{
				var set = new HashSet<Guid>(target.Restrictions ?? new List<Guid>());
				foreach ( Guid g in source.Restrictions ) _ = set.Add(g);
				target.Restrictions = set.ToList();
			}
			if ( source.InstallAfter?.Count > 0 )
			{
				var set = new HashSet<Guid>(target.InstallAfter ?? new List<Guid>());
				foreach ( Guid g in source.InstallAfter ) _ = set.Add(g);
				target.InstallAfter = set.ToList();
			}
			if ( source.InstallBefore?.Count > 0 )
			{
				var set = new HashSet<Guid>(target.InstallBefore ?? new List<Guid>());
				foreach ( Guid g in source.InstallBefore ) _ = set.Add(g);
				target.InstallBefore = set.ToList();
			}

			// Merge Instructions: append new ones if target has fewer; do NOT delete
			if ( source.Instructions != null && source.Instructions.Count > 0 )
			{
				if ( target.Instructions == null || target.Instructions.Count == 0 )
				{
					foreach ( Instruction instr in source.Instructions )
						target.Instructions.Add(instr);
				}
				else
				{
					// Heuristic: consider new instructions different if Action+Destination combo not present
					var existingKeys = new HashSet<string>(target.Instructions.Select(i => (i.ActionString + "|" + (i.Destination ?? string.Empty)).ToLowerInvariant()));
					foreach ( Instruction instr in source.Instructions )
					{
						string key = (instr.ActionString + "|" + (instr.Destination ?? string.Empty)).ToLowerInvariant();
						if ( !existingKeys.Contains(key) )
							target.Instructions.Add(instr);
					}
				}
			}

			// Merge Options: match by Name (case-insensitive). Do not change IsSelected.
			if ( source.Options != null && source.Options.Count > 0 )
			{
				Dictionary<string, Option> optMap = target.Options?.ToDictionary(o => (o.Name ?? string.Empty).Trim().ToLowerInvariant())
						  ?? new Dictionary<string, Option>();
				foreach ( Option srcOpt in source.Options )
				{
					string oname = (srcOpt.Name ?? string.Empty).Trim().ToLowerInvariant();
					if ( optMap.TryGetValue(oname, out Option trgOpt) )
					{
						if ( !string.IsNullOrWhiteSpace(srcOpt.Description) ) trgOpt.Description = srcOpt.Description;
						// append instructions if not present
						if ( srcOpt.Instructions != null && srcOpt.Instructions.Count > 0 )
						{
							var keys = new HashSet<string>(trgOpt.Instructions.Select(i => (i.ActionString + "|" + (i.Destination ?? string.Empty)).ToLowerInvariant()));
							foreach ( Instruction instr in srcOpt.Instructions )
							{
								string key = (instr.ActionString + "|" + (instr.Destination ?? string.Empty)).ToLowerInvariant();
								if ( !keys.Contains(key) ) trgOpt.Instructions.Add(instr);
							}
						}
					}
					else
					{
						// Add new option
						target.Options.Add(srcOpt);
					}
				}
			}
		}

		private static bool IsLikelyAccessibleUrl([NotNull] string url)
		{
			// Very lightweight validation: must be absolute HTTP(S) URL and not obviously invalid
			if ( string.IsNullOrWhiteSpace(url) ) return false;
			if ( !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) )
				return false;
			try
			{
				var uri = new Uri(url);
				// basic host presence check
				return !string.IsNullOrWhiteSpace(uri.Host);
			}
			catch
			{
				return false;
			}
		}
	}
}


