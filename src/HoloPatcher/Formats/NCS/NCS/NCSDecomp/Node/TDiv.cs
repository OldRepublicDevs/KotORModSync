namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TDiv : Token
    {
        public TDiv() : this(0, 0)
        {
        }

        public TDiv(int line, int pos) : base("DIV")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TDiv(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TDiv text.");
        }
    }
}





