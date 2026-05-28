namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AModBinaryOp : PBinaryOp
    {
        private TMod _mod;

        public AModBinaryOp()
        {
        }

        public AModBinaryOp(TMod mod)
        {
            SetMod(mod);
        }

        public override object Clone()
        {
            return new AModBinaryOp((TMod)CloneNode(_mod));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TMod GetMod()
        {
            return _mod;
        }

        public void SetMod(TMod node)
        {
            if (_mod != null)
            {
                _mod.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _mod = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_mod == child)
            {
                _mod = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_mod == oldChild)
            {
                SetMod((TMod)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_mod);
        }
    }
}





