namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ANonzeroJumpIf : PJumpIf
    {
        private TJnz _jnz;

        public ANonzeroJumpIf()
        {
        }

        public ANonzeroJumpIf(TJnz jnz)
        {
            SetJnz(jnz);
        }

        public override object Clone()
        {
            return new ANonzeroJumpIf((TJnz)CloneNode(_jnz));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TJnz GetJnz()
        {
            return _jnz;
        }

        public void SetJnz(TJnz node)
        {
            if (_jnz != null)
            {
                _jnz.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _jnz = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_jnz == child)
            {
                _jnz = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_jnz == oldChild)
            {
                SetJnz((TJnz)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_jnz);
        }
    }
}





