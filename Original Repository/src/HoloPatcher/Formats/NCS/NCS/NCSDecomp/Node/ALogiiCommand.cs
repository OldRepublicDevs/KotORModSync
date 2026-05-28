namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ALogiiCommand : PLogiiCommand
    {
        private PLogiiOp _logiiOp;
        private TIntegerConstant _pos;
        private TIntegerConstant _type;
        private TSemi _semi;

        public ALogiiCommand()
        {
        }

        public ALogiiCommand(PLogiiOp logiiOp, TIntegerConstant pos, TIntegerConstant type, TSemi semi)
        {
            SetLogiiOp(logiiOp);
            SetPos(pos);
            SetType(type);
            SetSemi(semi);
        }

        public override object Clone()
        {
            return new ALogiiCommand(
                (PLogiiOp)CloneNode(_logiiOp),
                (TIntegerConstant)CloneNode(_pos),
                (TIntegerConstant)CloneNode(_type),
                (TSemi)CloneNode(_semi));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PLogiiOp GetLogiiOp()
        {
            return _logiiOp;
        }

        public void SetLogiiOp(PLogiiOp node)
        {
            if (_logiiOp != null)
            {
                _logiiOp.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _logiiOp = node;
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
            if (_logiiOp == child)
            {
                _logiiOp = null;
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
            if (_logiiOp == oldChild)
            {
                SetLogiiOp((PLogiiOp)newChild);
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
            return ToString(_logiiOp) + ToString(_pos) + ToString(_type) + ToString(_semi);
        }
    }
}





