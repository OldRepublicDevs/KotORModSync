namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AMovespCmd : PCmd
    {
        private PMoveSpCommand _moveSpCommand;

        public AMovespCmd()
        {
        }

        public AMovespCmd(PMoveSpCommand moveSpCommand)
        {
            SetMoveSpCommand(moveSpCommand);
        }

        public override object Clone()
        {
            return new AMovespCmd((PMoveSpCommand)CloneNode(_moveSpCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PMoveSpCommand GetMoveSpCommand()
        {
            return _moveSpCommand;
        }

        public void SetMoveSpCommand(PMoveSpCommand node)
        {
            if (_moveSpCommand != null)
            {
                _moveSpCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _moveSpCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_moveSpCommand == child)
            {
                _moveSpCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_moveSpCommand == oldChild)
            {
                SetMoveSpCommand((PMoveSpCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_moveSpCommand);
        }
    }
}





