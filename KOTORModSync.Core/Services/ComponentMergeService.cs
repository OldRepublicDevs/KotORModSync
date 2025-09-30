// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
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
			[NotNull] List<Component> existing,
			[NotNull] List<Component> incoming,
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
		/// GUID-based merging: matches components by their GUID and updates existing ones
		/// </summary>
		private static void MergeByGuid([NotNull] List<Component> existing, [NotNull] List<Component> incoming)
		{
			// Create a dictionary for fast GUID lookup
			var existingByGuid = existing.ToDictionary(c => c.Guid, c => c);

			foreach ( var incomingComponent in incoming )
			{
				if ( existingByGuid.TryGetValue(incomingComponent.Guid, out var existingComponent) )
				{
					// Update existing component with new data
					UpdateComponentByGuid(existingComponent, incomingComponent);
				}
				else
				{
					// New component, add it
					existing.Add(incomingComponent);
					existingByGuid[incomingComponent.Guid] = incomingComponent;
				}
			}
		}

		/// <summary>
		/// Updates an existing component with data from an incoming component (GUID-based merge)
		/// </summary>
		private static void UpdateComponentByGuid([NotNull] Component target, [NotNull] Component source)
		{
			// Update all properties from source, but preserve the target's GUID
			var originalGuid = target.Guid;

			// Update basic properties
			target.Name = source.Name ?? target.Name;
			target.Author = source.Author ?? target.Author;
			target.Category = source.Category ?? target.Category;
			target.Tier = source.Tier ?? target.Tier;
			target.Description = source.Description ?? target.Description;
			target.Directions = source.Directions ?? target.Directions;
			target.InstallationMethod = source.InstallationMethod ?? target.InstallationMethod;

			// Merge collections (union)
			if ( source.Language?.Count > 0 )
			{
				var set = new HashSet<string>(target.Language ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
				foreach ( string lang in source.Language )
					if ( !string.IsNullOrWhiteSpace(lang) )
						_ = set.Add(lang);
				target.Language = set.ToList();
			}

			if ( source.ModLink?.Count > 0 )
			{
				var set = new HashSet<string>(target.ModLink ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
				foreach ( string link in source.ModLink )
					if ( !string.IsNullOrWhiteSpace(link) )
						_ = set.Add(link);
				target.ModLink = set.ToList();
			}

			// Merge GUID collections (union)
			if ( source.Dependencies?.Count > 0 )
			{
				var set = new HashSet<Guid>(target.Dependencies ?? new List<Guid>());
				foreach ( Guid g in source.Dependencies )
					_ = set.Add(g);
				target.Dependencies = set.ToList();
			}

			if ( source.Restrictions?.Count > 0 )
			{
				var set = new HashSet<Guid>(target.Restrictions ?? new List<Guid>());
				foreach ( Guid g in source.Restrictions )
					_ = set.Add(g);
				target.Restrictions = set.ToList();
			}

			if ( source.InstallAfter?.Count > 0 )
			{
				var set = new HashSet<Guid>(target.InstallAfter ?? new List<Guid>());
				foreach ( Guid g in source.InstallAfter )
					_ = set.Add(g);
				target.InstallAfter = set.ToList();
			}

			if ( source.InstallBefore?.Count > 0 )
			{
				var set = new HashSet<Guid>(target.InstallBefore ?? new List<Guid>());
				foreach ( Guid g in source.InstallBefore )
					_ = set.Add(g);
				target.InstallBefore = set.ToList();
			}

			// Replace instructions (GUID-based merge assumes complete replacement)
			if ( source.Instructions?.Count > 0 )
			{
				target.Instructions.Clear();
				foreach ( var instruction in source.Instructions )
					target.Instructions.Add(instruction);
			}

			// Replace options (GUID-based merge assumes complete replacement)
			if ( source.Options?.Count > 0 )
			{
				target.Options.Clear();
				foreach ( var option in source.Options )
					target.Options.Add(option);
			}

			// Preserve the original GUID
			target.Guid = originalGuid;
		}
	}
}
