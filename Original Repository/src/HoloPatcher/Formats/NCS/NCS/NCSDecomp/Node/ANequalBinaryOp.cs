namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ANequalBinaryOp : PBinaryOp
    {
        private TNequal _nequal;

        public ANequalBinaryOp()
        {
        }

        public ANequalBinaryOp(TNequal nequal)
        {
            SetNequal(nequal);
        }

        public override object Clone()
        {
            return new ANequalBinaryOp((TNequal)CloneNode(_nequal));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TNequal GetNequal()
        {
            return _nequal;
        }

        public void SetNequal(TNequal node)
        {
            if (_nequal != null)
            {
                _nequal.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _nequal = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_nequal == child)
            {
                _nequal = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_nequal == oldChild)
            {
                SetNequal((TNequal)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_nequal);
        }
    }
}





