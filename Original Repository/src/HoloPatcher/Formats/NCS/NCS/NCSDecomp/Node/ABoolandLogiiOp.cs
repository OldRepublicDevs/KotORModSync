namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ABoolandLogiiOp : PLogiiOp
    {
        private TBoolandii _boolandii;

        public ABoolandLogiiOp()
        {
        }

        public ABoolandLogiiOp(TBoolandii boolandii)
        {
            SetBoolandii(boolandii);
        }

        public override object Clone()
        {
            return new ABoolandLogiiOp((TBoolandii)CloneNode(_boolandii));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TBoolandii GetBoolandii()
        {
            return _boolandii;
        }

        public void SetBoolandii(TBoolandii node)
        {
            if (_boolandii != null)
            {
                _boolandii.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _boolandii = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_boolandii == child)
            {
                _boolandii = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_boolandii == oldChild)
            {
                SetBoolandii((TBoolandii)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_boolandii);
        }
    }
}





