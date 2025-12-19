// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/TRPar.java:9-33
// Original: public final class TRPar extends Token { public TRPar() { super.setText(")"); } ... public void setText(String text) { throw new RuntimeException("Cannot change TRPar text."); } }
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class TRPar : Token
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/TRPar.java:10-23
        // Original: public TRPar() { super.setText(")"); } public TRPar(int line, int pos) { super.setText(")"); ... }
        public TRPar()
        {
            base.SetText(")");
        }

        public TRPar(int line, int pos)
        {
            base.SetText(")");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TRPar(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTRPar(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TRPar text.");
        }
    }
}




