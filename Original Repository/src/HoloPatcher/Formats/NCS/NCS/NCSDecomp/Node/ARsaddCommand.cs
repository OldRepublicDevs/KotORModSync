using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;

namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ARsaddCommand : PRsaddCommand
    {
        private TRsadd _rsadd;
        private TIntegerConstant _pos;
        private TIntegerConstant _type;
        private TSemi _semi;

        public ARsaddCommand()
        {
        }

        public ARsaddCommand(TRsadd rsadd, TIntegerConstant pos, TIntegerConstant type, TSemi semi)
        {
            SetRsadd(rsadd);
            SetPos(pos);
            SetType(type);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new ARsaddCommand(
                (TRsadd)CloneNode(_rsadd),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_type),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // Call CaseARsaddCommand directly if sw is PrunedReversedDepthFirstAdapter or PrunedDepthFirstAdapter
            // This ensures the visitor pattern routes correctly to CaseARsaddCommand
            Debug($"DEBUG AST.ARsaddCommand.Apply: sw type={sw.GetType().Name}");
            if (sw is Analysis.PrunedReversedDepthFirstAdapter prdfa)
            {
                Debug($"DEBUG AST.ARsaddCommand.Apply: routing to PrunedReversedDepthFirstAdapter.CaseARsaddCommand");
                prdfa.CaseARsaddCommand(this);
            }
            else if (sw is Analysis.PrunedDepthFirstAdapter pdfa)
            {
                Debug($"DEBUG AST.ARsaddCommand.Apply: routing to PrunedDepthFirstAdapter.CaseARsaddCommand");
                pdfa.CaseARsaddCommand(this);
            }
            else
            {
                Debug($"DEBUG AST.ARsaddCommand.Apply: routing to DefaultIn");
                sw.DefaultIn(this);
            }
        }

        public TRsadd GetRsadd()
        {
            return _rsadd;
        }

        public void SetRsadd(TRsadd node)
        {
            if (_rsadd != null)
            {
                _rsadd.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _rsadd = node;
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
            if (_rsadd == child)
            {
                _rsadd = null;
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
            if (_rsadd == oldChild)
            {
                SetRsadd((TRsadd)newChild);
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
            return ToString(_rsadd) + ToString(_pos) + ToString(_type) + ToString(_semi);
        }
    }
}





