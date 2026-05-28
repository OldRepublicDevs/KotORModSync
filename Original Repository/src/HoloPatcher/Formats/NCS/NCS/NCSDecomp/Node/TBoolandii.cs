namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TBoolandii : Token
    {
        public TBoolandii() : this(0, 0)
        {
        }

        public TBoolandii(int line, int pos) : base("BOOLANDII")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TBoolandii(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TBoolandii text.");
        }
    }
}





