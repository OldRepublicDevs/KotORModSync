namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ALeqBinaryOp : PBinaryOp
    {
        private TLeq _leq;

        public ALeqBinaryOp()
        {
        }

        public ALeqBinaryOp(TLeq leq)
        {
            SetLeq(leq);
        }

        public override object Clone()
        {
            return new ALeqBinaryOp((TLeq)CloneNode(_leq));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TLeq GetLeq()
        {
            return _leq;
        }

        public void SetLeq(TLeq node)
        {
            if (_leq != null)
            {
                _leq.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _leq = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_leq == child)
            {
                _leq = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_leq == oldChild)
            {
                SetLeq((TLeq)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_leq);
        }
    }
}





