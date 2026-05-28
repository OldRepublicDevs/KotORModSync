namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ACondJumpCmd : PCmd
    {
        private PConditionalJumpCommand _conditionalJumpCommand;

        public ACondJumpCmd()
        {
        }

        public ACondJumpCmd(PConditionalJumpCommand conditionalJumpCommand)
        {
            SetConditionalJumpCommand(conditionalJumpCommand);
        }

        public override object Clone()
        {
            return new ACondJumpCmd((PConditionalJumpCommand)CloneNode(_conditionalJumpCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PConditionalJumpCommand GetConditionalJumpCommand()
        {
            return _conditionalJumpCommand;
        }

        public void SetConditionalJumpCommand(PConditionalJumpCommand node)
        {
            if (_conditionalJumpCommand != null)
            {
                _conditionalJumpCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _conditionalJumpCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_conditionalJumpCommand == child)
            {
                _conditionalJumpCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_conditionalJumpCommand == oldChild)
            {
                SetConditionalJumpCommand((PConditionalJumpCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_conditionalJumpCommand);
        }
    }
}





