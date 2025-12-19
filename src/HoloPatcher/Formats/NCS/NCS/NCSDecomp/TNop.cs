// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/TNop.java:9-33
// Original: public final class TNop extends Token { public TNop() { super.setText("NOP"); } ... public void setText(String text) { throw new RuntimeException("Cannot change TNop text."); } }
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class TNop : Token
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/node/TNop.java:10-23
        // Original: public TNop() { super.setText("NOP"); } public TNop(int line, int pos) { super.setText("NOP"); ... }
        public TNop()
        {
            base.SetText("NOP");
        }

        public TNop(int line, int pos)
        {
            base.SetText("NOP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TNop(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTNop(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TNop text.");
        }
    }
}




