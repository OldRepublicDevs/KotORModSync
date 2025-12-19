namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AGeqBinaryOp : PBinaryOp
    {
        private TGeq _geq;

        public AGeqBinaryOp()
        {
        }

        public AGeqBinaryOp(TGeq geq)
        {
            SetGeq(geq);
        }

        public override object Clone()
        {
            return new AGeqBinaryOp((TGeq)CloneNode(_geq));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TGeq GetGeq()
        {
            return _geq;
        }

        public void SetGeq(TGeq node)
        {
            if (_geq != null)
            {
                _geq.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _geq = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_geq == child)
            {
                _geq = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_geq == oldChild)
            {
                SetGeq((TGeq)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_geq);
        }
    }
}





