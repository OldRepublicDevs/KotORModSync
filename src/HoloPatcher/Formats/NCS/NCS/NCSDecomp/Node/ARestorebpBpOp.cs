namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ARestorebpBpOp : PBpOp
    {
        private TRestorebp _restorebp;

        public ARestorebpBpOp()
        {
        }

        public ARestorebpBpOp(TRestorebp restorebp)
        {
            SetRestorebp(restorebp);
        }

        public override object Clone()
        {
            return new ARestorebpBpOp((TRestorebp)CloneNode(_restorebp));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TRestorebp GetRestorebp()
        {
            return _restorebp;
        }

        public void SetRestorebp(TRestorebp node)
        {
            if (_restorebp != null)
            {
                _restorebp.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _restorebp = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_restorebp == child)
            {
                _restorebp = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_restorebp == oldChild)
            {
                SetRestorebp((TRestorebp)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_restorebp);
        }
    }
}





