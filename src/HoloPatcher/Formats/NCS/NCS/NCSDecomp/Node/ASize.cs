namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ASize : PSize
    {
        private TT _t;
        private TIntegerConstant _pos;
        private TIntegerConstant _integerConstant;
        private TSemi _semi;

        public ASize()
        {
        }

        public ASize(TT t, TIntegerConstant pos, TIntegerConstant integerConstant, TSemi semi)
        {
            SetT(t);
            SetPos(pos);
            SetIntegerConstant(integerConstant);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new ASize(
                (TT)CloneNode(_t),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_integerConstant),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TT GetT()
        {
            return _t;
        }

        public void SetT(TT node)
        {
            if (_t != null)
            {
                _t.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _t = node;
        }

        public TIntegerConstant GetPos()
        {
            return _pos;
        }

        public void SetPos(TIntegerConstant node)
        {
            if (_pos != null)
            {
                _pos.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _pos = node;
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

        public TSemi GetSemi()
        {
            return _semi;
        }

        public void SetSemi(TSemi node)
        {
            if (_semi != null)
            {
                _semi.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _semi = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_t == child)
            {
                _t = null;
                return;
            }
            if (_pos == child)
            {
                _pos = null;
                return;
            }
            if (_integerConstant == child)
            {
                _integerConstant = null;
                return;
            }
            if (_semi == child)
            {
                _semi = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_t == oldChild)
            {
                SetT((TT)newChild);
                return;
            }
            if (_pos == oldChild)
            {
                SetPos((TIntegerConstant)newChild);
                return;
            }
            if (_integerConstant == oldChild)
            {
                SetIntegerConstant((TIntegerConstant)newChild);
                return;
            }
            if (_semi == oldChild)
            {
                SetSemi((TSemi)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_t) + ToString(_pos) + ToString(_integerConstant) + ToString(_semi);
        }
    }
}





