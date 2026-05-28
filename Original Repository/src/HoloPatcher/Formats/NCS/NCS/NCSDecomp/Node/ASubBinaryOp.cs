namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ASubBinaryOp : PBinaryOp
    {
        private TSub _sub;

        public ASubBinaryOp()
        {
        }

        public ASubBinaryOp(TSub sub)
        {
            SetSub(sub);
        }

        public override object Clone()
        {
            return new ASubBinaryOp((TSub)CloneNode(_sub));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TSub GetSub()
        {
            return _sub;
        }

        public void SetSub(TSub node)
        {
            if (_sub != null)
            {
                _sub.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _sub = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_sub == child)
            {
                _sub = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_sub == oldChild)
            {
                SetSub((TSub)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_sub);
        }
    }
}





