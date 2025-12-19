namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AStoreStateCommand : PStoreStateCommand
    {
        private TStorestate _storestate;
        private TIntegerConstant _pos;
        private TIntegerConstant _offset;
        private TIntegerConstant _sizeBp;
        private TIntegerConstant _sizeSp;
        private TSemi _semi;

        public AStoreStateCommand()
        {
        }

        public AStoreStateCommand(TStorestate storestate, TIntegerConstant pos, TIntegerConstant offset, TIntegerConstant sizeBp, TIntegerConstant sizeSp, TSemi semi)
        {
            SetStorestate(storestate);
            SetPos(pos);
            SetOffset(offset);
            SetSizeBp(sizeBp);
            SetSizeSp(sizeSp);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new AStoreStateCommand(
                (TStorestate)CloneNode(_storestate),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_offset),
                (TIntegerConstant)CloneNode(_sizeBp),
                (TIntegerConstant)CloneNode(_sizeSp),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TStorestate GetStorestate()
        {
            return _storestate;
        }

        public void SetStorestate(TStorestate node)
        {
            if (_storestate != null)
            {
                _storestate.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _storestate = node;
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

        public TIntegerConstant GetOffset()
        {
            return _offset;
        }

        public void SetOffset(TIntegerConstant node)
        {
            if (_offset != null)
            {
                _offset.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _offset = node;
        }

        public TIntegerConstant GetSizeBp()
        {
            return _sizeBp;
        }

        public void SetSizeBp(TIntegerConstant node)
        {
            if (_sizeBp != null)
            {
                _sizeBp.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _sizeBp = node;
        }

        public TIntegerConstant GetSizeSp()
        {
            return _sizeSp;
        }

        public void SetSizeSp(TIntegerConstant node)
        {
            if (_sizeSp != null)
            {
                _sizeSp.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _sizeSp = node;
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
            if (_storestate == child)
            {
                _storestate = null;
                return;
            }
            if (_pos == child)
            {
                _pos = null;
                return;
            }
            if (_offset == child)
            {
                _offset = null;
                return;
            }
            if (_sizeBp == child)
            {
                _sizeBp = null;
                return;
            }
            if (_sizeSp == child)
            {
                _sizeSp = null;
                return;
            }
            if (_semi == child)
            {
                _semi = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_storestate == oldChild)
            {
                SetStorestate((TStorestate)newChild);
                return;
            }
            if (_pos == oldChild)
            {
                SetPos((TIntegerConstant)newChild);
                return;
            }
            if (_offset == oldChild)
            {
                SetOffset((TIntegerConstant)newChild);
                return;
            }
            if (_sizeBp == oldChild)
            {
                SetSizeBp((TIntegerConstant)newChild);
                return;
            }
            if (_sizeSp == oldChild)
            {
                SetSizeSp((TIntegerConstant)newChild);
                return;
            }
            if (_semi == oldChild)
            {
                SetSemi((TSemi)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_storestate) + ToString(_pos) + ToString(_offset) + ToString(_sizeBp) + ToString(_sizeSp) + ToString(_semi);
        }
    }
}





