namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AZeroJumpIf : PJumpIf
    {
        private TJz _jz;

        public AZeroJumpIf()
        {
        }

        public AZeroJumpIf(TJz jz)
        {
            SetJz(jz);
        }

        public override object Clone()
        {
            return new AZeroJumpIf((TJz)CloneNode(_jz));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TJz GetJz()
        {
            return _jz;
        }

        public void SetJz(TJz node)
        {
            if (_jz != null)
            {
                _jz.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _jz = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_jz == child)
            {
                _jz = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_jz == oldChild)
            {
                SetJz((TJz)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_jz);
        }
    }
}





