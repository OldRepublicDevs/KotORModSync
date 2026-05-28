namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TFloatConstant : Token
    {
        public TFloatConstant() : this("0.0", 0, 0)
        {
        }

        public TFloatConstant(string text, int line, int pos) : base(text)
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TFloatConstant(GetText(), GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }
    }
}





