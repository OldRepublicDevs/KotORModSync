namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AConstCmd : PCmd
    {
        private PConstCommand _constCommand;

        public AConstCmd()
        {
        }

        public AConstCmd(PConstCommand constCommand)
        {
            SetConstCommand(constCommand);
        }

        public override object Clone()
        {
            return new AConstCmd((PConstCommand)CloneNode(_constCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // Call CaseAConstCmd directly if sw is PrunedReversedDepthFirstAdapter or PrunedDepthFirstAdapter
            // This ensures the visitor pattern routes correctly to CaseAConstCmd
            if (sw is Analysis.PrunedReversedDepthFirstAdapter prdfa)
            {
                prdfa.CaseAConstCmd(this);
            }
            else if (sw is Analysis.PrunedDepthFirstAdapter pdfa)
            {
                pdfa.CaseAConstCmd(this);
            }
            else
            {
                sw.DefaultIn(this);
            }
        }

        public PConstCommand GetConstCommand()
        {
            return _constCommand;
        }

        public void SetConstCommand(PConstCommand node)
        {
            if (_constCommand != null)
            {
                _constCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _constCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_constCommand == child)
            {
                _constCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_constCommand == oldChild)
            {
                SetConstCommand((PConstCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_constCommand);
        }
    }
}





