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
        public IReadOnlyList<ModComponent> OrderedComponents { get; set; } = new List<ModComponent>();
        public IReadOnlyList<DependencyError> Errors { get; set; } = new List<DependencyError>();
        public IReadOnlyList<DependencyWarning> Warnings { get; set; } = new List<DependencyWarning>();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
    public class DependencyError
    {
        public string ComponentName { get; set; }
        public Guid ComponentGuid { get; set; }
        public string ErrorType { get; set; }
        public string Message { get; set; }
        public IReadOnlyList<string> AffectedComponents { get; set; } = new List<string>();
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
            [NotNull] IReadOnlyList<ModComponent> components,
            bool ignoreErrors = false)
        {
            if (components is null)
            {
                throw new ArgumentNullException(nameof(components));
            }

            var result = new DependencyResolutionResult();
            var componentDict = components.ToDictionary(c => c.Guid, c => c);

            // Validate all dependencies exist
            IReadOnlyList<DependencyError> validationErrors = ValidateDependencies(components, componentDict);

            // Build dependency graph
            Dictionary<Guid, HashSet<Guid>> dependencyGraph = BuildDependencyGraph(components);

            // Detect circular dependencies
            IReadOnlyList<DependencyError> circularDependencies = DetectCircularDependencies(dependencyGraph, componentDict);

            // Set result.Errors ONCE to accumulate all errors found so far
            var allErrors = result.Errors
                            .Concat(validationErrors)
                            .Concat(circularDependencies)
                            .ToList();

            try
            {
                if (allErrors.Count > 0 && !ignoreErrors)
                {
                    result.Success = false;
                    result.Errors = allErrors;
                    return result;
                }

                IReadOnlyList<ModComponent> orderedComponents = PerformTopologicalSort(dependencyGraph, componentDict);
                result.OrderedComponents = orderedComponents;
                result.Success = true;
                result.Errors = allErrors;
            }
            catch (Exception ex)
            {
                LogDependencyResolutionException(
                    ex,
                    components,
                    componentDict,
                    dependencyGraph);

                // Add the exception as a DependencyError and set it ONCE
                allErrors.Add(new DependencyError
                {
                    ComponentName = "System",
                    ComponentGuid = Guid.Empty,
                    ErrorType = "TopologicalSortFailed",
                    Message = $"Failed to resolve component order: {ex.Message}",
                });
                result.Errors = allErrors;
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// Generates InstallBefore/InstallAfter relationships based on current component order.
        /// Component at index i will have InstallBefore for all components 0 to i-1,
        /// and InstallAfter for all components i+1 to count-1.
        /// </summary>
        public static void GenerateDependenciesFromOrder([NotNull] IReadOnlyList<ModComponent> components)
        {
            if (components is null)
            {
                throw new ArgumentNullException(nameof(components));
            }

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
        public static void ClearAllDependencies([NotNull] IReadOnlyList<ModComponent> components)
        {
            if (components is null)
            {
                throw new ArgumentNullException(nameof(components));
            }

            foreach (ModComponent component in components)
            {
                component.InstallBefore.Clear();
                component.InstallAfter.Clear();
            }
        }

        private static IReadOnlyList<DependencyError> ValidateDependencies(
            IReadOnlyList<ModComponent> components,
            Dictionary<Guid, ModComponent> componentDict)
        {
            var errors = new List<DependencyError>();

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
            IReadOnlyList<ModComponent> components
            )
        {
            var graph = new Dictionary<Guid, HashSet<Guid>>();

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

        private static IReadOnlyList<DependencyError> DetectCircularDependencies(
            Dictionary<Guid, HashSet<Guid>> graph,
            Dictionary<Guid, ModComponent> componentDict)
        {
            var errors = new List<DependencyError>();
            var visited = new HashSet<Guid>();
            var recursionStack = new HashSet<Guid>();

            foreach (Guid componentGuid in graph.Keys)
            {
                if (!visited.Contains(componentGuid))
                {
                    IReadOnlyList<Guid> cycle = DetectCycleDFS(componentGuid, graph, visited, recursionStack);
                    if (cycle != null)
                    {
                        var cycleNames = cycle.Select(guid =>
                            componentDict.TryGetValue(guid, out ModComponent comp) ? comp.Name : guid.ToString()).ToList();

                        errors.Add(new DependencyError
                        {
                            ComponentName = string.Join(" → ", cycleNames),
                            ComponentGuid = cycle[0],
                            ErrorType = "CircularDependency",
                            Message = $"Circular dependency detected: {string.Join(" → ", cycleNames)}",
                            AffectedComponents = cycle.Select(g => g.ToString()).ToList(),
                        });
                    }
                }
            }

            return errors;
        }

        private static IReadOnlyList<Guid> DetectCycleDFS(
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
                        IReadOnlyList<Guid> cycle = DetectCycleDFS(neighbor, graph, visited, recursionStack);
                        if (cycle != null)
                        {
                            return cycle;
                        }
                    }
                    else if (recursionStack.Contains(neighbor))
                    {
                        // Found a cycle - reconstruct it
                        var cycle = new List<Guid>();
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
            return Array.Empty<Guid>();
        }

        private static IReadOnlyList<ModComponent> PerformTopologicalSort(
            Dictionary<Guid, HashSet<Guid>> graph,
            Dictionary<Guid, ModComponent> componentDict)
        {
            var result = new List<ModComponent>();
            var visited = new HashSet<Guid>();
            var tempMark = new HashSet<Guid>();

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
            {
                throw new InvalidOperationException($"Circular dependency detected involving component {current}");
            }

            if (visited.Contains(current))
            {
                return;
            }

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

        private static void LogDependencyResolutionException(
            Exception exception,
            IReadOnlyList<ModComponent> components,
            Dictionary<Guid, ModComponent> componentDict,
            Dictionary<Guid, HashSet<Guid>> dependencyGraph)
        {
            Logger.LogError($"DependencyResolverService.ResolveDependencies encountered {exception.GetType().Name}: {exception.Message}");

            if (exception is ArgumentOutOfRangeException outOfRangeException)
            {
                int? attemptedIndex = ExtractIndexValue(outOfRangeException.ActualValue);
                int maxIndex = components.Count > 0 ? components.Count - 1 : -1;

                Logger.LogError($"ArgumentOutOfRangeException details: ParamName='{outOfRangeException.ParamName ?? "(null)"}', ActualValue='{outOfRangeException.ActualValue ?? "(null)"}', AttemptedIndex={attemptedIndex?.ToString() ?? "unknown"}, ValidRange=0..{maxIndex}");

                LogComponentSnapshot(components, "components");
            }
            else if (exception is IndexOutOfRangeException)
            {
                LogComponentSnapshot(components, "components");
            }

            LogDependencyGraphSnapshot(dependencyGraph, componentDict);

            Logger.LogError($"Full exception stack trace:{Environment.NewLine}{exception}");
        }

        private static void LogComponentSnapshot(IReadOnlyList<ModComponent> components, string label)
        {
            Logger.LogError($"{label} snapshot (Count={components.Count}):");
            for (int i = 0; i < components.Count; i++)
            {
                ModComponent component = components[i];
                if (component is null)
                {
                    Logger.LogError($"  [{i}] null");
                    continue;
                }

                Logger.LogError($"  [{i}] Name='{component.Name}', Guid={component.Guid}, Dependencies={component.Dependencies.Count}, InstallBefore={component.InstallBefore.Count}, InstallAfter={component.InstallAfter.Count}");
            }
        }

        private static void LogDependencyGraphSnapshot(
            Dictionary<Guid, HashSet<Guid>> dependencyGraph,
            Dictionary<Guid, ModComponent> componentDict)
        {
            Logger.LogError($"Dependency graph snapshot (Nodes={dependencyGraph.Count}):");

            foreach (KeyValuePair<Guid, HashSet<Guid>> kvp in dependencyGraph)
            {
                string componentName = componentDict.TryGetValue(kvp.Key, out ModComponent component)
                    ? component.Name
                    : "(missing component)";

                string dependencies = kvp.Value.Count == 0
                    ? "(no dependencies)"
                    : string.Join(", ", kvp.Value.Select(guid =>
                        componentDict.TryGetValue(guid, out ModComponent dependencyComponent)
                            ? $"{dependencyComponent.Name} ({guid})"
                            : guid.ToString()));

                Logger.LogError($"  Node Guid={kvp.Key}, Name='{componentName}' depends on -> {dependencies}");
            }
        }

        private static int? ExtractIndexValue(object actualValue)
        {
            if (actualValue is null)
            {
                return null;
            }

            try
            {
                return Convert.ToInt32(actualValue, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }
    }
}
