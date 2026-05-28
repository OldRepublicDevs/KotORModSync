namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TShleft : Token
    {
        public TShleft() : this(0, 0)
        {
        }

        public TShleft(int line, int pos) : base("SHLEFT")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TShleft(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TShleft text.");
        }
    }
}





