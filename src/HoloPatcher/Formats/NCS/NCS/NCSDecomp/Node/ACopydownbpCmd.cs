namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ACopydownbpCmd : PCmd
    {
        private PCopyDownBpCommand _copyDownBpCommand;

        public ACopydownbpCmd()
        {
        }

        public ACopydownbpCmd(PCopyDownBpCommand copyDownBpCommand)
        {
            SetCopyDownBpCommand(copyDownBpCommand);
        }

        public override object Clone()
        {
            return new ACopydownbpCmd((PCopyDownBpCommand)CloneNode(_copyDownBpCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PCopyDownBpCommand GetCopyDownBpCommand()
        {
            return _copyDownBpCommand;
        }

        public void SetCopyDownBpCommand(PCopyDownBpCommand node)
        {
            if (_copyDownBpCommand != null)
            {
                _copyDownBpCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _copyDownBpCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_copyDownBpCommand == child)
            {
                _copyDownBpCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_copyDownBpCommand == oldChild)
            {
                SetCopyDownBpCommand((PCopyDownBpCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_copyDownBpCommand);
        }
    }
}





