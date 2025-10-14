// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using KOTORModSync.Core.Parsing;

namespace KOTORModSync.Core.Services
{
	public enum MergeStrategy
	{

		ByGuid,

		ByNameAndAuthor
	}

	public class MergeOptions
	{
		public bool ExcludeExistingOnly { get; set; }
		public bool ExcludeIncomingOnly { get; set; }
		public bool UseExistingOrder { get; set; }
		public MergeHeuristicsOptions HeuristicsOptions { get; set; }

		public static MergeOptions CreateDefault() => new MergeOptions
		{
			ExcludeExistingOnly = false,
			ExcludeIncomingOnly = false,
			UseExistingOrder = false,
			HeuristicsOptions = MergeHeuristicsOptions.CreateDefault()
		};
	}

	public static class ComponentMergeService
	{
		/// <summary>
		/// Asynchronously merges two instruction sets with optional URL validation.
		/// If downloadCache is provided, invalid URLs will be removed during the merge.
		/// </summary>
		[NotNull]
		public static async System.Threading.Tasks.Task<List<ModComponent>> MergeInstructionSetsAsync(
				[NotNull] string existingFilePath,
				[NotNull] string incomingFilePath,
				MergeStrategy strategy = MergeStrategy.ByNameAndAuthor,
				[CanBeNull] MergeOptions options = null,
				[CanBeNull] DownloadCacheService downloadCache = null,
				System.Threading.CancellationToken cancellationToken = default)
		{
			if ( existingFilePath == null )
				throw new ArgumentNullException(nameof(existingFilePath));
			if ( incomingFilePath == null )
				throw new ArgumentNullException(nameof(incomingFilePath));

			options = options ?? MergeOptions.CreateDefault();

			List<ModComponent> existing = FileLoadingService.LoadFromFile(existingFilePath);
			List<ModComponent> incoming = FileLoadingService.LoadFromFile(incomingFilePath);

			return await MergeComponentListsAsync(existing, incoming, strategy, options, downloadCache, cancellationToken).ConfigureAwait(false);
		}

		[NotNull]
		public static List<ModComponent> MergeInstructionSets(
				[NotNull] string existingFilePath,
				[NotNull] string incomingFilePath,
				MergeStrategy strategy = MergeStrategy.ByNameAndAuthor,
				[CanBeNull] MergeOptions options = null)
		{
			if ( existingFilePath == null )
				throw new ArgumentNullException(nameof(existingFilePath));
			if ( incomingFilePath == null )
				throw new ArgumentNullException(nameof(incomingFilePath));

			options = options ?? MergeOptions.CreateDefault();

			List<ModComponent> existing = FileLoadingService.LoadFromFile(existingFilePath);
			List<ModComponent> incoming = FileLoadingService.LoadFromFile(incomingFilePath);

			return MergeComponentLists(existing, incoming, strategy, options);
		}

