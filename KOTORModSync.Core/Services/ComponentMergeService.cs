



using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Services
{
	public enum MergeStrategy
	{
		
		
		
		ByGuid,
		
		
		
		ByNameAndAuthor
	}

	public static class ComponentMergeService
	{
		
		
		
		
		
		
		
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
