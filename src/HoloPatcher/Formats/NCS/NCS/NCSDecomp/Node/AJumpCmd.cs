namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AJumpCmd : PCmd
    {
        private PJumpCommand _jumpCommand;

        public AJumpCmd()
        {
        }

        public AJumpCmd(PJumpCommand jumpCommand)
        {
            SetJumpCommand(jumpCommand);
        }

        public override object Clone()
        {
            return new AJumpCmd((PJumpCommand)CloneNode(_jumpCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PJumpCommand GetJumpCommand()
        {
            return _jumpCommand;
        }

        public void SetJumpCommand(PJumpCommand node)
        {
            if (_jumpCommand != null)
            {
                _jumpCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _jumpCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_jumpCommand == child)
            {
                _jumpCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_jumpCommand == oldChild)
            {
                SetJumpCommand((PJumpCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_jumpCommand);
        }
    }
}





