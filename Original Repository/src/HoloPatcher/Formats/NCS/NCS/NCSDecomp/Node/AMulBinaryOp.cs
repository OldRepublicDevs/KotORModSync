namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AMulBinaryOp : PBinaryOp
    {
        private TMul _mul;

        public AMulBinaryOp()
        {
        }

        public AMulBinaryOp(TMul mul)
        {
            SetMul(mul);
        }

        public override object Clone()
        {
            return new AMulBinaryOp((TMul)CloneNode(_mul));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TMul GetMul()
        {
            return _mul;
        }

        public void SetMul(TMul node)
        {
            if (_mul != null)
            {
                _mul.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _mul = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_mul == child)
            {
                _mul = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_mul == oldChild)
            {
                SetMul((TMul)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_mul);
        }
    }
}





