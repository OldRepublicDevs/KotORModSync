// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KOTORModSync.Core.Services
{

	public static class CircularDependencyDetector
	{
		public class CircularDependencyResult
		{
			public bool HasCircularDependencies { get; set; }
			public List<List<Guid>> Cycles { get; set; } = new List<List<Guid>>();
			public Dictionary<Guid, KOTORModSync.Core.ModComponent> ComponentsByGuid { get; set; } = new Dictionary<Guid, KOTORModSync.Core.ModComponent>();
			public string DetailedErrorMessage { get; set; }
		}

		public static CircularDependencyResult DetectCircularDependencies( List<ModComponent> components )
		{
			CircularDependencyResult result = new CircularDependencyResult();
			Dictionary<Guid, ModComponent> componentsByGuid = components.ToDictionary( c => c.Guid, c => c );
			result.ComponentsByGuid = componentsByGuid;

			Dictionary<Guid, List<Guid>> graph = new Dictionary<Guid, List<Guid>>();
			foreach (ModComponent component in components)
			{
				if (!graph.ContainsKey( component.Guid ))
					graph[component.Guid] = new List<Guid>();

				foreach (Guid depGuid in component.Dependencies)
				{
					if (!componentsByGuid.ContainsKey( depGuid ))
						continue;
					if (!graph.ContainsKey( component.Guid ))
						graph[component.Guid] = new List<Guid>();
					graph[component.Guid].Add( depGuid );
				}

				foreach (Guid afterGuid in component.InstallAfter)
				{
					if (!componentsByGuid.ContainsKey( afterGuid ))
						continue;
					if (!graph.ContainsKey( component.Guid ))
						graph[component.Guid] = new List<Guid>();
					graph[component.Guid].Add( afterGuid );
				}
			}

			HashSet<Guid> visited = new HashSet<Guid>();
			HashSet<Guid> recursionStack = new HashSet<Guid>();
			List<Guid> currentPath = new List<Guid>();

			foreach (Guid guid in componentsByGuid.Keys.Where( guid => !visited.Contains( guid ) ))
			{
				if (DfsDetectCycle( guid, graph, visited, recursionStack, currentPath, result ))
					result.HasCircularDependencies = true;
			}

			if (result.HasCircularDependencies)
			{
				StringBuilder sb = new StringBuilder();
				_ = sb.AppendLine( "⚠️ CIRCULAR DEPENDENCY DETECTED" );
				_ = sb.AppendLine();
				_ = sb.AppendLine( $"Found {result.Cycles.Count} circular dependency cycle(s):" );
				_ = sb.AppendLine();

				for (int i = 0; i < result.Cycles.Count; i++)
				{
					List<Guid> cycle = result.Cycles[i];
					_ = sb.AppendLine( $"Cycle #{i + 1}:" );
					for (int j = 0; j < cycle.Count; j++)
					{
						Guid guid = cycle[j];
						if (!componentsByGuid.TryGetValue( guid, out ModComponent comp ))
							continue;
						_ = sb.Append( $"  {j + 1}. {comp.Name}" );
						if (!string.IsNullOrWhiteSpace( comp.Author ))
							_ = sb.Append( $" by {comp.Author}" );

						if (j < cycle.Count - 1)
						{
							Guid nextGuid = cycle[j + 1];
							if (componentsByGuid.TryGetValue( nextGuid, out ModComponent nextComp ))
								_ = sb.Append( $" → depends on → {nextComp.Name}" );
						}
						else
						{

							Guid firstGuid = cycle[0];
							if (componentsByGuid.TryGetValue( firstGuid, out ModComponent firstComp ))
								_ = sb.Append( $" → depends on → {firstComp.Name} (CYCLE!)" );
						}
						_ = sb.AppendLine();
					}
					_ = sb.AppendLine();
				}

				_ = sb.AppendLine( "💡 To fix this:" );
				_ = sb.AppendLine( "1. Uncheck one or more components in the cycle" );
				_ = sb.AppendLine( "2. Or remove/modify dependencies using the component editor" );
				_ = sb.AppendLine( "3. Or contact the mod authors about the circular dependency" );

				result.DetailedErrorMessage = sb.ToString();
			}

			return result;
		}

		private static bool DfsDetectCycle(
			Guid node,
			Dictionary<Guid, List<Guid>> graph,
			HashSet<Guid> visited,
			HashSet<Guid> recursionStack,
			List<Guid> currentPath,
			CircularDependencyResult result )
		{
			_ = visited.Add( node );
			_ = recursionStack.Add( node );
			currentPath.Add( node );

			if (graph.TryGetValue( node, out List<Guid> neighbors ))
			{
				foreach (Guid neighbor in neighbors)
				{
					if (!visited.Contains( neighbor ))
					{
						if (DfsDetectCycle( neighbor, graph, visited, recursionStack, currentPath, result ))
							return true;
					}
					else if (recursionStack.Contains( neighbor ))
					{

						int cycleStartIndex = currentPath.IndexOf( neighbor );
						List<Guid> cycle = currentPath.Skip( cycleStartIndex ).ToList();
						cycle.Add( neighbor );

						bool isDuplicate = result.Cycles.Exists( existingCycle =>
							existingCycle.Count == cycle.Count &&
							existingCycle.Intersect( cycle ).Count() == cycle.Count );

						if (!isDuplicate)
							result.Cycles.Add( cycle );

						return true;
					}
				}
			}

			_ = recursionStack.Remove( node );
			currentPath.RemoveAt( currentPath.Count - 1 );
			return false;
		}

		public static List<ModComponent> SuggestComponentsToRemove( CircularDependencyResult result )
		{
			if (!result.HasCircularDependencies)
				return new List<ModComponent>();

			Dictionary<Guid, int> componentCycleCount = new Dictionary<Guid, int>();
			foreach (List<Guid> cycle in result.Cycles)
			{
				foreach (Guid guid in cycle)
				{
					if (!componentCycleCount.ContainsKey( guid ))
						componentCycleCount[guid] = 0;
					componentCycleCount[guid]++;
				}
			}

			List<ModComponent> suggestions = componentCycleCount
				.OrderByDescending( kvp => kvp.Value )
				.Select( kvp => result.ComponentsByGuid.ContainsKey( kvp.Key ) ? result.ComponentsByGuid[kvp.Key] : null )
				.Where( comp => !(comp is null) )
				.Take( 3 )
				.ToList();

			return suggestions;
		}
	}
}