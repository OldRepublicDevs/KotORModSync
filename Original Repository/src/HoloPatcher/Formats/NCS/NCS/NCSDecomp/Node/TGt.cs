namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TGt : Token
    {
        public TGt() : this(0, 0)
        {
        }

        public TGt(int line, int pos) : base("GT")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TGt(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TGt text.");
        }
    }
}





