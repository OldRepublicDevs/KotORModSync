// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:50-635
// Original: public class MainPass extends PrunedDepthFirstAdapter
using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.ScriptNode;
using KOTORModSync.Formats.NCS.NCSDecomp.Scriptutils;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using UtilsType = KOTORModSync.Formats.NCS.NCSDecomp.Utils.Type;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public class MainPass : PrunedDepthFirstAdapter
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:51-64
        // Original: /** Live variable stack reflecting current execution point. */ protected LocalVarStack stack = new LocalVarStack(); protected NodeAnalysisData nodedata; protected SubroutineAnalysisData subdata; protected boolean skipdeadcode; /** Mutable script output for the current subroutine. */ protected SubScriptState state; private ActionsData actions; /** Whether we are operating on the globals block. */ protected boolean globals; /** Backup stack used around jumps to restore state. */ protected LocalVarStack backupstack; /** Declared return type of the current subroutine. */ protected Type type;
        /** Live variable stack reflecting current execution point. */
        protected LocalVarStack stack = new LocalVarStack();
        protected NodeAnalysisData nodedata;
        protected SubroutineAnalysisData subdata;
        protected bool skipdeadcode;
        /** Mutable script output for the current subroutine. */
        protected SubScriptState state;
        private ActionsData actions;
        /** Whether we are operating on the globals block. */
        protected bool globals;
        /** Backup stack used around jumps to restore state. */
        protected LocalVarStack backupstack;
        /** Declared return type of the current subroutine. */
        protected Utils.Type type;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:66-76
        // Original: public MainPass(SubroutineState state, NodeAnalysisData nodedata, SubroutineAnalysisData subdata, ActionsData actions)
        public MainPass(SubroutineState state, NodeAnalysisData nodedata, SubroutineAnalysisData subdata, ActionsData actions)
        {
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.actions = actions;
            state.InitStack(this.stack);
            this.skipdeadcode = false;
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:72
            // Original: this.state = new SubScriptState(nodedata, subdata, this.stack, state, actions, FileDecompiler.preferSwitches);
            this.state = new SubScriptState(nodedata, subdata, this.stack, state, actions, FileDecompiler.preferSwitches);
            this.globals = false;
            this.backupstack = null;
            this.type = state.Type();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:78-86
        // Original: protected MainPass(NodeAnalysisData nodedata, SubroutineAnalysisData subdata)
        protected MainPass(NodeAnalysisData nodedata, SubroutineAnalysisData subdata)
        {
            this.nodedata = nodedata;
            this.subdata = subdata;
            this.skipdeadcode = false;
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:80
            // Original: this.state = new SubScriptState(nodedata, subdata, this.stack, FileDecompiler.preferSwitches);
            this.state = new SubScriptState(nodedata, subdata, this.stack, FileDecompiler.preferSwitches);
            this.globals = true;
            this.backupstack = null;
            // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:85
            // Original: this.type = new Type((byte)-1);
            this.type = new UtilsType(unchecked((byte)-1));
        }

        public virtual void Done()
        {
            this.stack = null;
            this.nodedata = null;
            this.subdata = null;
            if (this.state != null)
            {
                this.state.ParseDone();
            }

            this.state = null;
            this.actions = null;
            this.backupstack = null;
            this.type = null;
        }

        public virtual void AssertStack()
        {
            if ((this.type.Equals((byte)0) || this.type.Equals((byte)255)) && this.stack.Size() > 0)
            {
                throw new Exception("Error: Final stack size " + this.stack.Size() + this.stack.ToString());
            }
        }

        public virtual string GetCode()
        {
            return this.state.ToString();
        }

        public virtual string GetProto()
        {
            return this.state.GetProto();
        }

        public virtual ASub GetScriptRoot()
        {
            return this.state.GetRoot();
        }

        public virtual SubScriptState GetState()
        {
            return this.state;
        }

        public override void OutARsaddCommand(ARsaddCommand node)
        {
            int pos = this.nodedata.TryGetPos(node);
            if (!this.skipdeadcode)
            {
                // Extract type from ARsaddCommand's GetType() which returns TIntegerConstant
                int typeVal = 0;
                if (node.GetType() != null && node.GetType().GetText() != null)
                {
                    if (int.TryParse(node.GetType().GetText(), out int parsedType))
                    {
                        typeVal = parsedType;
                    }
                }
                else
                {
                    // Fallback to NodeUtils.GetType for compatibility
                    typeVal = NodeUtils.GetType(node).ByteValue();
                }
                Variable var = new Variable(new UtilsType((byte)typeVal));
                this.stack.Push(var);
                var = null;
                this.state.TransformRSAdd(node);
            }
            else
            {
                Debug($"DEBUG MainPass: OutARsaddCommand at pos {pos} marked as dead code, skipping");
                this.state.TransformDeadCode(node);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:138-149
        // Original: @Override public void outACopyDownSpCommand(ACopyDownSpCommand node) { if (!this.skipdeadcode) { this.withRecovery(node, () -> { int copy = NodeUtils.stackSizeToPos(node.getSize()); int loc = NodeUtils.stackOffsetToPos(node.getOffset()); if (copy > 1) { this.stack.structify(loc - copy + 1, copy, this.subdata); } this.state.transformCopyDownSp(node); }); } else { this.state.transformDeadCode(node); } }
        public override void OutACopyDownSpCommand(ACopyDownSpCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.WithRecovery(node, () =>
                {
                    int copy = NodeUtils.StackSizeToPos(node.GetSize());
                    int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                    if (copy > 1)
                    {
                        this.stack.Structify(loc - copy + 1, copy, this.subdata);
                    }

                    this.state.TransformCopyDownSp(node);
                });
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:155-178
        // Original: @Override public void outACopyTopSpCommand(ACopyTopSpCommand node) { if (!this.skipdeadcode) { this.withRecovery(node, () -> { ... }); } else { this.state.transformDeadCode(node); } }
        public override void OutACopyTopSpCommand(ACopyTopSpCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.WithRecovery(node, () =>
                {
                    VarStruct varstruct = null;
                    int copy = NodeUtils.StackSizeToPos(node.GetSize());
                    int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                    if (copy > 1)
                    {
                        varstruct = this.stack.Structify(loc - copy + 1, copy, this.subdata);
                    }

                    this.state.TransformCopyTopSp(node);
                    if (copy > 1)
                    {
                        this.stack.Push(varstruct);
                    }
                    else
                    {
                        for (int i = 0; i < copy; i++)
                        {
                            StackEntry entry = this.stack.Get(loc);
                            this.stack.Push(entry);
                        }
                    }
                });
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:181-208
        // Original: @Override public void outAConstCommand(AConstCommand node) { if (!this.skipdeadcode) { this.withRecovery(node, () -> { ... }); } else { this.state.transformDeadCode(node); } }
        public override void OutAConstCommand(AConstCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.WithRecovery(node, () =>
                {
                    UtilsType type = NodeUtils.GetType(node);
                    Const aconst;
                    switch (type.ByteValue())
                    {
                        case 3:
                            aconst = Const.NewConst(type, NodeUtils.GetIntConstValue(node));
                            break;
                        case 4:
                            aconst = Const.NewConst(type, NodeUtils.GetFloatConstValue(node));
                            break;
                        case 5:
                            aconst = Const.NewConst(type, NodeUtils.GetStringConstValue(node));
                            break;
                        case 6:
                            aconst = Const.NewConst(type, NodeUtils.GetObjectConstValue(node));
                            break;
                        default:
                            throw new Exception("Invalid const type " + type);
                    }
                    this.stack.Push(aconst);
                    // Debug output - safely get value based on type
                    string debugValue = "?";
                    try
                    {
                        switch (type.ByteValue())
                        {
                            case 3:
                                debugValue = NodeUtils.GetIntConstValue(node).ToString();
                                break;
                            case 4:
                                debugValue = NodeUtils.GetFloatConstValue(node).ToString();
                                break;
                            case 5:
                                debugValue = "\"" + NodeUtils.GetStringConstValue(node) + "\"";
                                break;
                            case 6:
                                debugValue = NodeUtils.GetObjectConstValue(node).ToString();
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        debugValue = "error";
                    }
                    Debug($"DEBUG MainPass.OutAConstCommand: type={type.ByteValue()}, value={debugValue}, stack size={this.stack.Size()}");
                    this.state.TransformConst(node);
                });
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:211-248
        // Original: @Override public void outAActionCommand(AActionCommand node) { if (!this.skipdeadcode) { this.withRecovery(node, () -> { ... }); } else { this.state.transformDeadCode(node); } }
        public override void OutAActionCommand(AActionCommand node)
        {
            try
            {
                Console.Error.WriteLine("DEBUG MainPass.OutAActionCommand: ENTERED, actionId=" + NodeUtils.GetActionId(node) + ", skipdeadcode=" + this.skipdeadcode);
                Debug("DEBUG MainPass.OutAActionCommand: ENTERED, actionId=" + NodeUtils.GetActionId(node) + ", skipdeadcode=" + this.skipdeadcode);
                if (!this.skipdeadcode)
                {
                    this.WithRecovery(node, () =>
                    {
                    int remove = NodeUtils.ActionRemoveElementCount(node, this.actions);
                    int stackSize = this.stack.Size();

                    // Safety check: don't remove more than we have
                    if (remove > stackSize)
                    {
                        Debug("[MainPass] WARNING: ACTION trying to remove " + remove + " but stack only has " + stackSize + " elements. Action: " + (this.actions != null ? this.actions.GetName(NodeUtils.GetActionId(node)) : "unknown"));
                        remove = stackSize; // Remove what we can
                    }

                    int i = 0;

                    while (i < remove)
                    {
                        StackEntry entry = this.RemoveFromStack();
                        i += entry.Size();
                    }

                    UtilsType type;
                    try
                    {
                        type = NodeUtils.GetReturnType(node, this.actions);
                    }
                    catch (Exception)
                    {
                        // Action metadata missing or invalid - assume void return
                        type = new UtilsType((byte)0);
                    }
                    if (!type.Equals(unchecked((byte)(-16))))
                    {
                        if (!type.Equals((byte)0))
                        {
                            Variable var = new Variable(type);
                            this.stack.Push(var);
                        }
                    }
                    else
                    {
                        for (int ix = 0; ix < 3; ix++)
                        {
                            Variable var = new Variable((byte)4);
                            this.stack.Push(var);
                        }

                        this.stack.Structify(1, 3, this.subdata);
                    }

                    Console.Error.WriteLine("DEBUG MainPass.OutAActionCommand: about to call TransformAction, actionId=" + NodeUtils.GetActionId(node));
                    Debug("DEBUG MainPass.OutAActionCommand: about to call TransformAction, actionId=" + NodeUtils.GetActionId(node));
                    this.state.TransformAction(node);
                        Console.Error.WriteLine("DEBUG MainPass.OutAActionCommand: TransformAction completed");
                        Debug("DEBUG MainPass.OutAActionCommand: TransformAction completed");
                    });
                }
                else
                {
                    this.state.TransformDeadCode(node);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("DEBUG MainPass.OutAActionCommand: EXCEPTION: " + ex.Message + ", StackTrace: " + ex.StackTrace);
                Debug("DEBUG MainPass.OutAActionCommand: EXCEPTION: " + ex.Message);
                throw;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:251-263
        // Original: @Override public void outALogiiCommand(ALogiiCommand node) { if (!this.skipdeadcode) { this.withRecovery(node, () -> { ... }); } else { this.state.transformDeadCode(node); } }
        public override void OutALogiiCommand(ALogiiCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.WithRecovery(node, () =>
                {
                    this.RemoveFromStack();
                    this.RemoveFromStack();
                    Variable var = new Variable((byte)3);
                    this.stack.Push(var);
                    this.state.TransformLogii(node);
                });
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:266-309
        // Original: @Override public void outABinaryCommand(ABinaryCommand node) { if (!this.skipdeadcode) { this.withRecovery(node, () -> { ... }); } else { this.state.transformDeadCode(node); } }
        public override void OutABinaryCommand(ABinaryCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.WithRecovery(node, () =>
                {
                    int sizep1;
                    int sizep2;
                    int sizeresult;
                    UtilsType resulttype;
                    if (NodeUtils.IsEqualityOp(node))
                    {
                        if (NodeUtils.GetType(node).Equals((byte)36))
                        {
                            sizep1 = sizep2 = NodeUtils.StackSizeToPos(node.GetSize());
                        }
                        else
                        {
                            sizep2 = 1;
                            sizep1 = 1;
                        }

                        sizeresult = 1;
                        resulttype = new UtilsType((byte)3);
                    }
                    else if (NodeUtils.IsVectorAllowedOp(node))
                    {
                        sizep1 = NodeUtils.GetParam1Size(node);
                        sizep2 = NodeUtils.GetParam2Size(node);
                        sizeresult = NodeUtils.GetResultSize(node);
                        resulttype = NodeUtils.GetReturnType(node);
                    }
                    else
                    {
                        sizep1 = 1;
                        sizep2 = 1;
                        sizeresult = 1;
                        resulttype = new UtilsType((byte)3);
                    }

                    for (int i = 0; i < sizep1 + sizep2; i++)
                    {
                        this.RemoveFromStack();
                    }

                    for (int i = 0; i < sizeresult; i++)
                    {
                        Variable var = new Variable(resulttype);
                        this.stack.Push(var);
                    }

                    this.state.TransformBinary(node);
                });
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:312-318
        // Original: @Override public void outAUnaryCommand(AUnaryCommand node) { if (!this.skipdeadcode) { this.withRecovery(node, () -> this.state.transformUnary(node)); } else { this.state.transformDeadCode(node); } }
        public override void OutAUnaryCommand(AUnaryCommand node)
        {
            string opName = "?";
            try
            {
                var unaryOp = node.GetUnaryOp();
                if (unaryOp != null)
                {
                    opName = unaryOp.GetType().Name;
                }
            }
            catch { }
            Debug($"DEBUG MainPass.OutAUnaryCommand: op={opName}, skipdeadcode={this.skipdeadcode}");

            if (!this.skipdeadcode)
            {
                this.WithRecovery(node, () => this.state.TransformUnary(node));
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:321-345
        // Original: @Override public void outAMoveSpCommand(AMoveSpCommand node) { if (!this.skipdeadcode) { this.withRecovery(node, () -> { ... }); } else { this.state.transformDeadCode(node); } }
        public override void OutAMoveSpCommand(AMoveSpCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.WithRecovery(node, () =>
                {
                    this.state.TransformMoveSp(node);
                    this.backupstack = (LocalVarStack)this.stack.Clone();
                    int remove = NodeUtils.StackOffsetToPos(node.GetOffset());
                    List<object> entries = new List<object>();
                    int i = 0;

                    while (i < remove)
                    {
                        StackEntry entry = this.RemoveFromStack();
                        i += entry.Size();
                        if (typeof(Variable).IsInstanceOfType(entry) && !((Variable)entry).IsPlaceholder(this.stack) && !((Variable)entry).IsOnStack(this.stack))
                        {
                            entries.Add(entry);
                        }
                    }

                    if (entries.Count > 0 && !this.nodedata.DeadCode(node))
                    {
                        this.state.TransformMoveSPVariablesRemoved(entries, node);
                    }
                });
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            if (!this.skipdeadcode)
            {
                if (this.nodedata.LogOrCode(node))
                {
                    this.state.TransformLogOrExtraJump(node);
                }
                else
                {
                    this.state.TransformConditionalJump(node);
                }

                this.RemoveFromStack();
                if (!this.nodedata.LogOrCode(node))
                {
                    this.StoreStackState(this.nodedata.GetDestination(node), this.nodedata.DeadCode(node));
                }
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAJumpCommand(AJumpCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.state.TransformJump(node);
                this.StoreStackState(this.nodedata.GetDestination(node), this.nodedata.DeadCode(node));
                if (this.backupstack != null)
                {
                    this.stack.DoneWithStack();
                    this.stack = this.backupstack;
                    this.state.SetStack(this.stack);
                }
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            if (!this.skipdeadcode)
            {
                SubroutineState substate = this.subdata.GetState(this.nodedata.GetDestination(node));
                for (int paramsize = substate.GetParamCount(), i = 0; i < paramsize; ++i)
                {
                    this.RemoveFromStack();
                }

                this.state.TransformJSR(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutADestructCommand(ADestructCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.state.TransformDestruct(node);
                int removesize = NodeUtils.StackSizeToPos(node.GetSizeRem());
                int savestart = NodeUtils.StackSizeToPos(node.GetOffset());
                int savesize = NodeUtils.StackSizeToPos(node.GetSizeSave());
                this.stack.Destruct(removesize, savestart, savesize, this.subdata);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutACopyTopBpCommand(ACopyTopBpCommand node)
        {
            if (!this.skipdeadcode)
            {
                VarStruct varstruct = null;
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                if (copy > 1)
                {
                    varstruct = this.subdata.GetGlobalStack().Structify(loc - copy + 1, copy, this.subdata);
                }

                this.state.TransformCopyTopBp(node);
                if (copy > 1)
                {
                    this.stack.Push(varstruct);
                }
                else
                {
                    for (int i = 0; i < copy; ++i)
                    {
                        Variable varItem = (Variable)this.subdata.GetGlobalStack().Get(loc);
                        this.stack.Push(varItem);
                        --loc;
                    }
                }

                //Variable var = null;
                varstruct = null;
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutACopyDownBpCommand(ACopyDownBpCommand node)
        {
            if (!this.skipdeadcode)
            {
                int copy = NodeUtils.StackSizeToPos(node.GetSize());
                int loc = NodeUtils.StackOffsetToPos(node.GetOffset());
                if (copy > 1)
                {
                    this.subdata.GetGlobalStack().Structify(loc - copy + 1, copy, this.subdata);
                }

                this.state.TransformCopyDownBp(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAStoreStateCommand(AStoreStateCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.state.TransformStoreState(node);
                this.backupstack = null;
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAStackCommand(AStackCommand node)
        {
            if (!this.skipdeadcode)
            {
                this.state.TransformStack(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutAReturn(AReturn node)
        {
            if (!this.skipdeadcode)
            {
                this.state.TransformReturn(node);
            }
            else
            {
                this.state.TransformDeadCode(node);
            }
        }

        public override void OutASubroutine(ASubroutine node)
        {
        }

        public override void OutAProgram(AProgram node)
        {
        }

        public override void DefaultIn(Node node)
        {
            this.RestoreStackState(node);
            this.CheckOrigins(node);
            if (NodeUtils.IsCommandNode(node))
            {
                bool shouldProcess = this.nodedata.TryProcessCode(node);
                this.skipdeadcode = !shouldProcess;
                if (!shouldProcess && node is AST.PCmd cmd)
                {
                    // Log first few skipped commands for debugging
                    int pos = this.nodedata.TryGetPos(node);
                    Debug($"DEBUG MainPass: Command at pos {pos} marked as dead code (TryProcessCode returned false), type={node.GetType().Name}");
                }
            }
        }

        private StackEntry RemoveFromStack()
        {
            StackEntry entry = this.stack.Remove();
            if (entry is Variable && ((Variable)entry).IsPlaceholder(this.stack))
            {
                this.state.TransformPlaceholderVariableRemoved((Variable)entry);
            }

            return entry;
        }

        private void StoreStackState(Node node, bool isdead)
        {
            if (NodeUtils.IsStoreStackNode(node))
            {
                this.nodedata.SetStack(node, (LocalStack)this.stack.Clone(), false);
            }
        }

        private void RestoreStackState(Node node)
        {
            LocalVarStack restore = (LocalVarStack)this.nodedata.GetStack(node);
            if (restore != null)
            {
                this.stack.DoneWithStack();
                this.stack = restore;
                this.state.SetStack(this.stack);
                if (this.backupstack != null)
                {
                    this.backupstack.DoneWithStack();
                }

                this.backupstack = null;
            }

            restore = null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/MainPass.java:540-554
        // Original: private void withRecovery(Node node, Runnable action) { LocalVarStack stackSnapshot = (LocalVarStack)this.stack.clone(); LocalVarStack backupSnapshot = this.backupstack != null ? (LocalVarStack)this.backupstack.clone() : null; try { action.run(); } catch (RuntimeException e) { System.err.println("Decompiler recovery triggered at position " + this.nodedata.getPos(node) + ": " + e.getMessage()); e.printStackTrace(); this.stack = stackSnapshot; this.state.setStack(this.stack); this.backupstack = backupSnapshot; this.state.emitError(node, this.nodedata.getPos(node)); } }
        private void WithRecovery(Node node, System.Action action)
        {
            LocalVarStack stackSnapshot = (LocalVarStack)this.stack.Clone();
            LocalVarStack backupSnapshot = this.backupstack != null ? (LocalVarStack)this.backupstack.Clone() : null;
            try
            {
                action();
            }
            catch (Exception e)
            {
                // Log the exception details for debugging while allowing decompiler to continue
                int nodePos = this.nodedata.TryGetPos(node);
                Error("Decompiler recovery triggered at position " + (nodePos >= 0 ? nodePos.ToString() : "unknown") + ": " + e.Message);
                e.PrintStackTrace(JavaSystem.@err);
                this.stack = stackSnapshot;
                this.state.SetStack(this.stack);
                this.backupstack = backupSnapshot;
                this.state.EmitError(node, nodePos >= 0 ? nodePos : 0);
            }
        }

        private void CheckOrigins(Node node)
        {
            Node origin;
            while ((origin = this.GetNextOrigin(node)) != null)
            {
                this.state.TransformOriginFound(node, origin);
            }

            origin = null;
        }

        private Node GetNextOrigin(Node node)
        {
            return this.nodedata.RemoveLastOrigin(node);
        }
    }
}




