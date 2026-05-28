namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AOrLogiiOp : PLogiiOp
    {
        private TLogorii _logorii;

        public AOrLogiiOp()
        {
        }

        public AOrLogiiOp(TLogorii logorii)
        {
            SetLogorii(logorii);
        }

        public override object Clone()
        {
            return new AOrLogiiOp((TLogorii)CloneNode(_logorii));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TLogorii GetLogorii()
        {
            return _logorii;
        }

        public void SetLogorii(TLogorii node)
        {
            if (_logorii != null)
            {
                _logorii.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _logorii = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_logorii == child)
            {
                _logorii = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_logorii == oldChild)
            {
                SetLogorii((TLogorii)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_logorii);
        }
    }
}





