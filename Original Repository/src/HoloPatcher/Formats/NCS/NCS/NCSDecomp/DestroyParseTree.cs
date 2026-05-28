// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/DestroyParseTree.java:59-479
// Original: public class DestroyParseTree extends AnalysisAdapter
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Utils
{
    public class DestroyParseTree : AnalysisAdapter
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/DestroyParseTree.java:60-64
        // Original: @Override public void caseStart(Start node) { node.getPProgram().apply(this); node.setPProgram(null); }
        public override void CaseStart(Start node)
        {
            node.GetPProgram().Apply(this);
            node.SetPProgram(null);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/DestroyParseTree.java:67-95
        // Original: @Override public void caseAProgram(AProgram node) { ... }
        public override void CaseAProgram(AProgram node)
        {
            if (node.GetSize() != null)
            {
                node.GetSize().Apply(this);
            }

            if (node.GetConditional() != null)
            {
                node.GetConditional().Apply(this);
            }

            if (node.GetJumpToSubroutine() != null)
            {
                node.GetJumpToSubroutine().Apply(this);
            }

            if (node.GetReturn() != null)
            {
                node.GetReturn().Apply(this);
            }

            Object[] temp = node.GetSubroutine().ToArray();
            for (int i = 0; i < temp.Length; i++)
            {
                ((PSubroutine)temp[i]).Apply(this);
            }

            node.SetSize(null);
            node.SetConditional(null);
            node.SetJumpToSubroutine(null);
            node.SetReturn(null);
            node.SetSubroutine(new List<PSubroutine>());
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/DestroyParseTree.java:98-109
        // Original: @Override public void caseASubroutine(ASubroutine node) { ... }
        public override void CaseASubroutine(ASubroutine node)
        {
            if (node.GetCommandBlock() != null)
            {
                node.GetCommandBlock().Apply(this);
            }

            if (node.GetReturn() != null)
            {
                node.GetReturn().Apply(this);
            }

            node.SetCommandBlock(null);
            node.SetReturn(null);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/utils/DestroyParseTree.java:112-120
        // Original: @Override public void caseACommandBlock(ACommandBlock node) { ... }
        public override void CaseACommandBlock(ACommandBlock node)
        {
            Object[] temp = node.GetCmd().ToArray();
            for (int i = 0; i < temp.Length; i++)
            {
                ((PCmd)temp[i]).Apply(this);
            }

            node.SetCmd(new List<PCmd>());
        }

        public override void CaseAAddVarCmd(AAddVarCmd node)
        {
            if (node.GetRsaddCommand() != null)
            {
                node.GetRsaddCommand().Apply(this);
            }

            node.SetRsaddCommand(null);
        }

        public override void CaseAActionJumpCmd(AActionJumpCmd node)
        {
            if (node.GetStoreStateCommand() != null)
            {
                node.GetStoreStateCommand().Apply(this);
            }

            if (node.GetJumpCommand() != null)
            {
                node.GetJumpCommand().Apply(this);
            }

            if (node.GetCommandBlock() != null)
            {
                node.GetCommandBlock().Apply(this);
            }

            if (node.GetReturn() != null)
            {
                node.GetReturn().Apply(this);
            }

            node.SetStoreStateCommand(null);
            node.SetJumpCommand(null);
            node.SetCommandBlock(null);
            node.SetReturn(null);
        }

        public override void CaseAConstCmd(AConstCmd node)
        {
            if (node.GetConstCommand() != null)
            {
                node.GetConstCommand().Apply(this);
            }

            node.SetConstCommand(null);
        }

        public override void CaseACopydownspCmd(ACopydownspCmd node)
        {
            if (node.GetCopyDownSpCommand() != null)
            {
                node.GetCopyDownSpCommand().Apply(this);
            }

            node.SetCopyDownSpCommand(null);
        }

        public override void CaseACopytopspCmd(ACopytopspCmd node)
        {
            if (node.GetCopyTopSpCommand() != null)
            {
                node.GetCopyTopSpCommand().Apply(this);
            }

            node.SetCopyTopSpCommand(null);
        }

        public override void CaseACopydownbpCmd(ACopydownbpCmd node)
        {
            if (node.GetCopyDownBpCommand() != null)
            {
                node.GetCopyDownBpCommand().Apply(this);
            }

            node.SetCopyDownBpCommand(null);
        }

        public override void CaseACopytopbpCmd(ACopytopbpCmd node)
        {
            if (node.GetCopyTopBpCommand() != null)
            {
                node.GetCopyTopBpCommand().Apply(this);
            }

            node.SetCopyTopBpCommand(null);
        }

        public override void CaseACondJumpCmd(ACondJumpCmd node)
        {
            if (node.GetConditionalJumpCommand() != null)
            {
                node.GetConditionalJumpCommand().Apply(this);
            }

            node.SetConditionalJumpCommand(null);
        }

        public override void CaseAJumpCmd(AJumpCmd node)
        {
            if (node.GetJumpCommand() != null)
            {
                node.GetJumpCommand().Apply(this);
            }

            node.SetJumpCommand(null);
        }

        public override void CaseAJumpSubCmd(AJumpSubCmd node)
        {
            if (node.GetJumpToSubroutine() != null)
            {
                node.GetJumpToSubroutine().Apply(this);
            }

            node.SetJumpToSubroutine(null);
        }

        public override void CaseAMovespCmd(AMovespCmd node)
        {
            if (node.GetMoveSpCommand() != null)
            {
                node.GetMoveSpCommand().Apply(this);
            }

            node.SetMoveSpCommand(null);
        }

        public override void CaseALogiiCmd(ALogiiCmd node)
        {
            if (node.GetLogiiCommand() != null)
            {
                node.GetLogiiCommand().Apply(this);
            }

            node.SetLogiiCommand(null);
        }

        public override void CaseAUnaryCmd(AUnaryCmd node)
        {
            if (node.GetUnaryCommand() != null)
            {
                node.GetUnaryCommand().Apply(this);
            }

            node.SetUnaryCommand(null);
        }

        public override void CaseABinaryCmd(ABinaryCmd node)
        {
            if (node.GetBinaryCommand() != null)
            {
                node.GetBinaryCommand().Apply(this);
            }

            node.SetBinaryCommand(null);
        }

        public override void CaseADestructCmd(ADestructCmd node)
        {
            if (node.GetDestructCommand() != null)
            {
                node.GetDestructCommand().Apply(this);
            }

            node.SetDestructCommand(null);
        }

        public override void CaseABpCmd(ABpCmd node)
        {
            if (node.GetBpCommand() != null)
            {
                node.GetBpCommand().Apply(this);
            }

            node.SetBpCommand(null);
        }

        public override void CaseAActionCmd(AActionCmd node)
        {
            if (node.GetActionCommand() != null)
            {
                node.GetActionCommand().Apply(this);
            }

            node.SetActionCommand(null);
        }

        public override void CaseAStackOpCmd(AStackOpCmd node)
        {
            if (node.GetStackCommand() != null)
            {
                node.GetStackCommand().Apply(this);
            }

            node.SetStackCommand(null);
        }

        public override void CaseAReturnCmd(AReturnCmd node)
        {
            if (node.GetReturn() != null)
            {
                node.GetReturn().Apply(this);
            }

            node.SetReturn(null);
        }

        public override void CaseAStoreStateCmd(AStoreStateCmd node)
        {
            if (node.GetStoreStateCommand() != null)
            {
                node.GetStoreStateCommand().Apply(this);
            }

            node.SetStoreStateCommand(null);
        }

        public override void CaseAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            node.SetJumpIf(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetOffset(null);
            node.SetSemi(null);
        }

        public override void CaseAJumpCommand(AJumpCommand node)
        {
            node.SetJmp(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetOffset(null);
            node.SetSemi(null);
        }

        public override void CaseAJumpToSubroutine(AJumpToSubroutine node)
        {
            node.SetJsr(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetOffset(null);
            node.SetSemi(null);
        }

        public override void CaseAReturn(AReturn node)
        {
            node.SetRetn(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetSemi(null);
        }

        public override void CaseACopyDownSpCommand(ACopyDownSpCommand node)
        {
            node.SetCpdownsp(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetOffset(null);
            node.SetSize(null);
            node.SetSemi(null);
        }

        public override void CaseACopyTopSpCommand(ACopyTopSpCommand node)
        {
            node.SetCptopsp(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetOffset(null);
            node.SetSize(null);
            node.SetSemi(null);
        }

        public override void CaseACopyDownBpCommand(ACopyDownBpCommand node)
        {
            node.SetCpdownbp(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetOffset(null);
            node.SetSize(null);
            node.SetSemi(null);
        }

        public override void CaseACopyTopBpCommand(ACopyTopBpCommand node)
        {
            node.SetCptopbp(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetOffset(null);
            node.SetSize(null);
            node.SetSemi(null);
        }

        public override void CaseAMoveSpCommand(AMoveSpCommand node)
        {
            node.SetMovsp(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetOffset(null);
            node.SetSemi(null);
        }

        public override void CaseARsaddCommand(ARsaddCommand node)
        {
            node.SetRsadd(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetSemi(null);
        }

        public override void CaseAConstCommand(AConstCommand node)
        {
            node.SetConst(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetConstant(null);
            node.SetSemi(null);
        }

        public override void CaseAActionCommand(AActionCommand node)
        {
            node.SetAction(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetId(null);
            node.SetArgCount(null);
            node.SetSemi(null);
        }

        public override void CaseALogiiCommand(ALogiiCommand node)
        {
            node.SetLogiiOp(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetSemi(null);
        }

        public override void CaseABinaryCommand(ABinaryCommand node)
        {
            node.SetBinaryOp(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetSize(null);
            node.SetSemi(null);
        }

        public override void CaseAUnaryCommand(AUnaryCommand node)
        {
            node.SetUnaryOp(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetSemi(null);
        }

        public override void CaseAStackCommand(AStackCommand node)
        {
            node.SetStackOp(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetOffset(null);
            node.SetSemi(null);
        }

        public override void CaseADestructCommand(ADestructCommand node)
        {
            node.SetDestruct(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetSizeRem(null);
            node.SetOffset(null);
            node.SetSizeSave(null);
            node.SetSemi(null);
        }

        public override void CaseABpCommand(ABpCommand node)
        {
            node.SetBpOp(null);
            node.SetPos(null);
            node.SetType(null);
            node.SetSemi(null);
        }

        public override void CaseAStoreStateCommand(AStoreStateCommand node)
        {
            node.SetStorestate(null);
            node.SetPos(null);
            node.SetOffset(null);
            node.SetSizeBp(null);
            node.SetSizeSp(null);
            node.SetSemi(null);
        }
    }
}




