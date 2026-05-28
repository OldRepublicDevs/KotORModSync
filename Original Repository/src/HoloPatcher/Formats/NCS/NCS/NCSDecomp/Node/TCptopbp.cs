namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TCptopbp : Token
    {
        public TCptopbp() : this(0, 0)
        {
        }

        public TCptopbp(int line, int pos) : base("CPTOPBP")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TCptopbp(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TCptopbp text.");
        }
    }
}





