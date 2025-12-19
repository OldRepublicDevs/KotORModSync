namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TLogandii : Token
    {
        public TLogandii() : this(0, 0)
        {
        }

        public TLogandii(int line, int pos) : base("LOGANDII")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TLogandii(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TLogandii text.");
        }
    }
}





