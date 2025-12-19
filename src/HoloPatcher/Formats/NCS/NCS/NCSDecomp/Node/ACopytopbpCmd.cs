namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ACopytopbpCmd : PCmd
    {
        private PCopyTopBpCommand _copyTopBpCommand;

        public ACopytopbpCmd()
        {
        }

        public ACopytopbpCmd(PCopyTopBpCommand copyTopBpCommand)
        {
            SetCopyTopBpCommand(copyTopBpCommand);
        }

        public override object Clone()
        {
            return new ACopytopbpCmd((PCopyTopBpCommand)CloneNode(_copyTopBpCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PCopyTopBpCommand GetCopyTopBpCommand()
        {
            return _copyTopBpCommand;
        }

        public void SetCopyTopBpCommand(PCopyTopBpCommand node)
        {
            if (_copyTopBpCommand != null)
            {
                _copyTopBpCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _copyTopBpCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_copyTopBpCommand == child)
            {
                _copyTopBpCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_copyTopBpCommand == oldChild)
            {
                SetCopyTopBpCommand((PCopyTopBpCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_copyTopBpCommand);
        }
    }
}





