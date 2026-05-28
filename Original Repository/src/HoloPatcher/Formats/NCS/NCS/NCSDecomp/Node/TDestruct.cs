namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TDestruct : Token
    {
        public TDestruct() : this(0, 0)
        {
        }

        public TDestruct(int line, int pos) : base("DESTRUCT")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TDestruct(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TDestruct text.");
        }
    }
}





