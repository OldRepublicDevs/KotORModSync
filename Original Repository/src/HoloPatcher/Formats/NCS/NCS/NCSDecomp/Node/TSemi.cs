namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TSemi : Token
    {
        public TSemi() : this(0, 0)
        {
        }

        public TSemi(int line, int pos) : base(";")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TSemi(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TSemi text.");
        }
    }
}





