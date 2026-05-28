namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AExclOrLogiiOp : PLogiiOp
    {
        private TExcorii _excorii;

        public AExclOrLogiiOp()
        {
        }

        public AExclOrLogiiOp(TExcorii excorii)
        {
            SetExcorii(excorii);
        }

        public override object Clone()
        {
            return new AExclOrLogiiOp((TExcorii)CloneNode(_excorii));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TExcorii GetExcorii()
        {
            return _excorii;
        }

        public void SetExcorii(TExcorii node)
        {
            if (_excorii != null)
            {
                _excorii.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _excorii = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_excorii == child)
            {
                _excorii = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_excorii == oldChild)
            {
                SetExcorii((TExcorii)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_excorii);
        }
    }
}





