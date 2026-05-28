namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AJumpSubCmd : PCmd
    {
        private PJumpToSubroutine _jumpToSubroutine;

        public AJumpSubCmd()
        {
        }

        public AJumpSubCmd(PJumpToSubroutine jumpToSubroutine)
        {
            SetJumpToSubroutine(jumpToSubroutine);
        }

        public override object Clone()
        {
            return new AJumpSubCmd((PJumpToSubroutine)CloneNode(_jumpToSubroutine));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PJumpToSubroutine GetJumpToSubroutine()
        {
            return _jumpToSubroutine;
        }

        public void SetJumpToSubroutine(PJumpToSubroutine node)
        {
            if (_jumpToSubroutine != null)
            {
                _jumpToSubroutine.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _jumpToSubroutine = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_jumpToSubroutine == child)
            {
                _jumpToSubroutine = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_jumpToSubroutine == oldChild)
            {
                SetJumpToSubroutine((PJumpToSubroutine)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_jumpToSubroutine);
        }
    }
}





