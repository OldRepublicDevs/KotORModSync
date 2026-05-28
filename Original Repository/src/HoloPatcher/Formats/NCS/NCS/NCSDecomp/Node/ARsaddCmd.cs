using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;

namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class ARsaddCmd : PCmd
    {
        private PRsaddCommand _rsaddCommand;

        public ARsaddCmd()
        {
        }

        public ARsaddCmd(PRsaddCommand rsaddCommand)
        {
            SetRsaddCommand(rsaddCommand);
        }

        public override object Clone()
        {
            return new ARsaddCmd((PRsaddCommand)CloneNode(_rsaddCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // Call CaseARsaddCmd directly if sw is PrunedReversedDepthFirstAdapter or PrunedDepthFirstAdapter
            // This ensures the visitor pattern routes correctly to CaseARsaddCmd
            Debug($"DEBUG AST.ARsaddCmd.Apply: sw type={sw.GetType().Name}");
            if (sw is Analysis.PrunedReversedDepthFirstAdapter prdfa)
            {
                Debug($"DEBUG AST.ARsaddCmd.Apply: routing to PrunedReversedDepthFirstAdapter.CaseARsaddCmd");
                prdfa.CaseARsaddCmd(this);
            }
            else if (sw is Analysis.PrunedDepthFirstAdapter pdfa)
            {
                Debug($"DEBUG AST.ARsaddCmd.Apply: routing to PrunedDepthFirstAdapter.CaseARsaddCmd");
                pdfa.CaseARsaddCmd(this);
            }
            else
            {
                Debug($"DEBUG AST.ARsaddCmd.Apply: routing to DefaultIn");
                sw.DefaultIn(this);
            }
        }

        public PRsaddCommand GetRsaddCommand()
        {
            return _rsaddCommand;
        }

        public void SetRsaddCommand(PRsaddCommand node)
        {
            if (_rsaddCommand != null)
            {
                _rsaddCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _rsaddCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_rsaddCommand == child)
            {
                _rsaddCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_rsaddCommand == oldChild)
            {
                SetRsaddCommand((PRsaddCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_rsaddCommand);
        }
    }
}





