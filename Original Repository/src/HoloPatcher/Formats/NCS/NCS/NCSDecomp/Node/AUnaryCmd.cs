namespace KOTORModSync.Formats.NCS.NCSDecomp.AST
{
    public sealed class AUnaryCmd : PCmd
    {
        private PUnaryCommand _unaryCommand;

        public AUnaryCmd()
        {
        }

        public AUnaryCmd(PUnaryCommand unaryCommand)
        {
            SetUnaryCommand(unaryCommand);
        }

        public override object Clone()
        {
            return new AUnaryCmd((PUnaryCommand)CloneNode(_unaryCommand));
        }

        public override void Apply(Analysis.AnalysisAdapter sw)
        {
            // CRITICAL: Must call CaseAUnaryCmd, not just DefaultIn!
            // This enables the visitor pattern to properly traverse unary command nodes (NEGI, NEGF, NOTI, COMPI)
            // Without this, the visitor never reaches the AUnaryCommand child, so negation is never applied
            if (sw is Analysis.PrunedReversedDepthFirstAdapter prdfa)
            {
                prdfa.CaseAUnaryCmd(this);
            }
            else if (sw is Analysis.PrunedDepthFirstAdapter pdfa)
            {
                pdfa.CaseAUnaryCmd(this);
            }
            else
            {
                sw.DefaultIn(this);
            }
        }

        public PUnaryCommand GetUnaryCommand()
        {
            return _unaryCommand;
        }

        public void SetUnaryCommand(PUnaryCommand node)
        {
            if (_unaryCommand != null)
            {
                _unaryCommand.Parent(null);
            }
            if (node != null)
            {
                if (node.Parent() != null)
                {
                    node.Parent().RemoveChild(node);
                }
                node.Parent(this);
            }
            _unaryCommand = node;
        }

        public override void RemoveChild(Node child)
        {
            if (_unaryCommand == child)
            {
                _unaryCommand = null;
            }
        }

        public override void ReplaceChild(Node oldChild, Node newChild)
        {
            if (_unaryCommand == oldChild)
            {
                SetUnaryCommand((PUnaryCommand)newChild);
            }
        }

        public override string ToString()
        {
            return ToString(_unaryCommand);
        }
    }
}





