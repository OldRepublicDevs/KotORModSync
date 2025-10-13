// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace KOTORModSync.Core.Installation
{
	public sealed class InstallCoordinator
	{
		public InstallCoordinator()
		{
			SessionManager = new InstallSessionManager();
			BackupManager = new BackupManager();
		}

		public InstallSessionManager SessionManager { get; }
		public BackupManager BackupManager { get; }

		public async Task<ResumeResult> InitializeAsync([NotNull] IList<ModComponent> components, [NotNull] DirectoryInfo destinationPath, CancellationToken cancellationToken)
		{
			await SessionManager.InitializeAsync(components, destinationPath);
			await BackupManager.EnsureSnapshotAsync(destinationPath, cancellationToken);
			SessionManager.UpdateBackupPath(BackupManager.BackupPath);
			await SessionManager.SaveAsync();
			List<ModComponent> ordered = GetOrderedInstallList(components);
			return new ResumeResult(SessionManager.State.SessionId, ordered);
		}

		public static List<ModComponent> GetOrderedInstallList([NotNull][ItemNotNull] IList<ModComponent> components)
		{
			if ( components == null )
				throw new ArgumentNullException(nameof(components));

			var componentMap = components.ToDictionary(c => c.Guid);

			var adjacency = new Dictionary<Guid, List<Guid>>();
			var indegree = new Dictionary<Guid, int>();

			foreach ( ModComponent component in components )
			{
				adjacency[component.Guid] = new List<Guid>();
				indegree[component.Guid] = 0;
			}

			foreach ( ModComponent component in components )
			{
				foreach ( Guid dependency in component.Dependencies )
				{
					if ( !componentMap.ContainsKey(dependency) )
						continue;

					adjacency[dependency].Add(component.Guid);
					indegree[component.Guid]++;
				}

				foreach ( Guid installAfter in component.InstallAfter )
				{
					if ( !componentMap.ContainsKey(installAfter) )
						continue;

					adjacency[installAfter].Add(component.Guid);
					indegree[component.Guid]++;
				}

				foreach ( Guid installBefore in component.InstallBefore )
				{
					if ( !componentMap.ContainsKey(installBefore) )
						continue;

					aggAdjacency(component.Guid, installBefore);
				}
			}

			var queue = new Queue<Guid>(indegree.Where(kvp => kvp.Value == 0).Select(kvp => kvp.Key));
			var ordered = new List<ModComponent>();

			while ( queue.Count > 0 )
			{
				Guid current = queue.Dequeue();
				ordered.Add(componentMap[current]);

				foreach ( Guid dependent in adjacency[current] )
				{
					indegree[dependent]--;
					if ( indegree[dependent] == 0 )
						queue.Enqueue(dependent);
				}
			}

			if ( ordered.Count != components.Count )
			{
				foreach ( ModComponent component in components )
				{
					if ( !ordered.Contains(component) )
						ordered.Add(component);
				}
			}

			return ordered;

			void aggAdjacency(Guid from, Guid to)
			{
				adjacency[from].Add(to);
				indegree[to]++;
			}
		}

		public static void MarkBlockedDescendants([NotNull] IList<ModComponent> orderedComponents, Guid failedComponentId)
		{
			var visited = new HashSet<Guid>();
			var stack = new Stack<Guid>();
			stack.Push(failedComponentId);

			Dictionary<Guid, List<Guid>> dependentsMap = BuildDependentsMap(orderedComponents);

			while ( stack.Count > 0 )
			{
				Guid current = stack.Pop();
				if ( !dependentsMap.TryGetValue(current, out List<Guid> dependents) )
					continue;

				foreach ( Guid dependentId in dependents )
				{
					if ( visited.Add(dependentId) )
					{
						ModComponent dependent = orderedComponents.FirstOrDefault(c => c.Guid == dependentId);
						if ( dependent != null && dependent.InstallState == ModComponent.ComponentInstallState.Pending )
						{
							dependent.InstallState = ModComponent.ComponentInstallState.Blocked;
						}

						stack.Push(dependentId);
					}
				}
			}
		}

		private static Dictionary<Guid, List<Guid>> BuildDependentsMap(IList<ModComponent> components)
		{
			var map = new Dictionary<Guid, List<Guid>>();
			var componentMap = components.ToDictionary(c => c.Guid);

			foreach ( ModComponent component in components )
			{
				void addEdge(Guid from, Guid to)
				{
					if ( !componentMap.ContainsKey(to) )
						return;
					if ( !map.TryGetValue(from, out List<Guid> list) )
					{
						list = new List<Guid>();
						map[from] = list;
					}
					if ( !list.Contains(to) )
						list.Add(to);
				}

				foreach ( Guid dependency in component.Dependencies )
					addEdge(dependency, component.Guid);

				foreach ( Guid installAfter in component.InstallAfter )
					addEdge(installAfter, component.Guid);

				foreach ( Guid installBefore in component.InstallBefore )
					addEdge(component.Guid, installBefore);
			}

			return map;
		}

		public static void ClearSessionForTests(DirectoryInfo directoryInfo)
		{
			if ( directoryInfo == null )
				return;

			string sessionFolder = Path.Combine(directoryInfo.FullName, ".kotor_modsync");
			if ( !Directory.Exists(sessionFolder) )
				return;
			try
			{
				Directory.Delete(sessionFolder, recursive: true);
			}
			catch ( IOException )
			{

			}
			catch ( UnauthorizedAccessException )
			{

			}
		}

	}
}

