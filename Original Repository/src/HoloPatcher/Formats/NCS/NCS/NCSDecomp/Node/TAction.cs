namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TAction : Token
    {
        public TAction() : this(0, 0)
        {
        }

        public TAction(int line, int pos) : base("ACTION")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TAction(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TAction text.");
        }
    }
}





