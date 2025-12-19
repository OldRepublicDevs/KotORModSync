// 
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using KOTORModSync.Formats.NCS.NCSDecomp.Analysis;
using KOTORModSync.Formats.NCS.NCSDecomp.AST;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    public sealed class TDecibp : Token
    {
        public TDecibp()
        {
            base.SetText("DECIBP");
        }

        public TDecibp(int line, int pos)
        {
            base.SetText("DECIBP");
            this.SetLine(line);
            this.SetPos(pos);
        }

        public override object Clone()
        {
            return new TDecibp(this.GetLine(), this.GetPos());
        }
        public override void Apply(Switch sw)
        {
            ((IAnalysis)sw).CaseTDecibp(this);
        }

        public override void SetText(string text)
        {
            throw new Exception("Cannot change TDecibp text.");
        }
    }
}




