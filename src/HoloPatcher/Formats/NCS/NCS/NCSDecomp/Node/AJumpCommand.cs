namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AJumpCommand : PJumpCommand
    {
        private TJmp _jmp;
        private TIntegerConstant _pos;
        private TIntegerConstant _type;
        private TIntegerConstant _offset;
        private TSemi _semi;

        public AJumpCommand()
        {
        }

        public AJumpCommand(TJmp jmp, TIntegerConstant pos, TIntegerConstant type, TIntegerConstant offset, TSemi semi)
        {
            SetJmp(jmp);
            SetPos(pos);
            SetType(type);
            SetOffset(offset);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new AJumpCommand(
                (TJmp)CloneNode(_jmp),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_type),
                (TIntegerConstant)CloneNode(_offset),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TJmp GetJmp()
        {
            return _jmp;
        }

        public void SetJmp(TJmp node)
        {
            if (_jmp != null)
            {
                _jmp.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _jmp = node;
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
            if (_jmp == child)
            {
                _jmp = null;
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
            if (_offset == child)
            {
                _offset = null;
                return;
            }
            if (_semi == child)
            {
                _semi = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_jmp == oldChild)
            {
                SetJmp((TJmp)newChild);
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
            if (_offset == oldChild)
            {
                SetOffset((TIntegerConstant)newChild);
                return;
            }
            if (_semi == oldChild)
            {
                SetSemi((TSemi)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_jmp) + ToString(_pos) + ToString(_type) + ToString(_offset) + ToString(_semi);
        }
    }
}





