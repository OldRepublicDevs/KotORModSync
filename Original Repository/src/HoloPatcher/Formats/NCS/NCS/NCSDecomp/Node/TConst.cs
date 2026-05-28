namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TConst : Token
    {
        public TConst() : this(0, 0)
        {
        }

        public TConst(int line, int pos) : base("CONST")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TConst(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TConst text.");
        }
    }
}





