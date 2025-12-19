// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/PrunedReversedDepthFirstAdapter.java:59-939
// Original: public class PrunedReversedDepthFirstAdapter extends AnalysisAdapter
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Analysis
{
    public class PrunedReversedDepthFirstAdapter : AnalysisAdapter
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/PrunedReversedDepthFirstAdapter.java:60-62
        // Original: public void inStart(Start node) { this.defaultIn(node); }
        public virtual void InStart(Start node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutStart(Start node)
        {
            this.DefaultOut(node);
        }

        public new virtual void DefaultIn(Node node)
        {
        }

        public new virtual void DefaultOut(Node node)
        {
        }

        public override void CaseStart(Start node)
        {
            this.InStart(node);
            node.GetPProgram().Apply(this);
            this.OutStart(node);
        }

        public virtual void InAProgram(AProgram node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAProgram(AProgram node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAProgram(AProgram node)
        {
            this.InAProgram(node);
            Object[] temp = node.GetSubroutine().ToArray();
            for (int i = temp.Length - 1; i >= 0; --i)
            {
                ((PSubroutine)temp[i]).Apply(this);
            }

            if (node.GetReturn() != null)
            {
                node.GetReturn().Apply(this);
            }

            if (node.GetJumpToSubroutine() != null)
            {
                node.GetJumpToSubroutine().Apply(this);
            }

            this.OutAProgram(node);
        }

        public virtual void InASubroutine(ASubroutine node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutASubroutine(ASubroutine node)
        {
            this.DefaultOut(node);
        }

        public override void CaseASubroutine(ASubroutine node)
        {
            this.InASubroutine(node);
            if (node.GetReturn() != null)
            {
                node.GetReturn().Apply(this);
            }

            if (node.GetCommandBlock() != null)
            {
                node.GetCommandBlock().Apply(this);
            }

            this.OutASubroutine(node);
        }


        public virtual void InACommandBlock(ACommandBlock node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutACommandBlock(ACommandBlock node)
        {
            this.DefaultOut(node);
        }

        public override void CaseACommandBlock(ACommandBlock node)
        {
            this.InACommandBlock(node);
            Object[] temp = node.GetCmd().ToArray();
            for (int i = temp.Length - 1; i >= 0; --i)
            {
                ((PCmd)temp[i]).Apply(this);
            }

            this.OutACommandBlock(node);
        }


        public virtual void InAAddVarCmd(AAddVarCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAAddVarCmd(AAddVarCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAAddVarCmd(AAddVarCmd node)
        {
            this.InAAddVarCmd(node);
            if (node.GetRsaddCommand() != null)
            {
                node.GetRsaddCommand().Apply(this);
            }

            this.OutAAddVarCmd(node);
        }

        // Handle AST.ARsaddCmd as well (from NcsToAstConverter)
        // ARsaddCmd is similar to AAddVarCmd - it contains an ARsaddCommand
        public virtual void CaseARsaddCmd(AST.ARsaddCmd node)
        {
            // Treat AST.ARsaddCmd similar to AAddVarCmd - visit the RsaddCommand child
            this.DefaultIn(node);
            if (node.GetRsaddCommand() != null)
            {
                node.GetRsaddCommand().Apply(this);
            }

            this.DefaultOut(node);
        }

        public virtual void InAActionJumpCmd(AActionJumpCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAActionJumpCmd(AActionJumpCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAActionJumpCmd(AActionJumpCmd node)
        {
            this.InAActionJumpCmd(node);
            if (node.GetReturn() != null)
            {
                node.GetReturn().Apply(this);
            }

            if (node.GetCommandBlock() != null)
            {
                node.GetCommandBlock().Apply(this);
            }

            if (node.GetJumpCommand() != null)
            {
                node.GetJumpCommand().Apply(this);
            }

            if (node.GetStoreStateCommand() != null)
            {
                node.GetStoreStateCommand().Apply(this);
            }

            this.OutAActionJumpCmd(node);
        }

        public virtual void InAConstCmd(AConstCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAConstCmd(AConstCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAConstCmd(AConstCmd node)
        {
            this.InAConstCmd(node);
            if (node.GetConstCommand() != null)
            {
                node.GetConstCommand().Apply(this);
            }

            this.OutAConstCmd(node);
        }

        public virtual void InACopydownspCmd(ACopydownspCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutACopydownspCmd(ACopydownspCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseACopydownspCmd(ACopydownspCmd node)
        {
            this.InACopydownspCmd(node);
            if (node.GetCopyDownSpCommand() != null)
            {
                node.GetCopyDownSpCommand().Apply(this);
            }

            this.OutACopydownspCmd(node);
        }

        public virtual void InACopytopspCmd(ACopytopspCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutACopytopspCmd(ACopytopspCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseACopytopspCmd(ACopytopspCmd node)
        {
            this.InACopytopspCmd(node);
            if (node.GetCopyTopSpCommand() != null)
            {
                node.GetCopyTopSpCommand().Apply(this);
            }

            this.OutACopytopspCmd(node);
        }

        public virtual void InACopydownbpCmd(ACopydownbpCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutACopydownbpCmd(ACopydownbpCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseACopydownbpCmd(ACopydownbpCmd node)
        {
            this.InACopydownbpCmd(node);
            if (node.GetCopyDownBpCommand() != null)
            {
                node.GetCopyDownBpCommand().Apply(this);
            }

            this.OutACopydownbpCmd(node);
        }

        public virtual void InACopytopbpCmd(ACopytopbpCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutACopytopbpCmd(ACopytopbpCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseACopytopbpCmd(ACopytopbpCmd node)
        {
            this.InACopytopbpCmd(node);
            if (node.GetCopyTopBpCommand() != null)
            {
                node.GetCopyTopBpCommand().Apply(this);
            }

            this.OutACopytopbpCmd(node);
        }

        public virtual void InACondJumpCmd(ACondJumpCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutACondJumpCmd(ACondJumpCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseACondJumpCmd(ACondJumpCmd node)
        {
            this.InACondJumpCmd(node);
            if (node.GetConditionalJumpCommand() != null)
            {
                node.GetConditionalJumpCommand().Apply(this);
            }

            this.OutACondJumpCmd(node);
        }

        public virtual void InAJumpCmd(AJumpCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAJumpCmd(AJumpCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAJumpCmd(AJumpCmd node)
        {
            this.InAJumpCmd(node);
            if (node.GetJumpCommand() != null)
            {
                node.GetJumpCommand().Apply(this);
            }

            this.OutAJumpCmd(node);
        }

        public virtual void InAJumpSubCmd(AJumpSubCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAJumpSubCmd(AJumpSubCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAJumpSubCmd(AJumpSubCmd node)
        {
            this.InAJumpSubCmd(node);
            if (node.GetJumpToSubroutine() != null)
            {
                node.GetJumpToSubroutine().Apply(this);
            }

            this.OutAJumpSubCmd(node);
        }

        public virtual void InAMovespCmd(AMovespCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAMovespCmd(AMovespCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAMovespCmd(AMovespCmd node)
        {
            this.InAMovespCmd(node);
            if (node.GetMoveSpCommand() != null)
            {
                node.GetMoveSpCommand().Apply(this);
            }

            this.OutAMovespCmd(node);
        }

        public virtual void InALogiiCmd(ALogiiCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutALogiiCmd(ALogiiCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseALogiiCmd(ALogiiCmd node)
        {
            this.InALogiiCmd(node);
            if (node.GetLogiiCommand() != null)
            {
                node.GetLogiiCommand().Apply(this);
            }

            this.OutALogiiCmd(node);
        }

        public virtual void InAUnaryCmd(AUnaryCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAUnaryCmd(AUnaryCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAUnaryCmd(AUnaryCmd node)
        {
            this.InAUnaryCmd(node);
            if (node.GetUnaryCommand() != null)
            {
                node.GetUnaryCommand().Apply(this);
            }

            this.OutAUnaryCmd(node);
        }

        public virtual void InABinaryCmd(ABinaryCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutABinaryCmd(ABinaryCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseABinaryCmd(ABinaryCmd node)
        {
            this.InABinaryCmd(node);
            if (node.GetBinaryCommand() != null)
            {
                node.GetBinaryCommand().Apply(this);
            }

            this.OutABinaryCmd(node);
        }

        public virtual void InADestructCmd(ADestructCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutADestructCmd(ADestructCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseADestructCmd(ADestructCmd node)
        {
            this.InADestructCmd(node);
            if (node.GetDestructCommand() != null)
            {
                node.GetDestructCommand().Apply(this);
            }

            this.OutADestructCmd(node);
        }

        public virtual void InABpCmd(ABpCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutABpCmd(ABpCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseABpCmd(ABpCmd node)
        {
            this.InABpCmd(node);
            if (node.GetBpCommand() != null)
            {
                node.GetBpCommand().Apply(this);
            }

            this.OutABpCmd(node);
        }


        public virtual void InAActionCmd(AActionCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAActionCmd(AActionCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAActionCmd(AActionCmd node)
        {
            this.InAActionCmd(node);
            if (node.GetActionCommand() != null)
            {
                node.GetActionCommand().Apply(this);
            }

            this.OutAActionCmd(node);
        }

        public virtual void InAStackOpCmd(AStackOpCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAStackOpCmd(AStackOpCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAStackOpCmd(AStackOpCmd node)
        {
            this.InAStackOpCmd(node);
            if (node.GetStackCommand() != null)
            {
                node.GetStackCommand().Apply(this);
            }

            this.OutAStackOpCmd(node);
        }

        public virtual void InAReturnCmd(AReturnCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAReturnCmd(AReturnCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAReturnCmd(AReturnCmd node)
        {
            this.InAReturnCmd(node);
            if (node.GetReturn() != null)
            {
                node.GetReturn().Apply(this);
            }

            this.OutAReturnCmd(node);
        }

        public virtual void InAStoreStateCmd(AStoreStateCmd node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAStoreStateCmd(AStoreStateCmd node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAStoreStateCmd(AStoreStateCmd node)
        {
            this.InAStoreStateCmd(node);
            if (node.GetStoreStateCommand() != null)
            {
                node.GetStoreStateCommand().Apply(this);
            }

            this.OutAStoreStateCmd(node);
        }

        public virtual void InAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            this.InAConditionalJumpCommand(node);
            this.OutAConditionalJumpCommand(node);
        }

        public virtual void InAJumpCommand(AJumpCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAJumpCommand(AJumpCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAJumpCommand(AJumpCommand node)
        {
            this.InAJumpCommand(node);
            this.OutAJumpCommand(node);
        }

        public virtual void InAJumpToSubroutine(AJumpToSubroutine node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAJumpToSubroutine(AJumpToSubroutine node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAJumpToSubroutine(AJumpToSubroutine node)
        {
            this.InAJumpToSubroutine(node);
            this.OutAJumpToSubroutine(node);
        }

        public virtual void InAReturn(AReturn node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAReturn(AReturn node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAReturn(AReturn node)
        {
            this.InAReturn(node);
            this.OutAReturn(node);
        }

        public virtual void InACopyDownSpCommand(ACopyDownSpCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutACopyDownSpCommand(ACopyDownSpCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseACopyDownSpCommand(ACopyDownSpCommand node)
        {
            this.InACopyDownSpCommand(node);
            this.OutACopyDownSpCommand(node);
        }

        public virtual void InACopyTopSpCommand(ACopyTopSpCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutACopyTopSpCommand(ACopyTopSpCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseACopyTopSpCommand(ACopyTopSpCommand node)
        {
            this.InACopyTopSpCommand(node);
            this.OutACopyTopSpCommand(node);
        }

        public virtual void InACopyDownBpCommand(ACopyDownBpCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutACopyDownBpCommand(ACopyDownBpCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseACopyDownBpCommand(ACopyDownBpCommand node)
        {
            this.InACopyDownBpCommand(node);
            this.OutACopyDownBpCommand(node);
        }

        public virtual void InACopyTopBpCommand(ACopyTopBpCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutACopyTopBpCommand(ACopyTopBpCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseACopyTopBpCommand(ACopyTopBpCommand node)
        {
            this.InACopyTopBpCommand(node);
            this.OutACopyTopBpCommand(node);
        }

        public virtual void InAMoveSpCommand(AMoveSpCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAMoveSpCommand(AMoveSpCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAMoveSpCommand(AMoveSpCommand node)
        {
            this.InAMoveSpCommand(node);
            this.OutAMoveSpCommand(node);
        }

        public virtual void InARsaddCommand(ARsaddCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutARsaddCommand(ARsaddCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseARsaddCommand(ARsaddCommand node)
        {
            this.InARsaddCommand(node);
            this.OutARsaddCommand(node);
        }


        public virtual void InAConstCommand(AConstCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAConstCommand(AConstCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAConstCommand(AConstCommand node)
        {
            this.InAConstCommand(node);
            this.OutAConstCommand(node);
        }

        public virtual void InAActionCommand(AActionCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAActionCommand(AActionCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAActionCommand(AActionCommand node)
        {
            this.InAActionCommand(node);
            this.OutAActionCommand(node);
        }

        public virtual void InALogiiCommand(ALogiiCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutALogiiCommand(ALogiiCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseALogiiCommand(ALogiiCommand node)
        {
            this.InALogiiCommand(node);
            this.OutALogiiCommand(node);
        }

        public virtual void InABinaryCommand(ABinaryCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutABinaryCommand(ABinaryCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseABinaryCommand(ABinaryCommand node)
        {
            this.InABinaryCommand(node);
            this.OutABinaryCommand(node);
        }

        public virtual void InAUnaryCommand(AUnaryCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAUnaryCommand(AUnaryCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAUnaryCommand(AUnaryCommand node)
        {
            this.InAUnaryCommand(node);
            this.OutAUnaryCommand(node);
        }

        public virtual void InAStackCommand(AStackCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAStackCommand(AStackCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAStackCommand(AStackCommand node)
        {
            this.InAStackCommand(node);
            this.OutAStackCommand(node);
        }

        public virtual void InADestructCommand(ADestructCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutADestructCommand(ADestructCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseADestructCommand(ADestructCommand node)
        {
            this.InADestructCommand(node);
            this.OutADestructCommand(node);
        }

        public virtual void InABpCommand(ABpCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutABpCommand(ABpCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseABpCommand(ABpCommand node)
        {
            this.InABpCommand(node);
            this.OutABpCommand(node);
        }


        public virtual void InAStoreStateCommand(AStoreStateCommand node)
        {
            this.DefaultIn(node);
        }

        public virtual void OutAStoreStateCommand(AStoreStateCommand node)
        {
            this.DefaultOut(node);
        }

        public override void CaseAStoreStateCommand(AStoreStateCommand node)
        {
            this.InAStoreStateCommand(node);
            this.OutAStoreStateCommand(node);
        }
    }
}




