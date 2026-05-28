namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TMovsp : Token
    {
        public TMovsp() : this(0, 0)
        {
        }

        public TMovsp(int line, int pos) : base("MOVSP")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TMovsp(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TMovsp text.");
        }
    }
}





