// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using KOTORModSync.Core.Utility;

namespace KOTORModSync.Core.Services
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
	public class DependencyResolutionResult
	{
		public bool Success { get; set; }
		public List<ModComponent> OrderedComponents { get; set; } = new List<ModComponent>();
		public List<DependencyError> Errors { get; set; } = new List<DependencyError>();
		public List<DependencyWarning> Warnings { get; set; } = new List<DependencyWarning>();
	}

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
	public class DependencyError
	{
		public string ComponentName { get; set; }
		public Guid ComponentGuid { get; set; }
		public string ErrorType { get; set; }
		public string Message { get; set; }
		public List<string> AffectedComponents { get; set; } = new List<string>();
	}

	public class DependencyWarning
	{
		public string ComponentName { get; set; }
		public Guid ComponentGuid { get; set; }
		public string WarningType { get; set; }
		public string Message { get; set; }
	}

	public static class DependencyResolverService
	{
		/// <summary>
		/// Resolves component dependencies and returns components in the correct installation order.
		/// Handles InstallBefore/InstallAfter relationships and detects circular dependencies.
		/// </summary>
		public static DependencyResolutionResult ResolveDependencies(
			[NotNull] List<ModComponent> components,
			bool ignoreErrors = false)
		{
			if (components is null)
				throw new ArgumentNullException(nameof(components));

			DependencyResolutionResult result = new DependencyResolutionResult();
			Dictionary<Guid, ModComponent> componentDict = components.ToDictionary(c => c.Guid, c => c);

			// Validate all dependencies exist
			List<DependencyError> validationErrors = ValidateDependencies(components, componentDict);
			result.Errors.AddRange(validationErrors);

			if (result.Errors.Count > 0 && !ignoreErrors)
			{
				result.Success = false;
				return result;
			}

			// Build dependency graph
			Dictionary<Guid, HashSet<Guid>> dependencyGraph = BuildDependencyGraph(components, componentDict);

			// Detect circular dependencies
			List<DependencyError> circularDependencies = DetectCircularDependencies(dependencyGraph, componentDict);
			result.Errors.AddRange(circularDependencies);

			if (result.Errors.Count > 0 && !ignoreErrors)
			{
				result.Success = false;
				return result;
			}

			// Perform topological sort
			try
			{
				List<ModComponent> orderedComponents = PerformTopologicalSort(dependencyGraph, componentDict);
				result.OrderedComponents = orderedComponents;
				result.Success = true;
			}
			catch (Exception ex)
			{
				result.Success = false;
				result.Errors.Add(new DependencyError
				{
					ComponentName = "System",
					ComponentGuid = Guid.Empty,
					ErrorType = "TopologicalSortFailed",
					Message = $"Failed to resolve component order: {ex.Message}",
				});
			}

			return result;
		}

		/// <summary>
		/// Generates InstallBefore/InstallAfter relationships based on current component order.
		/// Component at index i will have InstallBefore for all components 0 to i-1,
		/// and InstallAfter for all components i+1 to count-1.
		/// </summary>
		public static void GenerateDependenciesFromOrder([NotNull] List<ModComponent> components)
		{
			if (components is null)
				throw new ArgumentNullException(nameof(components));

			// Clear existing dependencies
			foreach (ModComponent component in components)
			{
				component.InstallBefore.Clear();
				component.InstallAfter.Clear();
			}

			// Generate new dependencies based on order
			for (int i = 0; i < components.Count; i++)
			{
				ModComponent currentComponent = components[i];

				// InstallBefore: all components that come before this one
				for (int j = 0; j < i; j++)
				{
					currentComponent.InstallBefore.Add(components[j].Guid);
				}

				// InstallAfter: all components that come after this one
				for (int j = i + 1; j < components.Count; j++)
				{
					currentComponent.InstallAfter.Add(components[j].Guid);
				}
			}
		}

		/// <summary>
		/// Removes all InstallBefore/InstallAfter dependencies from all components.
		/// </summary>
		public static void ClearAllDependencies([NotNull] List<ModComponent> components)
		{
			if (components is null)
				throw new ArgumentNullException(nameof(components));

			foreach (ModComponent component in components)
			{
				component.InstallBefore.Clear();
				component.InstallAfter.Clear();
			}
		}

		private static List<DependencyError> ValidateDependencies(
			List<ModComponent> components,
			Dictionary<Guid, ModComponent> componentDict)
		{
			List<DependencyError> errors = new List<DependencyError>();

			foreach (ModComponent component in components)
			{
				// Check InstallBefore dependencies
				foreach (Guid beforeGuid in component.InstallBefore)
				{
					if (!componentDict.ContainsKey(beforeGuid))
					{
						errors.Add(new DependencyError
						{
							ComponentName = component.Name,
							ComponentGuid = component.Guid,
							ErrorType = "MissingInstallBefore",
							Message = $"InstallBefore references non-existent component with GUID: {beforeGuid}",
							AffectedComponents = new List<string> { beforeGuid.ToString() },
						});
					}
				}

				// Check InstallAfter dependencies
				foreach (Guid afterGuid in component.InstallAfter)
				{
					if (!componentDict.ContainsKey(afterGuid))
					{
						errors.Add(new DependencyError
						{
							ComponentName = component.Name,
							ComponentGuid = component.Guid,
							ErrorType = "MissingInstallAfter",
							Message = $"InstallAfter references non-existent component with GUID: {afterGuid}",
							AffectedComponents = new List<string> { afterGuid.ToString() },
						});
					}
				}

				// Check for self-references
				if (component.InstallBefore.Contains(component.Guid))
				{
					errors.Add(new DependencyError
					{
						ComponentName = component.Name,
						ComponentGuid = component.Guid,
						ErrorType = "SelfReference",
						Message = "Component references itself in InstallBefore",
					});
				}

				if (component.InstallAfter.Contains(component.Guid))
				{
					errors.Add(new DependencyError
					{
						ComponentName = component.Name,
						ComponentGuid = component.Guid,
						ErrorType = "SelfReference",
						Message = "Component references itself in InstallAfter",
					});
				}
			}

			return errors;
		}

		private static Dictionary<Guid, HashSet<Guid>> BuildDependencyGraph(
			List<ModComponent> components
, Dictionary<Guid, ModComponent> componentDict)
		{
			Dictionary<Guid, HashSet<Guid>> graph = new Dictionary<Guid, HashSet<Guid>>();

			// Initialize graph with all components
			foreach (ModComponent component in components)
			{
				graph[component.Guid] = new HashSet<Guid>();
			}

			// Add edges based on InstallBefore/InstallAfter
			foreach (ModComponent component in components)
			{
				// InstallBefore: this component must be installed before these components
				// So these components depend on this component
				foreach (Guid beforeGuid in component.InstallBefore)
				{
					if (graph.ContainsKey(beforeGuid))
					{
						graph[beforeGuid].Add(component.Guid);
					}
				}

				// InstallAfter: this component must be installed after these components
				// So this component depends on these components
				foreach (Guid afterGuid in component.InstallAfter)
				{
					if (graph.ContainsKey(afterGuid))
					{
						graph[component.Guid].Add(afterGuid);
					}
				}
			}

			return graph;
		}

		private static List<DependencyError> DetectCircularDependencies(
			Dictionary<Guid, HashSet<Guid>> graph,
			Dictionary<Guid, ModComponent> componentDict)
		{
			List<DependencyError> errors = new List<DependencyError>();
			HashSet<Guid> visited = new HashSet<Guid>();
			HashSet<Guid> recursionStack = new HashSet<Guid>();

			foreach (Guid componentGuid in graph.Keys)
			{
				if (!visited.Contains(componentGuid))
				{
					List<Guid> cycle = DetectCycleDFS(componentGuid, graph, visited, recursionStack);
					if (cycle != null)
					{
						List<string> cycleNames = cycle.Select(guid =>
							componentDict.TryGetValue(guid, out ModComponent comp) ? comp.Name : guid.ToString()).ToList();

						errors.Add(new DependencyError
						{
							ComponentName = string.Join(" → ", cycleNames),
							ComponentGuid = cycle.First(),
							ErrorType = "CircularDependency",
							Message = $"Circular dependency detected: {string.Join(" → ", cycleNames)}",
							AffectedComponents = cycle.Select(g => g.ToString()).ToList(),
						});
					}
				}
			}

			return errors;
		}

		private static List<Guid> DetectCycleDFS(
			Guid current,
			Dictionary<Guid, HashSet<Guid>> graph,
			HashSet<Guid> visited,
			HashSet<Guid> recursionStack)
		{
			visited.Add(current);
			recursionStack.Add(current);

			if (graph.ContainsKey(current))
			{
				foreach (Guid neighbor in graph[current])
				{
					if (!visited.Contains(neighbor))
					{
						List<Guid> cycle = DetectCycleDFS(neighbor, graph, visited, recursionStack);
						if (cycle != null)
							return cycle;
					}
					else if (recursionStack.Contains(neighbor))
					{
						// Found a cycle - reconstruct it
						List<Guid> cycle = new List<Guid>();
						Guid temp = current;
						while (temp != neighbor)
						{
							cycle.Add(temp);
							// Find the component that leads to temp
							temp = graph.FirstOrDefault(kvp => kvp.Value.Contains(temp)).Key;
						}
						cycle.Add(neighbor);
						cycle.Reverse();
						return cycle;
					}
				}
			}

			recursionStack.Remove(current);
			return null;
		}

		private static List<ModComponent> PerformTopologicalSort(
			Dictionary<Guid, HashSet<Guid>> graph,
			Dictionary<Guid, ModComponent> componentDict)
		{
			List<ModComponent> result = new List<ModComponent>();
			HashSet<Guid> visited = new HashSet<Guid>();
			HashSet<Guid> tempMark = new HashSet<Guid>();

			foreach (Guid componentGuid in graph.Keys)
			{
				if (!visited.Contains(componentGuid))
				{
					TopologicalSortDFS(componentGuid, graph, componentDict, visited, tempMark, result);
				}
			}

			return result;
		}

		private static void TopologicalSortDFS(
			Guid current,
			Dictionary<Guid, HashSet<Guid>> graph,
			Dictionary<Guid, ModComponent> componentDict,
			HashSet<Guid> visited,
			HashSet<Guid> tempMark,
			List<ModComponent> result)
		{
			if (tempMark.Contains(current))
				throw new InvalidOperationException($"Circular dependency detected involving component {current}");

			if (visited.Contains(current))
				return;

			visited.Add(current);
			tempMark.Add(current);

			if (graph.ContainsKey(current))
			{
				foreach (Guid neighbor in graph[current])
				{
					TopologicalSortDFS(neighbor, graph, componentDict, visited, tempMark, result);
				}
			}

			tempMark.Remove(current);
			if (componentDict.TryGetValue(current, out ModComponent component))
			{
				result.Add(component);
			}
		}
	}
}