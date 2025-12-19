namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ACopydownspCmd : PCmd
    {
        private PCopyDownSpCommand _copyDownSpCommand;

        public ACopydownspCmd()
        {
        }

        public ACopydownspCmd(PCopyDownSpCommand copyDownSpCommand)
        {
            SetCopyDownSpCommand(copyDownSpCommand);
        }

        public override object Clone()
        {
            return new ACopydownspCmd((PCopyDownSpCommand)CloneNode(_copyDownSpCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // Call CaseACopydownspCmd directly if sw is PrunedReversedDepthFirstAdapter or PrunedDepthFirstAdapter
            // This ensures the visitor pattern routes correctly to CaseACopydownspCmd
            if (sw is Analysis.PrunedReversedDepthFirstAdapter prdfa)
            {
                prdfa.CaseACopydownspCmd(this);
            }
            else if (sw is Analysis.PrunedDepthFirstAdapter pdfa)
            {
                pdfa.CaseACopydownspCmd(this);
            }
            else
            {
                sw.DefaultIn(this);
            }
        }

        public PCopyDownSpCommand GetCopyDownSpCommand()
        {
            return _copyDownSpCommand;
        }

        public void SetCopyDownSpCommand(PCopyDownSpCommand node)
        {
            if (_copyDownSpCommand != null)
            {
                _copyDownSpCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _copyDownSpCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_copyDownSpCommand == child)
            {
                _copyDownSpCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_copyDownSpCommand == oldChild)
            {
                SetCopyDownSpCommand((PCopyDownSpCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_copyDownSpCommand);
        }
    }
}





