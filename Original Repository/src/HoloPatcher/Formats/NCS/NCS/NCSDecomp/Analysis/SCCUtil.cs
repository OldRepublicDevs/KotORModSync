// Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/SCCUtil.java:1-117
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Analysis
{
    // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/SCCUtil.java:19-117
    // Original: public final class SCCUtil
    public sealed class SCCUtil
    {
        private SCCUtil()
        {
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/SCCUtil.java:23-27
        // Original: public static List<Set<Integer>> compute(Map<Integer, Set<Integer>> graph)
        public static List<HashSet<int>> Compute(Dictionary<int, HashSet<int>> graph)
        {
            Tarjan tarjan = new Tarjan(graph);
            List<HashSet<int>> sccs = tarjan.Run();
            return TopologicalOrder(graph, sccs, tarjan.ComponentIndex);
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/SCCUtil.java:29-62
        // Original: private static List<Set<Integer>> topologicalOrder(...)
        private static List<HashSet<int>> TopologicalOrder(Dictionary<int, HashSet<int>> graph, List<HashSet<int>> sccs, Dictionary<int, int> compIndex)
        {
            Dictionary<int, HashSet<int>> condensed = new Dictionary<int, HashSet<int>>();
            int[] indegree = new int[sccs.Count];

            foreach (KeyValuePair<int, HashSet<int>> entry in graph)
            {
                int fromComp = compIndex[entry.Key];
                foreach (int succ in entry.Value)
                {
                    int toComp = compIndex[succ];
                    if (fromComp != toComp)
                    {
                        if (!condensed.ContainsKey(fromComp))
                        {
                            condensed[fromComp] = new HashSet<int>();
                        }
                        if (condensed[fromComp].Add(toComp))
                        {
                            indegree[toComp]++;
                        }
                    }
                }
            }

            Queue<int> queue = new Queue<int>();
            for (int i = 0; i < indegree.Length; i++)
            {
                if (indegree[i] == 0)
                {
                    queue.Enqueue(i);
                }
            }

            List<HashSet<int>> ordered = new List<HashSet<int>>();
            while (queue.Count > 0)
            {
                int comp = queue.Dequeue();
                ordered.Add(sccs[comp]);
                if (condensed.ContainsKey(comp))
                {
                    foreach (int succ in condensed[comp])
                    {
                        if (--indegree[succ] == 0)
                        {
                            queue.Enqueue(succ);
                        }
                    }
                }
            }

            return ordered;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/SCCUtil.java:64-115
        // Original: private static class Tarjan
        private class Tarjan
        {
            private readonly Dictionary<int, HashSet<int>> graph;
            private readonly Dictionary<int, int> index = new Dictionary<int, int>();
            private readonly Dictionary<int, int> lowlink = new Dictionary<int, int>();
            private readonly Stack<int> stack = new Stack<int>();
            private readonly HashSet<int> onStack = new HashSet<int>();
            private readonly List<HashSet<int>> components = new List<HashSet<int>>();
            private int idx = 0;
            private readonly Dictionary<int, int> componentIndex = new Dictionary<int, int>();

            public Dictionary<int, int> ComponentIndex
            {
                get { return componentIndex; }
            }

            public Tarjan(Dictionary<int, HashSet<int>> graph)
            {
                this.graph = graph;
            }

            // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/SCCUtil.java:78-85
            // Original: List<Set<Integer>> run()
            public List<HashSet<int>> Run()
            {
                foreach (int node in this.graph.Keys)
                {
                    if (!this.index.ContainsKey(node))
                    {
                        this.StrongConnect(node);
                    }
                }
                return this.components;
            }

            // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/SCCUtil.java:87-114
            // Original: private void strongConnect(int v)
            private void StrongConnect(int v)
            {
                this.index[v] = this.idx;
                this.lowlink[v] = this.idx;
                this.idx++;
                this.stack.Push(v);
                this.onStack.Add(v);

                if (this.graph.ContainsKey(v))
                {
                    foreach (int w in this.graph[v])
                    {
                        if (!this.index.ContainsKey(w))
                        {
                            this.StrongConnect(w);
                            this.lowlink[v] = Math.Min(this.lowlink[v], this.lowlink[w]);
                        }
                        else if (this.onStack.Contains(w))
                        {
                            this.lowlink[v] = Math.Min(this.lowlink[v], this.index[w]);
                        }
                    }
                }

                if (this.lowlink[v] == this.index[v])
                {
                    HashSet<int> component = new HashSet<int>();
                    int w;
                    do
                    {
                        w = this.stack.Pop();
                        this.onStack.Remove(w);
                        component.Add(w);
                        this.componentIndex[w] = this.components.Count;
                    } while (w != v);
                    this.components.Add(component);
                }
            }
        }
    }
}

