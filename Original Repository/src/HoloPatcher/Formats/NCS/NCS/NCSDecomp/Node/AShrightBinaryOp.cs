namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AShrightBinaryOp : PBinaryOp
    {
        private TShright _shright;

        public AShrightBinaryOp()
        {
        }

        public AShrightBinaryOp(TShright shright)
        {
            SetShright(shright);
        }

        public override object Clone()
        {
            return new AShrightBinaryOp((TShright)CloneNode(_shright));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TShright GetShright()
        {
            return _shright;
        }

        public void SetShright(TShright node)
        {
            if (_shright != null)
            {
                _shright.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _shright = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_shright == child)
            {
                _shright = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_shright == oldChild)
            {
                SetShright((TShright)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_shright);
        }
    }
}





