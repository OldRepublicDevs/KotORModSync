namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AIntConstant : PConstant
    {
        private TIntegerConstant _integerConstant;

        public AIntConstant()
        {
        }

        public AIntConstant(TIntegerConstant integerConstant)
        {
            SetIntegerConstant(integerConstant);
        }

        public override object Clone()
        {
            return new AIntConstant((TIntegerConstant)CloneNode(_integerConstant));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TIntegerConstant GetIntegerConstant()
        {
            return _integerConstant;
        }

        public void SetIntegerConstant(TIntegerConstant node)
        {
            if (_integerConstant != null)
            {
                _integerConstant.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _integerConstant = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_integerConstant == child)
            {
                _integerConstant = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_integerConstant == oldChild)
            {
                SetIntegerConstant((TIntegerConstant)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_integerConstant);
        }
    }
}





