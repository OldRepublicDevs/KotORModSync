namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ACompUnaryOp : PUnaryOp
    {
        private TComp _comp;

        public ACompUnaryOp()
        {
        }

        public ACompUnaryOp(TComp comp)
        {
            SetComp(comp);
        }

        public override object Clone()
        {
            return new ACompUnaryOp((TComp)CloneNode(_comp));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TComp GetComp()
        {
            return _comp;
        }

        public void SetComp(TComp node)
        {
            if (_comp != null)
            {
                _comp.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _comp = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_comp == child)
            {
                _comp = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_comp == oldChild)
            {
                SetComp((TComp)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_comp);
        }
    }
}





