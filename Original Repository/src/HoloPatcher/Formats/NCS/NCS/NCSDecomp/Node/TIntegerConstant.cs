namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TIntegerConstant : Token
    {
        public TIntegerConstant() : this("0", 0, 0)
        {
        }

        public TIntegerConstant(string text, int line, int pos) : base(text)
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TIntegerConstant(GetText(), GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }
    }
}





