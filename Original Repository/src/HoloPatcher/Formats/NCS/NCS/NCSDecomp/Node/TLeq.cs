namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TLeq : Token
    {
        public TLeq() : this(0, 0)
        {
        }

        public TLeq(int line, int pos) : base("LEQ")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TLeq(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TLeq text.");
        }
    }
}





