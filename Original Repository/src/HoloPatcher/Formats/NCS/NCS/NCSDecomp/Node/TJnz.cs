namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TJnz : Token
    {
        public TJnz() : this(0, 0)
        {
        }

        public TJnz(int line, int pos) : base("JNZ")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TJnz(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TJnz text.");
        }
    }
}





