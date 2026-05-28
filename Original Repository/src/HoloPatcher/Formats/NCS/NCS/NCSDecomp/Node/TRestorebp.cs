namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TRestorebp : Token
    {
        public TRestorebp() : this(0, 0)
        {
        }

        public TRestorebp(int line, int pos) : base("RESTOREBP")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TRestorebp(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TRestorebp text.");
        }
    }
}





