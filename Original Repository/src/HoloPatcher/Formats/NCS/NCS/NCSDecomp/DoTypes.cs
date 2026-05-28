// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:40-786
// Original: public class DoTypes extends PrunedDepthFirstAdapter
using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;

using UtilsType = KOTORModSync.Formats.NCS.NCSDecomp.Utils.Type;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public class DoTypes : PrunedDepthFirstAdapter
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:41-56
        // Original: private SubroutineState state; /** Type-only view of the execution stack for inference. */ protected LocalTypeStack stack = new LocalTypeStack(); private NodeAnalysisData nodedata; private SubroutineAnalysisData subdata; private ActionsData actions; /** Whether we are in the first prototyping pass. */ private boolean initialproto; /** True while temporarily skipping sections during prototyping. */ private boolean protoskipping; /** Whether we should emit return type information during this pass. */ private boolean protoreturn; /** Skip nodes flagged as dead code. */ private boolean skipdeadcode; /** Backup stack used around jumps for restoration. */ private LocalTypeStack backupstack;
        private SubroutineState state;
        /** Type-only view of the execution stack for inference. */
        protected LocalTypeStack stack = new LocalTypeStack();
        private NodeAnalysisData nodedata;
        private SubroutineAnalysisData subdata;
        private ActionsData actions;
        private bool initialproto;
        private bool protoskipping;
        private bool protoreturn;
        private bool skipdeadcode;
        private LocalTypeStack backupstack;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:58-71
        // Original: public DoTypes(SubroutineState state, NodeAnalysisData nodedata, SubroutineAnalysisData subdata, ActionsData actions, boolean initialprototyping)
        public DoTypes(SubroutineState state, NodeAnalysisData nodedata, SubroutineAnalysisData subdata, ActionsData actions, bool initialprototyping)
        {
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.state = state;
            this.actions = actions;
            if (!initialprototyping)
            {
                this.state.InitStack(this.stack);
            }

            this.initialproto = initialprototyping;
            this.protoskipping = false;
            this.skipdeadcode = false;
            this.protoreturn = (this.initialproto || !state.Type().IsTyped());
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:73-88
        // Original: public void done() { ... this.stack.close(); ... this.backupstack.close(); ... }
        public virtual void Done()
        {
            this.state = null;
            if (this.stack != null)
            {
                this.stack.Close();
                this.stack = null;
            }

            this.nodedata = null;
            this.subdata = null;
            if (this.backupstack != null)
            {
                this.backupstack.Close();
                this.backupstack = null;
            }

            this.actions = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:90-96
        // Original: public void assertStack()
        public virtual void AssertStack()
        {
            if (this.stack.Size() > 0)
            {
                Debug("Uh-oh... dumping main() state:");
                this.state.PrintState();
                throw new Exception("Error: Final stack size " + this.stack.Size());
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:99-105
        // Original: @Override public void outARsaddCommand(ARsaddCommand node)
        public override void OutARsaddCommand(ARsaddCommand node)
        {
            if (!this.protoskipping && !this.skipdeadcode)
            {
                UtilsType type = NodeUtils.GetType(node);
                this.stack.Push(type);
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:106-128
        // Original: @Override public void outACopyDownSpCommand(ACopyDownSpCommand node)
        public override void OutACopyDownSpCommand(ACopyDownSpCommand node)
        {
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                bool isstruct = copy > 1;
                if (this.protoreturn && loc > this.stack.Size())
                {
                    if (isstruct)
                    {
                        StructType @struct = new StructType();
                        for (int i = copy; i >= 1; --i)
                        {
                            @struct.AddType(this.stack.Get(i, this.state));
                        }

                        this.state.SetReturnType(@struct, loc - this.stack.Size());
                        this.subdata.AddStruct(@struct);
                    }
                    else
                    {
                        this.state.SetReturnType(this.stack.Get(1, this.state), loc - this.stack.Size());
                    }
                }


            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:129-140
        // Original: @Override public void outACopyTopSpCommand(ACopyTopSpCommand node)
        public override void OutACopyTopSpCommand(ACopyTopSpCommand node)
        {
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                for (int i = 0; i < copy; ++i)
                {
                    this.stack.Push(this.stack.Get(loc, this.state));
                }
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:141-147
        // Original: @Override public void outAConstCommand(AConstCommand node)
        public override void OutAConstCommand(AConstCommand node)
        {
            if (!this.protoskipping && !this.skipdeadcode)
            {
                UtilsType type = NodeUtils.GetType(node);
                this.stack.Push(type);
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:148-167
        // Original: @Override public void outAActionCommand(AActionCommand node)
        public override void OutAActionCommand(AActionCommand node)
        {
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int remove = NodeUtils.ActionRemoveElementCount(node, this.actions);
                UtilsType type = NodeUtils.GetReturnType(node, this.actions);
                int add = NodeUtils.StackSizeToPos(type.TypeSize());

                // Safety check: don't remove more than we have
                if (remove > this.stack.Size())
                {
                    Debug("[DoTypes] WARNING: ACTION trying to remove " + remove + " but stack only has " + this.stack.Size() + " elements. Action: " + (this.actions != null ? this.actions.GetName(NodeUtils.GetActionId(node)) : "unknown"));

                    // Remove what we can
                    this.stack.Remove(this.stack.Size());
                }
                else
                {
                    this.stack.Remove(remove);
                }

                for (int i = 0; i < add; ++i)
                {
                    this.stack.Push(type);
                }

            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:168-180
        // Original: @Override public void outALogiiCommand(ALogiiCommand node)
        public override void OutALogiiCommand(ALogiiCommand node)
        {
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();

                // Safety check
                if (this.stack.Size() < 2)
                {
                    Debug("[DoTypes] WARNING: LOGII trying to remove 2 but stack only has " + this.stack.Size());
                    this.stack.Remove(this.stack.Size());
                }
                else
                {
                    this.stack.Remove(2);
                }

                this.stack.Push(new UtilsType((byte)3));
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:182-301
        // Original: @Override public void outABinaryCommand(ABinaryCommand node)
        public override void OutABinaryCommand(ABinaryCommand node)
        {
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int sizep3;
                int sizep2;
                int sizeresult;
                UtilsType resulttype;
                string opType;
                if (NodeUtils.IsEqualityOp(node))
                {
                    if (NodeUtils.GetType(node).Equals((byte)36))
                    {
                        sizep2 = (sizep3 = NodeUtils.StackSizeToPos(node.GetSize()));
                        opType = "equality_struct";
                    }
                    else
                    {
                        sizep2 = (sizep3 = 1);
                        opType = "equality";
                    }

                    sizeresult = 1;
                    resulttype = new UtilsType((byte)3);
                }
                else if (NodeUtils.IsVectorAllowedOp(node))
                {
                    sizep3 = NodeUtils.GetParam1Size(node);
                    sizep2 = NodeUtils.GetParam2Size(node);
                    sizeresult = NodeUtils.GetResultSize(node);
                    resulttype = NodeUtils.GetReturnType(node);
                    opType = "vector_op";
                }
                else
                {
                    sizep3 = 1;
                    sizep2 = 1;
                    sizeresult = 1;
                    resulttype = new UtilsType((byte)3);
                    opType = "default";
                }

                int totalRemove = sizep3 + sizep2;

                // Safety check
                if (totalRemove > this.stack.Size())
                {
                    Debug("[DoTypes] WARNING: BINARY trying to remove " + totalRemove + " but stack only has " + this.stack.Size() + ". opType=" + opType);
                    this.stack.Remove(this.stack.Size());
                }
                else
                {
                    this.stack.Remove(totalRemove);
                }

                for (int i = 0; i < sizeresult; ++i)
                {
                    this.stack.Push(resulttype);
                }
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:303-312
        // Original: @Override public void outAUnaryCommand(AUnaryCommand node)
        public override void OutAUnaryCommand(AUnaryCommand node)
        {
            // Unary operations don't change stack size - they operate on top of stack in place
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:314-372
        // Original: @Override public void outAMoveSpCommand(AMoveSpCommand node)
        public override void OutAMoveSpCommand(AMoveSpCommand node)
        {
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int removeCount = NodeUtils.StackOffsetToPos(node.GetOffset());
                if (this.initialproto)
                {
                    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:216-225
                    // Original: int params = this.stack.removePrototyping(remove); if (params > 8) { params = 8; } // sanity cap
                    int @params = this.stack.RemovePrototyping(removeCount);
                    if (@params > 8)
                    {
                        @params = 8; // sanity cap to avoid runaway counts from locals
                    }
                    if (@params > 0)
                    {
                        int current = this.state.GetParamCount();
                        if (current == 0 || @params < current)
                        {
                            this.state.SetParamCount(@params);
                        }
                    }
                }
                else
                {

                    // Safety check
                    if (removeCount > this.stack.Size())
                    {
                        Debug("[DoTypes] WARNING: MOVSP trying to remove " + removeCount + " but stack only has " + this.stack.Size());
                        this.stack.Remove(this.stack.Size());
                    }
                    else
                    {
                        this.stack.Remove(removeCount);
                    }
                }
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:374-384
        // Original: @Override public void outAStoreStateCommand(AStoreStateCommand node)
        public override void OutAStoreStateCommand(AStoreStateCommand node)
        {
            // STORESTATE doesn't modify the stack - it saves state for later
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:386-404
        // Original: @Override public void outAStackCommand(AStackCommand node)
        public override void OutAStackCommand(AStackCommand node)
        {
            // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:406-444
        // Original: @Override public void outAConditionalJumpCommand(AConditionalJumpCommand node)
        public override void OutAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();

                // Safety check
                if (this.stack.Size() < 1)
                {
                    Debug("[DoTypes] WARNING: JZ/JNZ trying to remove 1 but stack is empty");
                }
                else
                {
                    this.stack.Remove(1);
                }
            }

            this.CheckProtoskippingStart(node);
            if (!this.protoskipping && !this.skipdeadcode && !this.IsLogOr(node))
            {
                this.StoreStackState(this.nodedata.GetDestination(node));
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:446-470
        // Original: @Override public void outAJumpCommand(AJumpCommand node)
        public override void OutAJumpCommand(AJumpCommand node)
        {
            // JMP doesn't modify stack

            this.CheckProtoskippingStart(node);
            if (!this.protoskipping && !this.skipdeadcode)
            {
                this.StoreStackState(this.nodedata.GetDestination(node));
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:472-548
        // Original: @Override public void outAJumpToSubroutine(AJumpToSubroutine node)
        public override void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                SubroutineState substate = this.subdata.GetState(this.nodedata.GetDestination(node));
                if (!substate.IsPrototyped())
                {
                    Debug("Uh-oh...");
                    substate.PrintState();
                    throw new Exception("Hit JSR on unprototyped subroutine " + this.nodedata.GetPos(this.nodedata.GetDestination(node)));
                }

                int paramsize = substate.GetParamCount();
                if (substate.IsTotallyPrototyped())
                {

                    // Safety check
                    if (paramsize > this.stack.Size())
                    {
                        Debug("[DoTypes] WARNING: JSR trying to remove " + paramsize + " params but stack only has " + this.stack.Size());
                        this.stack.Remove(this.stack.Size());
                    }
                    else
                    {
                        this.stack.Remove(paramsize);
                    }
                }
                else
                {
                    this.stack.RemoveParams(paramsize, substate);
                    if (substate.Type().Equals(unchecked((byte)(-1))))
                    {
                        if (this.stack.Size() > 0)
                        {
                            substate.SetReturnType(this.stack.Get(1, this.state), 0);
                        }
                    }

                    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:276-282
                    // Original: if (substate.type().equals((byte)-15) && !substate.type().isTyped()) { for (int i = 0; i < substate.type().size(); i++) { Type type = this.stack.get(substate.type().size() - i, this.state); if (!type.equals((byte)-1)) { ((StructType)substate.type()).updateType(i, type); } } }
                    if (substate.Type().Equals(unchecked((byte)(-15))) && !substate.Type().IsTyped())
                    {
                        StructType structType = (StructType)substate.Type();
                        var typesList = structType.Types();
                        for (int i = 0; i < typesList.Count; ++i)
                        {
                            UtilsType type = this.stack.Get(typesList.Count - i, this.state);
                            if (!type.Equals(unchecked((byte)(-1))))
                            {
                                structType.UpdateType(i, type);
                            }
                        }
                    }
                }
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:550-592
        // Original: @Override public void outADestructCommand(ADestructCommand node)
        public override void OutADestructCommand(ADestructCommand node)
        {
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int removesize = NodeUtils.StackSizeToPos(node.GetSizeRem());
                int savestart = NodeUtils.StackSizeToPos(node.GetOffset());
                int savesize = NodeUtils.StackSizeToPos(node.GetSizeSave());
                int firstRemove = removesize - (savesize + savestart);

                // Safety check
                if (firstRemove > this.stack.Size())
                {
                    Debug("[DoTypes] WARNING: DESTRUCT first remove " + firstRemove + " but stack only has " + this.stack.Size());
                    firstRemove = this.stack.Size();
                }

                this.stack.Remove(firstRemove);

                // Second removal
                if (this.stack.Size() >= savesize + 1)
                {
                    this.stack.Remove(savesize + 1, savestart);
                }
                else
                {
                    Debug("[DoTypes] WARNING: DESTRUCT second remove issue");
                }
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:594-626
        // Original: @Override public void outACopyTopBpCommand(ACopyTopBpCommand node)
        public override void OutACopyTopBpCommand(ACopyTopBpCommand node)
        {
            if (!this.protoskipping && !this.skipdeadcode)
            {
                int before = this.stack.Size();
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                for (int i = 0; i < copy; ++i)
                {
                    this.stack.Push(this.subdata.GetGlobalStack().GetType(loc));
                    --loc;
                }
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoTypes.java:628-650
        // Original: @Override public void outACopyDownBpCommand(ACopyDownBpCommand node)
        public override void OutACopyDownBpCommand(ACopyDownBpCommand node)
        {
            // CPDOWNBP copies from stack to global - doesn't change SP stack size
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        public override void OutASubroutine(ASubroutine node)
        {
            if (this.initialproto)
            {
                this.state.StopPrototyping(true);
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        public override void DefaultIn(Node node)
        {
            if (!this.protoskipping)
            {
                this.RestoreStackState(node);
            }
            else
            {
                this.CheckProtoskippingDone(node);
            }

            if (NodeUtils.IsCommandNode(node))
            {
                this.skipdeadcode = this.nodedata.DeadCode(node);
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        private void CheckProtoskippingDone(Node node)
        {
            if (this.state.GetSkipEnd(this.nodedata.GetPos(node)))
            {
                this.protoskipping = false;
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        private void CheckProtoskippingStart(Node node)
        {
            if (this.state.GetSkipStart(this.nodedata.GetPos(node)))
            {
                this.protoskipping = true;
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        private void StoreStackState(Node node)
        {
            if (NodeUtils.IsStoreStackNode(node))
            {
                this.nodedata.SetStack(node, (LocalStack)this.stack.Clone(), true);
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        private void RestoreStackState(Node node)
        {
            LocalTypeStack restore = (LocalTypeStack)this.nodedata.GetStack(node);
            if (restore != null)
            {
                this.stack = restore;
            }
        }

        // Debug mode - set to true for verbose stack tracing
        // CPDOWNSP doesn't change stack size, it copies TO a location
        // Safety check: don't remove more than we have
        // Remove what we can
        // Safety check
        // Safety check
        // Unary operations don't change stack size - they operate on top of stack in place
        // Safety check
        // STORESTATE doesn't modify the stack - it saves state for later
        // Handle SAVEBP/RESTOREBP - these don't affect SP stack
        // Safety check
        // Safety check
        // Safety check
        // Second removal
        // CPDOWNBP copies from stack to global - doesn't change SP stack size
        private bool IsLogOr(Node node)
        {
            return this.nodedata.LogOrCode(node);
        }
    }
}




