namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TUnright : Token
    {
        public TUnright() : this(0, 0)
        {
        }

        public TUnright(int line, int pos) : base("UNRIGHT")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TUnright(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TUnright text.");
        }
    }
}





