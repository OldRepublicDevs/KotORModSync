namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ADivBinaryOp : PBinaryOp
    {
        private TDiv _div;

        public ADivBinaryOp()
        {
        }

        public ADivBinaryOp(TDiv div)
        {
            SetDiv(div);
        }

        public override object Clone()
        {
            return new ADivBinaryOp((TDiv)CloneNode(_div));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TDiv GetDiv()
        {
            return _div;
        }

        public void SetDiv(TDiv node)
        {
            if (_div != null)
            {
                _div.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _div = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_div == child)
            {
                _div = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_div == oldChild)
            {
                SetDiv((TDiv)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_div);
        }
    }
}





