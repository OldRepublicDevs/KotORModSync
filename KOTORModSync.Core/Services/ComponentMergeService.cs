// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{
	public enum MergeStrategy
	{
		/// <summary>
		/// Merge based on GUID matching (for TOML files with GUIDs)
		/// </summary>
		ByGuid,
		/// <summary>
		/// Merge based on name/author matching (for markdown files without GUIDs)
		/// </summary>
		ByNameAndAuthor
	}

	public static class ComponentMergeService
	{
		/// <summary>
		/// Merges components using the specified strategy
		/// </summary>
		/// <param name="existing">Existing components list to merge into</param>
		/// <param name="incoming">New components to merge</param>
		/// <param name="strategy">Merge strategy to use</param>
		/// <param name="options">Heuristics options for name-based merging (ignored for GUID-based)</param>
		public static void MergeInto(
			[NotNull] List<ModComponent> existing,
			[NotNull] List<ModComponent> incoming,
			MergeStrategy strategy,
			[CanBeNull] MergeHeuristicsOptions options = null)
		{
			if ( existing == null )
				throw new ArgumentNullException(nameof(existing));
			if ( incoming == null )
				throw new ArgumentNullException(nameof(incoming));

			switch ( strategy )
			{
				case MergeStrategy.ByGuid:
					MergeByGuid(existing, incoming);
					break;
				case MergeStrategy.ByNameAndAuthor:
					MarkdownMergeService.MergeInto(existing, incoming, options);
					break;
				default:
					throw new ArgumentException($"Unknown merge strategy: {strategy}");
			}
		}

		/// <summary>
		/// GUID-based merging: matches components by their GUID and updates existing ones,
		/// preserving the order of the incoming list
		/// </summary>
		private static void MergeByGuid([NotNull] List<ModComponent> existing, [NotNull] List<ModComponent> incoming)
		{
			// Create a dictionary for fast GUID lookup
			var existingByGuid = existing.ToDictionary(c => c.Guid, c => c);
			var matchedExisting = new HashSet<Guid>();
			var result = new List<ModComponent>();

			// Process incoming list in order, preserving its sequence
			foreach ( ModComponent incomingComponent in incoming )
			{
				if ( existingByGuid.TryGetValue(incomingComponent.Guid, out ModComponent existingComponent) )
				{
					// Update existing component with new data and add to result
					UpdateComponentByGuid(existingComponent, incomingComponent);
					result.Add(existingComponent);
					matchedExisting.Add(existingComponent.Guid);
				}
				else
				{
					// New component, add it at its position in the incoming list
					result.Add(incomingComponent);
					existingByGuid[incomingComponent.Guid] = incomingComponent;
				}
			}

			// Add any existing components that weren't in the incoming list
			// These maintain their relative order from the original existing list
			foreach ( ModComponent existingComponent in existing )
			{
				if ( !matchedExisting.Contains(existingComponent.Guid) )
				{
					// Find the best insertion point based on surrounding components
					int insertIndex = FindInsertionPoint(result, existingComponent, existing);
					result.Insert(insertIndex, existingComponent);
				}
			}

			// Replace the existing list with the merged result
			existing.Clear();
			existing.AddRange(result);
		}

		/// <summary>
		/// Finds the best insertion point for an unmatched existing component
		/// based on its position relative to matched components
		/// </summary>
		private static int FindInsertionPoint(
			[NotNull] List<ModComponent> result,
			[NotNull] ModComponent componentToInsert,
			[NotNull] List<ModComponent> originalExisting)
		{
			// Find components before and after this one in the original existing list
			int originalIndex = originalExisting.IndexOf(componentToInsert);
			if ( originalIndex < 0 ) return result.Count;

			// Look for the nearest matched component after this one in the original list
			for ( int i = originalIndex + 1; i < originalExisting.Count; i++ )
			{
				ModComponent afterComponent = originalExisting[i];
				int afterIndexInResult = result.FindIndex(c => c.Guid == afterComponent.Guid);
				if ( afterIndexInResult >= 0 )
				{
					// Insert before this component
					return afterIndexInResult;
				}
			}

			// No matched component found after, so append at the end
			return result.Count;
		}

		/// <summary>
		/// Updates an existing component with data from an incoming component (GUID-based merge)
		/// </summary>
		private static void UpdateComponentByGuid([NotNull] ModComponent target, [NotNull] ModComponent source)
		{
			// Update all properties from source, but preserve the target's GUID
			Guid originalGuid = target.Guid;

			// Update basic properties
			target.Name = source.Name;
			target.Author = source.Author;
			target.Category = source.Category;
			target.Tier = source.Tier;
			target.Description = source.Description;
			target.Directions = source.Directions;
			target.InstallationMethod = source.InstallationMethod;

			// Merge collections (union)
			if ( source.Language.Count > 0 )
			{
				var set = new HashSet<string>(target.Language, StringComparer.OrdinalIgnoreCase);
				foreach ( string lang in source.Language )
				{
					if ( !string.IsNullOrWhiteSpace(lang) )
					{
						_ = set.Add(lang);
						target.Language = set.ToList();
					}
				}
			}



			if ( source.ModLink.Count > 0 )
			{
				var set = new HashSet<string>(target.ModLink, StringComparer.OrdinalIgnoreCase);
				foreach ( string link in source.ModLink )
				{
					if ( !string.IsNullOrWhiteSpace(link) )
					{
						_ = set.Add(link);
						target.ModLink = set.ToList();
					}
				}
			}



			// Merge GUID collections (union)
			if ( source.Dependencies.Count > 0 )
			{
				var set = new HashSet<Guid>(target.Dependencies);
				foreach ( Guid g in source.Dependencies )
					_ = set.Add(g);
				target.Dependencies = set.ToList();
			}

			if ( source.Restrictions.Count > 0 )
			{
				var set = new HashSet<Guid>(target.Restrictions);
				foreach ( Guid g in source.Restrictions )
					_ = set.Add(g);
				target.Restrictions = set.ToList();
			}

			if ( source.InstallAfter.Count > 0 )
			{
				var set = new HashSet<Guid>(target.InstallAfter);
				foreach ( Guid g in source.InstallAfter )
					_ = set.Add(g);
				target.InstallAfter = set.ToList();
			}

			if ( source.InstallBefore.Count > 0 )
			{
				var set = new HashSet<Guid>(target.InstallBefore);
				foreach ( Guid g in source.InstallBefore )
					_ = set.Add(g);
				target.InstallBefore = set.ToList();
			}

			// Replace instructions (GUID-based merge assumes complete replacement)
			if ( source.Instructions.Count > 0 )
			{
				target.Instructions.Clear();
				foreach ( Instruction instruction in source.Instructions )
					target.Instructions.Add(instruction);
			}

			// Replace options (GUID-based merge assumes complete replacement)
			if ( source.Options.Count > 0 )
			{
				target.Options.Clear();
				foreach ( Option option in source.Options )
					target.Options.Add(option);
			}

			// Preserve the original GUID
			target.Guid = originalGuid;
		}
	}
}
