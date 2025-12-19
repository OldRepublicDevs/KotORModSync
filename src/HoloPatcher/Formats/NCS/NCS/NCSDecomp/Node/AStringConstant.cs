namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AStringConstant : PConstant
    {
        private TStringLiteral _stringLiteral;

        public AStringConstant()
        {
        }

        public AStringConstant(TStringLiteral stringLiteral)
        {
            SetStringLiteral(stringLiteral);
        }

        public override object Clone()
        {
            return new AStringConstant((TStringLiteral)CloneNode(_stringLiteral));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TStringLiteral GetStringLiteral()
        {
            return _stringLiteral;
        }

        public void SetStringLiteral(TStringLiteral node)
        {
            if (_stringLiteral != null)
            {
                _stringLiteral.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _stringLiteral = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_stringLiteral == child)
            {
                _stringLiteral = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_stringLiteral == oldChild)
            {
                SetStringLiteral((TStringLiteral)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_stringLiteral);
        }
    }
}





