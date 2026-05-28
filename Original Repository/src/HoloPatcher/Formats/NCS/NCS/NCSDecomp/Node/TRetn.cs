namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TRetn : Token
    {
        public TRetn() : this(0, 0)
        {
        }

        public TRetn(int line, int pos) : base("RETN")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TRetn(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TRetn text.");
        }
    }
}





