namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ACopyTopBpCommand : PCopyTopBpCommand
    {
        private TCptopbp _cptopbp;
        private TIntegerConstant _pos;
        private TIntegerConstant _type;
        private TIntegerConstant _offset;
        private TIntegerConstant _size;
        private TSemi _semi;

        public ACopyTopBpCommand()
        {
        }

        public ACopyTopBpCommand(TCptopbp cptopbp, TIntegerConstant pos, TIntegerConstant type, TIntegerConstant offset, TIntegerConstant size, TSemi semi)
        {
            SetCptopbp(cptopbp);
            SetPos(pos);
            SetType(type);
            SetOffset(offset);
            SetSize(size);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new ACopyTopBpCommand(
                (TCptopbp)CloneNode(_cptopbp),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_type),
                (TIntegerConstant)CloneNode(_offset),
                (TIntegerConstant)CloneNode(_size),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TCptopbp GetCptopbp()
        {
            return _cptopbp;
        }

        public void SetCptopbp(TCptopbp node)
        {
            if (_cptopbp != null)
            {
                _cptopbp.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _cptopbp = node;
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
            if (_cptopbp == child)
            {
                _cptopbp = null;
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
            if (_cptopbp == oldChild)
            {
                SetCptopbp((TCptopbp)newChild);
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
            return ToString(_cptopbp) + ToString(_pos) + ToString(_type) + ToString(_offset) + ToString(_size) + ToString(_semi);
        }
    }
}





