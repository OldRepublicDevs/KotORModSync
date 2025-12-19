namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TMod : Token
    {
        public TMod() : this(0, 0)
        {
        }

        public TMod(int line, int pos) : base("MOD")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TMod(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TMod text.");
        }
    }
}





