namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ABpCommand : PBpCommand
    {
        private PBpOp _bpOp;
        private TIntegerConstant _pos;
        private TIntegerConstant _type;
        private TSemi _semi;

        public ABpCommand()
        {
        }

        public ABpCommand(PBpOp bpOp, TIntegerConstant pos, TIntegerConstant type, TSemi semi)
        {
            SetBpOp(bpOp);
            SetPos(pos);
            SetType(type);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new ABpCommand(
                (PBpOp)CloneNode(_bpOp),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_type),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // Call CaseABpCommand directly if sw is PrunedReversedDepthFirstAdapter or PrunedDepthFirstAdapter
            // This ensures the visitor pattern routes correctly to CaseABpCommand
            if (sw is Analysis.PrunedReversedDepthFirstAdapter prdfa)
            {
                prdfa.CaseABpCommand(this);
            }
            else if (sw is Analysis.PrunedDepthFirstAdapter pdfa)
            {
                pdfa.CaseABpCommand(this);
            }
            else
            {
                sw.DefaultIn(this);
            }
        }

        public PBpOp GetBpOp()
        {
            return _bpOp;
        }

        public void SetBpOp(PBpOp node)
        {
            if (_bpOp != null)
            {
                _bpOp.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _bpOp = node;
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
            if (_bpOp == child)
            {
                _bpOp = null;
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
            if (_semi == child)
            {
                _semi = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_bpOp == oldChild)
            {
                SetBpOp((PBpOp)newChild);
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
            if (_semi == oldChild)
            {
                SetSemi((TSemi)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_bpOp) + ToString(_pos) + ToString(_type) + ToString(_semi);
        }
    }
}





