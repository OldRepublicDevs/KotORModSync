// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/TDot.java:9-33
// Original: public final class TDot extends Token { public TDot() { super.setText("."); } ... public void setText(String text) { throw new RuntimeException("Cannot change TDot text."); } }
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class TDot : Token
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/TDot.java:10-23
        // Original: public TDot() { super.setText("."); } public TDot(int line, int pos) { super.setText("."); ... }
        public TDot()
        {
            base.SetText(".");
        }

        public TDot(int line, int pos)
        {
            base.SetText(".");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TDot(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTDot(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TDot text.");
        }
    }
}




