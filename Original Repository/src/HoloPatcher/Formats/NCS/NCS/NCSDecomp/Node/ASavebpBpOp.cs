namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ASavebpBpOp : PBpOp
    {
        private TSavebp _savebp;

        public ASavebpBpOp()
        {
        }

        public ASavebpBpOp(TSavebp savebp)
        {
            SetSavebp(savebp);
        }

        public override object Clone()
        {
            return new ASavebpBpOp((TSavebp)CloneNode(_savebp));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TSavebp GetSavebp()
        {
            return _savebp;
        }

        public void SetSavebp(TSavebp node)
        {
            if (_savebp != null)
            {
                _savebp.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _savebp = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_savebp == child)
            {
                _savebp = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_savebp == oldChild)
            {
                SetSavebp((TSavebp)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_savebp);
        }
    }
}





