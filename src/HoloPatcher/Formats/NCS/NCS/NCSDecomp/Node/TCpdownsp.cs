namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TCpdownsp : Token
    {
        public TCpdownsp() : this(0, 0)
        {
        }

        public TCpdownsp(int line, int pos) : base("CPDOWNSP")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TCpdownsp(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TCpdownsp text.");
        }
    }
}





