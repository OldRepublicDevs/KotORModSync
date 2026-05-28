namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TMul : Token
    {
        public TMul() : this(0, 0)
        {
        }

        public TMul(int line, int pos) : base("MUL")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TMul(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TMul text.");
        }
    }
}





