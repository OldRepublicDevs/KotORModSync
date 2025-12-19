namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TExcorii : Token
    {
        public TExcorii() : this(0, 0)
        {
        }

        public TExcorii(int line, int pos) : base("EXCORII")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TExcorii(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TExcorii text.");
        }
    }
}





