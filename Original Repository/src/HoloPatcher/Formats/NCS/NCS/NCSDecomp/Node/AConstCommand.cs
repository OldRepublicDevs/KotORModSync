namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AConstCommand : PConstCommand
    {
        private TConst _const;
        private TIntegerConstant _pos;
        private TIntegerConstant _type;
        private PConstant _constant;
        private TSemi _semi;

        public AConstCommand()
        {
        }

        public AConstCommand(TConst constToken, TIntegerConstant pos, TIntegerConstant type, PConstant constant, TSemi semi)
        {
            SetConst(constToken);
            SetPos(pos);
            SetType(type);
            SetConstant(constant);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new AConstCommand(
                (TConst)CloneNode(_const),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_type),
                (PConstant)CloneNode(_constant),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // Call CaseAConstCommand directly if sw is PrunedReversedDepthFirstAdapter or PrunedDepthFirstAdapter
            // This ensures the visitor pattern routes correctly to CaseAConstCommand
            if (sw is Analysis.PrunedReversedDepthFirstAdapter prdfa)
            {
                prdfa.CaseAConstCommand(this);
            }
            else if (sw is Analysis.PrunedDepthFirstAdapter pdfa)
            {
                pdfa.CaseAConstCommand(this);
            }
            else
            {
                sw.DefaultIn(this);
            }
        }

        public TConst GetConst()
        {
            return _const;
        }

        public void SetConst(TConst node)
        {
            if (_const != null)
            {
                _const.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _const = node;
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

        public new TIntegerConstant GetType()
        {
            return _type;
        }

        public void SetType(TIntegerConstant node)
        {
            if (_type != null)
            {
                _type.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _type = node;
        }

        public PConstant GetConstant()
        {
            return _constant;
        }

        public void SetConstant(PConstant node)
        {
            if (_constant != null)
            {
                _constant.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _constant = node;
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
            if (_const == child)
            {
                _const = null;
                return;
            }
            if (_pos == child)
            {
                _pos = null;
                return;
            }
            if (_type == child)
            {
                _type = null;
                return;
            }
            if (_constant == child)
            {
                _constant = null;
                return;
            }
            if (_semi == child)
            {
                _semi = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_const == oldChild)
            {
                SetConst((TConst)newChild);
                return;
            }
            if (_pos == oldChild)
            {
                SetPos((TIntegerConstant)newChild);
                return;
            }
            if (_type == oldChild)
            {
                SetType((TIntegerConstant)newChild);
                return;
            }
            if (_constant == oldChild)
            {
                SetConstant((PConstant)newChild);
                return;
            }
            if (_semi == oldChild)
            {
                SetSemi((TSemi)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_const) + ToString(_pos) + ToString(_type) + ToString(_constant) + ToString(_semi);
        }
    }
}





