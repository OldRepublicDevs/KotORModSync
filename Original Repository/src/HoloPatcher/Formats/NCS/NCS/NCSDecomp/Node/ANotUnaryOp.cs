namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ANotUnaryOp : PUnaryOp
    {
        private TNot _not;

        public ANotUnaryOp()
        {
        }

        public ANotUnaryOp(TNot not)
        {
            SetNot(not);
        }

        public override object Clone()
        {
            return new ANotUnaryOp((TNot)CloneNode(_not));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TNot GetNot()
        {
            return _not;
        }

        public void SetNot(TNot node)
        {
            if (_not != null)
            {
                _not.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _not = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_not == child)
            {
                _not = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_not == oldChild)
            {
                SetNot((TNot)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_not);
        }
    }
}





