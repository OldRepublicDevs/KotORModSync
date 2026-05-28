namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TT : Token
    {
        public TT() : base("T")
        {
        }

        public TT(int line, int pos) : base("T")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TT(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TT text.");
        }
    }
}





