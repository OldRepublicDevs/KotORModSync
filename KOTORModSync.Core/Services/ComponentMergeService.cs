// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
    public class MergeOptions
	{
		public bool ExcludeExistingOnly { get; set; }
		public bool ExcludeIncomingOnly { get; set; }
		public bool UseExistingOrder { get; set; }
		public MergeHeuristicsOptions HeuristicsOptions { get; set; }

		// Field-level merge preferences (default: prefer incoming)
		public bool PreferExistingName { get; set; }
		public bool PreferExistingAuthor { get; set; }
		public bool PreferExistingDescription { get; set; }
		public bool PreferExistingDirections { get; set; }
		public bool PreferExistingCategory { get; set; }
		public bool PreferExistingTier { get; set; }
		public bool PreferExistingInstallationMethod { get; set; }
		public bool PreferExistingInstructions { get; set; }
		public bool PreferExistingOptions { get; set; }
		public bool PreferExistingModLinkFilenames { get; set; }

		// Convenience flags to set all at once
		public bool PreferAllExistingFields
		{
			set
			{
				PreferExistingName = value;
				PreferExistingAuthor = value;
				PreferExistingDescription = value;
				PreferExistingDirections = value;
				PreferExistingCategory = value;
				PreferExistingTier = value;
				PreferExistingInstallationMethod = value;
				PreferExistingInstructions = value;
				PreferExistingOptions = value;
				PreferExistingModLinkFilenames = value;
			}
		}

		public bool PreferAllIncomingFields
		{
			set
			{
				PreferExistingName = !value;
				PreferExistingAuthor = !value;
				PreferExistingDescription = !value;
				PreferExistingDirections = !value;
				PreferExistingCategory = !value;
				PreferExistingTier = !value;
				PreferExistingInstallationMethod = !value;
				PreferExistingInstructions = !value;
				PreferExistingOptions = !value;
				PreferExistingModLinkFilenames = !value;
			}
		}

		public static MergeOptions CreateDefault() => new MergeOptions
		{
			ExcludeExistingOnly = false,
			ExcludeIncomingOnly = false,
			UseExistingOrder = false,
			HeuristicsOptions = MergeHeuristicsOptions.CreateDefault(),
			// Default: prefer incoming (new/updated data) for all fields
			PreferExistingName = false,
			PreferExistingAuthor = false,
			PreferExistingDescription = false,
			PreferExistingDirections = false,
			PreferExistingCategory = false,
			PreferExistingTier = false,
			PreferExistingInstallationMethod = false,
			PreferExistingInstructions = false,
			PreferExistingOptions = false,
			PreferExistingModLinkFilenames = false,
		};
	}

	public static class ComponentMergeService
	{
		[NotNull]
		public static async System.Threading.Tasks.Task<List<ModComponent>> MergeInstructionSetsAsync(
				[NotNull] string existingFilePath,
				[NotNull] string incomingFilePath,
				[NotNull] MergeOptions options,
				[CanBeNull] DownloadCacheService downloadCache = null,
				bool sequential = true,
				System.Threading.CancellationToken cancellationToken = default)
		{
			if (existingFilePath is null)
				throw new ArgumentNullException(nameof(existingFilePath));
			if (incomingFilePath is null)
				throw new ArgumentNullException(nameof(incomingFilePath));

			options = options ?? MergeOptions.CreateDefault();


			List<ModComponent> existing = await FileLoadingService.LoadFromFileAsync(existingFilePath).ConfigureAwait(false);
			List<ModComponent> incoming = await FileLoadingService.LoadFromFileAsync(incomingFilePath).ConfigureAwait(false);

			return await MergeComponentListsAsync(
				existing,
				incoming,
				options,
				downloadCache,
				sequential,
				cancellationToken
			).ConfigureAwait(false);
		}

		[NotNull]
		public static List<ModComponent> MergeInstructionSets(
				[NotNull] string existingFilePath,
				[NotNull] string incomingFilePath,
				[NotNull] MergeOptions options)
		{
			if (existingFilePath is null)
				throw new ArgumentNullException(nameof(existingFilePath));
			if (incomingFilePath is null)
				throw new ArgumentNullException(nameof(incomingFilePath));

			options = options ?? MergeOptions.CreateDefault();

			List<ModComponent> existing = FileLoadingService.LoadFromFile(existingFilePath);
			List<ModComponent> incoming = FileLoadingService.LoadFromFile(incomingFilePath);

			return MergeComponentLists(existing, incoming, options);
		}

		[NotNull]
		public static async System.Threading.Tasks.Task<List<ModComponent>> MergeComponentListsAsync(
			[NotNull] List<ModComponent> existing,
			[NotNull] List<ModComponent> incoming,
			[NotNull] MergeOptions options,
			[CanBeNull] DownloadCacheService downloadCache = null,
			bool sequential = true,
			System.Threading.CancellationToken cancellationToken = default)
		{
			if (existing is null)
				throw new ArgumentNullException(nameof(existing));
			if (incoming is null)
				throw new ArgumentNullException(nameof(incoming));

			options = options ?? MergeOptions.CreateDefault();
			options.HeuristicsOptions = options.HeuristicsOptions ?? MergeHeuristicsOptions.CreateDefault();

			List<ModComponent> result;

			if (options.UseExistingOrder)
			{
				result = new List<ModComponent>(existing);

				await MergeIntoAsync(
					result,
					incoming,
					options.HeuristicsOptions,
					options,
					downloadCache,
					sequential,
					cancellationToken
				).ConfigureAwait(false);

				if (options.ExcludeExistingOnly)
				{
					HashSet<Guid> matchedGuids = new HashSet<Guid>(incoming.Select(c => c.Guid));
					result.RemoveAll(c => !matchedGuids.Contains(c.Guid));
				}
			}
			else
			{
				result = new List<ModComponent>(incoming);

				await MergeIntoAsync(
					result,
					existing,
					options.HeuristicsOptions,
					options,
					downloadCache,
					sequential,
					cancellationToken
				).ConfigureAwait(false);

				if (options.ExcludeExistingOnly)
				{
					HashSet<Guid> matchedGuids = new HashSet<Guid>(incoming.Select(c => c.Guid));
					result.RemoveAll(c => !matchedGuids.Contains(c.Guid));
				}
			}

			return result;
		}

		[NotNull]
		public static List<ModComponent> MergeComponentLists(
			[NotNull] List<ModComponent> existing,
			[NotNull] List<ModComponent> incoming,
			[NotNull] MergeOptions options)
		{
			if (existing is null)
				throw new ArgumentNullException(nameof(existing));
			if (incoming is null)
				throw new ArgumentNullException(nameof(incoming));

			options = options ?? MergeOptions.CreateDefault();
			options.HeuristicsOptions = options.HeuristicsOptions ?? MergeHeuristicsOptions.CreateDefault();

			List<ModComponent> result;

			if (options.UseExistingOrder)
			{
				result = new List<ModComponent>(existing);
				MergeInto(result, incoming, options.HeuristicsOptions, options);

				if (options.ExcludeExistingOnly)
				{
					HashSet<Guid> matchedGuids = new HashSet<Guid>(incoming.Select(c => c.Guid));
					result.RemoveAll(c => !matchedGuids.Contains(c.Guid));
				}
			}
			else
			{
				// When ExcludeExistingOnly: don't add unmatched components from existing
				// When ExcludeIncomingOnly: don't add unmatched components from incoming (but we start with incoming, so this is handled by removal)
				// Default: add all unmatched components from both sides

				result = new List<ModComponent>(incoming);
				MergeInto(result, existing, options.HeuristicsOptions, options);

				if (options.ExcludeIncomingOnly)
				{
					HashSet<Guid> matchedGuids = new HashSet<Guid>(existing.Select(c => c.Guid));
					result.RemoveAll(c => !matchedGuids.Contains(c.Guid));
				}
			}

			return result;
		}

		public static async System.Threading.Tasks.Task MergeIntoAsync(
			[NotNull] List<ModComponent> incomingList,
			[NotNull] List<ModComponent> existingList,
			[NotNull] MergeHeuristicsOptions heuristicsOptions,
			[NotNull] MergeOptions mergeOptions,
			[CanBeNull] DownloadCacheService downloadCache = null,
			bool sequential = true,
			System.Threading.CancellationToken cancellationToken = default)
		{
			if (incomingList is null)
				throw new ArgumentNullException(nameof(incomingList));
			if (existingList is null)
				throw new ArgumentNullException(nameof(existingList));

			await MergeModComponentsAsync(incomingList, existingList, heuristicsOptions, mergeOptions, downloadCache, sequential, cancellationToken).ConfigureAwait(false);
		}

		public static void MergeInto(
			[NotNull] List<ModComponent> incomingList,
			[NotNull] List<ModComponent> existingList,
			[NotNull] MergeHeuristicsOptions heuristicsOptions,
			[NotNull] MergeOptions mergeOptions)
		{
			if (incomingList is null)
				throw new ArgumentNullException(nameof(incomingList));
			if (existingList is null)
				throw new ArgumentNullException(nameof(existingList));

			MergeModComponents(incomingList, existingList, heuristicsOptions, mergeOptions);
		}

        /// <summary>
        /// Unified merge pipeline: matches by GUID first, then falls back to name/author matching.
        /// Async version that supports URL validation during merge.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        private static async System.Threading.Tasks.Task MergeModComponentsAsync(
			[NotNull] List<ModComponent> incomingList,
			[NotNull] List<ModComponent> existingList,
			[CanBeNull] MergeHeuristicsOptions heuristicsOptions = null,
			[CanBeNull] MergeOptions mergeOptions = null,
			[CanBeNull] DownloadCacheService downloadCache = null,
			bool sequential = true,
			System.Threading.CancellationToken cancellationToken = default)
		{
			if (incomingList is null)
				throw new ArgumentNullException(nameof(incomingList));
			if (existingList is null)
				throw new ArgumentNullException(nameof(existingList));

			heuristicsOptions = heuristicsOptions
								?? MergeHeuristicsOptions.CreateDefault();
			mergeOptions = mergeOptions
						   ?? MergeOptions.CreateDefault();

			// Validate URLs before merge if enabled
			if (heuristicsOptions.ValidateExistingLinksBeforeReplace && downloadCache != null)

			{
				await ValidateAndFilterUrlsAsync(incomingList, existingList, downloadCache, sequential, cancellationToken).ConfigureAwait(false);
			}

			// Build GUID lookup for existing components
			Dictionary<Guid, ModComponent> existingByGuid = new Dictionary<Guid, ModComponent>();
			foreach (ModComponent existing in existingList)
			{
				existingByGuid[existing.Guid] = existing;
			}

			// Track matched components to avoid double-matching
			HashSet<ModComponent> matchedExistingComponents = new HashSet<ModComponent>();

			// Process each incoming component
			foreach (ModComponent incomingComponent in incomingList)
			{
				ModComponent matchedExisting;

				// Step 1: Try to match by GUID
				if (existingByGuid.TryGetValue(incomingComponent.Guid, out ModComponent existingByGuidMatch))
				{
					matchedExisting = existingByGuidMatch;

					await Logger.LogVerboseAsync($"[UnifiedMerge] Matched '{incomingComponent.Name}' by GUID: {incomingComponent.Guid}").ConfigureAwait(false);
				}
				// Step 2: If no GUID match, try name/author heuristics
				else
				{
					matchedExisting = FindHeuristicMatch(
						existingList.Where(e => !matchedExistingComponents.Contains(e)).ToList(),
						incomingComponent,
						heuristicsOptions);

					if (matchedExisting != null)

					{
						await Logger.LogVerboseAsync($"[UnifiedMerge] Matched '{incomingComponent.Name}' by name/author to '{matchedExisting.Name}'").ConfigureAwait(false);
					}
				}

				// Step 3: Update matched component with incoming data
				if (matchedExisting != null)
				{
					// Update the INCOMING component with data from EXISTING based on preferences
					// This preserves incoming component in the result list but merges in existing data where needed
					UpdateModComponentFields(incomingComponent, matchedExisting, heuristicsOptions, mergeOptions);
					matchedExistingComponents.Add(matchedExisting);
				}
				// else: incoming component has no match, it stays as-is in the result
			}

			// Step 4: Add unmatched existing components to the result
			List<ModComponent> unmatchedExisting = existingList.Where(e => !matchedExistingComponents.Contains(e)).ToList();
			foreach (ModComponent existingComponent in unmatchedExisting)
			{
				// Find insertion point to maintain relative order from existing list
				int insertIndex = FindInsertionPointForUnmatched(incomingList, existingComponent, existingList);
				incomingList.Insert(insertIndex, existingComponent);
			}
		}

		/// <summary>
		/// Unified merge pipeline: matches by GUID first, then falls back to name/author matching.
		/// Synchronous version.
		/// </summary>
		private static void MergeModComponents(
			[NotNull] List<ModComponent> incomingList,
			[NotNull] List<ModComponent> existingList,
			[NotNull] MergeHeuristicsOptions heuristicsOptions,
			[NotNull] MergeOptions mergeOptions)
		{
			if (incomingList is null)
				throw new ArgumentNullException(nameof(incomingList));
			if (existingList is null)
				throw new ArgumentNullException(nameof(existingList));

			// Build GUID lookup for existing components
			Dictionary<Guid, ModComponent> existingByGuid = new Dictionary<Guid, ModComponent>();
			foreach (ModComponent existing in existingList)
			{
				existingByGuid[existing.Guid] = existing;
			}

			// Track matched components to avoid double-matching
			HashSet<ModComponent> matchedExistingComponents = new HashSet<ModComponent>();

			// Process each incoming component
			foreach (ModComponent incomingComponent in incomingList)
			{
				ModComponent matchedExisting;

				// Step 1: Try to match by GUID
				if (existingByGuid.TryGetValue(incomingComponent.Guid, out ModComponent existingByGuidMatch))
				{
					matchedExisting = existingByGuidMatch;
					Logger.LogVerbose($"[UnifiedMerge] Matched '{incomingComponent.Name}' by GUID: {incomingComponent.Guid}");
				}
				// Step 2: If no GUID match, try name/author heuristics
				else
				{
					matchedExisting = FindHeuristicMatch(
						existingList.Where(e => !matchedExistingComponents.Contains(e)).ToList(),
						incomingComponent,
						heuristicsOptions);

					if (matchedExisting != null)
					{
						Logger.LogVerbose($"[UnifiedMerge] Matched '{incomingComponent.Name}' by name/author to '{matchedExisting.Name}'");
					}
				}

				// Step 3: Update matched component with incoming data
				if (matchedExisting != null)
				{
					// Update the INCOMING component with data from EXISTING based on preferences
					// This preserves incoming component in the result list but merges in existing data where needed
					UpdateModComponentFields(incomingComponent, matchedExisting, heuristicsOptions, mergeOptions);
					matchedExistingComponents.Add(matchedExisting);
				}
				// else: incoming component has no match, it stays as-is in the result
			}

			// Step 4: Add unmatched existing components to the result
			List<ModComponent> unmatchedExisting = existingList.Where(e => !matchedExistingComponents.Contains(e)).ToList();
			foreach (ModComponent existingComponent in unmatchedExisting)
			{
				// Find insertion point to maintain relative order from existing list
				int insertIndex = FindInsertionPointForUnmatched(incomingList, existingComponent, existingList);
				incomingList.Insert(insertIndex, existingComponent);
			}
		}

		/// <summary>
		/// Validates URLs for both incoming and existing components and removes invalid ones.
		/// </summary>
		private static async System.Threading.Tasks.Task ValidateAndFilterUrlsAsync(
			[NotNull] List<ModComponent> incomingList,
			[NotNull] List<ModComponent> existingList,
			[NotNull] DownloadCacheService downloadCache,
			bool sequential = true,
			System.Threading.CancellationToken cancellationToken = default)

		{
			await Logger.LogVerboseAsync("[ComponentMerge] Validating URLs before merge...")

.ConfigureAwait(false);

			HashSet<string> allUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (ModComponent component in incomingList)
			{
				if (component.ModLinkFilenames != null)
				{
					foreach (string url in

component.ModLinkFilenames.Keys)
					{
						if (!string.IsNullOrWhiteSpace(url))
							allUrls.Add(url);
					}
				}
			}
			foreach (ModComponent component in existingList)
			{
				if (component.ModLinkFilenames != null)
				{
					foreach (string url in component.ModLinkFilenames.Keys)
					{
						if (!string.IsNullOrWhiteSpace(url))
							allUrls.Add(url);
					}
				}
			}

			List<string> validUrls = await ValidateUrlsViaResolutionAsync(
				allUrls.ToList(),
				downloadCache,
				sequential,
				cancellationToken
			).ConfigureAwait(false);
			HashSet<string> validUrlSet = new HashSet<string>(validUrls, StringComparer.OrdinalIgnoreCase);

			foreach (ModComponent component in incomingList)
			{
				if (component.ModLinkFilenames != null && component.ModLinkFilenames.Count > 0)
				{
					List<string> urlsToRemove = component.ModLinkFilenames.Keys.Where(url => !validUrlSet.Contains(url)).ToList();
					foreach (string invalidUrl in urlsToRemove)
					{
						component.ModLinkFilenames.Remove(invalidUrl);
					}
					if (urlsToRemove.Count > 0)
					{
						await Logger.LogVerboseAsync($"[ComponentMerge] Removed {urlsToRemove.Count} invalid URL(s) from component: {component.Name}").ConfigureAwait(false);
					}
				}
			}
			foreach (ModComponent component in existingList)
			{
				if (component.ModLinkFilenames != null && component.ModLinkFilenames.Count > 0)
				{
					List<string> urlsToRemove = component.ModLinkFilenames.Keys.Where(url => !validUrlSet.Contains(url)).ToList();
					foreach (string invalidUrl in urlsToRemove)
					{
						component.ModLinkFilenames.Remove(invalidUrl);
					}
					if (urlsToRemove.Count > 0)
					{
						await Logger.LogVerboseAsync($"[ComponentMerge] Removed {urlsToRemove.Count} invalid URL(s) from component: {component.Name}").ConfigureAwait(false);
					}
				}
			}
		}

		/// <summary>
		/// Finds the best insertion point for an unmatched existing component in the incoming list.
		/// </summary>
		private static int FindInsertionPointForUnmatched(
			[NotNull] List<ModComponent> incomingList,
			[NotNull] ModComponent unmatchedExisting,
			[NotNull] List<ModComponent> originalExistingList)
		{
			int originalIndex = originalExistingList.IndexOf(unmatchedExisting);
			if (originalIndex < 0)
				return incomingList.Count;

			// Look for the next component in the existing list that appears in incoming
			for (int i = originalIndex + 1; i < originalExistingList.Count; i++)
			{
				ModComponent afterComponent = originalExistingList[i];
				int afterIndexInIncoming = incomingList.FindIndex(c => c.Guid == afterComponent.Guid);
				if (afterIndexInIncoming >= 0)
				{
					return afterIndexInIncoming; // Insert before this component
				}
			}

			return incomingList.Count; // Add to end
		}

		/// <summary>
		/// Updates a component with data from another component based on merge options and heuristics.
		/// The target parameter receives updates from the source parameter.
		/// </summary>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private static void UpdateModComponentFields(
			[NotNull] ModComponent target,
			[NotNull] ModComponent source,
			[NotNull] MergeHeuristicsOptions heuristicsOptions,
			[NotNull] MergeOptions mergeOptions)
		{
			// GUID merge logic: prefer the GUID that exists (non-empty)
			// If incoming has a GUID but existing has empty, keep incoming GUID
			if (target.Guid != Guid.Empty && source.Guid == Guid.Empty)
			{
				// Keep incoming GUID, don't overwrite with empty
			}
			// If existing has a GUID but incoming has empty, preserve existing GUID
			else if (target.Guid == Guid.Empty && source.Guid != Guid.Empty)
			{
				target.Guid = source.Guid;
			}
			// If both have GUIDs but they're different (heuristic match), prefer existing GUID
			// This ensures consistency and prevents overwriting stable GUIDs with auto-generated ones
			else if (target.Guid != Guid.Empty && source.Guid != Guid.Empty && target.Guid != source.Guid)
			{
				target.Guid = source.Guid;
			}

			// Update text fields based on preferences and heuristicsOptions.SkipBlankUpdates
			if (
				!(heuristicsOptions.SkipBlankUpdates && IsBlank(source.Name))
				&& mergeOptions.PreferExistingName && !string.IsNullOrWhiteSpace(source.Name)
			)
			{
				target.Name = source.Name;
			}
			if (
				!(heuristicsOptions.SkipBlankUpdates && IsBlank(source.Author))
				&& mergeOptions.PreferExistingAuthor && !string.IsNullOrWhiteSpace(source.Author)
			)
			{
				target.Author = source.Author;
			}
			if (
				!(heuristicsOptions.SkipBlankUpdates && IsBlank(source.Description))
				&& mergeOptions.PreferExistingDescription && !string.IsNullOrWhiteSpace(source.Description)
			)
			{
				target.Description = source.Description;
			}
			if (
				!(heuristicsOptions.SkipBlankUpdates && IsBlank(source.Directions))
				&& mergeOptions.PreferExistingDirections && !string.IsNullOrWhiteSpace(source.Directions)
			)
			{
				target.Directions = source.Directions;
			}
			if (
				!(heuristicsOptions.SkipBlankUpdates && IsBlank(source.Category))
				&& mergeOptions.PreferExistingCategory && source.Category.Count > 0
			)
			{
				target.Category = new List<string>(source.Category);
			}
			if (
				!(heuristicsOptions.SkipBlankUpdates && IsBlank(source.Tier))
				&& mergeOptions.PreferExistingTier && !string.IsNullOrWhiteSpace(source.Tier)
			)
			{
				target.Tier = source.Tier;
			}
			if (
				!(heuristicsOptions.SkipBlankUpdates && IsBlank(source.InstallationMethod))
				&& mergeOptions.PreferExistingInstallationMethod && !string.IsNullOrWhiteSpace(source.InstallationMethod)
			)
			{
				target.InstallationMethod = source.InstallationMethod;
			}

			// Merge arrays (union of both)
			if (source.Language.Count > 0)
			{
				HashSet<string> languageSet = new HashSet<string>(target.Language, StringComparer.OrdinalIgnoreCase);
				foreach (string lang in source.Language)
				{
					if (!string.IsNullOrWhiteSpace(lang))
						languageSet.Add(lang);
				}
				target.Language = languageSet.ToList();
			}

			// Merge ModLinkFilenames with heuristicsOptions-based filtering
			if (source.ModLinkFilenames.Count > 0 && !mergeOptions.PreferExistingModLinkFilenames)
			{
				// Build a set of URLs to merge from source
				HashSet<string> sourceUrlsToMerge = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (string link in source.ModLinkFilenames.Keys)
				{
					if (!string.IsNullOrWhiteSpace(link))
						sourceUrlsToMerge.Add(link);
				}

				// Filter URLs if validation is enabled
				if (heuristicsOptions.ValidateExistingLinksBeforeReplace)
				{
					sourceUrlsToMerge = new HashSet<string>(sourceUrlsToMerge.Where(IsLikelyAccessibleUrl), StringComparer.OrdinalIgnoreCase);

					// Also remove invalid URLs from target
					List<string> targetUrlsToRemove = target.ModLinkFilenames.Keys.Where(url => !IsLikelyAccessibleUrl(url)).ToList();
					foreach (string invalidUrl in targetUrlsToRemove)
					{
						target.ModLinkFilenames.Remove(invalidUrl);
					}
				}

				// Merge URLs from source into target, copying the actual filename dictionaries
				foreach (string url in sourceUrlsToMerge)
				{
					if (!target.ModLinkFilenames.ContainsKey(url))
					{
						// Copy the filename dictionary from source
						if (source.ModLinkFilenames.TryGetValue(url, out Dictionary<string, bool?> sourceFilenames) && sourceFilenames != null)
						{
							// Deep copy the dictionary to avoid shared references
							Dictionary<string, bool?> copiedFilenames = new Dictionary<string, bool?>(sourceFilenames, StringComparer.Ordinal);
							target.ModLinkFilenames[url] = copiedFilenames;
						}
						else
						{
							target.ModLinkFilenames[url] = new Dictionary<string, bool?>(StringComparer.Ordinal);
						}
					}
					else
					{
						// URL exists in target - merge filenames from source into target's dictionary
						if (source.ModLinkFilenames.TryGetValue(url, out Dictionary<string, bool?> sourceFilenames) && sourceFilenames != null)
						{
							Dictionary<string, bool?> targetFilenames = target.ModLinkFilenames[url];
							if (targetFilenames != null)
							{
								foreach (KeyValuePair<string, bool?> filenameEntry in sourceFilenames)
								{
									if (!targetFilenames.ContainsKey(filenameEntry.Key))
									{
										targetFilenames[filenameEntry.Key] = filenameEntry.Value;
									}
								}
							}
						}
					}
				}
			}

			if (source.Dependencies.Count > 0)
			{
				HashSet<Guid> depsSet = new HashSet<Guid>(target.Dependencies);
				foreach (Guid dep in source.Dependencies)
					depsSet.Add(dep);
				target.Dependencies = depsSet.ToList();
			}

			if (source.Restrictions.Count > 0)
			{
				HashSet<Guid> resSet = new HashSet<Guid>(target.Restrictions);
				foreach (Guid res in source.Restrictions)
					resSet.Add(res);
				target.Restrictions = resSet.ToList();
			}

			if (source.InstallAfter.Count > 0)
			{
				HashSet<Guid> afterSet = new HashSet<Guid>(target.InstallAfter);
				foreach (Guid after in source.InstallAfter)
					afterSet.Add(after);
				target.InstallAfter = afterSet.ToList();
			}

			if (source.InstallBefore.Count > 0)
			{
				HashSet<Guid> beforeSet = new HashSet<Guid>(target.InstallBefore);
				foreach (Guid before in source.InstallBefore)
					beforeSet.Add(before);
				target.InstallBefore = beforeSet.ToList();
			}

			// Merge Instructions based on preference
			if (source.Instructions.Count > 0 && !mergeOptions.PreferExistingInstructions)
			{
				if (target.Instructions.Count == 0)
				{
					// Copy all instructions from source (deep copy to avoid shared references)
					foreach (Instruction instr in source.Instructions)
					{
						target.Instructions.Add(CloneInstruction(instr));
					}
				}
				else
				{
					// Merge instructions, avoiding duplicates based on Action+Destination key
					// Build lookup for existing instructions by key for GUID preservation
					Dictionary<string, Instruction> existingInstrByKey = target.Instructions
						.ToDictionary(i => (i.ActionString + "|" + i.Destination).ToLowerInvariant(), StringComparer.Ordinal);

					HashSet<string> existingKeys = new HashSet<string>(existingInstrByKey.Keys, StringComparer.Ordinal);
					foreach (Instruction instr in source.Instructions)
					{
						string key = (instr.ActionString + "|" + (instr.Destination)).ToLowerInvariant();
						if (!existingKeys.Contains(key))
						{
							target.Instructions.Add(CloneInstruction(instr));
						}
						else if (existingInstrByKey.TryGetValue(key, out Instruction existingInstr))
						{
							// Update existing instruction with merged data
							int index = target.Instructions.IndexOf(existingInstr);
							if (index >= 0)
							{
								Instruction cloned = CloneInstruction(instr);
								// Ensure GUID is preserved (use existing if incoming was empty, otherwise use incoming)
								if (
									instr.Action == existingInstr.Action
									&& instr.Source == existingInstr.Source
									&& string.Equals(
										instr.Destination,
										existingInstr.Destination,
										StringComparison.OrdinalIgnoreCase
									) && string.Equals(
										instr.Arguments,
										existingInstr.Arguments,
										StringComparison.OrdinalIgnoreCase
									))
								{
									cloned.Source = existingInstr.Source;
								}
								target.Instructions[index] = cloned;
							}
						}
					}
				}
			}

			// Merge Options based on preference
			if (source.Options.Count > 0 && !mergeOptions.PreferExistingOptions)
			{
				Dictionary<string, Option> optMap = target.Options.ToDictionary(o => o.Name.Trim().ToLowerInvariant(), StringComparer.Ordinal);
				foreach (Option srcOpt in source.Options)
				{
					string oname = srcOpt.Name.Trim().ToLowerInvariant();
					if (optMap.TryGetValue(oname, out Option trgOpt))
					{
						// GUID merge logic: prefer the GUID that exists (non-empty)
						// If incoming has a GUID but existing has empty, keep incoming GUID
						if (srcOpt.Guid != Guid.Empty && trgOpt.Guid == Guid.Empty)
						{
							trgOpt.Guid = srcOpt.Guid;
						}
						// If existing has a GUID but incoming has empty, preserve existing GUID
						else if (srcOpt.Guid == Guid.Empty && trgOpt.Guid != Guid.Empty)
						{
							// Keep existing GUID (already set in trgOpt)
						}
						// If both have GUIDs but they're different, prefer existing GUID
						else if (srcOpt.Guid != Guid.Empty && trgOpt.Guid != Guid.Empty && srcOpt.Guid != trgOpt.Guid)
						{
							// Keep existing GUID (already set in trgOpt)
						}

						// Merge description if source has one
						if (!string.IsNullOrWhiteSpace(srcOpt.Description))
							trgOpt.Description = srcOpt.Description;

						// Merge Restrictions
						if (srcOpt.Restrictions.Count > 0)
						{
							HashSet<Guid> restrictionsSet = new HashSet<Guid>(trgOpt.Restrictions);
							foreach (Guid restriction in srcOpt.Restrictions)
								restrictionsSet.Add(restriction);
							trgOpt.Restrictions = restrictionsSet.ToList();
						}

						// Merge Dependencies
						if (srcOpt.Dependencies.Count > 0)
						{
							HashSet<Guid> dependenciesSet = new HashSet<Guid>(trgOpt.Dependencies);
							foreach (Guid dependency in srcOpt.Dependencies)
								dependenciesSet.Add(dependency);
							trgOpt.Dependencies = dependenciesSet.ToList();
						}

						// Merge instructions (deep copy to avoid shared references)
						if (srcOpt.Instructions.Count > 0)
						{
							// Build lookup for existing instructions by key for GUID preservation
							Dictionary<string, Instruction> existingInstrByKey = trgOpt.Instructions
								.ToDictionary(i => (i.ActionString + "|" + i.Destination).ToLowerInvariant(), StringComparer.Ordinal);

							HashSet<string> keys = new HashSet<string>(existingInstrByKey.Keys, StringComparer.Ordinal);
							foreach (Instruction instr in srcOpt.Instructions)
							{
								string key = (instr.ActionString + "|" + instr.Destination).ToLowerInvariant();
								if (!keys.Contains(key))
								{
									trgOpt.Instructions.Add(CloneInstruction(instr));
								}
								else if (existingInstrByKey.TryGetValue(key, out Instruction existingInstr))
								{
									// Update existing instruction with merged data
									int index = trgOpt.Instructions.IndexOf(existingInstr);
									if (index >= 0)
									{
										Instruction cloned = CloneInstruction(instr);
										trgOpt.Instructions[index] = cloned;
									}
								}
							}
						}
					}
					else
					{
						// Option doesn't exist in target, add a deep copy
						target.Options.Add(CloneOption(srcOpt));
					}
				}
			}
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
		private static async System.Threading.Tasks.Task<List<string>> ValidateUrlsViaResolutionAsync(
					[NotNull] List<string> urls,
					[NotNull] DownloadCacheService downloadCache,
					bool sequential = true,
					System.Threading.CancellationToken cancellationToken = default)
		{
			if (urls.Count == 0)
				return new List<string>();

			List<string> validUrls = new List<string>();
			List<string> failedToResolveUrls = new List<string>();
			List<string> timedOutUrls = new List<string>();

			await Logger.LogVerboseAsync($"[ComponentMerge] Validating {urls.Count} URLs via filename resolution...").ConfigureAwait(false);

			Dictionary<string, Dictionary<string, bool?>> tempModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>(StringComparer.OrdinalIgnoreCase);
			foreach (string url in urls)
			{
				tempModLinkFilenames[url] = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
			}

			ModComponent tempComponent = new ModComponent
			{
				Name = "TempValidation",
				Guid = Guid.NewGuid(),
				ModLinkFilenames = tempModLinkFilenames,
			};

			IReadOnlyDictionary<string, List<string>> resolvedUrlsReadOnly = await downloadCache.PreResolveUrlsAsync(
				tempComponent, null,
				sequential: sequential,
				cancellationToken
			).ConfigureAwait(false);

			// Convert to Dictionary for easier manipulation
			Dictionary<string, List<string>> resolvedUrls = resolvedUrlsReadOnly?.ToDictionary(
				kvp => kvp.Key,
				kvp => new List<string>(kvp.Value),
				StringComparer.OrdinalIgnoreCase
			) ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

			foreach (string url in urls)
			{
				if (resolvedUrls.TryGetValue(url, out List<string> filenames) &&
					 filenames.Count > 0 && !string.IsNullOrWhiteSpace(filenames[0]))
				{
					validUrls.Add(url);
					await Logger.LogVerboseAsync($"[ComponentMerge] ✓ Resolved: {url} -> {filenames[0]}").ConfigureAwait(false);
				}
				else
				{
					if (IsUrlLikelyTimedOut(url))
					{
						timedOutUrls.Add(url);
						await Logger.LogWarningAsync($"[ComponentMerge] ✗ URL timed out during resolution: {url}").ConfigureAwait(false);
					}
					else
					{
						failedToResolveUrls.Add(url);
						await Logger.LogVerboseAsync($"[ComponentMerge] ⚠ Failed to resolve: {url}").ConfigureAwait(false);
					}
				}
			}

			if (failedToResolveUrls.Count > 0)
			{
				await Logger.LogVerboseAsync($"[ComponentMerge] Checking existence of {failedToResolveUrls.Count} unresolved URL(s)...").ConfigureAwait(false);

				using (HttpClient httpClient = new HttpClient())
				{
					httpClient.Timeout = TimeSpan.FromSeconds(10);
					httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

					foreach (string url in failedToResolveUrls)
					{
						bool exists = await CheckUrlExistsAsync(httpClient, url, cancellationToken).ConfigureAwait(false);
						if (exists)
						{
							validUrls.Add(url);
							await Logger.LogVerboseAsync($"[ComponentMerge] ✓ URL exists (but unresolved): {url}").ConfigureAwait(false);
						}
						else
						{
							await Logger.LogWarningAsync($"[ComponentMerge] ✗ Invalid/Broken URL: {url}").ConfigureAwait(false);
						}
					}
				}
			}

			if (timedOutUrls.Count > 0)
			{
				await Logger.LogVerboseAsync($"[ComponentMerge] Checking existence of {timedOutUrls.Count} timed out URL(s)...").ConfigureAwait(false);

				using (HttpClient httpClient = new HttpClient())
				{
					httpClient.Timeout = TimeSpan.FromSeconds(5);
					httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

					foreach (string url in timedOutUrls)
					{
						bool exists = await CheckUrlExistsAsync(httpClient, url, cancellationToken).ConfigureAwait(false);
						if (exists)
						{
							validUrls.Add(url);
							await Logger.LogVerboseAsync($"[ComponentMerge] ✓ URL exists (but timed out): {url}").ConfigureAwait(false);
						}
						else
						{
							await Logger.LogWarningAsync($"[ComponentMerge] ✗ Invalid/Timed out URL: {url}").ConfigureAwait(false);
						}
					}
				}
			}

			int removedCount = urls.Count - validUrls.Count;
			if (removedCount > 0)
				await Logger.LogAsync($"[ComponentMerge] Filtered out {removedCount} invalid URL(s)").ConfigureAwait(false);

			return validUrls;
		}
		private static bool IsUrlLikelyTimedOut(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				return false;

			if (url.Contains("mega.nz") || url.Contains("mega.co.nz"))
				return true;

			return false;
		}

		private static async System.Threading.Tasks.Task<bool> CheckUrlExistsAsync(
			HttpClient httpClient,
			string url,
			System.Threading.CancellationToken cancellationToken)
		{
			try
			{
				if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
				{
					await Logger.LogVerboseAsync($"[ComponentMerge] Invalid URL syntax: {url}").ConfigureAwait(false);
					return false;
				}

				if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
					 !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
				{
					await Logger.LogVerboseAsync($"[ComponentMerge] Unsupported URL scheme: {uri.Scheme}").ConfigureAwait(false);
					return false;
				}

				using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url))
				{
					using (HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
					{
						int statusCode = (int)response.StatusCode;
						return statusCode >= 200 && statusCode < 400;
					}
				}
			}
			catch (HttpRequestException)
			{
				return false;
			}
			catch (System.Threading.Tasks.TaskCanceledException)
			{
				await Logger.LogVerboseAsync($"[ComponentMerge] Timeout checking URL: {url}").ConfigureAwait(false);
				return false;
			}
			catch (Exception ex)
			{
				await Logger.LogVerboseAsync($"[ComponentMerge] Error checking URL {url}: {ex.Message}").ConfigureAwait(false);
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
			if (trim) s = s.Trim();
			if (ignorePunctuation)
			{
				char[] arr = s.Where(c => !char.IsPunctuation(c)).ToArray();
				s = new string(arr);
			}
			if (ignoreCase) s = s.ToLowerInvariant();
			return s;
		}

		private static readonly char[] s_spaceSeparatorArray = new[] { ' ' };

		private static double JaccardSimilarity([NotNull] string a, [NotNull] string b)
		{
			if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0.0;
			HashSet<string> setA = new HashSet<string>(a.Split(s_spaceSeparatorArray, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
			HashSet<string> setB = new HashSet<string>(b.Split(s_spaceSeparatorArray, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
			int intersection = setA.Intersect(setB, StringComparer.Ordinal).Count();
			int union = setA.Union(setB, StringComparer.Ordinal).Count();
			return union == 0 ? 0.0 : (double)intersection / union;
		}

		private static bool IsBlank([CanBeNull] string v) => string.IsNullOrWhiteSpace(v);

		private static bool IsBlank([CanBeNull] List<string> v) => v is null || v.Count == 0;

		private static ModComponent FindHeuristicMatch(
			[NotNull] List<ModComponent> incomingModComponents,
			[NotNull] ModComponent existingComponent,
			[NotNull] MergeHeuristicsOptions opt)
		{
			string existingName = Normalize(existingComponent.Name, opt.IgnoreCase, opt.IgnorePunctuation, opt.TrimWhitespace);
			string existingAuthor = Normalize(existingComponent.Author, opt.IgnoreCase, opt.IgnorePunctuation, opt.TrimWhitespace);

			double bestScore = 0.0;
			ModComponent best = null;
			foreach (ModComponent incomingComp in incomingModComponents)
			{
				string incomingName = Normalize(incomingComp.Name, opt.IgnoreCase, opt.IgnorePunctuation, opt.TrimWhitespace);
				string incomingAuthor = Normalize(incomingComp.Author, opt.IgnoreCase, opt.IgnorePunctuation, opt.TrimWhitespace);

				double score = 0.0;
				if (opt.UseNameExact && !string.IsNullOrEmpty(existingName) && string.Equals(existingName, incomingName, StringComparison.Ordinal)) score += 1.0;
				if (opt.UseAuthorExact && !string.IsNullOrEmpty(existingAuthor) && string.Equals(existingAuthor, incomingAuthor, StringComparison.Ordinal)) score += 1.0;
				if (opt.UseNameSimilarity && !string.IsNullOrEmpty(existingName)) score += JaccardSimilarity(existingName, incomingName);
				if (opt.UseAuthorSimilarity && !string.IsNullOrEmpty(existingAuthor)) score += JaccardSimilarity(existingAuthor, incomingAuthor) * 0.5;

				if (score < 0.5 && opt.MatchByDomainIfNoNameAuthorMatch)
				{
					string existingDomain = GetPrimaryDomain(existingComponent);
					string incomingDomain = GetPrimaryDomain(incomingComp);
					if (!string.IsNullOrEmpty(existingDomain) && string.Equals(existingDomain, incomingDomain, StringComparison.Ordinal))
						score += 0.6;
				}

				if (score > bestScore && score >= opt.MinNameSimilarity)
				{
					bestScore = score;
					best = incomingComp;
				}
			}
			return best;
		}

		private static string GetPrimaryDomain([NotNull] ModComponent c)
		{
			string url = c.ModLinkFilenames.Keys.FirstOrDefault();
			if (string.IsNullOrWhiteSpace(url)) return null;
			try
			{
				Uri uri = new Uri(url, UriKind.Absolute);
				return uri.Host.ToLowerInvariant();
			}
			catch
			{
				return null;
			}
		}

		private static bool IsLikelyAccessibleUrl([NotNull] string url)
		{
			if (string.IsNullOrWhiteSpace(url)) return false;
			if (!(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
				return false;
			try
			{
				Uri uri = new Uri(url);
				return !string.IsNullOrWhiteSpace(uri.Host);
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Creates a deep copy of an Instruction to avoid shared references between components.
		/// </summary>
		[NotNull]
		private static Instruction CloneInstruction([NotNull] Instruction source)
		{
			return new Instruction
			{
				Action = source.Action,
				Source = new List<string>(source.Source),
				Destination = source.Destination,
				Overwrite = source.Overwrite,
				Arguments = source.Arguments,
				Dependencies = new List<Guid>(source.Dependencies),
				Restrictions = new List<Guid>(source.Restrictions),
			};
		}

		/// <summary>
		/// Creates a deep copy of an Option to avoid shared references between components.
		/// </summary>
		[NotNull]
		private static Option CloneOption([NotNull] Option source)
		{
			Option cloned = new Option
			{
				Guid = source.Guid,
				Name = source.Name,
				Description = source.Description,
				IsSelected = source.IsSelected,
				Instructions = new System.Collections.ObjectModel.ObservableCollection<Instruction>(),
				Restrictions = new List<Guid>(source.Restrictions),
				Dependencies = new List<Guid>(source.Dependencies),
			};

			// Deep copy all instructions
			foreach (Instruction instr in source.Instructions)
			{
				cloned.Instructions.Add(CloneInstruction(instr));
			}

			return cloned;
		}
	}
}