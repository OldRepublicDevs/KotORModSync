namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TLogorii : Token
    {
        public TLogorii() : this(0, 0)
        {
        }

        public TLogorii(int line, int pos) : base("LOGORII")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TLogorii(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TLogorii text.");
        }
    }
}





