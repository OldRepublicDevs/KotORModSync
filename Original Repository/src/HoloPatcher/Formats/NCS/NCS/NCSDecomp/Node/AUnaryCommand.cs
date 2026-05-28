namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AUnaryCommand : PUnaryCommand
    {
        private PUnaryOp _unaryOp;
        private TIntegerConstant _pos;
        private TIntegerConstant _type;
        private TSemi _semi;

        public AUnaryCommand()
        {
        }

        public AUnaryCommand(PUnaryOp unaryOp, TIntegerConstant pos, TIntegerConstant type, TSemi semi)
        {
            SetUnaryOp(unaryOp);
            SetPos(pos);
            SetType(type);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new AUnaryCommand(
                (PUnaryOp)CloneNode(_unaryOp),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_type),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // CRITICAL: Must call CaseAUnaryCommand, not just DefaultIn!
            // This enables the visitor pattern to properly process unary commands (NEGI, NEGF, NOTI, COMPI)
            // Without this, negation operations are not applied to values (e.g., -6 becomes 6)
            if (sw is Analysis.PrunedReversedDepthFirstAdapter prdfa)
            {
                prdfa.CaseAUnaryCommand(this);
            }
            else if (sw is Analysis.PrunedDepthFirstAdapter pdfa)
            {
                pdfa.CaseAUnaryCommand(this);
            }
            else
            {
                sw.DefaultIn(this);
            }
        }

        public PUnaryOp GetUnaryOp()
        {
            return _unaryOp;
        }

        public void SetUnaryOp(PUnaryOp node)
        {
            if (_unaryOp != null)
            {
                _unaryOp.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _unaryOp = node;
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
            if (_unaryOp == child)
            {
                _unaryOp = null;
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
            if (_unaryOp == oldChild)
            {
                SetUnaryOp((PUnaryOp)newChild);
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
            return ToString(_unaryOp) + ToString(_pos) + ToString(_type) + ToString(_semi);
        }
    }
}





