namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AAndLogiiOp : PLogiiOp
    {
        private TLogandii _logandii;

        public AAndLogiiOp()
        {
        }

        public AAndLogiiOp(TLogandii logandii)
        {
            SetLogandii(logandii);
        }

        public override object Clone()
        {
            return new AAndLogiiOp((TLogandii)CloneNode(_logandii));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TLogandii GetLogandii()
        {
            return _logandii;
        }

        public void SetLogandii(TLogandii node)
        {
            if (_logandii != null)
            {
                _logandii.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _logandii = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_logandii == child)
            {
                _logandii = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_logandii == oldChild)
            {
                SetLogandii((TLogandii)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_logandii);
        }
    }
}





