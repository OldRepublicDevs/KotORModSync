namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TCpdownbp : Token
    {
        public TCpdownbp() : this(0, 0)
        {
        }

        public TCpdownbp(int line, int pos) : base("CPDOWNBP")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TCpdownbp(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TCpdownbp text.");
        }
    }
}





