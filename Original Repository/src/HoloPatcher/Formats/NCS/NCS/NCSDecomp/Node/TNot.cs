namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TNot : Token
    {
        public TNot() : this(0, 0)
        {
        }

        public TNot(int line, int pos) : base("NOT")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TNot(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TNot text.");
        }
    }
}





