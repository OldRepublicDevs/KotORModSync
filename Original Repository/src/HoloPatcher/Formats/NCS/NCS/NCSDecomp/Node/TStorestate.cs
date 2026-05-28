namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class TStorestate : Token
    {
        public TStorestate() : this(0, 0)
        {
        }

        public TStorestate(int line, int pos) : base("STORE_STATE")
        {
            SetLine(line);
            SetPos(pos);
        }

        public override object Clone()
        {
            return new TStorestate(GetLine(), GetPos());
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public new void SetText(string text)
        {
            throw new System.Exception("Cannot change TStorestate text.");
        }
    }
}





