namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AInclOrLogiiOp : PLogiiOp
    {
        private TIncorii _incorii;

        public AInclOrLogiiOp()
        {
        }

        public AInclOrLogiiOp(TIncorii incorii)
        {
            SetIncorii(incorii);
        }

        public override object Clone()
        {
            return new AInclOrLogiiOp((TIncorii)CloneNode(_incorii));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TIncorii GetIncorii()
        {
            return _incorii;
        }

        public void SetIncorii(TIncorii node)
        {
            if (_incorii != null)
            {
                _incorii.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _incorii = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_incorii == child)
            {
                _incorii = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_incorii == oldChild)
            {
                SetIncorii((TIncorii)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_incorii);
        }
    }
}





