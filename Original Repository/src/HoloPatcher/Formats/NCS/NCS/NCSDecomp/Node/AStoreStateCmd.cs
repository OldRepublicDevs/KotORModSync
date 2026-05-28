namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AStoreStateCmd : PCmd
    {
        private PStoreStateCommand _storeStateCommand;

        public AStoreStateCmd()
        {
        }

        public AStoreStateCmd(PStoreStateCommand storeStateCommand)
        {
            SetStoreStateCommand(storeStateCommand);
        }

        public override object Clone()
        {
            return new AStoreStateCmd((PStoreStateCommand)CloneNode(_storeStateCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            sw.DefaultIn(this);
        }

        public PStoreStateCommand GetStoreStateCommand()
        {
            return _storeStateCommand;
        }

        public void SetStoreStateCommand(PStoreStateCommand node)
        {
            if (_storeStateCommand != null)
            {
                _storeStateCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _storeStateCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_storeStateCommand == child)
            {
                _storeStateCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_storeStateCommand == oldChild)
            {
                SetStoreStateCommand((PStoreStateCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_storeStateCommand);
        }
    }
}





