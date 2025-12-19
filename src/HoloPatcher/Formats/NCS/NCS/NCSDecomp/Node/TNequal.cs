namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TNequal : Token
    {
        public TNequal() : this(0, 0)
        {
        }

        public TNequal(int line, int pos) : base("NEQUAL")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TNequal(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TNequal text.");
        }
    }
}






