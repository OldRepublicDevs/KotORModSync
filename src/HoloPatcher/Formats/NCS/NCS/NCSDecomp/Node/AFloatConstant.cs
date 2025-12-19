namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AFloatConstant : PConstant
    {
        private TFloatConstant _floatConstant;

        public AFloatConstant()
        {
        }

        public AFloatConstant(TFloatConstant floatConstant)
        {
            SetFloatConstant(floatConstant);
        }

        public override object Clone()
        {
            return new AFloatConstant((TFloatConstant)CloneNode(_floatConstant));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TFloatConstant GetFloatConstant()
        {
            return _floatConstant;
        }

        public void SetFloatConstant(TFloatConstant node)
        {
            if (_floatConstant != null)
            {
                _floatConstant.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _floatConstant = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_floatConstant == child)
            {
                _floatConstant = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_floatConstant == oldChild)
            {
                SetFloatConstant((TFloatConstant)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_floatConstant);
        }
    }
}