		/// <summary>
		/// Asynchronously merges component lists with optional URL validation.
		/// If downloadCache is provided, invalid URLs will be removed during the merge.
		/// </summary>
		[NotNull]
		public static async System.Threading.Tasks.Task<List<ModComponent>> MergeComponentListsAsync(
			[NotNull] List<ModComponent> existing,
			[NotNull] List<ModComponent> incoming,
			MergeStrategy strategy,
			[CanBeNull] MergeOptions options = null,
			[CanBeNull] DownloadCacheService downloadCache = null,
			System.Threading.CancellationToken cancellationToken = default)
		{
			if ( existing == null )
				throw new ArgumentNullException(nameof(existing));
			if ( incoming == null )
				throw new ArgumentNullException(nameof(incoming));

			options = options ?? MergeOptions.CreateDefault();
			options.HeuristicsOptions = options.HeuristicsOptions ?? MergeHeuristicsOptions.CreateDefault();

			List<ModComponent> result;

			if ( options.UseExistingOrder )
			{
				// Start with EXISTING, merge INCOMING into it
				// AddNewWhenNoMatchFound controls whether to add items from INCOMING
				options.HeuristicsOptions.AddNewWhenNoMatchFound = !options.ExcludeIncomingOnly;

				result = new List<ModComponent>(existing);
				await MergeIntoAsync(result, incoming, strategy, options.HeuristicsOptions, downloadCache, cancellationToken).ConfigureAwait(false);

				if ( options.ExcludeExistingOnly )
				{
					var matchedGuids = new HashSet<Guid>(incoming.Select(c => c.Guid));
					result.RemoveAll(c => !matchedGuids.Contains(c.Guid));
				}
			}
			else
			{
				// Start with INCOMING, merge EXISTING into it
				// AddNewWhenNoMatchFound controls whether to add items from EXISTING
				options.HeuristicsOptions.AddNewWhenNoMatchFound = !options.ExcludeExistingOnly;

				result = new List<ModComponent>(incoming);
				await MergeIntoAsync(result, existing, strategy, options.HeuristicsOptions, downloadCache, cancellationToken).ConfigureAwait(false);

				if ( options.ExcludeIncomingOnly )
				{
					var matchedGuids = new HashSet<Guid>(existing.Select(c => c.Guid));
					result.RemoveAll(c => !matchedGuids.Contains(c.Guid));
				}

				if ( options.ExcludeExistingOnly )
				{
					var matchedGuids = new HashSet<Guid>(incoming.Select(c => c.Guid));
					result = result.Where(c => matchedGuids.Contains(c.Guid)).ToList();
				}
			}

			return result;
		}

		[NotNull]
		public static List<ModComponent> MergeComponentLists(
			[NotNull] List<ModComponent> existing,
			[NotNull] List<ModComponent> incoming,
			MergeStrategy strategy,
			[CanBeNull] MergeOptions options = null)
		{
			if ( existing == null )
				throw new ArgumentNullException(nameof(existing));
			if ( incoming == null )
				throw new ArgumentNullException(nameof(incoming));

			options = options ?? MergeOptions.CreateDefault();
			options.HeuristicsOptions = options.HeuristicsOptions ?? MergeHeuristicsOptions.CreateDefault();

			List<ModComponent> result;

			if ( options.UseExistingOrder )
			{
				// Start with EXISTING, merge INCOMING into it
				// AddNewWhenNoMatchFound controls whether to add items from INCOMING
				options.HeuristicsOptions.AddNewWhenNoMatchFound = !options.ExcludeIncomingOnly;

				result = new List<ModComponent>(existing);
				MergeInto(result, incoming, strategy, options.HeuristicsOptions);

				if ( options.ExcludeExistingOnly )
				{
					var matchedGuids = new HashSet<Guid>(incoming.Select(c => c.Guid));
					result.RemoveAll(c => !matchedGuids.Contains(c.Guid));
				}
			}
			else
			{
				// Start with INCOMING, merge EXISTING into it
				// AddNewWhenNoMatchFound controls whether to add items from EXISTING
				options.HeuristicsOptions.AddNewWhenNoMatchFound = !options.ExcludeExistingOnly;

				result = new List<ModComponent>(incoming);
				MergeInto(result, existing, strategy, options.HeuristicsOptions);

				if ( options.ExcludeIncomingOnly )
				{
					var matchedGuids = new HashSet<Guid>(existing.Select(c => c.Guid));
					result.RemoveAll(c => !matchedGuids.Contains(c.Guid));
				}

				if ( options.ExcludeExistingOnly )
				{
					var matchedGuids = new HashSet<Guid>(incoming.Select(c => c.Guid));
					result = result.Where(c => matchedGuids.Contains(c.Guid)).ToList();
				}
			}

			return result;
		}

