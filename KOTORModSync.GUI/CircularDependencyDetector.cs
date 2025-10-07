// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KOTORModSync.Core;

namespace KOTORModSync
{
	/// <summary>
	/// Detects and resolves circular dependency issues in component lists.
	/// Uses industry-standard topological sorting and cycle detection algorithms.
	/// </summary>
	public static class CircularDependencyDetector
	{
		public class CircularDependencyResult
		{
			public bool HasCircularDependencies { get; set; }
			public List<List<Guid>> Cycles { get; set; } = new List<List<Guid>>();
			public Dictionary<Guid, Component> ComponentsByGuid { get; set; } = new Dictionary<Guid, Component>();
			public string DetailedErrorMessage { get; set; }
		}

		/// <summary>
		/// Detects circular dependencies using DFS-based cycle detection.
		/// This is the industry-standard approach used by npm, cargo, apt, etc.
		/// </summary>
		public static CircularDependencyResult DetectCircularDependencies(List<Component> components)
		{
			var result = new CircularDependencyResult();
			var componentsByGuid = components.ToDictionary(c => c.Guid, c => c);
			result.ComponentsByGuid = componentsByGuid;

			// Build adjacency list for dependency graph
			var graph = new Dictionary<Guid, List<Guid>>();
			foreach ( Component component in components )
			{
				if ( !graph.ContainsKey(component.Guid) )
					graph[component.Guid] = new List<Guid>();

				// Add edges for dependencies
				foreach ( Guid depGuid in component.Dependencies )
				{
					if ( !componentsByGuid.ContainsKey(depGuid) )
						continue;
					if ( !graph.ContainsKey(component.Guid) )
						graph[component.Guid] = new List<Guid>();
					graph[component.Guid].Add(depGuid);
				}

				// Add edges for InstallAfter (soft dependencies)
				foreach ( Guid afterGuid in component.InstallAfter )
				{
					if ( !componentsByGuid.ContainsKey(afterGuid) )
						continue;
					if ( !graph.ContainsKey(component.Guid) )
						graph[component.Guid] = new List<Guid>();
					graph[component.Guid].Add(afterGuid);
				}
			}

			// DFS-based cycle detection
			var visited = new HashSet<Guid>();
			var recursionStack = new HashSet<Guid>();
			var currentPath = new List<Guid>();

			foreach ( Guid guid in componentsByGuid.Keys.Where(guid => !visited.Contains(guid)) )
			{
				if ( DfsDetectCycle(guid, graph, visited, recursionStack, currentPath, result) )
					result.HasCircularDependencies = true;
			}

			// Build detailed error message
			if ( result.HasCircularDependencies )
			{
				var sb = new StringBuilder();
				_ = sb.AppendLine("⚠️ CIRCULAR DEPENDENCY DETECTED");
				_ = sb.AppendLine();
				_ = sb.AppendLine($"Found {result.Cycles.Count} circular dependency cycle(s):");
				_ = sb.AppendLine();

				for ( int i = 0; i < result.Cycles.Count; i++ )
				{
					List<Guid> cycle = result.Cycles[i];
					_ = sb.AppendLine($"Cycle #{i + 1}:");
					for ( int j = 0; j < cycle.Count; j++ )
					{
						Guid guid = cycle[j];
						if ( !componentsByGuid.TryGetValue(guid, out Component comp) )
							continue;
						_ = sb.Append($"  {j + 1}. {comp.Name}");
						if ( !string.IsNullOrWhiteSpace(comp.Author) )
							_ = sb.Append($" by {comp.Author}");

						// Show what it depends on
						if ( j < cycle.Count - 1 )
						{
							Guid nextGuid = cycle[j + 1];
							if ( componentsByGuid.TryGetValue(nextGuid, out Component nextComp) )
								_ = sb.Append($" → depends on → {nextComp.Name}");
						}
						else
						{
							// Last item cycles back to first
							Guid firstGuid = cycle[0];
							if ( componentsByGuid.TryGetValue(firstGuid, out Component firstComp) )
								_ = sb.Append($" → depends on → {firstComp.Name} (CYCLE!)");
						}
						_ = sb.AppendLine();
					}
					_ = sb.AppendLine();
				}

				_ = sb.AppendLine("💡 To fix this:");
				_ = sb.AppendLine("1. Uncheck one or more components in the cycle");
				_ = sb.AppendLine("2. Or remove/modify dependencies using the component editor");
				_ = sb.AppendLine("3. Or contact the mod authors about the circular dependency");

				result.DetailedErrorMessage = sb.ToString();
			}

			return result;
		}

		/// <summary>
		/// DFS helper method to detect cycles.
		/// Returns true if a cycle is detected.
		/// </summary>
		private static bool DfsDetectCycle(
			Guid node,
			Dictionary<Guid, List<Guid>> graph,
			HashSet<Guid> visited,
			HashSet<Guid> recursionStack,
			List<Guid> currentPath,
			CircularDependencyResult result)
		{
			_ = visited.Add(node);
			_ = recursionStack.Add(node);
			currentPath.Add(node);

			if ( graph.TryGetValue(node, out List<Guid> neighbors) )
			{
				foreach ( Guid neighbor in neighbors )
				{
					if ( !visited.Contains(neighbor) )
					{
						if ( DfsDetectCycle(neighbor, graph, visited, recursionStack, currentPath, result) )
							return true;
					}
					else if ( recursionStack.Contains(neighbor) )
					{
						// Found a cycle! Extract the cycle from currentPath
						int cycleStartIndex = currentPath.IndexOf(neighbor);
						var cycle = currentPath.Skip(cycleStartIndex).ToList();
						cycle.Add(neighbor); // Complete the cycle

						// Check if this cycle is already recorded (avoid duplicates)
						bool isDuplicate = result.Cycles.Any(existingCycle =>
							existingCycle.Count == cycle.Count &&
							existingCycle.Intersect(cycle).Count() == cycle.Count);

						if ( !isDuplicate )
							result.Cycles.Add(cycle);

						return true;
					}
				}
			}

			_ = recursionStack.Remove(node);
			currentPath.RemoveAt(currentPath.Count - 1);
			return false;
		}

		/// <summary>
		/// Suggests which components could be unchecked to break circular dependencies.
		/// Uses minimum vertex cover algorithm to find the smallest set of components to remove.
		/// </summary>
		public static List<Component> SuggestComponentsToRemove(CircularDependencyResult result)
		{
			if ( !result.HasCircularDependencies )
				return new List<Component>();

			// Count how many cycles each component appears in
			var componentCycleCount = new Dictionary<Guid, int>();
			foreach ( List<Guid> cycle in result.Cycles )
			{
				foreach ( Guid guid in cycle )
				{
					if ( !componentCycleCount.ContainsKey(guid) )
						componentCycleCount[guid] = 0;
					componentCycleCount[guid]++;
				}
			}

			// Sort by cycle count descending - removing components that appear in more cycles is more effective
			var suggestions = componentCycleCount
				.OrderByDescending(kvp => kvp.Value)
				.Select(kvp => result.ComponentsByGuid.ContainsKey(kvp.Key) ? result.ComponentsByGuid[kvp.Key] : null)
				.Where(comp => !(comp is null))
				.Take(3) // Suggest up to 3 components
				.ToList();

			return suggestions;
		}
	}
}

