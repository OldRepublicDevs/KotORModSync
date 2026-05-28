namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AGtBinaryOp : PBinaryOp
    {
        private TGt _gt;

        public AGtBinaryOp()
        {
        }

        public AGtBinaryOp(TGt gt)
        {
            SetGt(gt);
        }

        public override object Clone()
        {
            return new AGtBinaryOp((TGt)CloneNode(_gt));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TGt GetGt()
        {
            return _gt;
        }

        public void SetGt(TGt node)
        {
            if (_gt != null)
            {
                _gt.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _gt = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_gt == child)
            {
                _gt = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_gt == oldChild)
            {
                SetGt((TGt)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_gt);
        }
    }
}





