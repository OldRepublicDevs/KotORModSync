namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AAddBinaryOp : PBinaryOp
    {
        private TAdd _add;

        public AAddBinaryOp()
        {
        }

        public AAddBinaryOp(TAdd add)
        {
            SetAdd(add);
        }

        public override object Clone()
        {
            return new AAddBinaryOp((TAdd)CloneNode(_add));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TAdd GetAdd()
        {
            return _add;
        }

        public void SetAdd(TAdd node)
        {
            if (_add != null)
            {
                _add.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _add = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_add == child)
            {
                _add = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_add == oldChild)
            {
                SetAdd((TAdd)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_add);
        }
    }
}





