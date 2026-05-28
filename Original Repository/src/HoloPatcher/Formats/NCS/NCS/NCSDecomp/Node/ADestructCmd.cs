namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ADestructCmd : PCmd
    {
        private PDestructCommand _destructCommand;

        public ADestructCmd()
        {
        }

        public ADestructCmd(PDestructCommand destructCommand)
        {
            SetDestructCommand(destructCommand);
        }

        public override object Clone()
        {
            return new ADestructCmd((PDestructCommand)CloneNode(_destructCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PDestructCommand GetDestructCommand()
        {
            return _destructCommand;
        }

        public void SetDestructCommand(PDestructCommand node)
        {
            if (_destructCommand != null)
            {
                _destructCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _destructCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_destructCommand == child)
            {
                _destructCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_destructCommand == oldChild)
            {
                SetDestructCommand((PDestructCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_destructCommand);
        }
    }
}





