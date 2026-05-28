namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TCptopsp : Token
    {
        public TCptopsp() : this(0, 0)
        {
        }

        public TCptopsp(int line, int pos) : base("CPTOPSP")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TCptopsp(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TCptopsp text.");
        }
    }
}





