// Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:1-243
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Analysis
{
    // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:36-243
    // Original: public class CallSiteAnalyzer extends PrunedDepthFirstAdapter
    public class CallSiteAnalyzer : PrunedDepthFirstAdapter
    {
        private readonly NodeAnalysisData nodedata;
        private readonly SubroutineAnalysisData subdata;
        private readonly ActionsData actions;
        private readonly Dictionary<int, int> inferredParams = new Dictionary<int, int>();
        private bool skipdeadcode;
        private int height;
        private int growth;
        private SubroutineState state;

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:46-50
        // Original: public CallSiteAnalyzer(NodeAnalysisData nodedata, SubroutineAnalysisData subdata, ActionsData actions)
        public CallSiteAnalyzer(NodeAnalysisData nodedata, SubroutineAnalysisData subdata, ActionsData actions)
        {
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.actions = actions;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:52-65
        // Original: public Map<Integer, Integer> analyze()
        public Dictionary<int, int> Analyze()
        {
            IEnumerator<object> subs = this.subdata.GetSubroutines();

            while (subs.HasNext())
            {
                this.AnalyzeSubroutine((ASubroutine)subs.Next());
            }

            return this.inferredParams;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:67-73
        // Original: private void analyzeSubroutine(ASubroutine sub)
        private void AnalyzeSubroutine(ASubroutine sub)
        {
            this.state = this.subdata.GetState(sub);
            this.height = this.InitialHeight();
            this.growth = 0;
            this.skipdeadcode = false;
            sub.Apply(this);
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:75-86
        // Original: private int initialHeight()
        private int InitialHeight()
        {
            int initial = 0;
            if (this.state != null)
            {
                if (!this.state.Type().Equals((byte)0))
                {
                    initial++;
                }

                initial += this.state.GetParamCount();
            }

            return initial;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:88-93
        // Original: @Override public void defaultIn(Node node)
        public override void DefaultIn(Node node)
        {
            if (NodeUtils.IsCommandNode(node))
            {
                this.skipdeadcode = !this.nodedata.TryProcessCode(node);
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:95-100
        // Original: @Override public void outARsaddCommand(ARsaddCommand node)
        public override void OutARsaddCommand(ARsaddCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.Push(1);
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:102-107
        // Original: @Override public void outAConstCommand(AConstCommand node)
        public override void OutAConstCommand(AConstCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.Push(1);
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:109-114
        // Original: @Override public void outACopyTopSpCommand(ACopyTopSpCommand node)
        public override void OutACopyTopSpCommand(ACopyTopSpCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.Push(NodeUtils.StackSizeToPos(node.GetSize()));
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:116-121
        // Original: @Override public void outACopyTopBpCommand(ACopyTopBpCommand node)
        public override void OutACopyTopBpCommand(ACopyTopBpCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.Push(NodeUtils.StackSizeToPos(node.GetSize()));
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:123-137
        // Original: @Override public void outAActionCommand(AActionCommand node)
        public override void OutAActionCommand(AActionCommand node)
        {
            if (!this.skipdeadcode)
            {
                int remove = NodeUtils.ActionRemoveElementCount(node, this.actions);
                Utils.Type rettype = NodeUtils.GetReturnType(node, this.actions);
                int add;
                try
                {
                    add = NodeUtils.StackSizeToPos(rettype.TypeSize());
                }
                catch (Exception)
                {
                    add = 1;
                }
                this.Pop(remove);
                this.Push(add);
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:139-145
        // Original: @Override public void outALogiiCommand(ALogiiCommand node)
        public override void OutALogiiCommand(ALogiiCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.Pop(2);
                this.Push(1);
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:147-175
        // Original: @Override public void outABinaryCommand(ABinaryCommand node)
        public override void OutABinaryCommand(ABinaryCommand node)
        {
            if (!this.skipdeadcode)
            {
                int sizep1;
                int sizep2;
                int sizeresult;
                if (NodeUtils.IsEqualityOp(node))
                {
                    if (NodeUtils.GetType(node).Equals((byte)36))
                    {
                        sizep1 = sizep2 = NodeUtils.StackSizeToPos(node.GetSize());
                    }
                    else
                    {
                        sizep1 = 1;
                        sizep2 = 1;
                    }

                    sizeresult = 1;
                }
                else if (NodeUtils.IsVectorAllowedOp(node))
                {
                    sizep1 = NodeUtils.GetParam1Size(node);
                    sizep2 = NodeUtils.GetParam2Size(node);
                    sizeresult = NodeUtils.GetResultSize(node);
                }
                else
                {
                    sizep1 = 1;
                    sizep2 = 1;
                    sizeresult = 1;
                }

                this.Pop(sizep1 + sizep2);
                this.Push(sizeresult);
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:177-182
        // Original: @Override public void outAConditionalJumpCommand(AConditionalJumpCommand node)
        public override void OutAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.Pop(1);
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:184-189
        // Original: @Override public void outAJumpCommand(AJumpCommand node)
        public override void OutAJumpCommand(AJumpCommand node)
        {
            if (!this.skipdeadcode && NodeUtils.GetJumpDestinationPos(node) < this.nodedata.GetPos(node))
            {
                this.ResetGrowth();
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:191-204
        // Original: @Override public void outAJumpToSubroutine(AJumpToSubroutine node)
        public override void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            if (!this.skipdeadcode)
            {
                int dest = NodeUtils.GetJumpDestinationPos(node);
                int inferred = Math.Max(0, this.growth);
                if (inferred == 0)
                {
                    inferred = Math.Max(0, this.height);
                }

                if (this.inferredParams.ContainsKey(dest))
                {
                    this.inferredParams[dest] = Math.Max(this.inferredParams[dest], inferred);
                }
                else
                {
                    this.inferredParams[dest] = inferred;
                }
                this.Pop(inferred);
                this.ResetGrowth();
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:206-212
        // Original: @Override public void outAMoveSpCommand(AMoveSpCommand node)
        public override void OutAMoveSpCommand(AMoveSpCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.Pop(NodeUtils.StackOffsetToPos(node.GetOffset()));
                this.ResetGrowth();
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:214-220
        // Original: @Override public void outADestructCommand(ADestructCommand node)
        public override void OutADestructCommand(ADestructCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.Pop(NodeUtils.StackSizeToPos(node.GetSizeRem()));
                this.ResetGrowth();
            }
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:222-229
        // Original: private void push(int count)
        private void Push(int count)
        {
            if (count <= 0)
            {
                return;
            }

            this.height += count;
            this.growth += count;
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:231-238
        // Original: private void pop(int count)
        private void Pop(int count)
        {
            if (count <= 0)
            {
                return;
            }

            this.height = Math.Max(0, this.height - count);
            this.growth = Math.Max(0, this.growth - count);
        }

        // Matching NCSDecomp implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/CallSiteAnalyzer.java:240-242
        // Original: private void resetGrowth()
        private void ResetGrowth()
        {
            this.growth = 0;
        }
    }
}

