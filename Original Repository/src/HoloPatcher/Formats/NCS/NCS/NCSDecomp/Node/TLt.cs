namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TLt : Token
    {
        public TLt() : this(0, 0)
        {
        }

        public TLt(int line, int pos) : base("LT")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TLt(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TLt text.");
        }
    }
}





