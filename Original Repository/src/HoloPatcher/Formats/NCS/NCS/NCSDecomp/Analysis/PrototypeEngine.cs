// Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/PrototypeEngine.java:1-156
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections.Generic;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Analysis
{
    // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/PrototypeEngine.java:26-156
    // Original: public class PrototypeEngine
    public class PrototypeEngine
    {
        private const int MAX_PASSES = 3;
        private readonly NodeAnalysisData nodedata;
        private readonly SubroutineAnalysisData subdata;
        private readonly ActionsData actions;
        private readonly bool strict;

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/PrototypeEngine.java:33-38
        // Original: public PrototypeEngine(NodeAnalysisData nodedata, SubroutineAnalysisData subdata, ActionsData actions, boolean strict)
        public PrototypeEngine(NodeAnalysisData nodedata, SubroutineAnalysisData subdata, ActionsData actions, bool strict)
        {
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.actions = actions;
            this.strict = strict;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/PrototypeEngine.java:40-61
        // Original: public void run()
        public void Run()
        {
            CallGraphBuilder.CallGraph graph = new CallGraphBuilder(this.nodedata, this.subdata).Build();
            Dictionary<int, ASubroutine> subByPos = this.IndexSubroutines();

            int mainPos = this.nodedata.TryGetPos(this.subdata.GetMainSub());
            if (mainPos < 0)
            {
                Debug("WARNING: Main subroutine has no position, skipping prototype engine");
                return;
            }
            HashSet<int> reachable = graph.ReachableFrom(mainPos);
            if (this.subdata.GetGlobalsSub() != null)
            {
                int globalsPos = this.nodedata.TryGetPos(this.subdata.GetGlobalsSub());
                if (globalsPos >= 0)
                {
                    HashSet<int> globalsReachable = graph.ReachableFrom(globalsPos);
                    foreach (int pos in globalsReachable)
                    {
                        reachable.Add(pos);
                    }
                }
            }

            List<HashSet<int>> sccs = SCCUtil.Compute(graph.Edges());
            foreach (HashSet<int> scc in sccs)
            {
                bool containsReachable = false;
                foreach (int pos in scc)
                {
                    if (reachable.Contains(pos))
                    {
                        containsReachable = true;
                        break;
                    }
                }
                if (!containsReachable)
                {
                    continue;
                }
                this.PrototypeComponent(scc, subByPos);
            }

            Dictionary<int, int> callsiteParams = new CallSiteAnalyzer(this.nodedata, this.subdata, this.actions).Analyze();
            this.EnsureAllPrototyped(subByPos.Values, callsiteParams);
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/PrototypeEngine.java:63-71
        // Original: private Map<Integer, ASubroutine> indexSubroutines()
        private Dictionary<int, ASubroutine> IndexSubroutines()
        {
            Dictionary<int, ASubroutine> map = new Dictionary<int, ASubroutine>();
            IEnumerator<object> it = this.subdata.GetSubroutines();
            while (it.HasNext())
            {
                ASubroutine sub = (ASubroutine)it.Next();
                int pos = this.nodedata.TryGetPos(sub);
                if (pos >= 0)
                {
                    map[pos] = sub;
                }
            }
            return map;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/PrototypeEngine.java:73-102
        // Original: private void prototypeComponent(Set<Integer> component, Map<Integer, ASubroutine> subByPos)
        private void PrototypeComponent(HashSet<int> component, Dictionary<int, ASubroutine> subByPos)
        {
            List<ASubroutine> subs = new List<ASubroutine>();
            foreach (int pos in component)
            {
                if (subByPos.ContainsKey(pos))
                {
                    subs.Add(subByPos[pos]);
                }
            }

            for (int pass = 0; pass < MAX_PASSES; pass++)
            {
                bool progress = false;
                foreach (ASubroutine sub in subs)
                {
                    SubroutineState state = this.subdata.GetState(sub);
                    if (state.IsPrototyped())
                    {
                        continue;
                    }

                    sub.Apply(new SubroutinePathFinder(state, this.nodedata, this.subdata, pass));
                    if (state.IsBeingPrototyped())
                    {
                        DoTypes dotypes = new DoTypes(state, this.nodedata, this.subdata, this.actions, true);
                        sub.Apply(dotypes);
                        dotypes.Done();
                        progress = progress || state.IsPrototyped();
                    }
                }
                if (!progress)
                {
                    break;
                }
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/PrototypeEngine.java:104-139
        // Original: private void ensureAllPrototyped(...)
        private void EnsureAllPrototyped(IEnumerable<ASubroutine> subs, Dictionary<int, int> callsiteParams)
        {
            foreach (ASubroutine sub in subs)
            {
                SubroutineState state = this.subdata.GetState(sub);
                if (!state.IsPrototyped())
                {
                    int pos = this.nodedata.TryGetPos(sub);
                    if (pos < 0)
                    {
                        Debug("WARNING: Subroutine has no position, skipping prototype");
                        continue;
                    }
                    if (this.strict)
                    {
                        Debug(
                            "Strict signatures: missing prototype for subroutine at " + pos.ToString() + " (continuing)"
                        );
                    }
                    int inferredParams = 0;
                    if (callsiteParams.ContainsKey(pos))
                    {
                        inferredParams = callsiteParams[pos];
                    }
                    int movespParams = this.EstimateParamsFromMovesp(sub);
                    // Prefer the smaller non-zero estimate to avoid over-counting locals;
                    // fall back to whichever is available when the other is zero.
                    if (inferredParams > 0 && movespParams > 0)
                    {
                        inferredParams = Math.Min(inferredParams, movespParams);
                    }
                    else if (inferredParams == 0 && movespParams > 0)
                    {
                        inferredParams = movespParams;
                    }
                    if (inferredParams < 0)
                    {
                        inferredParams = 0;
                    }
                    state.StartPrototyping();
                    state.SetParamCount(inferredParams);
                    // Default to void when return type is still unknown.
                    if (!state.Type().IsTyped())
                    {
                        state.SetReturnType(new Utils.Type((byte)0), 0);
                    }
                    state.EnsureParamPlaceholders();
                    state.StopPrototyping(true);
                }
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/PrototypeEngine.java:141-155
        // Original: private int estimateParamsFromMovesp(ASubroutine sub)
        private int EstimateParamsFromMovesp(ASubroutine sub)
        {
            MovespParamEstimator estimator = new MovespParamEstimator();
            sub.Apply(estimator);
            return estimator.MaxParams;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/PrototypeEngine.java:141-155
        // Helper class for estimating params from movesp
        private class MovespParamEstimator : PrunedDepthFirstAdapter
        {
            public int MaxParams { get; private set; }

            public MovespParamEstimator()
            {
                this.MaxParams = 0;
            }

            public override void OutAMoveSpCommand(AMoveSpCommand node)
            {
                int @params = NodeUtils.StackOffsetToPos(node.GetOffset());
                if (@params > this.MaxParams)
                {
                    this.MaxParams = @params;
                }
            }
        }
    }
}