		/// <summary>
		/// Asynchronously merges incoming list into base list with optional URL validation.
		/// </summary>
		public static async System.Threading.Tasks.Task MergeIntoAsync(
			[NotNull] List<ModComponent> baseList,
			[NotNull] List<ModComponent> incoming,
			MergeStrategy strategy,
			[CanBeNull] MergeHeuristicsOptions options = null,
			[CanBeNull] DownloadCacheService downloadCache = null,
			System.Threading.CancellationToken cancellationToken = default)
		{
			if ( baseList == null )
				throw new ArgumentNullException(nameof(baseList));
			if ( incoming == null )
				throw new ArgumentNullException(nameof(incoming));

			switch ( strategy )
			{
				case MergeStrategy.ByGuid:
					MergeByGuid(baseList, incoming);
					break;
				case MergeStrategy.ByNameAndAuthor:
					await MarkdownMergeService.MergeIntoAsync(baseList, incoming, options, false, downloadCache, cancellationToken).ConfigureAwait(false);
					break;
				default:
					throw new ArgumentException($"Unknown merge strategy: {strategy}");
			}
		}

		public static void MergeInto(
			[NotNull] List<ModComponent> baseList,
			[NotNull] List<ModComponent> incoming,
			MergeStrategy strategy,
			[CanBeNull] MergeHeuristicsOptions options = null)
		{
			if ( baseList == null )
				throw new ArgumentNullException(nameof(baseList));
			if ( incoming == null )
				throw new ArgumentNullException(nameof(incoming));

			switch ( strategy )
			{
				case MergeStrategy.ByGuid:
					MergeByGuid(baseList, incoming);
					break;
				case MergeStrategy.ByNameAndAuthor:
					MarkdownMergeService.MergeInto(baseList, incoming, options);
					break;
				default:
					throw new ArgumentException($"Unknown merge strategy: {strategy}");
			}
		}


		private static void MergeByGuid([NotNull] List<ModComponent> existing, [NotNull] List<ModComponent> incoming)
		{

			var existingByGuid = existing.ToDictionary(c => c.Guid, c => c);
			var matchedExisting = new HashSet<Guid>();
			var result = new List<ModComponent>();

			foreach ( ModComponent incomingComponent in incoming )
			{
				if ( existingByGuid.TryGetValue(incomingComponent.Guid, out ModComponent existingComponent) )
				{

					UpdateComponentByGuid(existingComponent, incomingComponent);
					result.Add(existingComponent);
					matchedExisting.Add(existingComponent.Guid);
				}
				else
				{

					result.Add(incomingComponent);
					existingByGuid[incomingComponent.Guid] = incomingComponent;
				}
			}

			foreach ( ModComponent existingComponent in existing )
			{
				if ( !matchedExisting.Contains(existingComponent.Guid) )
				{

					int insertIndex = FindInsertionPoint(result, existingComponent, existing);
					result.Insert(insertIndex, existingComponent);
				}
			}

			existing.Clear();
			existing.AddRange(result);
		}


		private static int FindInsertionPoint(
			[NotNull] List<ModComponent> result,
			[NotNull] ModComponent componentToInsert,
			[NotNull] List<ModComponent> originalExisting)
		{

			int originalIndex = originalExisting.IndexOf(componentToInsert);
			if ( originalIndex < 0 ) return result.Count;

			for ( int i = originalIndex + 1; i < originalExisting.Count; i++ )
			{
				ModComponent afterComponent = originalExisting[i];
				int afterIndexInResult = result.FindIndex(c => c.Guid == afterComponent.Guid);
				if ( afterIndexInResult >= 0 )
				{

					return afterIndexInResult;
				}
			}

			return result.Count;
		}

		private static void UpdateComponentByGuid([NotNull] ModComponent target, [NotNull] ModComponent source)
		{

			Guid originalGuid = target.Guid;

			target.Name = source.Name;
			target.Author = source.Author;
			target.Category = source.Category;
			target.Tier = source.Tier;
			target.Description = source.Description;
			target.Directions = source.Directions;
			target.InstallationMethod = source.InstallationMethod;

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

			if ( source.Instructions.Count > 0 )
			{
				target.Instructions.Clear();
				foreach ( Instruction instruction in source.Instructions )
					target.Instructions.Add(instruction);
			}

			if ( source.Options.Count > 0 )
			{
				target.Options.Clear();
				foreach ( Option option in source.Options )
					target.Options.Add(option);
			}

			target.Guid = originalGuid;
		}
	}
}
