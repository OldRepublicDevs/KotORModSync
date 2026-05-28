namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TShright : Token
    {
        public TShright() : this(0, 0)
        {
        }

        public TShright(int line, int pos) : base("SHRIGHT")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TShright(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TShright text.");
        }
    }
}





