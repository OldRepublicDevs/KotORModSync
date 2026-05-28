namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ADestructCommand : PDestructCommand
    {
        private TDestruct _destruct;
        private TIntegerConstant _pos;
        private TIntegerConstant _type;
        private TIntegerConstant _sizeRem;
        private TIntegerConstant _offset;
        private TIntegerConstant _sizeSave;
        private TSemi _semi;

        public ADestructCommand()
        {
        }

        public ADestructCommand(TDestruct destruct, TIntegerConstant pos, TIntegerConstant type, TIntegerConstant sizeRem, TIntegerConstant offset, TIntegerConstant sizeSave, TSemi semi)
        {
            SetDestruct(destruct);
            SetPos(pos);
            SetType(type);
            SetSizeRem(sizeRem);
            SetOffset(offset);
            SetSizeSave(sizeSave);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new ADestructCommand(
                (TDestruct)CloneNode(_destruct),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_type),
                (TIntegerConstant)CloneNode(_sizeRem),
                (TIntegerConstant)CloneNode(_offset),
                (TIntegerConstant)CloneNode(_sizeSave),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TDestruct GetDestruct()
        {
            return _destruct;
        }

        public void SetDestruct(TDestruct node)
        {
            if (_destruct != null)
            {
                _destruct.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _destruct = node;
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

        public TIntegerConstant GetSizeRem()
        {
            return _sizeRem;
        }

        public void SetSizeRem(TIntegerConstant node)
        {
            if (_sizeRem != null)
            {
                _sizeRem.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _sizeRem = node;
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

        public TIntegerConstant GetSizeSave()
        {
            return _sizeSave;
        }

        public void SetSizeSave(TIntegerConstant node)
        {
            if (_sizeSave != null)
            {
                _sizeSave.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _sizeSave = node;
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
            if (_destruct == child)
            {
                _destruct = null;
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
            if (_sizeRem == child)
            {
                _sizeRem = null;
                return;
            }
            if (_offset == child)
            {
                _offset = null;
                return;
            }
            if (_sizeSave == child)
            {
                _sizeSave = null;
                return;
            }
            if (_semi == child)
            {
                _semi = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_destruct == oldChild)
            {
                SetDestruct((TDestruct)newChild);
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
            if (_sizeRem == oldChild)
            {
                SetSizeRem((TIntegerConstant)newChild);
                return;
            }
            if (_offset == oldChild)
            {
                SetOffset((TIntegerConstant)newChild);
                return;
            }
            if (_sizeSave == oldChild)
            {
                SetSizeSave((TIntegerConstant)newChild);
                return;
            }
            if (_semi == oldChild)
            {
                SetSemi((TSemi)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_destruct) + ToString(_pos) + ToString(_type) + ToString(_sizeRem) + ToString(_offset) + ToString(_sizeSave) + ToString(_semi);
        }
    }
}





