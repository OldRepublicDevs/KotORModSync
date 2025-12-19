namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TGeq : Token
    {
        public TGeq() : this(0, 0)
        {
        }

        public TGeq(int line, int pos) : base("GEQ")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TGeq(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TGeq text.");
        }
    }
}





