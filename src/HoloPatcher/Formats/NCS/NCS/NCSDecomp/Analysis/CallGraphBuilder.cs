// Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:1-90
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Analysis
{
    // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:23-89
    // Original: public class CallGraphBuilder extends PrunedDepthFirstAdapter
    public class CallGraphBuilder : PrunedDepthFirstAdapter
    {
        private readonly NodeAnalysisData nodedata;
        private readonly SubroutineAnalysisData subdata;
        private readonly Dictionary<int, HashSet<int>> edges = new Dictionary<int, HashSet<int>>();
        private int current;

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:29-32
        // Original: public CallGraphBuilder(NodeAnalysisData nodedata, SubroutineAnalysisData subdata)
        public CallGraphBuilder(NodeAnalysisData nodedata, SubroutineAnalysisData subdata)
        {
            this.nodedata = nodedata;
            this.subdata = subdata;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:34-37
        // Original: public CallGraph build()
        public CallGraph Build()
        {
            IEnumerator<object> subs = this.subdata.GetSubroutines();
            while (subs.HasNext())
            {
                ((ASubroutine)subs.Next()).Apply(this);
            }
            return new CallGraph(this.edges);
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:39-43
        // Original: @Override public void inASubroutine(ASubroutine node)
        public override void InASubroutine(ASubroutine node)
        {
            this.current = this.nodedata.GetPos(node);
            if (!this.edges.ContainsKey(this.current))
            {
                this.edges[this.current] = new HashSet<int>();
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:45-52
        // Original: @Override public void outAJumpToSubroutine(AJumpToSubroutine node)
        public override void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            Node dest = this.nodedata.GetDestination(node);
            if (dest is ASubroutine)
            {
                int dstPos = this.nodedata.GetPos(dest);
                if (!this.edges.ContainsKey(this.current))
                {
                    this.edges[this.current] = new HashSet<int>();
                }
                this.edges[this.current].Add(dstPos);
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:54-88
        // Original: public static class CallGraph
        public class CallGraph
        {
            private readonly Dictionary<int, HashSet<int>> forward;

            // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:57-60
            // Original: CallGraph(Map<Integer, Set<Integer>> forward)
            internal CallGraph(Dictionary<int, HashSet<int>> forward)
            {
                this.forward = new Dictionary<int, HashSet<int>>();
                foreach (KeyValuePair<int, HashSet<int>> entry in forward)
                {
                    this.forward[entry.Key] = new HashSet<int>(entry.Value);
                }
            }

            // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:62-64
            // Original: public Map<Integer, Set<Integer>> edges()
            public Dictionary<int, HashSet<int>> Edges()
            {
                Dictionary<int, HashSet<int>> result = new Dictionary<int, HashSet<int>>();
                foreach (KeyValuePair<int, HashSet<int>> entry in this.forward)
                {
                    result[entry.Key] = new HashSet<int>(entry.Value);
                }
                return result;
            }

            // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:66-68
            // Original: public Set<Integer> successors(int node)
            public HashSet<int> Successors(int node)
            {
                if (this.forward.ContainsKey(node))
                {
                    return new HashSet<int>(this.forward[node]);
                }
                return new HashSet<int>();
            }

            // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:70-72
            // Original: public Set<Integer> nodes()
            public HashSet<int> Nodes()
            {
                return new HashSet<int>(this.forward.Keys);
            }

            // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:74-78
            // Original: public Set<Integer> reachableFrom(int start)
            public HashSet<int> ReachableFrom(int start)
            {
                HashSet<int> seen = new HashSet<int>();
                this.Dfs(start, seen);
                return seen;
            }

            // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallGraphBuilder.java:80-87
            // Original: private void dfs(int node, Set<Integer> seen)
            private void Dfs(int node, HashSet<int> seen)
            {
                if (!seen.Add(node))
                {
                    return;
                }
                if (this.forward.ContainsKey(node))
                {
                    foreach (int succ in this.forward[node])
                    {
                        this.Dfs(succ, seen);
                    }
                }
            }
        }
    }
}

