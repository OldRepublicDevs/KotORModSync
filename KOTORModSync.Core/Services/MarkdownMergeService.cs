// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{
	public static class MarkdownMergeService
	{
		/// <summary>
		/// Validates URLs by attempting to resolve filenames using DownloadCacheService.
		/// If resolution fails, checks if the URL actually exists via HTTP request.
		/// Returns only URLs that either resolve successfully OR exist but can't be resolved.
		/// </summary>
		public static async System.Threading.Tasks.Task<List<string>> ValidateUrlsViaResolutionAsync(
			[NotNull] List<string> urls,
			[NotNull] DownloadCacheService downloadCache,
			System.Threading.CancellationToken cancellationToken = default)
		{
			if ( urls == null || urls.Count == 0 )
				return new List<string>();

			if ( downloadCache == null )
				return urls; // No validation, return all

			var validUrls = new List<string>();
			var failedToResolveUrls = new List<string>();

			Logger.LogVerbose($"[MarkdownMerge] Validating {urls.Count} URLs via filename resolution...");

			// Create a temporary component to use for resolution
			var tempComponent = new ModComponent
			{
				Name = "TempValidation",
				Guid = System.Guid.NewGuid(),
				ModLink = new List<string>(urls)
			};

			// Step 1: Try to resolve URLs using existing download handlers
			var resolvedUrls = await downloadCache.PreResolveUrlsAsync(tempComponent, null, cancellationToken).ConfigureAwait(false);

			foreach ( string url in urls )
			{
				if ( resolvedUrls.TryGetValue(url, out List<string> filenames) && 
				     filenames != null && filenames.Count > 0 && !string.IsNullOrWhiteSpace(filenames[0]) )
				{
					// URL successfully resolved to a filename - keep it
					validUrls.Add(url);
					Logger.LogVerbose($"[MarkdownMerge] ✓ Resolved: {url} -> {filenames[0]}");
				}
				else
				{
					// URL failed to resolve - needs further validation
					failedToResolveUrls.Add(url);
					Logger.LogVerbose($"[MarkdownMerge] ⚠ Failed to resolve: {url}");
				}
			}

			// Step 2: For URLs that failed to resolve, check if they actually exist
			if ( failedToResolveUrls.Count > 0 )
			{
				Logger.LogVerbose($"[MarkdownMerge] Checking existence of {failedToResolveUrls.Count} unresolved URL(s)...");

				using ( var httpClient = new System.Net.Http.HttpClient { Timeout = System.TimeSpan.FromSeconds(5) } )
				{
					httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

					foreach ( string url in failedToResolveUrls )
					{
						bool exists = await CheckUrlExistsAsync(httpClient, url, cancellationToken).ConfigureAwait(false);
						if ( exists )
						{
							// URL exists but couldn't be resolved - keep it anyway
							validUrls.Add(url);
							Logger.LogVerbose($"[MarkdownMerge] ✓ URL exists (but unresolved): {url}");
						}
						else
						{
							// URL doesn't exist - remove it
							Logger.LogWarning($"[MarkdownMerge] ✗ Invalid/Broken URL: {url}");
						}
					}
				}
			}

			int removedCount = urls.Count - validUrls.Count;
			if ( removedCount > 0 )
			{
				Logger.Log($"[MarkdownMerge] Filtered out {removedCount} invalid URL(s)");
			}

			return validUrls;
		}

		/// <summary>
		/// Checks if a URL exists by making an HTTP HEAD request.
		/// Returns true if the URL is accessible, false otherwise.
		/// </summary>
		private static async System.Threading.Tasks.Task<bool> CheckUrlExistsAsync(
			System.Net.Http.HttpClient httpClient,
			string url,
			System.Threading.CancellationToken cancellationToken)
		{
			try
			{
				// Basic syntax validation
				if ( !System.Uri.TryCreate(url, System.UriKind.Absolute, out System.Uri uri) )
				{
					Logger.LogVerbose($"[MarkdownMerge] Invalid URL syntax: {url}");
					return false;
				}

				// Ensure it's HTTP/HTTPS
				if ( !uri.Scheme.Equals("http", System.StringComparison.OrdinalIgnoreCase) &&
				     !uri.Scheme.Equals("https", System.StringComparison.OrdinalIgnoreCase) )
				{
					Logger.LogVerbose($"[MarkdownMerge] Unsupported URL scheme: {uri.Scheme}");
					return false;
				}

				// Try HEAD request first (faster)
				using ( var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, url) )
				{
					using ( var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false) )
					{
						// Accept any 2xx or 3xx status code
						int statusCode = (int)response.StatusCode;
						return statusCode >= 200 && statusCode < 400;
					}
				}
			}
			catch ( System.Net.Http.HttpRequestException )
			{
				// HTTP error (404, 500, etc.) - URL doesn't exist or is inaccessible
				return false;
			}
			catch ( System.Threading.Tasks.TaskCanceledException )
			{
				// Timeout - consider it invalid
				Logger.LogVerbose($"[MarkdownMerge] Timeout checking URL: {url}");
				return false;
			}
			catch ( System.Exception ex )
			{
				// Other error - consider it invalid
				Logger.LogVerbose($"[MarkdownMerge] Error checking URL {url}: {ex.Message}");
				return false;
			}
		}

		private static string Normalize(
			[NotNull] string value,
			bool ignoreCase,
			bool ignorePunctuation,
			bool trim)
		{
			string s = value;
			if ( trim ) s = s.Trim();
			if ( ignorePunctuation )
			{
				char[] arr = s.Where(c => !char.IsPunctuation(c)).ToArray();
				s = new string(arr);
			}
			if ( ignoreCase ) s = s.ToLowerInvariant();
			return s;
		}
		private static readonly char[] s_spaceSeparatorArray = new[] { ' ' };

		private static double JaccardSimilarity([NotNull] string a, [NotNull] string b)
		{
			if ( string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b) ) return 0.0;
			var setA = new HashSet<string>(a.Split(s_spaceSeparatorArray, StringSplitOptions.RemoveEmptyEntries));
			var setB = new HashSet<string>(b.Split(s_spaceSeparatorArray, StringSplitOptions.RemoveEmptyEntries));
			int intersection = setA.Intersect(setB).Count();
			int union = setA.Union(setB).Count();
			return union == 0 ? 0.0 : (double)intersection / union;
		}

		private static bool IsBlank([CanBeNull] string v) => string.IsNullOrWhiteSpace(v);

		private static bool IsBlank([CanBeNull] List<string> v) => v == null || v.Count == 0;

		[NotNull]
		private static string NormalizeKey([NotNull] ModComponent c)
		{
			string name = c.Name;
			string author = c.Author;
			return (name + "|" + author).Trim().ToLowerInvariant();
		}

		/// <summary>
		/// Asynchronously merges incoming components into the base list, with optional URL validation.
		/// If downloadCache is provided, invalid URLs will be removed during the merge.
		/// </summary>
		public static async System.Threading.Tasks.Task MergeIntoAsync(
			[NotNull] List<ModComponent> baseList,
			[NotNull] List<ModComponent> incoming,
			[CanBeNull] MergeHeuristicsOptions options = null,
			bool useExistingOrder = false,
			[CanBeNull] DownloadCacheService downloadCache = null,
			System.Threading.CancellationToken cancellationToken = default)
		{
			if ( baseList == null )
				throw new ArgumentNullException(nameof(baseList));
			if ( incoming == null )
				throw new ArgumentNullException(nameof(incoming));

			if ( options == null ) options = MergeHeuristicsOptions.CreateDefault();

			// If URL validation is requested, validate all URLs from both lists
			if ( options.ValidateExistingLinksBeforeReplace && downloadCache != null )
			{
				await Logger.LogVerboseAsync("[MarkdownMerge] Validating URLs before merge...");

				// Collect all unique URLs from both base and incoming
				var allUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach ( var component in baseList )
				{
					if ( component.ModLink != null )
					{
						foreach ( string url in component.ModLink )
						{
							if ( !string.IsNullOrWhiteSpace(url) )
								allUrls.Add(url);
						}
					}
				}
				foreach ( var component in incoming )
				{
					if ( component.ModLink != null )
					{
						foreach ( string url in component.ModLink )
						{
							if ( !string.IsNullOrWhiteSpace(url) )
								allUrls.Add(url);
						}
					}
				}

				// Validate URLs via resolution
				var validUrls = await ValidateUrlsViaResolutionAsync(allUrls.ToList(), downloadCache, cancellationToken).ConfigureAwait(false);
				var validUrlSet = new HashSet<string>(validUrls, StringComparer.OrdinalIgnoreCase);

				// Remove invalid URLs from components
				foreach ( var component in baseList )
				{
					if ( component.ModLink != null && component.ModLink.Count > 0 )
					{
						int originalCount = component.ModLink.Count;
						component.ModLink = component.ModLink.Where(url => validUrlSet.Contains(url)).ToList();
						if ( component.ModLink.Count < originalCount )
						{
							await Logger.LogVerboseAsync($"[MarkdownMerge] Removed {originalCount - component.ModLink.Count} invalid URL(s) from component: {component.Name}");
						}
					}
				}
				foreach ( var component in incoming )
				{
					if ( component.ModLink != null && component.ModLink.Count > 0 )
					{
						int originalCount = component.ModLink.Count;
						component.ModLink = component.ModLink.Where(url => validUrlSet.Contains(url)).ToList();
						if ( component.ModLink.Count < originalCount )
						{
							await Logger.LogVerboseAsync($"[MarkdownMerge] Removed {originalCount - component.ModLink.Count} invalid URL(s) from component: {component.Name}");
						}
					}
				}
			}

			// Proceed with normal merge
			MergeInto(baseList, incoming, options, useExistingOrder);
		}

		public static void MergeInto(
			[NotNull] List<ModComponent> baseList,
			[NotNull] List<ModComponent> incoming,
			[CanBeNull] MergeHeuristicsOptions options = null,
			bool useExistingOrder = false)
		{
			if ( baseList == null )
				throw new ArgumentNullException(nameof(baseList));
			if ( incoming == null )
				throw new ArgumentNullException(nameof(incoming));

			if ( options == null ) options = MergeHeuristicsOptions.CreateDefault();


			var map = baseList.ToDictionary(NormalizeKey, c => c);
			var matchedBase = new HashSet<ModComponent>();
			var result = new List<ModComponent>();


			foreach ( ModComponent inc in incoming )
			{
				string key = NormalizeKey(inc);

				if ( options.UseNameExact && options.UseAuthorExact && map.TryGetValue(key, out ModComponent match) )
				{
					UpdateComponent(match, inc);
					result.Add(match);
					matchedBase.Add(match);
				}
				else
				{

					ModComponent heuristicMatch = FindHeuristicMatch(baseList, inc, options);
					if ( heuristicMatch != null )
					{
						UpdateComponent(heuristicMatch, inc, options);
						result.Add(heuristicMatch);
						_ = matchedBase.Add(heuristicMatch);
					}
					else if ( options.AddNewWhenNoMatchFound )
					{

						result.Add(inc);
						map[key] = inc;
					}
				}
			}



			foreach ( ModComponent existingComponent in baseList )
			{
				if ( matchedBase.Contains(existingComponent) )
					continue;

				int insertIndex = FindInsertionPointByNameAuthor(result, existingComponent, baseList);
				result.Insert(insertIndex, existingComponent);
			}


			baseList.Clear();
			baseList.AddRange(result);
		}

		private static int FindInsertionPointByNameAuthor(
			[NotNull] List<ModComponent> result,
			[NotNull] ModComponent componentToInsert,
			[NotNull] List<ModComponent> originalBaseList)
		{

			int originalIndex = originalBaseList.IndexOf(componentToInsert);
			if ( originalIndex < 0 )
				return result.Count;


			for ( int i = originalIndex + 1; i < originalBaseList.Count; i++ )
			{
				ModComponent afterComponent = originalBaseList[i];
				int afterIndexInResult = result.IndexOf(afterComponent);
				if ( afterIndexInResult >= 0 )
				{

					return afterIndexInResult;
				}
			}


			return result.Count;
		}

		private static ModComponent FindHeuristicMatch(
			[NotNull] List<ModComponent> baseList,
			[NotNull] ModComponent incoming,
			[NotNull] MergeHeuristicsOptions opt)
		{
			string inName = Normalize(incoming.Name, opt.IgnoreCase, opt.IgnorePunctuation, opt.TrimWhitespace);
			string inAuthor = Normalize(incoming.Author, opt.IgnoreCase, opt.IgnorePunctuation, opt.TrimWhitespace);

			double bestScore = 0.0;
			ModComponent best = null;
			foreach ( ModComponent e in baseList )
			{
				string exName = Normalize(e.Name, opt.IgnoreCase, opt.IgnorePunctuation, opt.TrimWhitespace);
				string exAuthor = Normalize(e.Author, opt.IgnoreCase, opt.IgnorePunctuation, opt.TrimWhitespace);

				double score = 0.0;
				if ( opt.UseNameExact && !string.IsNullOrEmpty(inName) && inName == exName ) score += 1.0;
				if ( opt.UseAuthorExact && !string.IsNullOrEmpty(inAuthor) && inAuthor == exAuthor ) score += 1.0;
				if ( opt.UseNameSimilarity && !string.IsNullOrEmpty(inName) ) score += JaccardSimilarity(inName, exName);
				if ( opt.UseAuthorSimilarity && !string.IsNullOrEmpty(inAuthor) ) score += JaccardSimilarity(inAuthor, exAuthor) * 0.5;


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

		private static string GetPrimaryDomain([NotNull] ModComponent c)
		{
			string url = c.ModLink.FirstOrDefault();
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

		private static void UpdateComponent(
			[NotNull] ModComponent target,
			[NotNull] ModComponent source,
			[CanBeNull] MergeHeuristicsOptions options = null
		)
		{
			if ( options == null ) options = MergeHeuristicsOptions.CreateDefault();

			if ( !(options.SkipBlankUpdates && IsBlank(source.Author)) )
				target.Author = string.IsNullOrWhiteSpace(source.Author) ? target.Author : source.Author;
			if ( !(options.SkipBlankUpdates && IsBlank(source.Category)) )
				target.Category = IsBlank(source.Category) ? target.Category : new List<string>(source.Category);
			if ( !(options.SkipBlankUpdates && IsBlank(source.Tier)) )
				target.Tier = string.IsNullOrWhiteSpace(source.Tier) ? target.Tier : source.Tier;
			if ( !(options.SkipBlankUpdates && IsBlank(source.Description)) )
				target.Description = string.IsNullOrWhiteSpace(source.Description) ? target.Description : source.Description;
			if ( !(options.SkipBlankUpdates && IsBlank(source.Directions)) )
				target.Directions = string.IsNullOrWhiteSpace(source.Directions) ? target.Directions : source.Directions;
			if ( !(options.SkipBlankUpdates && IsBlank(source.InstallationMethod)) )
				target.InstallationMethod = string.IsNullOrWhiteSpace(source.InstallationMethod) ? target.InstallationMethod : source.InstallationMethod;


			if ( source.Language.Count > 0 )
			{
				var set = new HashSet<string>(target.Language, StringComparer.OrdinalIgnoreCase);
				foreach ( string lang in source.Language )
				{
					if ( !string.IsNullOrWhiteSpace(lang) )
						_ = set.Add(lang);
				}
				target.Language = set.ToList();
			}

			if ( source.ModLink.Count > 0 )
			{
				var set = new HashSet<string>(target.ModLink, StringComparer.OrdinalIgnoreCase);
				foreach ( string link in source.ModLink )
				{
					if ( string.IsNullOrWhiteSpace(link) )
					    continue;
					_ = set.Add(link);
				}

				if ( options.ValidateExistingLinksBeforeReplace )
				{
					// For synchronous merge, only do basic URI syntax validation
					// Full validation (with HTTP requests) happens in MergeIntoAsync
					set = new HashSet<string>(set.Where(IsLikelyAccessibleUrl), StringComparer.OrdinalIgnoreCase);
				}
				target.ModLink = set.ToList();
			}


			if ( source.Dependencies?.Count > 0 )
			{
				var set = new HashSet<Guid>(target.Dependencies ?? new List<Guid>());
				foreach ( Guid g in source.Dependencies )
				{
					_ = set.Add(g);
				}
				target.Dependencies = set.ToList();
			}
			if ( source.Restrictions?.Count > 0 )
			{
				var set = new HashSet<Guid>(target.Restrictions ?? new List<Guid>());
				foreach ( Guid g in source.Restrictions )
				{
					_ = set.Add(g);
				}
				target.Restrictions = set.ToList();
			}
			if ( source.InstallAfter?.Count > 0 )
			{
				var set = new HashSet<Guid>(target.InstallAfter ?? new List<Guid>());
				foreach ( Guid g in source.InstallAfter )
				{
					_ = set.Add(g);
				}
				target.InstallAfter = set.ToList();
			}
			if ( source.InstallBefore?.Count > 0 )
			{
				var set = new HashSet<Guid>(target.InstallBefore ?? new List<Guid>());
				foreach ( Guid g in source.InstallBefore )
				{
					_ = set.Add(g);
				}
				target.InstallBefore = set.ToList();
			}


			if ( source.Instructions != null && source.Instructions.Count > 0 )
			{
				if ( target.Instructions == null || target.Instructions.Count == 0 )
				{
					foreach ( Instruction instr in source.Instructions )
					{
						target.Instructions.Add(instr);
					}
				}
				else
				{

					var existingKeys = new HashSet<string>(target.Instructions.Select(i => (i.ActionString + "|" + i.Destination).ToLowerInvariant()));
					foreach ( Instruction instr in source.Instructions )
					{
						string key = (instr.ActionString + "|" + (instr.Destination ?? string.Empty)).ToLowerInvariant();
						if ( !existingKeys.Contains(key) )
							target.Instructions.Add(instr);
					}
				}
			}


			if ( source.Options != null && source.Options.Count > 0 )
			{
				Dictionary<string, Option> optMap = target.Options?.ToDictionary(o => o.Name.Trim().ToLowerInvariant())
						  ?? new Dictionary<string, Option>();
				foreach ( Option srcOpt in source.Options )
				{
					string oname = (srcOpt.Name ?? string.Empty).Trim().ToLowerInvariant();
					if ( optMap.TryGetValue(oname, out Option trgOpt) )
					{
						if ( !string.IsNullOrWhiteSpace(srcOpt.Description) ) trgOpt.Description = srcOpt.Description;

						if ( srcOpt.Instructions != null && srcOpt.Instructions.Count > 0 )
						{
							var keys = new HashSet<string>(trgOpt.Instructions.Select(i => (i.ActionString + "|" + i.Destination).ToLowerInvariant()));
							foreach ( Instruction instr in srcOpt.Instructions )
							{
								string key = (instr.ActionString + "|" + (instr.Destination ?? string.Empty)).ToLowerInvariant();
								if ( !keys.Contains(key) ) trgOpt.Instructions.Add(instr);
							}
						}
					}
					else
					{

						target.Options.Add(srcOpt);
					}
				}
			}
		}

		private static bool IsLikelyAccessibleUrl([NotNull] string url)
		{

			if ( string.IsNullOrWhiteSpace(url) ) return false;
			if ( !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) )
				return false;
			try
			{
				var uri = new Uri(url);

				return !string.IsNullOrWhiteSpace(uri.Host);
			}
			catch
			{
				return false;
			}
		}
	}
}



