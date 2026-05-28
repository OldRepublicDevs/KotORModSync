namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TAdd : Token
    {
        public TAdd() : this(0, 0)
        {
        }

        public TAdd(int line, int pos) : base("ADD")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TAdd(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TAdd text.");
        }
    }
}





