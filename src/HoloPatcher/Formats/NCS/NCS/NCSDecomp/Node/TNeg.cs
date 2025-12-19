namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TNeg : Token
    {
        public TNeg() : this(0, 0)
        {
        }

        public TNeg(int line, int pos) : base("NEG")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TNeg(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TNeg text.");
        }
    }
}





