namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AUnrightBinaryOp : PBinaryOp
    {
        private TUnright _unright;

        public AUnrightBinaryOp()
        {
        }

        public AUnrightBinaryOp(TUnright unright)
        {
            SetUnright(unright);
        }

        public override object Clone()
        {
            return new AUnrightBinaryOp((TUnright)CloneNode(_unright));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TUnright GetUnright()
        {
            return _unright;
        }

        public void SetUnright(TUnright node)
        {
            if (_unright != null)
            {
                _unright.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _unright = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_unright == child)
            {
                _unright = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_unright == oldChild)
            {
                SetUnright((TUnright)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_unright);
        }
    }
}





