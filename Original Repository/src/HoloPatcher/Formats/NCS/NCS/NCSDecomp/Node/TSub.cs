namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TSub : Token
    {
        public TSub() : this(0, 0)
        {
        }

        public TSub(int line, int pos) : base("SUB")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TSub(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TSub text.");
        }
    }
}





