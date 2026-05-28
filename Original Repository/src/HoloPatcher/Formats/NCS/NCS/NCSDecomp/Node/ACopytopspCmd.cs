namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ACopytopspCmd : PCmd
    {
        private PCopyTopSpCommand _copyTopSpCommand;

        public ACopytopspCmd()
        {
        }

        public ACopytopspCmd(PCopyTopSpCommand copyTopSpCommand)
        {
            SetCopyTopSpCommand(copyTopSpCommand);
        }

        public override object Clone()
        {
            return new ACopytopspCmd((PCopyTopSpCommand)CloneNode(_copyTopSpCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PCopyTopSpCommand GetCopyTopSpCommand()
        {
            return _copyTopSpCommand;
        }

        public void SetCopyTopSpCommand(PCopyTopSpCommand node)
        {
            if (_copyTopSpCommand != null)
            {
                _copyTopSpCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _copyTopSpCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_copyTopSpCommand == child)
            {
                _copyTopSpCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_copyTopSpCommand == oldChild)
            {
                SetCopyTopSpCommand((PCopyTopSpCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_copyTopSpCommand);
        }
    }
}





