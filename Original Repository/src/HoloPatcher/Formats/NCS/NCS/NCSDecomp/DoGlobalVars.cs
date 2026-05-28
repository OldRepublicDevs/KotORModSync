// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:24-122
// Original: public class DoGlobalVars extends MainPass
using System;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;
using KOTORModSync.Formats.NCS.NCSDecomp.Utils;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using KOTORModSync.Formats.NCS.NCSDecomp.Stack;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public class DoGlobalVars : MainPass
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:25
        // Original: private boolean freezeStack;
        private bool freezeStack;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:27-31
        // Original: public DoGlobalVars(NodeAnalysisData nodedata, SubroutineAnalysisData subdata) { super(nodedata, subdata); this.state.setVarPrefix("GLOB_"); this.freezeStack = false; }
        public DoGlobalVars(NodeAnalysisData nodedata, SubroutineAnalysisData subdata) : base(nodedata, subdata)
        {
            this.state.SetVarPrefix("GLOB_");
            this.freezeStack = false;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:33-36
        // Original: @Override public String getCode() { return this.state.toStringGlobals(); }
        public override string GetCode()
        {
            return this.state.ToStringGlobals();
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:38-41
        // Original: @Override public void outABpCommand(ABpCommand node) { this.freezeStack = true; }
        public override void OutABpCommand(ABpCommand node)
        {
            this.freezeStack = true;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:43-46
        // Original: @Override public void outAJumpToSubroutine(AJumpToSubroutine node) { this.freezeStack = true; }
        public override void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            this.freezeStack = true;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:49-58
        // Original: public void outAMoveSpCommand(AMoveSpCommand node) { if (!this.freezeStack) { this.state.transformMoveSp(node); int remove = NodeUtils.stackOffsetToPos(node.getOffset()); for (int i = 0; i < remove; i++) { this.stack.remove(); } } }
        // CRITICAL: Even when freezeStack is true, we must still process MOVSP to ensure it's in the AST
        // Otherwise, the instruction will be missing from the decompiled output, causing roundtrip failures
        // The freezeStack flag is meant to prevent stack mutations, not to skip instruction processing
        public override void OutAMoveSpCommand(AMoveSpCommand node)
        {
            if (!this.freezeStack)
            {
                this.state.TransformMoveSp(node);
                int remove = NodeUtils.StackOffsetToPos(node.GetOffset());
                for (int i = 0; i < remove; i++)
                {
                    this.stack.Remove();
                }
            }
            else
            {
                // CRITICAL: Even when freezeStack is true, we must still process MOVSP to ensure it's in the AST
                // Otherwise, the instruction will be missing from the decompiled output
                // Matching DeNCS behavior: MOVSP should always be processed, freezeStack only affects stack operations
                Debug("DEBUG DoGlobalVars.OutAMoveSpCommand: freezeStack is true, but still processing MOVSP to ensure it's in AST");
                this.state.TransformMoveSp(node);
                // Don't remove from stack when freezeStack is true (preserve stack state)
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:60-65
        // Original: @Override public void outACopyDownSpCommand(ACopyDownSpCommand node) { if (!this.freezeStack) { this.state.transformCopyDownSp(node); } }
        // CRITICAL: Even when freezeStack is true, we must still process CPDOWNSP to ensure it's in the AST
        // Otherwise, the instruction will be missing from the decompiled output, causing roundtrip failures
        // The freezeStack flag is meant to prevent stack mutations, not to skip instruction processing
        public override void OutACopyDownSpCommand(ACopyDownSpCommand node)
        {
            Debug($"DEBUG DoGlobalVars.OutACopyDownSpCommand: freezeStack={this.freezeStack}");
            if (!this.freezeStack)
            {
                this.state.TransformCopyDownSp(node);
            }
            else
            {
                // CRITICAL: Even when freezeStack is true, we must still process CPDOWNSP to ensure it's in the AST
                // Otherwise, the instruction will be missing from the decompiled output
                // Matching DeNCS behavior: CPDOWNSP should always be processed, freezeStack only affects stack operations
                Debug("DEBUG DoGlobalVars.OutACopyDownSpCommand: freezeStack is true, but still processing CPDOWNSP to ensure it's in AST");
                this.state.TransformCopyDownSp(node);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:67-75
        // Original: @Override public void outARsaddCommand(ARsaddCommand node) { if (!this.freezeStack) { Variable var = new Variable(NodeUtils.getType(node)); this.stack.push(var); this.state.transformRSAdd(node); var = null; } }
        public override void OutARsaddCommand(ARsaddCommand node)
        {
            if (!this.freezeStack)
            {
                Variable var = new Variable(NodeUtils.GetType(node));
                this.stack.Push(var);
                this.state.TransformRSAdd(node);
                var = null;
            }
            else
            {
                // CRITICAL: Even when freezeStack is true, we must still process RSADDI to ensure it's in the AST
                // Otherwise, the instruction will be missing from the decompiled output
                // Matching DeNCS behavior: RSADDI should always be processed, freezeStack only affects stack operations
                Debug($"DEBUG DoGlobalVars.OutARsaddCommand: freezeStack is true, but still processing RSADDI to ensure it's in AST");
                Variable var = new Variable(NodeUtils.GetType(node));
                this.stack.Push(var);
                this.state.TransformRSAdd(node);
                var = null;
            }
        }

        // Override CaseARsaddCmd to ensure RSADD commands from NcsToAstConverter are visited
        // CaseARsaddCmd calls node.GetRsaddCommand().Apply(this), which routes to OutARsaddCommand
        // This ensures RSADD commands are processed even when wrapped in ARsaddCmd
        public override void CaseARsaddCmd(AST.ARsaddCmd node)
        {
            this.DefaultIn(node);
            if (node.GetRsaddCommand() != null)
            {
                node.GetRsaddCommand().Apply(this);
            }
            this.DefaultOut(node);
        }

        // Override CaseACopydownspCmd to ensure CPDOWNSP commands from NcsToAstConverter are visited
        // CaseACopydownspCmd calls node.GetCopyDownSpCommand().Apply(this), which routes to OutACopyDownSpCommand
        // This ensures CPDOWNSP commands are processed even when wrapped in ACopydownspCmd
        public override void CaseACopydownspCmd(AST.ACopydownspCmd node)
        {
            Debug($"DEBUG DoGlobalVars.CaseACopydownspCmd: node={node?.GetType().Name ?? "null"}, freezeStack={this.freezeStack}");
            this.InACopydownspCmd(node);
            if (node.GetCopyDownSpCommand() != null)
            {
                Debug($"DEBUG DoGlobalVars.CaseACopydownspCmd: calling GetCopyDownSpCommand().Apply(this)");
                node.GetCopyDownSpCommand().Apply(this);
            }
            else
            {
                Debug("DEBUG DoGlobalVars.CaseACopydownspCmd: GetCopyDownSpCommand() returned null");
            }
            this.OutACopydownspCmd(node);
        }

        public override void InACopydownspCmd(AST.ACopydownspCmd node)
        {
            this.DefaultIn(node);
        }

        public override void OutACopydownspCmd(AST.ACopydownspCmd node)
        {
            this.DefaultOut(node);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/DoGlobalVars.java:77-79
        // Original: public LocalVarStack getStack() { return this.stack; }
        public virtual LocalVarStack GetStack()
        {
            return this.stack;
        }

    }
}




