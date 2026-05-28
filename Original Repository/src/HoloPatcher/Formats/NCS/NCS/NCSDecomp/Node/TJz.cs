namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TJz : Token
    {
        public TJz() : this(0, 0)
        {
        }

        public TJz(int line, int pos) : base("JZ")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TJz(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TJz text.");
        }
    }
}





