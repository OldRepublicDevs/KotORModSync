namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TComp : Token
    {
        public TComp() : this(0, 0)
        {
        }

        public TComp(int line, int pos) : base("COMP")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TComp(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TComp text.");
        }
    }
}





