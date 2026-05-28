namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ABpCmd : PCmd
    {
        private PBpCommand _bpCommand;

        public ABpCmd()
        {
        }

        public ABpCmd(PBpCommand bpCommand)
        {
            SetBpCommand(bpCommand);
        }

        public override object Clone()
        {
            return new ABpCmd((PBpCommand)CloneNode(_bpCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // Call CaseABpCmd directly if sw is PrunedReversedDepthFirstAdapter or PrunedDepthFirstAdapter
            // This ensures the visitor pattern routes correctly to CaseABpCmd
            if (sw is Analysis.PrunedReversedDepthFirstAdapter prdfa)
            {
                prdfa.CaseABpCmd(this);
            }
            else if (sw is Analysis.PrunedDepthFirstAdapter pdfa)
            {
                pdfa.CaseABpCmd(this);
            }
            else
            {
                sw.DefaultIn(this);
            }
        }

        public PBpCommand GetBpCommand()
        {
            return _bpCommand;
        }

        public void SetBpCommand(PBpCommand node)
        {
            if (_bpCommand != null)
            {
                _bpCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _bpCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_bpCommand == child)
            {
                _bpCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_bpCommand == oldChild)
            {
                SetBpCommand((PBpCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_bpCommand);
        }
    }
}





