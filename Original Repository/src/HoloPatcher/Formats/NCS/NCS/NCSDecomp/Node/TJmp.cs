namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TJmp : Token
    {
        public TJmp() : this(0, 0)
        {
        }

        public TJmp(int line, int pos) : base("JMP")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TJmp(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TJmp text.");
        }
    }
}





