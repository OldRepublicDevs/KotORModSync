namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ABinaryCommand : PBinaryCommand
    {
        private PBinaryOp _binaryOp;
        private TIntegerConstant _pos;
        private TIntegerConstant _type;
        private TIntegerConstant _size;
        private TSemi _semi;

        public ABinaryCommand()
        {
        }

        public ABinaryCommand(PBinaryOp binaryOp, TIntegerConstant pos, TIntegerConstant type, TIntegerConstant size, TSemi semi)
        {
            SetBinaryOp(binaryOp);
            SetPos(pos);
            SetType(type);
            SetSize(size);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new ABinaryCommand(
                (PBinaryOp)CloneNode(_binaryOp),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_type),
                (TIntegerConstant)CloneNode(_size),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PBinaryOp GetBinaryOp()
        {
            return _binaryOp;
        }

        public void SetBinaryOp(PBinaryOp node)
        {
            if (_binaryOp != null)
            {
                _binaryOp.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _binaryOp = node;
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

        public TIntegerConstant GetSize()
        {
            return _size;
        }

        public void SetSize(TIntegerConstant node)
        {
            if (_size != null)
            {
                _size.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _size = node;
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
            if (_binaryOp == child)
            {
                _binaryOp = null;
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
            if (_size == child)
            {
                _size = null;
                return;
            }
            if (_semi == child)
            {
                _semi = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_binaryOp == oldChild)
            {
                SetBinaryOp((PBinaryOp)newChild);
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
            if (_size == oldChild)
            {
                SetSize((TIntegerConstant)newChild);
                return;
            }
            if (_semi == oldChild)
            {
                SetSemi((TSemi)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_binaryOp) + ToString(_pos) + ToString(_type) + ToString(_size) + ToString(_semi);
        }
    }
}





