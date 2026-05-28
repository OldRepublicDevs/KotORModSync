// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/parser/TokenIndex.java:66-335
// Original: class TokenIndex extends AnalysisAdapter
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;

namespace KOTORModSync.Formats.NCS.NCSDecomp.Parser
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/parser/TokenIndex.java:66-67
    // Original: class TokenIndex extends AnalysisAdapter { int index; }
    class TokenIndex : AnalysisAdapter
    {
        public int index;
        public override void CaseTLPar(TLPar node)
        {
            this.index = 0;
        }

        public override void CaseTRPar(TRPar node)
        {
            this.index = 1;
        }

        public override void CaseTSemi(TSemi node)
        {
            this.index = 2;
        }

        public override void CaseTDot(TDot node)
        {
            this.index = 3;
        }

        public override void CaseTCpdownsp(TCpdownsp node)
        {
            this.index = 4;
        }

        public override void CaseTRsadd(TRsadd node)
        {
            this.index = 5;
        }

        public override void CaseTCptopsp(TCptopsp node)
        {
            this.index = 6;
        }

        public override void CaseTConst(TConst node)
        {
            this.index = 7;
        }

        public override void CaseTAction(TAction node)
        {
            this.index = 8;
        }

        public override void CaseTLogandii(TLogandii node)
        {
            this.index = 9;
        }

        public override void CaseTLogorii(TLogorii node)
        {
            this.index = 10;
        }

        public override void CaseTIncorii(TIncorii node)
        {
            this.index = 11;
        }

        public override void CaseTExcorii(TExcorii node)
        {
            this.index = 12;
        }

        public override void CaseTBoolandii(TBoolandii node)
        {
            this.index = 13;
        }

        public override void CaseTEqual(TEqual node)
        {
            this.index = 14;
        }

        public override void CaseTNequal(TNequal node)
        {
            this.index = 15;
        }

        public override void CaseTGeq(TGeq node)
        {
            this.index = 16;
        }

        public override void CaseTGt(TGt node)
        {
            this.index = 17;
        }

        public override void CaseTLt(TLt node)
        {
            this.index = 18;
        }

        public override void CaseTLeq(TLeq node)
        {
            this.index = 19;
        }

        public override void CaseTShleft(TShleft node)
        {
            this.index = 20;
        }

        public override void CaseTShright(TShright node)
        {
            this.index = 21;
        }

        public override void CaseTUnright(TUnright node)
        {
            this.index = 22;
        }

        public override void CaseTAdd(TAdd node)
        {
            this.index = 23;
        }

        public override void CaseTSub(TSub node)
        {
            this.index = 24;
        }

        public override void CaseTMul(TMul node)
        {
            this.index = 25;
        }

        public override void CaseTDiv(TDiv node)
        {
            this.index = 26;
        }

        public override void CaseTMod(TMod node)
        {
            this.index = 27;
        }

        public override void CaseTNeg(TNeg node)
        {
            this.index = 28;
        }

        public override void CaseTComp(TComp node)
        {
            this.index = 29;
        }

        public override void CaseTMovsp(TMovsp node)
        {
            this.index = 30;
        }

        public override void CaseTJmp(TJmp node)
        {
            this.index = 31;
        }

        public override void CaseTJsr(TJsr node)
        {
            this.index = 32;
        }

        public override void CaseTJz(TJz node)
        {
            this.index = 33;
        }

        public override void CaseTRetn(TRetn node)
        {
            this.index = 34;
        }

        public override void CaseTDestruct(TDestruct node)
        {
            this.index = 35;
        }

        public override void CaseTNot(TNot node)
        {
            this.index = 36;
        }

        public override void CaseTDecisp(TDecisp node)
        {
            this.index = 37;
        }

        public override void CaseTIncisp(TIncisp node)
        {
            this.index = 38;
        }

        public override void CaseTJnz(TJnz node)
        {
            this.index = 39;
        }

        public override void CaseTCpdownbp(TCpdownbp node)
        {
            this.index = 40;
        }

        public override void CaseTCptopbp(TCptopbp node)
        {
            this.index = 41;
        }

        public override void CaseTDecibp(TDecibp node)
        {
            this.index = 42;
        }

        public override void CaseTIncibp(TIncibp node)
        {
            this.index = 43;
        }

        public override void CaseTSavebp(TSavebp node)
        {
            this.index = 44;
        }

        public override void CaseTRestorebp(TRestorebp node)
        {
            this.index = 45;
        }

        public override void CaseTStorestate(TStorestate node)
        {
            this.index = 46;
        }

        public override void CaseTNop(TNop node)
        {
            this.index = 47;
        }

        public override void CaseTT(TT node)
        {
            this.index = 48;
        }

        public override void CaseTStringLiteral(TStringLiteral node)
        {
            this.index = 49;
        }

        public override void CaseTIntegerConstant(TIntegerConstant node)
        {
            this.index = 50;
        }

        public override void CaseTFloatConstant(TFloatConstant node)
        {
            this.index = 51;
        }

        public override void CaseEOF(EOF node)
        {
            this.index = 52;
        }
    }
}




