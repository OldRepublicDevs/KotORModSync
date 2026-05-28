namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ALtBinaryOp : PBinaryOp
    {
        private TLt _lt;

        public ALtBinaryOp()
        {
        }

        public ALtBinaryOp(TLt lt)
        {
            SetLt(lt);
        }

        public override object Clone()
        {
            return new ALtBinaryOp((TLt)CloneNode(_lt));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TLt GetLt()
        {
            return _lt;
        }

        public void SetLt(TLt node)
        {
            if (_lt != null)
            {
                _lt.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _lt = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_lt == child)
            {
                _lt = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_lt == oldChild)
            {
                SetLt((TLt)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_lt);
        }
    }
}





