namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AMoveSpCommand : PMoveSpCommand
    {
        private TMovsp _movsp;
        private TIntegerConstant _pos;
        private TIntegerConstant _type;
        private TIntegerConstant _offset;
        private TSemi _semi;

        public AMoveSpCommand()
        {
        }

        public AMoveSpCommand(TMovsp movsp, TIntegerConstant pos, TIntegerConstant type, TIntegerConstant offset, TSemi semi)
        {
            SetMovsp(movsp);
            SetPos(pos);
            SetType(type);
            SetOffset(offset);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new AMoveSpCommand(
                (TMovsp)CloneNode(_movsp),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_type),
                (TIntegerConstant)CloneNode(_offset),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public TMovsp GetMovsp()
        {
            return _movsp;
        }

        public void SetMovsp(TMovsp node)
        {
            if (_movsp != null)
            {
                _movsp.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _movsp = node;
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
            if (_movsp == child)
            {
                _movsp = null;
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
            if (_movsp == oldChild)
            {
                SetMovsp((TMovsp)newChild);
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
            return ToString(_movsp) + ToString(_pos) + ToString(_type) + ToString(_offset) + ToString(_semi);
        }
    }
}





