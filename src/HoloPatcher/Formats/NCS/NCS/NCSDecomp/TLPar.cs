// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/TLPar.java:9-33
// Original: public final class TLPar extends Token { public TLPar() { super.setText("("); } ... public void setText(String text) { throw new RuntimeException("Cannot change TLPar text."); } }
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class TLPar : Token
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/TLPar.java:10-23
        // Original: public TLPar() { super.setText("("); } public TLPar(int line, int pos) { super.setText("("); ... }
        public TLPar()
        {
            base.SetText("(");
        }

        public TLPar(int line, int pos)
        {
            base.SetText("(");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TLPar(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTLPar(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TLPar text.");
        }
    }
}




