namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TEqual : Token
    {
        public TEqual() : this(0, 0)
        {
        }

        public TEqual(int line, int pos) : base("EQUAL")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TEqual(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TEqual text.");
        }
    }
}





