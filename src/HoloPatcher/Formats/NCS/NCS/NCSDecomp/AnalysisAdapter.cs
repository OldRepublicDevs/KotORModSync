// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/AnalysisAdapter.java:147-748
// Original: public class AnalysisAdapter implements Analysis
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Analysis
{
    public class AnalysisAdapter : IAnalysis
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/AnalysisAdapter.java:148-149
        // Original: private Hashtable<Node, Object> in; private Hashtable<Node, Object> out;
        private Dictionary<object, object> @in;
        private Dictionary<object, object> @out;
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/AnalysisAdapter.java:152-154
        // Original: @Override public Object getIn(Node node) { return this.in == null ? null : this.in.get(node); }
        public virtual object GetIn(Node node)
        {
            if (this.@in == null)
            {
                return null;
            }

            object result;
            return this.@in.TryGetValue(node, out result) ? result : null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/AnalysisAdapter.java:156-168
        // Original: @Override public void setIn(Node node, Object in) { if (this.in == null) { this.in = new Hashtable<>(1); } if (in != null) { this.in.put(node, in); } else { this.in.remove(node); } }
        public virtual void SetIn(Node node, object @in)
        {
            if (this.@in == null)
            {
                this.@in = new Dictionary<object, object>(1);
            }

            if (@in != null)
            {
                this.@in[node] = @in;
            }
            else
            {
                this.@in.Remove(node);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/AnalysisAdapter.java:170-172
        // Original: @Override public Object getOut(Node node) { return this.out == null ? null : this.out.get(node); }
        public virtual object GetOut(Node node)
        {
            if (this.@out == null)
            {
                return null;
            }

            object result;
            return this.@out.TryGetValue(node, out result) ? result : null;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/analysis/AnalysisAdapter.java:174-185
        // Original: @Override public void setOut(Node node, Object out) { if (this.out == null) { this.out = new Hashtable<>(1); } if (out != null) { this.out.put(node, out); } else { this.out.remove(node); } }
        public virtual void SetOut(Node node, object @out)
        {
            if (this.@out == null)
            {
                this.@out = new Dictionary<object, object>(1);
            }

            if (@out != null)
            {
                this.@out[node] = @out;
            }
            else
            {
                this.@out.Remove(node);
            }
        }

        public virtual void CaseStart(Start node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAProgram(AProgram node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseASubroutine(ASubroutine node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseACommandBlock(ACommandBlock node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAAddVarCmd(AAddVarCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAActionJumpCmd(AActionJumpCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAConstCmd(AConstCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseACopydownspCmd(ACopydownspCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseACopytopspCmd(ACopytopspCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseACopydownbpCmd(ACopydownbpCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseACopytopbpCmd(ACopytopbpCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseACondJumpCmd(ACondJumpCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAJumpCmd(AJumpCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAJumpSubCmd(AJumpSubCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAMovespCmd(AMovespCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseALogiiCmd(ALogiiCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAUnaryCmd(AUnaryCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseABinaryCmd(ABinaryCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseADestructCmd(ADestructCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseABpCmd(ABpCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAActionCmd(AActionCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAStackOpCmd(AStackOpCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAReturnCmd(AReturnCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAStoreStateCmd(AStoreStateCmd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAAndLogiiOp(AAndLogiiOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAOrLogiiOp(AOrLogiiOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAInclOrLogiiOp(AInclOrLogiiOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAExclOrLogiiOp(AExclOrLogiiOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseABitAndLogiiOp(ABitAndLogiiOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAEqualBinaryOp(AEqualBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseANequalBinaryOp(ANequalBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAGeqBinaryOp(AGeqBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAGtBinaryOp(AGtBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseALtBinaryOp(ALtBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseALeqBinaryOp(ALeqBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAShrightBinaryOp(AShrightBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAShleftBinaryOp(AShleftBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAUnrightBinaryOp(AUnrightBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAAddBinaryOp(AAddBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseASubBinaryOp(ASubBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAMulBinaryOp(AMulBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseADivBinaryOp(ADivBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAModBinaryOp(AModBinaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseANegUnaryOp(ANegUnaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseACompUnaryOp(ACompUnaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseANotUnaryOp(ANotUnaryOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseADecispStackOp(ADecispStackOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAIncispStackOp(AIncispStackOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseADecibpStackOp(ADecibpStackOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAIncibpStackOp(AIncibpStackOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAIntConstant(AIntConstant node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAFloatConstant(AFloatConstant node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAStringConstant(AStringConstant node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAZeroJumpIf(AZeroJumpIf node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseANonzeroJumpIf(ANonzeroJumpIf node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseASavebpBpOp(ASavebpBpOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseARestorebpBpOp(ARestorebpBpOp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAConditionalJumpCommand(AConditionalJumpCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAJumpCommand(AJumpCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAJumpToSubroutine(AJumpToSubroutine node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAReturn(AReturn node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseACopyDownSpCommand(ACopyDownSpCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseACopyTopSpCommand(ACopyTopSpCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseACopyDownBpCommand(ACopyDownBpCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseACopyTopBpCommand(ACopyTopBpCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAMoveSpCommand(AMoveSpCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseARsaddCommand(ARsaddCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAConstCommand(AConstCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAActionCommand(AActionCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseALogiiCommand(ALogiiCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseABinaryCommand(ABinaryCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAUnaryCommand(AUnaryCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAStackCommand(AStackCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseADestructCommand(ADestructCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseABpCommand(ABpCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseAStoreStateCommand(AStoreStateCommand node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseASize(ASize node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTLPar(TLPar node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTRPar(TRPar node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTSemi(TSemi node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTDot(TDot node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTCpdownsp(TCpdownsp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTRsadd(TRsadd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTCptopsp(TCptopsp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTConst(TConst node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTAction(TAction node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTLogandii(TLogandii node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTLogorii(TLogorii node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTIncorii(TIncorii node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTExcorii(TExcorii node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTBoolandii(TBoolandii node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTEqual(TEqual node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTNequal(TNequal node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTGeq(TGeq node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTGt(TGt node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTLt(TLt node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTLeq(TLeq node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTShleft(TShleft node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTShright(TShright node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTUnright(TUnright node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTAdd(TAdd node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTSub(TSub node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTMul(TMul node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTDiv(TDiv node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTMod(TMod node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTNeg(TNeg node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTComp(TComp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTMovsp(TMovsp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTJmp(TJmp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTJsr(TJsr node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTJz(TJz node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTRetn(TRetn node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTDestruct(TDestruct node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTNot(TNot node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTDecisp(TDecisp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTIncisp(TIncisp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTJnz(TJnz node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTCpdownbp(TCpdownbp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTCptopbp(TCptopbp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTDecibp(TDecibp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTIncibp(TIncibp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTSavebp(TSavebp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTRestorebp(TRestorebp node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTStorestate(TStorestate node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTNop(TNop node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTT(TT node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTStringLiteral(TStringLiteral node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTBlank(TBlank node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTIntegerConstant(TIntegerConstant node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseTFloatConstant(TFloatConstant node)
        {
            this.DefaultCase(node);
        }

        public virtual void CaseEOF(EOF node)
        {
            this.DefaultCase(node);
        }

        public virtual void DefaultCase(Node node)
        {
        }

        public virtual void DefaultIn(Node node)
        {
        }

        public virtual void DefaultOut(Node node)
        {
        }
    }
}




