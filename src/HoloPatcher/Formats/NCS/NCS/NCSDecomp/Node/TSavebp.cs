namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TSavebp : Token
    {
        public TSavebp() : this(0, 0)
        {
        }

        public TSavebp(int line, int pos) : base("SAVEBP")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TSavebp(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TSavebp text.");
        }
    }
}





