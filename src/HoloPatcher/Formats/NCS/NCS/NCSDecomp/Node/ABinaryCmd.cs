namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ABinaryCmd : PCmd
    {
        private PBinaryCommand _binaryCommand;

        public ABinaryCmd()
        {
        }

        public ABinaryCmd(PBinaryCommand binaryCommand)
        {
            SetBinaryCommand(binaryCommand);
        }

        public override object Clone()
        {
            return new ABinaryCmd((PBinaryCommand)CloneNode(_binaryCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PBinaryCommand GetBinaryCommand()
        {
            return _binaryCommand;
        }

        public void SetBinaryCommand(PBinaryCommand node)
        {
            if (_binaryCommand != null)
            {
                _binaryCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _binaryCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_binaryCommand == child)
            {
                _binaryCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_binaryCommand == oldChild)
            {
                SetBinaryCommand((PBinaryCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_binaryCommand);
        }
    }
}





